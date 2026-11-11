# Prompts for your actiTIME login and password and stores them with dotnet user-secrets (outside the repo).
$ErrorActionPreference = "Stop"
$project = Join-Path (Split-Path -Parent $PSScriptRoot) "src/ActiTimeMcp"

$user = Read-Host "actiTIME username"
$secure = Read-Host "actiTIME password" -AsSecureString
$plain = [System.Net.NetworkCredential]::new("", $secure).Password

dotnet user-secrets --project $project set "ActiTime:Username" $user | Out-Null
dotnet user-secrets --project $project set "ActiTime:Password" $plain | Out-Null
Write-Host "Saved. Base URL, username and password are configured."
