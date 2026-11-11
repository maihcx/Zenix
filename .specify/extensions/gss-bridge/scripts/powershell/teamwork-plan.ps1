# teamwork-plan.ps1 — Autonomous Multi-Agent Teamwork Planner with Graphify Scoping & Sentinel Gate.
[CmdletBinding()]
param(
    [string]$TasksPath = "",
    [string]$FeatureDirectory = "",
    [string]$Actor = "",
    [switch]$PruneContext,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')
. (Join-Path $ScriptDir 'bridge-state.ps1')

$RepoRoot = Get-BridgeRepoRoot
$SpecifyDir = Join-Path $RepoRoot ".specify"
$ResolvedActor = Resolve-BridgeActor $Actor

# Locate tasks.md
$resolvedTasksPath = $TasksPath
if (-not $resolvedTasksPath) {
    $targetDir = $FeatureDirectory
    if (-not $targetDir) {
        $handoffPath = Join-Path $SpecifyDir "gss-handoff.json"
        if (Test-Path -LiteralPath $handoffPath) {
            $handoff = Get-Content -LiteralPath $handoffPath -Raw | ConvertFrom-Json
            $targetDir = $handoff.feature_directory
        }
    }
    if ($targetDir) {
        $candidate = Join-Path $RepoRoot "$targetDir\tasks.md"
        if (Test-Path -LiteralPath $candidate) {
            $resolvedTasksPath = $candidate
        }
    }
}

if (-not $resolvedTasksPath -or -not (Test-Path -LiteralPath $resolvedTasksPath)) {
    Write-Error "[gss-bridge] tasks.md could not be found. Provide -TasksPath or ensure active feature has tasks.md."
    exit 2
}

# Parse tasks.md
$lines = Get-Content -LiteralPath $resolvedTasksPath
$pTasks = New-Object System.Collections.Generic.List[psobject]

foreach ($line in $lines) {
    if ($line -match '^\s*-\s*\[\s*\]\s*(T\d+)\s+\[P\]\s+(.*)$') {
        $pTasks.Add([pscustomobject]@{
            Id = $Matches[1]
            Description = $Matches[2].Trim()
            Raw = $line.Trim()
            SuspectNode = ""
            TargetFiles = @()
        })
    }
}

if ($pTasks.Count -eq 0) {
    if ($Json) {
        @{ status = "no_parallel_tasks"; message = "No pending [P] tasks found in tasks.md." } | ConvertTo-Json
    } else {
        Write-Output "[gss-bridge teamwork] No pending [P] tasks found in $resolvedTasksPath."
        Write-Output "Tasks should be executed sequentially."
    }
    exit 0
}

# Load graphify-out/graph.json
$GraphPath = Ensure-GraphifyIndex -RepoRoot $RepoRoot -Silent:($Json -eq $true)
$Nodes = @{}
$Neighbors = @{}

if (Test-Path -LiteralPath $GraphPath) {
    $graphData = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json
    foreach ($n in $graphData.nodes) {
        $Nodes[$n.id] = $n
        $Neighbors[$n.id] = New-Object System.Collections.Generic.HashSet[string]
    }
    $edges = if ($graphData.links) { $graphData.links } elseif ($graphData.edges) { $graphData.edges } else { @() }
    foreach ($e in $edges) {
        $u = [string]$e.source
        $v = [string]$e.target
        if ($Neighbors.ContainsKey($u) -and $Neighbors.ContainsKey($v)) {
            [void]$Neighbors[$u].Add($v)
            [void]$Neighbors[$v].Add($u)
        }
    }
}

# Match nodes for each task
foreach ($task in $pTasks) {
    $desc = $task.Description
    $matchedId = $null
    foreach ($nid in $Nodes.Keys) {
        $lbl = [string]$Nodes[$nid].label
        if ($lbl -and $desc.IndexOf($lbl, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $matchedId = $nid
            break
        } elseif ($nid -and $desc.IndexOf($nid, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $matchedId = $nid
            break
        }
    }
    if ($matchedId) {
        $task.SuspectNode = $matchedId
        $targetFile = $Nodes[$matchedId].source_file
        if ($targetFile) { $task.TargetFiles = @($targetFile) }
    }
}

# Compute BFS distance between two nodes
function Get-GraphDistance {
    param([string]$a, [string]$b)
    if (-not $a -or -not $b -or -not $Neighbors.ContainsKey($a) -or -not $Neighbors.ContainsKey($b)) {
        return 999
    }
    if ($a -eq $b) { return 0 }

    $visited = New-Object System.Collections.Generic.HashSet[string]
    $queue = New-Object System.Collections.Generic.Queue[psobject]
    $queue.Enqueue([pscustomobject]@{ Node = $a; Dist = 0 })
    [void]$visited.Add($a)

    while ($queue.Count -gt 0) {
        $curr = $queue.Dequeue()
        if ($curr.Node -eq $b) { return $curr.Dist }
        if ($curr.Dist -lt 3) {
            foreach ($nbr in $Neighbors[$curr.Node]) {
                if (-not $visited.Contains($nbr)) {
                    [void]$visited.Add($nbr)
                    $queue.Enqueue([pscustomobject]@{ Node = $nbr; Dist = $curr.Dist + 1 })
                }
            }
        }
    }
    return 999
}

# Wave Partitioning
$waves = New-Object System.Collections.Generic.List[System.Collections.Generic.List[psobject]]

foreach ($task in $pTasks) {
    $placed = $false
    foreach ($wave in $waves) {
        $conflict = $false
        foreach ($existing in $wave) {
            if ($task.SuspectNode -and $existing.SuspectNode) {
                $dist = Get-GraphDistance $task.SuspectNode $existing.SuspectNode
                if ($dist -le 2) {
                    $conflict = $true
                    break
                }
            }
        }
        if (-not $conflict) {
            $wave.Add($task)
            $placed = $true
            break
        }
    }
    if (-not $placed) {
        $newWave = New-Object System.Collections.Generic.List[psobject]
        $newWave.Add($task)
        $waves.Add($newWave)
    }
}

# Build Teamwork Plan
$allTargetNodes = @($pTasks | Where-Object { $_.SuspectNode } | ForEach-Object { $_.SuspectNode } | Select-Object -Unique)
$allNodesStr = ($allTargetNodes -join ", ")
if (-not $allNodesStr) { $allNodesStr = "all modified components" }

$orchestratorRole = [ordered]@{
    role = "GSS Lead Orchestrator"
    description = "Orchestrates multi-agent wave execution, manages .specify/gss-handoff.json, spins up isolated worktrees, and finalizes merge verification."
    responsibilities = @(
        "Read spec.md, plan.md, and tasks.md to preserve global architecture contracts",
        "Manage isolated git worktrees via wave-worktree.ps1 (-Action create, merge, cleanup)",
        "Dispatch parallel Coder subagents per wave via invoke_subagent",
        "Trigger Sentinel Reviewer subagent for verification gate before merging worktrees",
        "Update knowledge graph via graphify update . and log decisions in .specify/agent-decisions.jsonl"
    )
}

$sentinelRole = [ordered]@{
    role = "GSS Sentinel Adversarial Reviewer"
    description = "Independent verification gate guarding against regression, circular dependency cycles, and architectural drift before worktree merge."
    verification_commands = @(
        ".\.specify\extensions\gss-bridge\scripts\powershell\test-impact.ps1 -Node `"$allNodesStr`" -DetectChanged",
        ".\.specify\extensions\gss-bridge\scripts\powershell\drift-detector.ps1 -FailOnCycles"
    )
    subagent_prompt = @"
Role: GSS Sentinel Adversarial Reviewer
Task: Perform independent verification gate on completed wave worktrees.

Verification Steps:
1. Run Test Impact Analysis:
   .\.specify\extensions\gss-bridge\scripts\powershell\test-impact.ps1 -Node "$allNodesStr" -DetectChanged
2. Run Architectural Drift & Circular Dependency check:
   .\.specify\extensions\gss-bridge\scripts\powershell\drift-detector.ps1 -FailOnCycles
3. Execute targeted test suites and examine test runner output.
4. If any test fails or architecture cycles are detected, REJECT with actionable feedback.
5. If all tests pass with evidence, APPROVE the wave for merge.
"@
}

$waveResults = @()
$waveIdx = 1

foreach ($wave in $waves) {
    $coderList = @()
    foreach ($t in $wave) {
        $files = if ($t.TargetFiles.Count -gt 0) { $t.TargetFiles } else { @("Identified by Graphify for $($t.SuspectNode)") }
        $safeNode = if ($t.SuspectNode) { ($t.SuspectNode -replace '[^a-zA-Z0-9_-]', '').ToLower() } else { "task" }
        $wtName = "wave-$waveIdx-$safeNode-$($t.Id.ToLower())"
        
        $promptLines = @(
            "Task: [$($t.Id)] $($t.Description)",
            "Role: GSS Coder Subagent",
            "Assigned Worktree: .specify/worktrees/$wtName (branch: gss-worktree/$wtName)",
            "Scoped Files: $(($files -join ', '))",
            "",
            "Strict TDD & Verification Instructions:",
            "1. RED: Write a failing unit/integration test first in the assigned worktree. Confirm it fails.",
            "2. GREEN: Write minimal code within scoped files to pass the test.",
            "3. REFACTOR: Clean up code while keeping test suite green.",
            "4. EVIDENCE: Run verification commands and return actual output to Lead Orchestrator.",
            "",
            "Constraints: Stay strictly within assigned worktree and scoped files. Do not modify unrelated components."
        )
        $subagentPrompt = $promptLines -join "`r`n"

        $coderList += [ordered]@{
            task_id = $t.Id
            role = "GSS Coder Subagent: $($t.Id)"
            description = $t.Description
            target_node = if ($t.SuspectNode) { $t.SuspectNode } else { "(unmapped)" }
            worktree_name = $wtName
            worktree_path = ".specify/worktrees/$wtName"
            worktree_branch = "gss-worktree/$wtName"
            scoped_files = $files
            prune_context = $PruneContext.IsPresent
            subagent_prompt = $subagentPrompt
        }
    }

    $waveResults += [ordered]@{
        wave_number = $waveIdx
        parallel_coders_count = $wave.Count
        coders = $coderList
    }
    $waveIdx++
}

if ($Json) {
    [ordered]@{
        teamwork_protocol = "GSS-Teamwork v1.0"
        tasks_file = $resolvedTasksPath
        total_parallel_tasks = $pTasks.Count
        total_waves = $waves.Count
        roster = [ordered]@{
            orchestrator = $orchestratorRole
            sentinel = $sentinelRole
            waves = $waveResults
        }
    } | ConvertTo-Json -Depth 6
} else {
    Write-Output "================================================================="
    Write-Output "GSS-BRIDGE AUTONOMOUS MULTI-AGENT TEAMWORK PLAN"
    Write-Output "================================================================="
    Write-Output "Source Tasks: $resolvedTasksPath"
    Write-Output "Total [P] Tasks: $($pTasks.Count) across $($waves.Count) execution wave(s)"
    Write-Output ""
    Write-Output "### 1. Lead Orchestrator"
    Write-Output "- Role: $($orchestratorRole.role)"
    Write-Output "- Responsibilities: Coordinates worktrees, subagent waves, and sentinel verification gate."
    Write-Output ""
    Write-Output "### 2. Sentinel Adversarial Reviewer"
    Write-Output "- Role: $($sentinelRole.role)"
    Write-Output "- Verification Commands:"
    foreach ($cmd in $sentinelRole.verification_commands) {
        Write-Output "  - $cmd"
    }
    Write-Output ""
    Write-Output "### 3. Coder Subagents (Wave by Wave Worktree Allocations)"
    for ($i = 0; $i -lt $waves.Count; $i++) {
        $wNum = $i + 1
        $currentWave = $waveResults[$i]
        Write-Output "#### Wave $wNum (Dispatch $($currentWave.parallel_coders_count) Parallel Coder Subagent$(if ($currentWave.parallel_coders_count -gt 1) { 's' } else { '' }))"
        foreach ($c in $currentWave.coders) {
            Write-Output "- **[$($c.task_id)]**: $($c.description)"
            Write-Output "  - Target Node: $($c.target_node)"
            Write-Output "  - Isolated Worktree: $($c.worktree_path) ($($c.worktree_branch))"
            Write-Output "  - Discipline: Strict TDD (RED -> GREEN -> REFACTOR)."
        }
        Write-Output ""
    }
    Write-Output "Next step: Launch team via Antigravity /teamwork or invoke_subagent."
}
exit 0
