<#
.SYNOPSIS
  Registers a Windows scheduled task that opens Copilot Chat (agent mode) with the timesheet prompt every weekday at 17:00.

.EXAMPLE
  ./scripts/register-daily-reminder.ps1
  ./scripts/register-daily-reminder.ps1 -At 17:30 -Client copilot-cli
  ./scripts/register-daily-reminder.ps1 -Unregister
#>
param(
    [string]$At = "17:00",
    [ValidateSet("vscode", "copilot-cli")]
    [string]$Client = "vscode",
    [string]$TaskName = "actiTIME daily timesheet",
    [switch]$Unregister
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

if ($Unregister) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Removed scheduled task '$TaskName'."
    return
}

if ($Client -eq "vscode") {
    # `code chat` opens the Chat view in the given workspace and submits the prompt; /timesheet runs .github/prompts/timesheet.prompt.md
    $code = (Get-Command code -ErrorAction Stop).Source
    $action = New-ScheduledTaskAction -Execute $code -Argument "chat --mode agent `"/timesheet`"" -WorkingDirectory $repo
}
else {
    # GitHub Copilot CLI, interactive session in Windows Terminal. Requires the server to be added via `/mcp add` in copilot.
    $prompt = "Use the actitime MCP server's daily_timesheet workflow: check what I've logged today, ask me what I worked on, then log it after I confirm."
    $action = New-ScheduledTaskAction -Execute "wt.exe" -Argument "-d `"$repo`" copilot -i `"$prompt`""
}

$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday, Tuesday, Wednesday, Thursday, Friday -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings `
    -Description "Opens GitHub Copilot to fill in today's actiTIME timesheet." -Force | Out-Null

Write-Host "Registered '$TaskName' for weekdays at $At using $Client."
Write-Host "Test it now with: Start-ScheduledTask -TaskName '$TaskName'"
