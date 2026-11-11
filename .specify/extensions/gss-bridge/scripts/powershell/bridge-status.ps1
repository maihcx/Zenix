# bridge-status.ps1 — read-only GSS-Bridge state and readiness introspection.
[CmdletBinding()]
param(
    [switch]$Json,
    [switch]$Readiness,
    [string]$Actor = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')
. (Join-Path $ScriptDir 'bridge-state.ps1')

$RepoRoot = Get-BridgeRepoRoot
$SpecifyDir = Join-Path $RepoRoot '.specify'
if (-not (Test-Path -LiteralPath $SpecifyDir -PathType Container)) {
    [Console]::Error.WriteLine('[bridge] not inside a Spec-Kit repository (.specify missing)')
    exit 2
}

$HandoffPath = Join-Path $SpecifyDir 'gss-handoff.json'
if (-not (Test-Path -LiteralPath $HandoffPath)) {
    $HandoffPath = Join-Path $SpecifyDir 'superpowers-handoff.json'
}

$ResolvedActor = Resolve-BridgeActor -Argument $Actor

$State = 'parseable'
$Handoff = $null
if (-not (Test-Path -LiteralPath $HandoffPath -PathType Leaf)) {
    $State = 'no-handoff'
} else {
    try {
        $Handoff = Get-Content -LiteralPath $HandoffPath -Raw | ConvertFrom-Json
    } catch {
        $State = 'corrupted'
    }
}

$FeatureDir = ''
$Status = ''
$Owner = ''

switch ($State) {
    'no-handoff' {
        $FeatureDir = '(none)'
        $Status = '(no handoff)'
        $Owner = 'unknown'
    }
    'corrupted' {
        $FeatureDir = '(unknown)'
        $Status = '(corrupted handoff)'
        $Owner = '(unknown)'
    }
    'parseable' {
        $FeatureDir = if ($Handoff.feature_directory) { [string]$Handoff.feature_directory } else { '(none)' }
        $Status = if ($Handoff.status) { [string]$Handoff.status } else { '(unknown)' }
        $Owner = if ($Handoff.artifact_owner) { [string]$Handoff.artifact_owner } else { 'unknown' }
    }
}

$PendingLabel = '(none)'
$PendingCount = -1
if ($FeatureDir -ne '(none)' -and $FeatureDir -ne '(unknown)') {
    $featureFull = if ([System.IO.Path]::IsPathRooted($FeatureDir)) { $FeatureDir } else { Join-Path $RepoRoot $FeatureDir }
    if (Test-Path -LiteralPath $featureFull) {
        $pRes = Get-PendingTaskCount -TasksPath (Join-Path $featureFull 'tasks.md')
        if ($pRes.TasksMdExists) {
            $PendingCount = $pRes.Count
            $PendingLabel = [string]$pRes.Count
        } else {
            $PendingLabel = '(no tasks.md)'
        }
    }
}

# Graphify check
$GraphPath = Join-Path $RepoRoot "graphify-out\graph.json"
$GraphExists = Test-Path -LiteralPath $GraphPath -PathType Leaf

# Next recommendation
$Next = switch ($Status) {
    'ready'     { "Run /speckit-plan or /speckit-tasks on $FeatureDir" }
    'executing' { "Run /speckit-gss-bridge-execute (or /gss-bridge) to implement pending tasks with TDD discipline" }
    'blocked'   { "Resolve contract gap in $FeatureDir, then resume with /gss-bridge" }
    'complete'  { "Feature is complete. Start a new feature with /speckit-specify" }
    default     { "Run /speckit-specify to create a new feature specification" }
}

if ($Readiness) {
    $hasPs = $true
    $hasGit = [bool](Get-Command git -ErrorAction SilentlyContinue)
    $hasPython = [bool](Get-Command python -ErrorAction SilentlyContinue) -or [bool](Get-Command python3 -ErrorAction SilentlyContinue)

    $toolsStatus = if ($hasPs -and $hasGit) { "ok" } else { "warning" }
    $pkgStatus = if (Test-Path -LiteralPath (Join-Path $SpecifyDir "extensions\gss-bridge\extension.yml")) { "ok" } else { "warning" }
    $graphStatus = if ($GraphExists) { "ready (graphify-out/graph.json)" } else { "not-indexed (run /graphify)" }

    if ($Json) {
        [ordered]@{
            script_flavor = "ps"
            required_tools = @{ status = $toolsStatus; git = $hasGit; python = $hasPython }
            namespace = @{ status = "ok"; prefix = "speckit.gss-bridge" }
            package_files = @{ status = $pkgStatus }
            bridge_state = @{ status = $State; feature = $FeatureDir; handoff_status = $Status; pending_tasks = $PendingCount }
            graphify = @{ status = if ($GraphExists) { "ok" } else { "not-built" }; indexed = $GraphExists }
            agents = @{ status = "verified"; supported = @("codex", "claude", "antigravity", "gemini") }
            next = $Next
        } | ConvertTo-Json -Depth 5
    } else {
        Write-Output "[bridge readiness]"
        Write-Output "  Script flavor: ps"
        Write-Output "  Required tools: $toolsStatus (git: $hasGit, python: $hasPython)"
        Write-Output "  Namespace: ok (speckit.gss-bridge)"
        Write-Output "  Package files: $pkgStatus"
        Write-Output "  Bridge state: $Status"
        Write-Output "  Graphify: $graphStatus"
        Write-Output "  Agents: verified (codex, claude, antigravity, gemini)"
        Write-Output "  Next: $Next"
    }
    exit 0
}

if ($Json) {
    [ordered]@{
        feature_directory = $FeatureDir
        status = $Status
        artifact_owner = $Owner
        actor = $ResolvedActor
        pending_tasks = $PendingCount
        graphify_indexed = $GraphExists
        next = $Next
    } | ConvertTo-Json
} else {
    Write-BridgeStateSummaryFull -HandoffPath $HandoffPath -RepoRoot $RepoRoot -Actor $ResolvedActor
    Write-Output "  Next: $Next"
}
exit 0
