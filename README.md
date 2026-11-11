# actiTIME MCP server (.NET)

An MCP server that lets GitHub Copilot (or any MCP client) read and fill in your
[actiTIME](https://www.actitime.com/api-documentation) timesheet conversationally:

> "Today: 4h on the Acme checkout refactor, 30m standup, and about 1.5h reviewing PRs for Globex."

Copilot finds the matching tasks, shows you a table, and logs it once you confirm.

Tasks are predefined in actiTIME: the server has **no way to create tasks** and only logs time against existing ones.

## Tools

| Tool | What it does |
|---|---|
| `whoami` | Confirms credentials, returns your user and today's date |
| `list_customers` / `list_projects` | Browse customers and projects (name filter) |
| `list_favorite_tasks` | Your usual tasks (checked first, before any search), with live status |
| `add_favorite_task` / `remove_favorite_task` | Manage favourites conversationally |
| `search_tasks` | Find open tasks by words / project / customer (fallback when no favourite fits) |
| `log_time` | Log one or many entries (`set` = overwrite cell, `add` = add to it). Durations like `1h30`, `1.5h`, `45m`, `1:30`; dates like `today`, `yesterday`, `monday`, `2026-11-11` |
| `get_timesheet` | What you've already logged for a day or range, with task/project names |
| `parse_duration` | Helper to sanity-check durations |

Prompt: `daily_timesheet` – the end-of-day check-in workflow.

## Setup

Requires the .NET 10 SDK and an actiTIME **Online or Self-Hosted** edition with API access (2019.2+).
The API uses your normal actiTIME login (HTTP Basic auth).

```bash
dotnet publish src/ActiTimeMcp -c Release -o publish
```

Store credentials with user secrets (kept outside the repo, in your Windows profile):

```bash
dotnet user-secrets --project src/ActiTimeMcp set "ActiTime:BaseUrl" "https://yourcompany.actitime.com"
```

```bash
dotnet user-secrets --project src/ActiTimeMcp set "ActiTime:Username" "your.login"
```

```bash
dotnet user-secrets --project src/ActiTimeMcp set "ActiTime:Password" "your-password"
```

Alternatively set env vars `ACTITIME_URL`, `ACTITIME_USERNAME`, `ACTITIME_PASSWORD`.
Optional: `ActiTime:TimeZone` (e.g. `GMT Standard Time`) if your machine's zone differs from your actiTIME day.

## Use with GitHub Copilot in VS Code

This repo already has `.vscode/mcp.json`. Open the folder in VS Code, start the `actitime` server from the
MCP view (or the Start code lens in `mcp.json`), switch Chat to **Agent** mode and type `/timesheet`
(the prompt file at `.github/prompts/timesheet.prompt.md`).

To have it available in every workspace, add the same entry to your user-level MCP config
(Command Palette → *MCP: Open User Configuration*) using the absolute path to `publish\ActiTimeMcp.exe`,
and copy the prompt file to your user prompts folder.

### GitHub Copilot CLI

```bash
copilot
```

Then `/mcp add`, name `actitime`, type `stdio`, command = full path to `publish\ActiTimeMcp.exe`.

## Favourite tasks

Most days you log to the same handful of tasks. Mark them as favourites and the check-in matches against them
first, only searching all of actiTIME when nothing fits.

Manage them by just asking Copilot, e.g.:

> "Add the Calls task to my favourites as 'meetings', keywords standup, sync, default comment 'Team calls'."
> "Show my favourite tasks." / "Remove Supplier selection from favourites."

They're stored locally (never in actiTIME) in `%APPDATA%ctitime-mcpavorites.json`, which you can also edit by hand:

```json
[
  { "taskId": 135, "alias": "meetings", "keywords": ["standup", "sync"], "defaultComment": "Team calls" },
  { "taskId": 116, "alias": "suppliers" }
]
```

Only `taskId` is required; the server fills in task/project/customer names. Override the location with
`ActiTime:FavoritesFile` (or env var `ACTITIME__FAVORITESFILE`).

## The 5 p.m. reminder

Copilot itself has no "ask me at 5 p.m." trigger (the cloud coding agent runs unattended and can't chat with you),
so a Windows scheduled task opens Copilot with the prompt at 5 p.m. and you answer it:

```bash
pwsh ./scripts/register-daily-reminder.ps1
```

Options: `-At 17:30`, `-Client copilot-cli` (opens Copilot CLI in Windows Terminal instead of VS Code), `-Unregister`.

## Notes

* Durations are stored in minutes by actiTIME; a bare number ≤ 12 is treated as hours, > 12 as minutes.
* `log_time` in `set` mode is idempotent: re-running the check-in won't double-count.
* Rate limits (HTTP 429) are retried automatically using `Retry-After`.
* Three failed logins within 10 seconds get your IP banned for a minute, so fix credentials before retrying.
