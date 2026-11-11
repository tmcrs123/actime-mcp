using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol;

namespace ActiTimeMcp;

/// <summary>Thin typed wrapper over the actiTIME REST API v1.</summary>
public sealed class ActiTimeClient(HttpClient http)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ActiUser? _me; // the authenticated user never changes for a process

    public async Task<ActiUser> GetMeAsync(CancellationToken ct = default)
        => _me ??= await GetAsync<ActiUser>("users/me", ct);

    public async Task<List<Customer>> GetCustomersAsync(bool includeArchived, CancellationToken ct = default)
    {
        var items = (await GetAsync<PagedResult<Customer>>("customers?limit=1000", ct)).Items;
        return includeArchived ? items : items.Where(c => !c.Archived).ToList();
    }

    public async Task<List<Project>> GetProjectsAsync(long? customerId, bool includeArchived, CancellationToken ct = default)
    {
        var q = new QueryBuilder().Add("limit", "1000").Add("customerIds", customerId);
        var items = (await GetAsync<PagedResult<Project>>("projects" + q, ct)).Items;
        return includeArchived ? items : items.Where(p => !p.Archived).ToList();
    }

    public async Task<List<ActiTask>> SearchTasksAsync(
        string? words, long? projectId, long? customerId, string? status, int limit, CancellationToken ct = default)
    {
        var q = new QueryBuilder()
            .Add("limit", limit)
            .Add("words", words)
            .Add("projectIds", projectId)
            .Add("customerIds", customerId)
            .Add("status", status);
        return (await GetAsync<PagedResult<ActiTask>>("tasks" + q, ct)).Items;
    }

    public Task<ActiTask> GetTaskAsync(long taskId, CancellationToken ct = default)
        => GetAsync<ActiTask>($"tasks/{taskId}", ct);

    /// <summary>Fetches several tasks by id in one call (any status).</summary>
    public async Task<List<ActiTask>> GetTasksByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return [];
        var q = new QueryBuilder().Add("ids", string.Join(",", list)).Add("limit", list.Count);
        return (await GetAsync<PagedResult<ActiTask>>("tasks" + q, ct)).Items;
    }

    public Task<TimeTrackResponse> GetTimeTrackAsync(
        long userId, DateOnly from, DateOnly to, long? taskId = null, CancellationToken ct = default)
    {
        var q = new QueryBuilder()
            .Add("userIds", userId)
            .Add("dateFrom", from.ToString("yyyy-MM-dd"))
            .Add("dateTo", to.ToString("yyyy-MM-dd"))
            .Add("taskIds", taskId)
            .Add("includeReferenced", "comments,tasks");
        return GetAsync<TimeTrackResponse>("timetrack" + q, ct);
    }

    /// <summary>Sets (overwrites) the time and comment of a user/day/task cell. time = 0 deletes the record.</summary>
    public Task<TimeTrackRecord> SetTimeAsync(
        long userId, DateOnly date, long taskId, int minutes, string? comment, CancellationToken ct = default)
    {
        object body = comment is null ? new { time = minutes } : new { time = minutes, comment };
        return SendAsync<TimeTrackRecord>(HttpMethod.Patch, $"timetrack/{userId}/{date:yyyy-MM-dd}/{taskId}", body, ct);
    }

    // ---------------- plumbing ----------------

    private Task<T> GetAsync<T>(string path, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, path, null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var req = new HttpRequestMessage(method, path);
            if (body is not null) req.Content = JsonContent.Create(body, body.GetType(), options: Json);

            HttpResponseMessage resp;
            try
            {
                resp = await http.SendAsync(req, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new McpException($"Could not reach actiTIME at {http.BaseAddress}: {ex.Message}. Check ACTITIME_URL and your network/VPN.");
            }
            using var _ = resp;

            if (resp.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
            {
                var wait = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * (attempt + 1));
                await Task.Delay(wait, ct);
                continue;
            }

            if (!resp.IsSuccessStatusCode)
                throw await ToExceptionAsync(method, path, resp, ct);

            if (resp.StatusCode == HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
                return default!;

            return (await resp.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }

    private static async Task<Exception> ToExceptionAsync(HttpMethod method, string path, HttpResponseMessage resp, CancellationToken ct)
    {
        var raw = await resp.Content.ReadAsStringAsync(ct);
        string detail = raw;
        try
        {
            var err = JsonSerializer.Deserialize<ApiError>(raw, Json);
            if (!string.IsNullOrWhiteSpace(err?.Message)) detail = $"{err.Message} ({err.Key})";
        }
        catch (JsonException) { /* non-JSON body */ }

        var hint = resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized => " Check ACTITIME_USERNAME / ACTITIME_PASSWORD.",
            HttpStatusCode.Forbidden => " Your actiTIME user may lack permission for this, or the IP is briefly banned after failed logins.",
            HttpStatusCode.NotFound => " The object does not exist or is not visible to you.",
            _ => ""
        };
        return new McpException($"actiTIME {method} /{path} failed with {(int)resp.StatusCode}: {detail}.{hint}");
    }

    private sealed class QueryBuilder
    {
        private readonly List<string> _parts = [];

        public QueryBuilder Add(string key, object? value)
        {
            if (value is null || value is string s && string.IsNullOrWhiteSpace(s)) return this;
            _parts.Add($"{key}={Uri.EscapeDataString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)}");
            return this;
        }

        public override string ToString() => _parts.Count == 0 ? "" : "?" + string.Join("&", _parts);
    }
}
