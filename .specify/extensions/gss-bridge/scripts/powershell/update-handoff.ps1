param(
    [ValidateSet("ready", "executing", "blocked", "complete")]
    [string]$Status = "ready",

    [string]$FeatureDirectory = "",

    [string]$Reason = "",

    [string]$ArtifactOwner = "",

    [string[]]$ReviewOnlyAgents = @(),

    [string]$Actor = "",

    [switch]$ClearFeatureDirectory,

    [psobject]$AppendArchiveEntry = $null
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "common-actor-resolution.ps1")
. (Join-Path $ScriptDir "bridge-state.ps1")

function Convert-ToProjectPath {
    param([string]$RepoRoot, [string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ($fullPath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($rootPath.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Replace("\", "/")
    }
    return $fullPath.Replace("\", "/")
}

function Write-BridgeEvent {
    param(
        [string]$RepoRoot, [string]$Action, [string]$Status,
        [string]$FeatureDirectory, [string]$Decision, [string]$Reason,
        [string]$SnapshotId, [string]$Actor, [string]$PriorActor = $null
    )
    $event = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        action = $Action
        status = $Status
        feature_directory = $FeatureDirectory
        decision = $Decision
        reason = $Reason
        actor = $Actor
        prior_actor = $PriorActor
        snapshot_id = $SnapshotId
    }
    $eventPath = Join-Path $RepoRoot ".specify\bridge-events.jsonl"
    ($event | ConvertTo-Json -Compress -Depth 4) + [Environment]::NewLine | Add-Content -LiteralPath $eventPath -Encoding UTF8
}

function Ensure-GraphifyExclusions {
    param([string]$RepoRoot)
    if ([string]::IsNullOrWhiteSpace($RepoRoot) -or -not (Test-Path -LiteralPath $RepoRoot)) { return }
    $ignorePath = Join-Path $RepoRoot ".graphifyignore"
    $mandatoryPatterns = @(
        ".specify/",
        ".agents/",
        ".claude/",
        ".gemini/",
        ".cursor/",
        ".windsurf/",
        ".kilo/",
        ".devin/",
        ".codex/",
        ".copilot/",
        ".ai/",
        "graphify-out/",
        "raw/",
        ".graphify*"
    )
    if (Test-Path -LiteralPath $ignorePath) {
        $existing = Get-Content -LiteralPath $ignorePath -Raw
        $missing = New-Object System.Collections.Generic.List[string]
        foreach ($pat in $mandatoryPatterns) {
            if ($existing -notmatch [regex]::Escape($pat)) {
                $missing.Add($pat)
            }
        }
        if ($missing.Count -gt 0) {
            $appendBlock = [Environment]::NewLine + "# GSS-Bridge Meta-Tooling Exclusions" + [Environment]::NewLine + ($missing -join [Environment]::NewLine) + [Environment]::NewLine
            [System.IO.File]::AppendAllText($ignorePath, $appendBlock, [System.Text.Encoding]::UTF8)
        }
    } else {
        $defaultIgnore = @"
# =====================================================================
# Graphify Ignore Rules: AI Knowledge & Meta-Tooling Exclusions
# Generated and maintained by GSS-Bridge Protocol
# =====================================================================

# 1. Spec-Kit & Agent Governance Frameworks (AI Knowledge & Directives)
.specify/
.agents/
.claude/
.gemini/
.cursor/
.windsurf/
.kilo/
.devin/
.codex/
.copilot/
.ai/

# 2. Graphify Internal Engine, Intermediate Processing & Artifacts
graphify-out/
raw/
.graphify*

# 3. Transpiled Bundles, Minified Workers & Build Artifacts
node_modules/
dist/
build/
bin/
obj/
out/
target/
.cache/
*.min.js
*.min.css
*.worker.js
*.worker-*.js
*.bundle.js
editorWorker*.js
"@
        [System.IO.File]::WriteAllText($ignorePath, $defaultIgnore.Replace("`r`n", "`n").Replace("`n", "`r`n"), [System.Text.Encoding]::UTF8)
    }

    $outDir = Join-Path $RepoRoot "graphify-out"
    if (Test-Path -LiteralPath $outDir) {
        $buildCfg = Join-Path $outDir ".graphify_build.json"
        try {
            $cfgExcludes = New-Object System.Collections.Generic.List[string]
            if (Test-Path -LiteralPath $buildCfg) {
                try {
                    $parsed = Get-Content -LiteralPath $buildCfg -Raw | ConvertFrom-Json
                    if ($parsed -and $parsed.excludes) {
                        foreach ($item in $parsed.excludes) {
                            $cfgExcludes.Add([string]$item)
                        }
                    }
                } catch {}
            }
            foreach ($pat in $mandatoryPatterns) {
                if (-not $cfgExcludes.Contains($pat)) {
                    $cfgExcludes.Add($pat)
                }
            }
            $hash = [ordered]@{ excludes = @($cfgExcludes); gitignore = $true }
            $json = $hash | ConvertTo-Json -Depth 4
            [System.IO.File]::WriteAllText($buildCfg, $json, [System.Text.Encoding]::UTF8)
        } catch {}
    }
}
function New-BridgeSnapshot {
    param([string]$RepoRoot, [string]$Status, [string]$FeatureDirectoryFullPath)
    if ([string]::IsNullOrWhiteSpace($FeatureDirectoryFullPath) -or -not (Test-Path -LiteralPath $FeatureDirectoryFullPath)) { return $null }
    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssfffffffZ")
    $snapshotId = "$timestamp-$Status"
    $snapshotRoot = Join-Path $RepoRoot ".specify\bridge-snapshots\$snapshotId"
    New-Item -ItemType Directory -Force -Path $snapshotRoot | Out-Null
    foreach ($artifact in @("spec.md", "plan.md", "tasks.md")) {
        $p = Join-Path $FeatureDirectoryFullPath $artifact
        if (Test-Path -LiteralPath $p) { Copy-Item -LiteralPath $p -Destination (Join-Path $snapshotRoot $artifact) -Force }
    }
    $constitutionSrc = Join-Path $RepoRoot ".specify\memory\constitution.md"
    if (Test-Path -LiteralPath $constitutionSrc) { Copy-Item -LiteralPath $constitutionSrc -Destination (Join-Path $snapshotRoot "constitution.md") -Force }
    return $snapshotId
}

$repoRoot = Get-BridgeRepoRoot
$Actor = Resolve-BridgeActor -Argument $Actor
Ensure-GraphifyExclusions -RepoRoot $repoRoot

$specifyDir = Join-Path $repoRoot ".specify"
if (-not (Test-Path -LiteralPath $specifyDir)) {
    New-Item -ItemType Directory -Force -Path $specifyDir | Out-Null
}

$existingHandoffPath = Join-Path $specifyDir "gss-handoff.json"
if (-not (Test-Path -LiteralPath $existingHandoffPath)) {
    $existingHandoffPath = Join-Path $specifyDir "superpowers-handoff.json"
}

$priorFeatureDirectory = $null
$priorArtifactOwner = $null
$priorActor = $null
$priorArtifactsSha256 = $null

if (Test-Path -LiteralPath $existingHandoffPath) {
    try {
        $existing = Get-Content -LiteralPath $existingHandoffPath -Raw | ConvertFrom-Json
        if ($existing.feature_directory) { $priorFeatureDirectory = [string]$existing.feature_directory }
        if ($existing.artifact_owner) { $priorArtifactOwner = [string]$existing.artifact_owner }
        if ($existing.artifacts_sha256) { $priorArtifactsSha256 = $existing.artifacts_sha256 }
    } catch { }

    $eventLogPath = Join-Path $specifyDir "bridge-events.jsonl"
    if (Test-Path -LiteralPath $eventLogPath) {
        $lastLines = Get-Content -LiteralPath $eventLogPath -Tail 20 -ErrorAction SilentlyContinue
        if ($lastLines) {
            for ($i = $lastLines.Count - 1; $i -ge 0; $i--) {
                try {
                    $ev = $lastLines[$i] | ConvertFrom-Json
                    if ($ev.action -eq "handoff" -and $ev.actor) {
                        $priorActor = [string]$ev.actor
                        break
                    }
                } catch { }
            }
        }
    }
}

$targetFeatureDir = if ($ClearFeatureDirectory) { $null } elseif ($FeatureDirectory) { $FeatureDirectory } else { $priorFeatureDirectory }

$featureDirectoryFullPath = $null
$featureDirectoryProjectPath = $null

if ($targetFeatureDir) {
    $featureDirectoryFullPath = if ([System.IO.Path]::IsPathRooted($targetFeatureDir)) { [System.IO.Path]::GetFullPath($targetFeatureDir) } else { [System.IO.Path]::GetFullPath((Join-Path $repoRoot $targetFeatureDir)) }
    $featureDirectoryProjectPath = Convert-ToProjectPath -RepoRoot $repoRoot -Path $featureDirectoryFullPath
}

$isBugWorkflow = ($featureDirectoryProjectPath -and $featureDirectoryProjectPath.Contains("bugs"))
$sourceOfTruth = if ($isBugWorkflow) {
    [ordered]@{
        constitution = ".specify/memory/constitution.md"
        assessment = "$featureDirectoryProjectPath/assessment.md"
        fix = "$featureDirectoryProjectPath/fix.md"
    }
} else {
    [ordered]@{
        constitution = ".specify/memory/constitution.md"
        spec = $null
        plan = $null
        tasks = $null
    }
}

$missing = New-Object System.Collections.Generic.List[string]
if ($featureDirectoryFullPath) {
    if ($isBugWorkflow) {
        $assessFull = Join-Path $featureDirectoryFullPath "assessment.md"
        if (-not (Test-Path -LiteralPath $assessFull)) {
            $missing.Add("$featureDirectoryProjectPath/assessment.md")
        }
    } else {
        foreach ($pair in @(@{ k = "spec"; f = "spec.md" }, @{ k = "plan"; f = "plan.md" }, @{ k = "tasks"; f = "tasks.md" })) {
            $artifactFull = Join-Path $featureDirectoryFullPath $pair.f
            $sourceOfTruth[$pair.k] = Convert-ToProjectPath -RepoRoot $repoRoot -Path $artifactFull
            if (-not (Test-Path -LiteralPath $artifactFull)) { $missing.Add($sourceOfTruth[$pair.k]) }
        }
    }
}

$resolvedStatus = $Status
$blockedReason = $null
if ($missing.Count -gt 0 -and $Status -ne "complete" -and $Status -ne "ready") {
    $resolvedStatus = "blocked"
    $blockedReason = if ([string]::IsNullOrWhiteSpace($Reason)) { "Missing required Spec-Kit artifacts: " + (($missing.ToArray()) -join ", ") } else { $Reason }
}
elseif ($Status -eq "blocked") {
    $blockedReason = if ([string]::IsNullOrWhiteSpace($Reason)) { "(no reason provided)" } else { $Reason }
}
$eventReason = if ([string]::IsNullOrWhiteSpace($Reason)) { $blockedReason } else { $Reason }

$owner = if ($ArtifactOwner) { $ArtifactOwner } elseif ($priorArtifactOwner) { $priorArtifactOwner } elseif ($Actor -in @("codex", "claude", "antigravity")) { $Actor } else { "unknown" }
$reviewOnly = @($ReviewOnlyAgents | Where-Object { $_ -and $_ -ne $owner } | Select-Object -Unique)

$snapshotId = $null
if ($featureDirectoryFullPath -and (Test-Path -LiteralPath $featureDirectoryFullPath)) {
    $snapshotId = New-BridgeSnapshot -RepoRoot $repoRoot -Status $resolvedStatus -FeatureDirectoryFullPath $featureDirectoryFullPath
}

$freshArtifactsSha256 = $null
if ($resolvedStatus -eq 'executing' -or $resolvedStatus -eq 'complete') {
    if ($featureDirectoryFullPath -and (Test-Path -LiteralPath $featureDirectoryFullPath)) {
        $freshArtifactsSha256 = Get-ArtifactsSha256Map -FeatureFull $featureDirectoryFullPath
    } else {
        $freshArtifactsSha256 = [ordered]@{ 'spec.md' = $null; 'plan.md' = $null; 'tasks.md' = $null }
    }
}

$handoff = [ordered]@{
    schema_version = 1
    updated_at = (Get-Date).ToUniversalTime().ToString("o")
    feature_directory = $featureDirectoryProjectPath
    source_of_truth = $sourceOfTruth
    supersedes = @("speckit.implement")
    executor = if ($resolvedStatus -eq "ready") { "speckit" } else { "gss-bridge" }
    capabilities = @(
        "graphify-codebase-intelligence",
        "blast-radius-analysis",
        "task-parallelism-verification",
        "executing-plans",
        "test-driven-development",
        "subagent-scoping",
        "systematic-debugging",
        "verification-before-completion",
        "requesting-code-review",
        "finishing-a-development-branch"
    )
    status = $resolvedStatus
    blocked_reason = $blockedReason
    artifact_owner = $owner
    review_only_agents = @($reviewOnly)
    last_snapshot_id = $snapshotId
    artifacts_sha256 = $freshArtifactsSha256
    instructions = "Use /gss-bridge or /speckit-gss-bridge-execute. GSS-Bridge combines Spec-Kit governance, Graphify topology context, and Superpowers TDD execution discipline."
}

# Write gss-handoff.json
$gssHandoffPath = Join-Path $specifyDir "gss-handoff.json"
$handoff | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $gssHandoffPath -Encoding UTF8

# Write superpowers-handoff.json for backward compatibility
$spHandoffPath = Join-Path $specifyDir "superpowers-handoff.json"
$handoff | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $spHandoffPath -Encoding UTF8

$eventReasonAugmented = $eventReason
if ($priorActor -and $priorActor -ne $Actor) {
    $changeNote = "actor change $priorActor -> $Actor"
    $eventReasonAugmented = if ([string]::IsNullOrWhiteSpace($eventReasonAugmented)) { $changeNote } else { "$changeNote; $eventReasonAugmented" }
}

Write-BridgeEvent -RepoRoot $repoRoot -Action "handoff" -Status $resolvedStatus -FeatureDirectory $featureDirectoryProjectPath -Decision "updated" -Reason $eventReasonAugmented -SnapshotId $snapshotId -Actor $Actor -PriorActor $priorActor

# Auto Graphify Sync upon completion
if ($resolvedStatus -eq "complete" -and (Test-Path -LiteralPath (Join-Path $repoRoot "graphify-out\graph.json"))) {
    if (Get-Command "graphify" -ErrorAction SilentlyContinue) {
        try {
            Write-Output "[bridge] Triggering automatic Graphify sync (graphify --update)..."
            & graphify update . | Out-Null
            Write-Output "[bridge] Knowledge graph synchronized."
        } catch {
            Write-Verbose "Auto graphify sync failed: $($_.Exception.Message)"
        }
    }
}

Write-BridgeStateSummaryFull -HandoffPath $gssHandoffPath -RepoRoot $repoRoot -Actor $Actor -PriorActor $priorActor -EmitCompleteWarning:($resolvedStatus -eq "complete")

Write-Output "Wrote .specify/gss-handoff.json with status '$resolvedStatus'."
