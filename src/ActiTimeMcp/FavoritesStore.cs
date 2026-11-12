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
public sealed class FavoritesStore(IOptions<ActiTimeOptions> options) : JsonListStore<FavoriteTask>
{
    public override string FilePath => string.IsNullOrWhiteSpace(options.Value.FavoritesFile)
        ? DefaultPath("favorites.json")
        : Environment.ExpandEnvironmentVariables(options.Value.FavoritesFile);
}
