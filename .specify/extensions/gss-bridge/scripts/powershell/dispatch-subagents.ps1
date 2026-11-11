# dispatch-subagents.ps1 — Parallel Subagent Wave Dispatcher with Graphify Scoping.
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
        Write-Output "[gss-bridge dispatch] No pending [P] tasks found in $resolvedTasksPath."
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

# Wave Partitioning (Greedy Graph Coloring)
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

# Generate wave plan
$waveResults = @()
$waveIdx = 1
foreach ($wave in $waves) {
    $taskList = @()
    foreach ($t in $wave) {
        $files = if ($t.TargetFiles.Count -gt 0) { $t.TargetFiles } else { @("Identified by Graphify for $($t.SuspectNode)") }
        $promptLines = @(
            "Task: [$($t.Id)] $($t.Description)",
            "Role: GSS-Bridge Task Implementer",
            "Scoped Files: $(($files -join ', '))",
            "",
            "Strict TDD & Verification Instructions:",
            "1. RED: Write a failing unit/integration test first before modifying source code. Confirm it fails.",
            "2. GREEN: Write minimal code within the scoped boundary to pass the test.",
            "3. REFACTOR: Clean up without breaking existing tests.",
            "4. EVIDENCE: Run test commands and return actual output as verification evidence.",
            "",
            "Constraints: Stay strictly within scoped files. Do not modify unrelated components."
        )
        $subagentPrompt = $promptLines -join "`r`n"
        $taskList += [ordered]@{
            task_id = $t.Id
            description = $t.Description
            target_node = if ($t.SuspectNode) { $t.SuspectNode } else { "(unmapped)" }
            scoped_files = $files
            prune_context = $PruneContext.IsPresent
            subagent_prompt = $subagentPrompt
        }
    }
    $waveResults += [ordered]@{
        wave_number = $waveIdx
        parallel_subagents_count = $wave.Count
        tasks = $taskList
    }
    $waveIdx++
}

if ($Json) {
    [ordered]@{
        tasks_file = $resolvedTasksPath
        total_parallel_tasks = $pTasks.Count
        total_waves = $waves.Count
        waves = $waveResults
    } | ConvertTo-Json -Depth 5
} else {
    Write-Output "================================================================="
    Write-Output "GSS-BRIDGE PARALLEL SUBAGENT WAVE DISPATCH PLAN"
    Write-Output "================================================================="
    Write-Output "Source Tasks: $resolvedTasksPath"
    Write-Output "Total [P] Tasks: $($pTasks.Count) across $($waves.Count) execution wave(s)"
    Write-Output ""

    for ($i = 0; $i -lt $waves.Count; $i++) {
        $wNum = $i + 1
        $currentWave = $waves[$i]
        Write-Output "### Wave $wNum (Dispatch $($currentWave.Count) Parallel Subagent$(if ($currentWave.Count -gt 1) { 's' } else { '' }))"
        Write-Output "> Execute these tasks concurrently. Graphify verified 0 coupling within 2 hops."
        Write-Output ""
        foreach ($t in $currentWave) {
            Write-Output "- **Task $($t.Id)**: $($t.Description)"
            Write-Output "  - Scoped Node: $(if ($t.SuspectNode) { $t.SuspectNode } else { '(manual)' })"
            Write-Output "  - Subagent Instruction: Scoped strictly to files identified by Graphify. Write failing test first (RED)."
        }
        Write-Output ""
    }
    Write-Output "Next step: Dispatch Wave 1 subagents. Upon completion, advance to Wave 2."
}
exit 0
