using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ActiTimeMcp;

[McpServerToolType]
public sealed class FavoriteTools(FavoritesStore store, ActiTimeClient api)
{
    [McpServerTool(Name = "list_favorite_tasks", ReadOnly = true, Idempotent = true)]
    [Description("""
        Lists the user's favourite tasks: the predefined actiTIME tasks they log time against most often, each with an optional
        alias, keywords and default comment. ALWAYS check this list first when mapping what the user worked on to a task;
        only fall back to search_tasks when no favourite clearly fits. When asking the user what they worked on, show them their open
        favourites as a short numbered list so they don't have to remember them. Live task status is included, so completed tasks are flagged.
        """)]
    public async Task<object> ListFavoriteTasks(CancellationToken ct)
    {
        var favs = await store.LoadAsync(ct);
        if (favs.Count == 0)
            return new { favorites = Array.Empty<object>(), file = store.FilePath, hint = "No favourites yet. Add some with add_favorite_task." };

        // Refresh names/status from actiTIME so renamed or completed tasks are visible.
        Dictionary<long, ActiTask> live;
        string? warning = null;
        try
        {
            live = (await api.GetTasksByIdsAsync(favs.Select(f => f.TaskId), ct)).ToDictionary(t => t.Id);
        }
        catch (McpException ex)
        {
            live = [];
            warning = "Could not refresh from actiTIME, showing saved names: " + ex.Message;
        }

        var result = favs.Select(f =>
        {
            live.TryGetValue(f.TaskId, out var t);
            return new
            {
                f.TaskId,
                f.Alias,
                f.Keywords,
                f.DefaultComment,
                TaskName = t?.Name ?? f.TaskName,
                ProjectName = t?.ProjectName ?? f.ProjectName,
                CustomerName = t?.CustomerName ?? f.CustomerName,
                Status = t?.Status ?? (live.Count > 0 ? "not found / no access" : "unknown"),
            };
        }).ToList();

        return new { favorites = result, file = store.FilePath, warning };
    }

    [McpServerTool(Name = "add_favorite_task", Idempotent = true)]
    [Description("""
        Adds an existing actiTIME task to the user's favourites (or updates its alias/keywords/default comment if it is already one).
        Stored locally only; does not change anything in actiTIME. Get the task id from search_tasks first.
        """)]
    public async Task<object> AddFavoriteTask(
        [Description("Id of an existing task (from search_tasks).")] long taskId,
        [Description("Optional short name the user calls it, e.g. 'standup'.")] string? alias = null,
        [Description("Optional extra words that should map to this task, e.g. ['daily', 'scrum'].")] List<string>? keywords = null,
        [Description("Optional default comment used when the user gives none.")] string? defaultComment = null,
        CancellationToken ct = default)
    {
        var task = await api.GetTaskAsync(taskId, ct); // validates the task exists and is visible

        var favs = await store.UpdateAsync(list =>
        {
            var fav = list.FirstOrDefault(f => f.TaskId == taskId);
            if (fav is null) list.Add(fav = new FavoriteTask { TaskId = taskId });
            fav.TaskName = task.Name;
            fav.ProjectName = task.ProjectName;
            fav.CustomerName = task.CustomerName;
            if (alias is not null) fav.Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
            if (keywords is not null)
                fav.Keywords = keywords.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (defaultComment is not null) fav.DefaultComment = string.IsNullOrWhiteSpace(defaultComment) ? null : defaultComment.Trim();
        }, ct);

        var saved = favs.First(f => f.TaskId == taskId);
        return new { added = saved, totalFavorites = favs.Count, file = store.FilePath };
    }

    [McpServerTool(Name = "remove_favorite_task", Idempotent = true)]
    [Description("Removes a task from the user's favourites (local only; the task itself is untouched in actiTIME).")]
    public async Task<object> RemoveFavoriteTask(
        [Description("Task id to remove from favourites.")] long taskId,
        CancellationToken ct = default)
    {
        var removed = false;
        var favs = await store.UpdateAsync(list => removed = list.RemoveAll(f => f.TaskId == taskId) > 0, ct);
        return new { removed, totalFavorites = favs.Count };
    }
}
