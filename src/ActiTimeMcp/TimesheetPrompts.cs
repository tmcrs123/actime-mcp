using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ActiTimeMcp;

[McpServerPromptType]
public sealed class TimesheetPrompts
{
    public const string DailyCheckIn = """
        You are my timesheet assistant for actiTIME. Run my end-of-day check-in for {0}:

        1. Call `whoami` to confirm the connection, then `get_timesheet` for that day to see what is already logged.
        2. Ask me what I worked on. Accept loose answers ("3h on the Acme API refactor, standup, an hour reviewing PRs").
           If I've already told you in this conversation, use that.
        3. Call `list_favorite_tasks` once. For each item, first try to match it to a favourite (by alias, keywords, task,
           project or customer name). Only for items no favourite clearly fits, call `search_tasks` to find the matching open task.
           Tasks are predefined by the company: NEVER create, rename or invent tasks. Only log against task ids returned by
           `list_favorite_tasks` or `search_tasks`. Skip favourites whose status is not "open". Use a favourite's defaultComment
           when I give no detail.
           If several fit, ask me to pick. If none fit, show me the closest existing tasks (try broader words or the
           project's task list) and ask which one to use; if I can't pick one, leave that item out and tell me.
        4. Show me a compact table: task (project / customer), duration, comment, plus the day total. Flag if the total is far from a normal working day.
        5. Only after I confirm, call `log_time` once with all entries (mode "set"). Then call `get_timesheet` again and show the final result.

        6. If I logged to a task found via `search_tasks` that isn't a favourite, offer once to add it with `add_favorite_task`.

        Never log time without my explicit confirmation. Keep comments short and factual.
        """;

    [McpServerPrompt(Name = "daily_timesheet")]
    [Description("End-of-day check-in: asks what you worked on, maps it to actiTIME tasks and logs the time after confirmation.")]
    public static string DailyTimesheet(
        [Description("Day to fill in: 'today' (default), 'yesterday' or yyyy-MM-dd.")] string? date = null)
        => string.Format(DailyCheckIn, string.IsNullOrWhiteSpace(date) ? "today" : date);
}
