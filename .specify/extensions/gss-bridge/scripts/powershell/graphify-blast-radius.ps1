# graphify-blast-radius.ps1 — Calculate blast radius and check task coupling for GSS-Bridge.
[CmdletBinding()]
param(
    [string]$Node = "",
    [switch]$CheckCoupling,
    [string]$NodeA = "",
    [string]$NodeB = "",
    [int]$MaxHops = 2,
    [switch]$Json,
    [switch]$Mermaid
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')

$RepoRoot = Get-BridgeRepoRoot
$GraphPath = Ensure-GraphifyIndex -RepoRoot $RepoRoot -Silent:($Json -eq $true)

if (-not (Test-Path -LiteralPath $GraphPath -PathType Leaf)) {
    if ($Json) {
        @{ status = "error"; message = "graphify-out/graph.json not found and auto-initialization failed. Run /graphify first." } | ConvertTo-Json
    } else {
        Write-Output "[graphify] ERROR: graphify-out/graph.json not found and auto-initialization failed."
        Write-Output "Please run '/graphify' on the workspace before calculating blast radius."
    }
    exit 2
}

$GraphData = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json
$Nodes = @{}
foreach ($n in $GraphData.nodes) {
    $Nodes[$n.id] = $n
}

# Normalize edges from 'links' or 'edges'
$Edges = if ($GraphData.links) { $GraphData.links } elseif ($GraphData.edges) { $GraphData.edges } else { @() }

# Build adjacency lists
$Neighbors = @{}
$InEdges = @{}
$OutEdges = @{}

foreach ($nid in $Nodes.Keys) {
    $Neighbors[$nid] = New-Object System.Collections.Generic.HashSet[string]
    $InEdges[$nid] = New-Object System.Collections.Generic.List[psobject]
    $OutEdges[$nid] = New-Object System.Collections.Generic.List[psobject]
}

foreach ($e in $Edges) {
    $u = [string]$e.source
    $v = [string]$e.target
    if ($Nodes.ContainsKey($u) -and $Nodes.ContainsKey($v)) {
        [void]$Neighbors[$u].Add($v)
        [void]$Neighbors[$v].Add($u)
        $OutEdges[$u].Add($e)
        $InEdges[$v].Add($e)
    }
}

function Find-MatchingNodeId {
    param([string]$Term)
    if ([string]::IsNullOrWhiteSpace($Term)) { return $null }
    $clean = $Term.Trim().ToLowerInvariant()

    # Exact ID match
    if ($Nodes.ContainsKey($Term)) { return $Term }

    # Label exact match
    foreach ($nid in $Nodes.Keys) {
        $lbl = [string]$Nodes[$nid].label
        if ($lbl -and $lbl.ToLowerInvariant() -eq $clean) { return $nid }
    }

    # Substring match
    foreach ($nid in $Nodes.Keys) {
        $lbl = [string]$Nodes[$nid].label
        if (($lbl -and $lbl.ToLowerInvariant().Contains($clean)) -or $nid.ToLowerInvariant().Contains($clean)) {
            return $nid
        }
    }

    return $null
}

# --- Mode 1: Coupling Check between NodeA and NodeB ---
if ($CheckCoupling) {
    if (-not $NodeA -or -not $NodeB) {
        Write-Error "Parameters -NodeA and -NodeB are required when -CheckCoupling is specified."
        exit 1
    }

    $idA = Find-MatchingNodeId $NodeA
    $idB = Find-MatchingNodeId $NodeB

    if (-not $idA -or -not $idB) {
        $missing = @()
        if (-not $idA) { $missing += $NodeA }
        if (-not $idB) { $missing += $NodeB }
        $msg = "Could not locate node(s) in graph: $($missing -join ', ')"
        if ($Json) {
            @{ status = "unknown"; message = $msg } | ConvertTo-Json
        } else {
            Write-Output "[graphify] WARNING: $msg"
        }
        exit 0
    }

    # BFS shortest path
    $queue = New-Object System.Collections.Generic.Queue[psobject]
    $visited = New-Object System.Collections.Generic.HashSet[string]
    $queue.Enqueue([pscustomobject]@{ Id = $idA; Dist = 0; Path = @($idA) })
    [void]$visited.Add($idA)

    $foundPath = $null
    while ($queue.Count -gt 0) {
        $curr = $queue.Dequeue()
        if ($curr.Id -eq $idB) {
            $foundPath = $curr
            break
        }
        if ($curr.Dist -lt $MaxHops) {
            foreach ($nbr in $Neighbors[$curr.Id]) {
                if (-not $visited.Contains($nbr)) {
                    [void]$visited.Add($nbr)
                    $nextPath = $curr.Path + @($nbr)
                    $queue.Enqueue([pscustomobject]@{ Id = $nbr; Dist = ($curr.Dist + 1); Path = $nextPath })
                }
            }
        }
    }

    if ($foundPath) {
        $pathLabels = $foundPath.Path | ForEach-Object { if ($Nodes[$_].label) { $Nodes[$_].label } else { $_ } }
        $result = [ordered]@{
            status = "coupled"
            safe_for_parallel = $false
            distance = $foundPath.Dist
            path = $pathLabels
            recommendation = "Execute sequentially. Direct coupling detected within $($foundPath.Dist) hops."
        }
        if ($Json) {
            $result | ConvertTo-Json -Depth 4
        } else {
            Write-Output "[graphify blast-radius] COUPLED: $NodeA <--> $NodeB"
            Write-Output "  Hops: $($foundPath.Dist)"
            Write-Output "  Path: $($pathLabels -join ' -> ')"
            Write-Output "  Verdict: Unsafe for parallel [P] execution. Execute sequentially!"
        }
        exit 1
    } else {
        $result = [ordered]@{
            status = "independent"
            safe_for_parallel = $true
            distance = -1
            recommendation = "Safe for parallel [P] subagent execution. No path within $MaxHops hops."
        }
        if ($Json) {
            $result | ConvertTo-Json
        } else {
            Write-Output "[graphify blast-radius] INDEPENDENT: $NodeA and $NodeB"
            Write-Output "  No coupling found within $MaxHops hops."
            Write-Output "  Verdict: Safe for parallel [P] subagent execution."
        }
        exit 0
    }
}

# --- Mode 2: Blast Radius Calculation for a Single Node ---
if (-not $Node) {
    Write-Output "Usage: graphify-blast-radius.ps1 -Node <NodeName> OR -CheckCoupling -NodeA <Name> -NodeB <Name>"
    exit 1
}

$targetId = Find-MatchingNodeId $Node
if (-not $targetId) {
    if ($Json) {
        @{ status = "error"; message = "Node '$Node' not found in graph." } | ConvertTo-Json
    } else {
        Write-Output "[graphify] Node '$Node' was not found in graphify-out/graph.json."
    }
    exit 1
}

$targetNode = $Nodes[$targetId]
$targetLabel = if ($targetNode.label) { $targetNode.label } else { $targetId }

# 1-hop callers and callees
$callers1Hop = @($InEdges[$targetId] | ForEach-Object { $src = [string]$_.source; if ($Nodes[$src].label) { $Nodes[$src].label } else { $src } } | Select-Object -Unique)
$callees1Hop = @($OutEdges[$targetId] | ForEach-Object { $tgt = [string]$_.target; if ($Nodes[$tgt].label) { $Nodes[$tgt].label } else { $tgt } } | Select-Object -Unique)

# 2-hop transitives
$hop2Set = New-Object System.Collections.Generic.HashSet[string]
foreach ($nbr in $Neighbors[$targetId]) {
    foreach ($nbr2 in $Neighbors[$nbr]) {
        if ($nbr2 -ne $targetId -and -not $Neighbors[$targetId].Contains($nbr2)) {
            $lbl = if ($Nodes[$nbr2].label) { $Nodes[$nbr2].label } else { $nbr2 }
            [void]$hop2Set.Add($lbl)
        }
    }
}

$risk = if ($callers1Hop.Count -gt 5 -or $hop2Set.Count -gt 15) { "HIGH" } elseif ($callers1Hop.Count -gt 2 -or $hop2Set.Count -gt 5) { "MEDIUM" } else { "LOW" }

$report = [ordered]@{
    node = $targetLabel
    source_file = $targetNode.source_file
    source_location = $targetNode.source_location
    callers_1hop = $callers1Hop
    callees_1hop = $callees1Hop
    transitive_2hop_count = $hop2Set.Count
    transitive_2hop_samples = @($hop2Set | Select-Object -First 10)
    risk_level = $risk
}

function Get-SafeMermaidId([string]$str) {
    $clean = $str -replace '[^a-zA-Z0-9_]', '_'
    if ($clean -match '^[0-9]') { $clean = "n_$clean" }
    return $clean
}

$targetSafeId = Get-SafeMermaidId $targetLabel
$mermaidLines = New-Object System.Collections.Generic.List[string]
$mermaidLines.Add('```mermaid')
$mermaidLines.Add('flowchart LR')
$mermaidLines.Add('    classDef targetNode fill:#4f46e5,stroke:#312e81,stroke-width:2px,color:#ffffff;')
$mermaidLines.Add('    classDef callerNode fill:#0ea5e9,stroke:#0369a1,stroke-width:1px,color:#ffffff;')
$mermaidLines.Add('    classDef calleeNode fill:#10b981,stroke:#047857,stroke-width:1px,color:#ffffff;')
$mermaidLines.Add("    $targetSafeId[`"$targetLabel`"]:::targetNode")

foreach ($c in $callers1Hop) {
    $cId = Get-SafeMermaidId $c
    $mermaidLines.Add("    $cId[`"$c`"]:::callerNode --> $targetSafeId")
}
foreach ($c in $callees1Hop) {
    $cId = Get-SafeMermaidId $c
    $mermaidLines.Add("    $targetSafeId --> $cId[`"$c`"]:::calleeNode")
}
$mermaidLines.Add('```')
$mermaidBlock = ($mermaidLines -join [Environment]::NewLine)

$report['mermaid'] = $mermaidBlock

if ($Json) {
    $report | ConvertTo-Json -Depth 4
} elseif ($Mermaid) {
    Write-Output $mermaidBlock
} else {
    Write-Output "## Blast Radius & Architecture Context (Graphify)"
    Write-Output "- **Target Node**: $targetLabel"
    Write-Output "- **Source Location**: $($targetNode.source_file) : $($targetNode.source_location)"
    Write-Output "- **Direct Callers (1-hop)**: $(if ($callers1Hop.Count -gt 0) { $callers1Hop -join ', ' } else { '(none)' })"
    Write-Output "- **Direct Callees (1-hop)**: $(if ($callees1Hop.Count -gt 0) { $callees1Hop -join ', ' } else { '(none)' })"
    Write-Output "- **Transitive Impact (2-hop)**: $($hop2Set.Count) node(s) impacted"
    Write-Output "- **Coupling / Risk Assessment**: $risk"
    Write-Output ""
    Write-Output "### Architecture Call Flow"
    Write-Output $mermaidBlock
}
exit 0
