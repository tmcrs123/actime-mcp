using Microsoft.Extensions.Options;

namespace ActiTimeMcp;

/// <summary>One cell the user submitted through log_time: a task on a day.</summary>
public sealed class RecentEntry
{
    /// <summary>The day the time was logged for (yyyy-MM-dd), not the day it was submitted.</summary>
    public string Date { get; set; } = "";
    public long TaskId { get; set; }
    public int Minutes { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Remembers what was submitted via log_time (default: %APPDATA%\actitime-mcp\recent.json), so the next check-in can
/// offer the tasks from the previous working day. Local only; keeps the last <see cref="KeepDays"/> days.
/// </summary>
public sealed class RecentEntriesStore(IOptions<ActiTimeOptions> options) : JsonListStore<RecentEntry>
{
    public const int KeepDays = 30;

    public override string FilePath => string.IsNullOrWhiteSpace(options.Value.RecentFile)
        ? DefaultPath("recent.json")
        : Environment.ExpandEnvironmentVariables(options.Value.RecentFile);

    /// <summary>Upserts the given cells (0 minutes removes the cell) and drops anything older than KeepDays.</summary>
    public Task RecordAsync(IEnumerable<RecentEntry> entries, DateOnly today, CancellationToken ct = default)
    {
        var cutoff = today.AddDays(-KeepDays).ToString("yyyy-MM-dd");
        return UpdateAsync(list =>
        {
            foreach (var e in entries)
            {
                list.RemoveAll(x => x.Date == e.Date && x.TaskId == e.TaskId);
                if (e.Minutes > 0) list.Add(e);
            }
            list.RemoveAll(x => string.CompareOrdinal(x.Date, cutoff) < 0);
            list.Sort((a, b) => string.CompareOrdinal(a.Date, b.Date) is var c and not 0 ? c : a.TaskId.CompareTo(b.TaskId));
        }, ct);
    }

    /// <summary>The entries of the most recent day before <paramref name="before"/> that has any (e.g. Friday when it's Monday).</summary>
    public async Task<(string? Date, List<RecentEntry> Entries)> PreviousDayAsync(DateOnly before, CancellationToken ct = default)
    {
        var key = before.ToString("yyyy-MM-dd");
        var list = await LoadAsync(ct);
        var date = list.Where(x => string.CompareOrdinal(x.Date, key) < 0).Select(x => x.Date).DefaultIfEmpty().Max(StringComparer.Ordinal);
        return date is null ? (null, []) : (date, list.Where(x => x.Date == date).ToList());
    }
}
