using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ActiTimeMcp;

/// <summary>A task the user logs time against often, with optional words that help match what they say to it.</summary>
public sealed class FavoriteTask
{
    public long TaskId { get; set; }

    /// <summary>Short name the user uses for it, e.g. "standup".</summary>
    public string? Alias { get; set; }

    /// <summary>Extra words that should map to this task, e.g. ["daily", "scrum", "meeting"].</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Snapshot of the task/project/customer names when it was added (for readability of the file).</summary>
    public string? TaskName { get; set; }
    public string? ProjectName { get; set; }
    public string? CustomerName { get; set; }

    /// <summary>Default comment to use when the user doesn't give one.</summary>
    public string? DefaultComment { get; set; }
}

/// <summary>
/// Stores favourite tasks in a local JSON file (default: %APPDATA%\actitime-mcp\favorites.json).
/// Local only: nothing here is written to actiTIME. The file can also be edited by hand.
/// </summary>
public sealed class FavoritesStore(IOptions<ActiTimeOptions> options)
{
    private static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string FilePath => string.IsNullOrWhiteSpace(options.Value.FavoritesFile)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "actitime-mcp", "favorites.json")
        : Environment.ExpandEnvironmentVariables(options.Value.FavoritesFile);

    public async Task<List<FavoriteTask>> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { return await ReadAsync(ct); }
        finally { _lock.Release(); }
    }

    /// <summary>Applies a change to the list and saves it atomically.</summary>
    public async Task<List<FavoriteTask>> UpdateAsync(Action<List<FavoriteTask>> change, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var list = await ReadAsync(ct);
            change(list);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, FileJson), ct);
            File.Move(tmp, FilePath, overwrite: true);
            return list;
        }
        finally { _lock.Release(); }
    }

    private async Task<List<FavoriteTask>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath)) return [];
        var text = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<FavoriteTask>>(text, FileJson) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ModelContextProtocol.McpException($"Favorites file {FilePath} is not valid JSON: {ex.Message}");
        }
    }
}
