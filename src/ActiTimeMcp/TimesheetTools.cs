using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ActiTimeMcp;

[McpServerToolType]
public sealed class TimesheetTools(ActiTimeClient api, IOptions<ActiTimeOptions> options)
{
    private TimeZoneInfo Tz => string.IsNullOrWhiteSpace(options.Value.TimeZone)
        ? TimeZoneInfo.Local
        : TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    // ---------------- discovery ----------------

    [McpServerTool(Name = "whoami", ReadOnly = true, Idempotent = true)]
    [Description("Returns the actiTIME user these credentials belong to, plus today's date in the configured time zone. Call this first to confirm the connection works.")]
    public async Task<object> WhoAmI(CancellationToken ct)
    {
        var me = await api.GetMeAsync(ct);
        var today = Parsing.ParseDate(null, Tz);
        return new { me.Id, me.Username, me.FullName, me.Email, today = today.ToString("yyyy-MM-dd"), dayOfWeek = today.DayOfWeek.ToString() };
    }

    [McpServerTool(Name = "list_customers", ReadOnly = true, Idempotent = true)]
    [Description("Lists actiTIME customers (clients). Optionally filter by a case-insensitive name fragment.")]
    public async Task<object> ListCustomers(
        [Description("Optional case-insensitive substring of the customer name.")] string? nameContains = null,
        [Description("Include archived customers. Default false.")] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var all = await api.GetCustomersAsync(includeArchived, ct);
        return all.Where(c => Matches(c.Name, nameContains)).Select(c => new { c.Id, c.Name, c.Archived }).ToList();
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, Idempotent = true)]
    [Description("Lists actiTIME projects, optionally for one customer and/or filtered by a name fragment. Tasks always belong to a project.")]
    public async Task<object> ListProjects(
        [Description("Optional customer id to restrict to.")] long? customerId = null,
        [Description("Optional case-insensitive substring of the project or customer name.")] string? nameContains = null,
        [Description("Include archived projects. Default false.")] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var all = await api.GetProjectsAsync(customerId, includeArchived, ct);
        return all
            .Where(p => Matches(p.Name, nameContains) || Matches(p.CustomerName, nameContains) && nameContains is not null)
            .Select(p => new { p.Id, p.Name, p.CustomerId, p.CustomerName, p.Archived })
            .ToList();
    }

    [McpServerTool(Name = "search_tasks", ReadOnly = true, Idempotent = true)]
    [Description("""
        Searches all actiTIME tasks you can see. Check list_favorite_tasks first; use this only when no favourite fits. Use this to map what the user says they worked on ("fixed the login bug for Acme")
        to a concrete task id before logging time. 'words' matches task, project and customer names.
        Tasks are predefined by the company and cannot be created here. If nothing fits, ask the user which existing task to use;
        never guess.
        """)]
    public async Task<object> SearchTasks(
        [Description("Free-text words to match against task/project/customer names. Leave empty to list everything in scope.")] string? words = null,
        [Description("Restrict to one project id.")] long? projectId = null,
        [Description("Restrict to one customer id.")] long? customerId = null,
        [Description("'open' (default), 'completed', or 'all'.")] string status = "open",
        [Description("Max results, default 50.")] int limit = 50,
        CancellationToken ct = default)
    {
        var st = status.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : status.ToLowerInvariant();
        var tasks = await api.SearchTasksAsync(words, projectId, customerId, st, Math.Clamp(limit, 1, 1000), ct);
        return tasks.Select(t => new
        {
            t.Id, t.Name, t.Status, t.ProjectId, t.ProjectName, t.CustomerId, t.CustomerName, t.TypeOfWorkName, t.Description
        }).ToList();
    }

    // ---------------- writes ----------------

    public sealed class TimeEntryInput
    {
        [Description("Id of an existing task (from search_tasks).")]
        public long TaskId { get; set; }

        [Description("Time spent, e.g. '1h30m', '1.5h', '45m', '1:30'. '0' removes the entry.")]
        public string Duration { get; set; } = "";

        [Description("Short description of what was done. Shown in actiTIME as the cell comment.")]
        public string? Comment { get; set; }

        [Description("Day: yyyy-MM-dd, 'today', 'yesterday' or a weekday name. Defaults to today.")]
        public string? Date { get; set; }
    }

    [McpServerTool(Name = "log_time", Destructive = true, Idempotent = false)]
    [Description("""
        Records time for the current user on one or more tasks. Each entry is one task on one day.
        mode 'set' (default) makes the cell equal to the given duration and comment (safe to re-run);
        mode 'add' adds to whatever is already there and appends the comment.
        Always show the user the list of entries and get confirmation before calling this.
        """)]
    public async Task<object> LogTime(
        [Description("Entries to record.")] List<TimeEntryInput> entries,
        [Description("'set' (default, overwrite the cell) or 'add' (add to existing time).")] string mode = "set",
        CancellationToken ct = default)
    {
        if (entries is null || entries.Count == 0) throw new McpException("No entries given.");
        var add = mode.Equals("add", StringComparison.OrdinalIgnoreCase);
        if (!add && !mode.Equals("set", StringComparison.OrdinalIgnoreCase))
            throw new McpException("mode must be 'set' or 'add'.");

        var me = await api.GetMeAsync(ct);
        var results = new List<LogResult>();

        foreach (var e in entries)
        {
            try
            {
                var date = Parsing.ParseDate(e.Date, Tz);
                var minutes = Parsing.ParseDurationMinutes(e.Duration);
                var comment = string.IsNullOrWhiteSpace(e.Comment) ? null : e.Comment.Trim();

                if (add)
                {
                    var existing = (await api.GetTimeTrackAsync(me.Id, date, date, e.TaskId, ct))
                        .Data.SelectMany(d => d.Records).FirstOrDefault(r => r.TaskId == e.TaskId);
                    if (existing is not null)
                    {
                        minutes += existing.Time;
                        if (!string.IsNullOrWhiteSpace(existing.Comment))
                            comment = comment is null ? existing.Comment : $"{existing.Comment}; {comment}";
                    }
                    if (minutes > 24 * 60) throw new McpException($"Total for that day would be {minutes} minutes (> 24h).");
                }

                if (minutes == 0) comment = ""; // removing the entry: clear the comment too, or it lingers on the cell

                var saved = await api.SetTimeAsync(me.Id, date, e.TaskId, minutes, comment, ct);
                var finalMinutes = saved?.Time ?? minutes;
                results.Add(new LogResult(true, date.ToString("yyyy-MM-dd"), e.TaskId, finalMinutes,
                    Parsing.FormatMinutes(finalMinutes), saved?.Comment ?? comment, null));
            }
            catch (Exception ex) when (ex is McpException or HttpRequestException)
            {
                results.Add(new LogResult(false, e.Date, e.TaskId, null, e.Duration, e.Comment, ex.Message));
            }
        }

        return new { saved = results.Count(r => r.Ok), failed = results.Count(r => !r.Ok), results };
    }

    public sealed record LogResult(bool Ok, string? Date, long TaskId, int? Minutes, string? Duration, string? Comment, string? Error);

    // ---------------- reads ----------------

    [McpServerTool(Name = "get_timesheet", ReadOnly = true, Idempotent = true)]
    [Description("Shows what the current user has already logged, per day, between two dates (inclusive). Use it to avoid double-logging and to show a summary after logging.")]
    public async Task<List<DaySummary>> GetTimesheet(
        [Description("Start day: yyyy-MM-dd, 'today', 'yesterday', weekday name. Default today.")] string? from = null,
        [Description("End day (inclusive). Defaults to the start day.")] string? to = null,
        CancellationToken ct = default)
    {
        var me = await api.GetMeAsync(ct);
        var dFrom = Parsing.ParseDate(from, Tz);
        var dTo = to is null ? dFrom : Parsing.ParseDate(to, Tz);
        if (dTo < dFrom) (dFrom, dTo) = (dTo, dFrom);
        if (dTo.DayNumber - dFrom.DayNumber > 62) throw new McpException("Please ask for at most ~2 months at a time.");

        var tt = await api.GetTimeTrackAsync(me.Id, dFrom, dTo, null, ct);
        var taskInfo = new Dictionary<long, (string? Name, string? Project, string? Customer)>();
        var missing = new HashSet<long>();

        foreach (var id in tt.Data.SelectMany(d => d.Records).Select(r => r.TaskId).Distinct())
        {
            if (tt.Tasks is not null && tt.Tasks.TryGetValue(id.ToString(), out var el))
                taskInfo[id] = (Str(el, "name"), Str(el, "projectName"), Str(el, "customerName"));
            else
                missing.Add(id);
        }
        // Fallback if the referenced map was not returned or lacks names.
        foreach (var id in missing)
        {
            try { var t = await api.GetTaskAsync(id, ct); taskInfo[id] = (t.Name, t.ProjectName, t.CustomerName); }
            catch (McpException) { taskInfo[id] = (null, null, null); }
        }

        return tt.Data
            .Select(d =>
            {
                var entries = d.Records.Where(r => r.Time > 0).Select(r =>
                {
                    var info = taskInfo.GetValueOrDefault(r.TaskId);
                    return new TimeEntry(d.Date, r.TaskId, info.Name, info.Project, info.Customer, r.Time, Parsing.FormatMinutes(r.Time), r.Comment);
                }).ToList();
                var total = entries.Sum(x => x.Minutes);
                return new DaySummary(d.Date, total, Parsing.FormatMinutes(total), entries);
            })
            .Where(d => d.Entries.Count > 0)
            .OrderBy(d => d.Date)
            .ToList();
    }

    [McpServerTool(Name = "parse_duration", ReadOnly = true, Idempotent = true)]
    [Description("Utility: converts a human duration ('1h30', '1.5h', '45m') to minutes so you can double-check totals before logging.")]
    public object ParseDuration(string duration)
    {
        var m = Parsing.ParseDurationMinutes(duration);
        return new { minutes = m, formatted = Parsing.FormatMinutes(m) };
    }

    private static bool Matches(string? value, string? fragment)
        => string.IsNullOrWhiteSpace(fragment) || (value?.Contains(fragment.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    private static string? Str(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
