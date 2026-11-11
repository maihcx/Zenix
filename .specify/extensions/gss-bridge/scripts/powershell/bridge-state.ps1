# bridge-state.ps1 — shared helper for state-summary + pending-task counting in GSS-Bridge.
$ErrorActionPreference = "Stop"

$script:BridgeTaskIdRegex = '^- \[ \] T\d+'
$script:BridgeDeferredHeaderRegex = '^#+\s+.*\b(deferred|optional|out of scope|won.?t do|future|wontfix|backlog)\b'

function Get-PendingTaskCount {
    param([Parameter(Mandatory = $true)][string]$TasksPath)

    if (-not (Test-Path -LiteralPath $TasksPath)) {
        return @{ Count = -1; TasksMdExists = $false }
    }

    $lines = Get-Content -LiteralPath $TasksPath -ErrorAction SilentlyContinue
    if (-not $lines) {
        return @{ Count = 0; TasksMdExists = $true }
    }

    $inExempt = $false
    $count = 0
    foreach ($line in $lines) {
        if ($line -match '^#+\s+') {
            $inExempt = ($line -imatch $script:BridgeDeferredHeaderRegex)
            continue
        }
        if (-not $inExempt -and $line -match $script:BridgeTaskIdRegex) {
            $count++
        }
    }
    return @{ Count = $count; TasksMdExists = $true }
}

function Get-ArtifactsSha256Map {
    param([Parameter(Mandatory = $true)][string]$FeatureFull)

    $result = [ordered]@{}
    foreach ($name in @('spec.md', 'plan.md', 'tasks.md')) {
        $path = Join-Path $FeatureFull $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $result[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower()
        } else {
            $result[$name] = $null
        }
    }
    return $result
}

function Get-BridgeStateLines {
    param(
        [string]$FeatureDirectory,
        [string]$Status,
        [string]$ArtifactOwner,
        [string]$Actor,
        [string]$PriorActor,
        [hashtable]$PendingResult,
        [string]$RepoRoot
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('[bridge state]')

    $dirLabel = if ([string]::IsNullOrWhiteSpace($FeatureDirectory)) { '(none)' } else { $FeatureDirectory }
    $lines.Add("  Feature directory: $dirLabel")

    $statusLabel = if ([string]::IsNullOrWhiteSpace($Status)) { '(unknown)' } else { $Status }
    $lines.Add("  Status: $statusLabel")

    $ownerLabel = if ([string]::IsNullOrWhiteSpace($ArtifactOwner)) { 'unknown' } else { $ArtifactOwner }
    $lines.Add("  Artifact owner: $ownerLabel")

    $actorLabel = if ([string]::IsNullOrWhiteSpace($Actor)) { 'unknown' } else { $Actor }
    if (-not [string]::IsNullOrWhiteSpace($PriorActor) -and $PriorActor -ne $actorLabel) {
        $lines.Add("  Actor: $PriorActor → $actorLabel")
    } else {
        $lines.Add("  Actor: $actorLabel")
    }

    if ($PendingResult) {
        if (-not $PendingResult.TasksMdExists) {
            $lines.Add("  Pending tasks: (no tasks.md)")
        } else {
            $lines.Add("  Pending tasks: $($PendingResult.Count)")
        }
    }

    # Graphify status
    if ($RepoRoot) {
        $graphPath = Join-Path $RepoRoot "graphify-out\graph.json"
        if (Test-Path -LiteralPath $graphPath) {
            $lines.Add("  Graphify: indexed (graph.json)")
        } else {
            $lines.Add("  Graphify: not built (run /graphify)")
        }
    }

    return ,$lines.ToArray()
}

function Write-BridgeStateSummaryFull {
    param(
        [string]$HandoffPath,
        [string]$RepoRoot,
        [string]$Actor,
        [string]$PriorActor = $null,
        [switch]$EmitCompleteWarning
    )

    $featureDir = ""
    $status = ""
    $owner = ""
    $pendingResult = $null

    if (Test-Path -LiteralPath $HandoffPath) {
        try {
            $h = Get-Content -LiteralPath $HandoffPath -Raw | ConvertFrom-Json
            if ($h.feature_directory) { $featureDir = [string]$h.feature_directory }
            if ($h.status) { $status = [string]$h.status }
            if ($h.artifact_owner) { $owner = [string]$h.artifact_owner }

            if ($featureDir) {
                $featureFull = if ([System.IO.Path]::IsPathRooted($featureDir)) { $featureDir } else { Join-Path $RepoRoot $featureDir }
                $tasksPath = Join-Path $featureFull "tasks.md"
                $pendingResult = Get-PendingTaskCount -TasksPath $tasksPath
            }
        } catch { }
    }

    $lines = Get-BridgeStateLines -FeatureDirectory $featureDir -Status $status -ArtifactOwner $owner -Actor $Actor -PriorActor $PriorActor -PendingResult $pendingResult -RepoRoot $RepoRoot
    foreach ($l in $lines) { Write-Output $l }

    if ($EmitCompleteWarning -and $status -eq "complete" -and $pendingResult -and $pendingResult.Count -gt 0) {
        [Console]::Error.WriteLine("[bridge] WARNING: Feature marked complete but $($pendingResult.Count) task(s) remain unchecked in tasks.md")
    }
}
