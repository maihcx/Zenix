param(
    [string]$Actor = "",
    [string]$Reason = "Auto-archive complete handoff before new feature."
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "common-actor-resolution.ps1")

$repoRoot = Get-BridgeRepoRoot
$Actor = Resolve-BridgeActor -Argument $Actor

$handoffPath = Join-Path $repoRoot ".specify\gss-handoff.json"
if (-not (Test-Path -LiteralPath $handoffPath)) {
    $handoffPath = Join-Path $repoRoot ".specify\superpowers-handoff.json"
}

if (-not (Test-Path -LiteralPath $handoffPath)) {
    Write-Output "No handoff file at $handoffPath; nothing to archive."
    return
}

$state = Get-Content -LiteralPath $handoffPath -Raw | ConvertFrom-Json
$currentStatus = if ($state.status) { [string]$state.status } else { "" }
if ($currentStatus -ne "complete") {
    Write-Output "No complete handoff to archive (current status: '$currentStatus')."
    return
}

$priorFeatureDirectory = if ($state.feature_directory) { [string]$state.feature_directory } else { "" }

$updateScript = Join-Path $ScriptDir "update-handoff.ps1"
& $updateScript -Status ready -ClearFeatureDirectory -ArtifactOwner unknown -Reason $Reason -Actor $Actor | Out-Null

$updatedState = Get-Content -LiteralPath $handoffPath -Raw | ConvertFrom-Json
$snapshotId = if ($updatedState.last_snapshot_id) { [string]$updatedState.last_snapshot_id } else { $null }

$event = [ordered]@{
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
    action = "archive"
    status = "archived"
    feature_directory = $priorFeatureDirectory
    decision = "archive"
    reason = $Reason
    actor = $Actor
    snapshot_id = $snapshotId
}
$eventPath = Join-Path $repoRoot ".specify\bridge-events.jsonl"
($event | ConvertTo-Json -Compress -Depth 4) + [Environment]::NewLine | Add-Content -LiteralPath $eventPath -Encoding UTF8

Write-Output "Auto-archived handoff for '$priorFeatureDirectory' (snapshot: $snapshotId)."
