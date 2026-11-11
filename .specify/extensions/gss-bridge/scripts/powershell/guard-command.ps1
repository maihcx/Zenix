param(
    [Parameter(Mandatory = $true)]
    [string]$Action,

    [string]$Reason = "",
    [string]$Actor = "",
    [string]$TargetFeatureDirectory = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "common-actor-resolution.ps1")
. (Join-Path $ScriptDir "bridge-state.ps1")

function Write-GuardEvent {
    param([string]$RepoRoot, [string]$ActionName, [string]$Decision, [string]$ReasonText, [string]$Actor, [string]$FeatureDirectory)
    $event = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        action = "guard"
        status = $Decision
        feature_directory = $FeatureDirectory
        decision = $Decision
        reason = $ReasonText
        actor = $Actor
        checked_action = $ActionName
    }
    $path = Join-Path $RepoRoot ".specify\bridge-events.jsonl"
    ($event | ConvertTo-Json -Compress -Depth 4) + [Environment]::NewLine | Add-Content -LiteralPath $path -Encoding UTF8
}

$repoRoot = Get-BridgeRepoRoot
$Actor = Resolve-BridgeActor -Argument $Actor

# Check handoff state (support gss-handoff.json and superpowers-handoff.json)
$handoffPath = Join-Path $repoRoot ".specify\gss-handoff.json"
if (-not (Test-Path -LiteralPath $handoffPath)) {
    $handoffPath = Join-Path $repoRoot ".specify\superpowers-handoff.json"
}

$handoffStatus = $null
$handoffFeatureDir = $null
if (Test-Path -LiteralPath $handoffPath) {
    try {
        $h = Get-Content -LiteralPath $handoffPath -Raw | ConvertFrom-Json
        if ($h.status) { $handoffStatus = [string]$h.status }
        if ($h.feature_directory) { $handoffFeatureDir = [string]$h.feature_directory }
    } catch { }
}

$decision = $null
$denyReason = $null

# Rule 0: Bug fix guard — assessment contract mandatory before fix
if ($Action -in @("speckit.bug.fix", "speckit.gss-bridge.bug-fix", "speckit-bug-fix", "speckit-gss-bridge-bug-fix", "gss-bridge.bug-fix", "gss-bridge-bug-fix")) {
    $targetBugDir = if ($TargetFeatureDirectory) { $TargetFeatureDirectory } elseif ($handoffFeatureDir -and $handoffFeatureDir.Contains("bugs")) { $handoffFeatureDir } else { "" }
    if ($targetBugDir) {
        $assessFile = Join-Path $repoRoot (Join-Path $targetBugDir "assessment.md")
        if (-not (Test-Path -LiteralPath $assessFile)) {
            $decision = "deny"
            $denyReason = "$Action blocked: assessment contract '$targetBugDir/assessment.md' not found. Run /speckit-gss-bridge-bug-assess with Graphify blast radius first."
        }
    }
}
# Rule 1: Deny speckit.implement when handoff is executing
elseif ($Action -eq "speckit.implement" -and $handoffStatus -eq "executing") {
    $decision = "deny"
    $denyReason = "speckit.implement blocked while GSS-Bridge handoff is executing; use /speckit-gss-bridge-execute (or /gss-bridge)"
}
# Rule 2: Deny superpowers:writing-plans from creating docs/plans/ when active feature exists
elseif ($Action -eq "superpowers:writing-plans" -and $handoffFeatureDir) {
    $specPath = Join-Path $repoRoot (Join-Path $handoffFeatureDir "spec.md")
    $planPath = Join-Path $repoRoot (Join-Path $handoffFeatureDir "plan.md")
    if ((Test-Path -LiteralPath $specPath) -and (Test-Path -LiteralPath $planPath)) {
        $decision = "deny"
        $denyReason = "Standalone planning in docs/plans/ is prohibited. Record TDD test matrices directly into $handoffFeatureDir/plan.md and tasks.md."
    }
}
# Rule 3: Deny superpowers:brainstorming when active feature has spec.md + plan.md
elseif ($Action -eq "superpowers:brainstorming" -and $handoffFeatureDir) {
    $specPath = Join-Path $repoRoot (Join-Path $handoffFeatureDir "spec.md")
    $planPath = Join-Path $repoRoot (Join-Path $handoffFeatureDir "plan.md")
    if ((Test-Path -LiteralPath $specPath) -and (Test-Path -LiteralPath $planPath)) {
        $decision = "deny"
        $denyReason = "Native superpowers brainstorming is forbidden while Spec-Kit owns active design artifacts"
    }
}
# Rule 4: Deny speckit.constitution while handoff is executing
elseif ($Action -eq "speckit.constitution" -and $handoffStatus -eq "executing") {
    $decision = "deny"
    $denyReason = "constitution edits blocked during active handoff; mark handoff blocked first"
}
# Rule 5: Allow any other speckit.*
elseif ($Action -like "speckit.*") {
    $decision = "allow"
}
# Rule 6 (default): allow
else {
    $decision = "allow"
}

# Multi-Agent Collision Check
if ($handoffStatus -eq "executing" -and $priorActor -and $priorActor -ne $Actor) {
    if ($TargetFeatureDirectory -and $handoffFeatureDir -and $TargetFeatureDirectory -ne $handoffFeatureDir) {
        Write-Output "[bridge WARNING] Multi-Agent Concurrency: Agent '$priorActor' is currently executing '$handoffFeatureDir'. Concurrent execution by '$Actor' on '$TargetFeatureDirectory' requires verification of zero blast radius overlap."
    }
}

Write-GuardEvent -RepoRoot $repoRoot -ActionName $Action -Decision $decision -ReasonText $denyReason -Actor $Actor -FeatureDirectory $handoffFeatureDir

Write-BridgeStateSummaryFull -HandoffPath $handoffPath -RepoRoot $repoRoot -Actor $Actor

if ($decision -eq "deny") {
    Write-Output "Guard denied $Action."
    if ($denyReason) { Write-Output "Reason: $denyReason" }
    exit 1
}

Write-Output "Guard allowed $Action."
exit 0
