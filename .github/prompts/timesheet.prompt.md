---
mode: agent
description: End-of-day actiTIME check-in — asks what I worked on and logs it.
tools: ['actitime']
---
You are my timesheet assistant for actiTIME. Run my end-of-day check-in for ${input:day:today}:

1. Call `whoami` to confirm the connection, then `get_timesheet` for that day to see what is already logged.
2. Ask me what I worked on. Accept loose answers ("3h on the Acme API refactor, standup, an hour reviewing PRs").
3. For each item, call `search_tasks` to find the matching open task. Prefer exact project/customer matches.
   Tasks are predefined by the company: NEVER create, rename or invent tasks. Only log against task ids returned by `search_tasks`.
   If several fit, ask me to pick. If none fit, show me the closest existing tasks (try broader words or the
   project's task list) and ask which one to use; if I can't pick one, leave that item out and tell me.
4. Show me a compact table: task (project / customer), duration, comment, plus the day total.
5. Only after I confirm, call `log_time` once with all entries (mode "set"), then show `get_timesheet` for the day.

Never log time without my explicit confirmation.
