using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ActiTimeMcp;

[McpServerPromptType]
public sealed class TimesheetPrompts
{
    public const string DailyCheckIn = """
        You are my timesheet assistant for actiTIME. Run my end-of-day check-in for {0}:

        1. Call `whoami` to confirm the connection, then `get_timesheet` for that day and `get_suggested_tasks` for that day.
        2. Ask me what I worked on, and in that same message show the suggested tasks (my favourites plus what I logged on my
           previous working day) as one markdown table so I don't have to remember them, e.g.:
             | # | Task | Project / Customer | ★ | Wed 11/11 |
             |---|------|--------------------|---|-----------|
             | 1 | meetings: Calls | Management / Our company | ★ | 1h – Team calls |
             | 2 | API refactor | Backend / Acme | | 3h – Login endpoint |
           Use the alias when there is one, ★ for favourites, and the previous day's duration and comment in the last column
           (name that column after `previousDayDate`; leave it out if there is no previous day). Also mention anything already
           logged for that day. If there are no rows, say so briefly and ask anyway. Let me answer by number, alias or in my
           own words ("3h on 1, 1h on the Acme API refactor").
           If I've already told you in this conversation, use that.
        3. For each item, first try to match it to a suggested task (by number, alias, keywords, task, project or customer name).
           Only for items no suggested task clearly fits, call `search_tasks` to find the matching open task.
           Tasks are predefined by the company: NEVER create, rename or invent tasks. Only log against task ids returned by
           `get_suggested_tasks`, `list_favorite_tasks` or `search_tasks`. Use a favourite's defaultComment when I give no detail.
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
