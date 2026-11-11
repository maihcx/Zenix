# test-impact.ps1 — Graph-Guided Test Impact Analysis (TIA) for GSS-Bridge.
[CmdletBinding()]
param(
    [string]$Node = "",
    [switch]$DetectChanged,
    [string]$Graph = "",
    [int]$MaxHops = 2,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')

$RepoRoot = Get-BridgeRepoRoot
$GraphPath = if ($Graph) {
    if ([System.IO.Path]::IsPathRooted($Graph)) { $Graph } else { Join-Path $RepoRoot $Graph }
} else {
    Ensure-GraphifyIndex -RepoRoot $RepoRoot -Silent:($Json -eq $true)
}

if (-not (Test-Path -LiteralPath $GraphPath)) {
    Write-Error "Graphify index missing at '$GraphPath'. Run /graphify first."
    exit 2
}

$pythonCmd = "python"
if (-not (Get-Command "python" -ErrorAction SilentlyContinue)) {
    if (Get-Command "python3" -ErrorAction SilentlyContinue) { $pythonCmd = "python3" }
    elseif (Get-Command "py" -ErrorAction SilentlyContinue) { $pythonCmd = "py" }
}

$targetNode = $Node
if ($DetectChanged) {
    $diffOut = & git status --porcelain 2>$null
    if ($diffOut) {
        $changedFiles = @()
        foreach ($line in ($diffOut -split "`r?`n")) {
            if ($line.Length -gt 3) {
                $changedFiles += $line.Substring(3).Trim()
            }
        }
        $targetNode = $changedFiles -join ","
    }
}

$pyScript = @'
import sys, os, json, re

graph_path = sys.argv[1] if len(sys.argv) > 1 else ""
target_spec = sys.argv[2] if len(sys.argv) > 2 else ""
max_hops = int(sys.argv[3]) if len(sys.argv) > 3 and sys.argv[3].isdigit() else 2

with open(graph_path, "r", encoding="utf-8-sig") as f:
    gdata = json.load(f)

nodes = {}
for n in gdata.get("nodes", []):
    nid = str(n.get("id", ""))
    nodes[nid] = n

edges = gdata.get("links") or gdata.get("edges") or []

adj = {}
for nid in nodes:
    adj[nid] = set()

for e in edges:
    u = str(e.get("source", ""))
    v = str(e.get("target", ""))
    if u in adj and v in adj:
        adj[u].add(v)
        adj[v].add(u)

def is_test_node(nid, n_obj):
    sfile = (n_obj.get("source_file") or "").lower()
    lbl = (n_obj.get("label") or nid).lower()
    patterns = ["test_", "_test", "tests/", "test/", ".spec.", ".test."]
    return any(p in sfile or p in lbl for p in patterns)

initial_nodes = set()
targets = [t.strip() for t in target_spec.split(",") if t.strip()]

for t in targets:
    for nid, nobj in nodes.items():
        sfile = nobj.get("source_file", "")
        lbl = nobj.get("label", nid)
        if t == nid or t == lbl or (sfile and (t in sfile or sfile in t)):
            initial_nodes.add(nid)

visited = set(initial_nodes)
current_layer = set(initial_nodes)

impacted_test_nodes = set()
for hop in range(1, max_hops + 1):
    next_layer = set()
    for u in current_layer:
        for v in adj.get(u, set()):
            if v not in visited:
                visited.add(v)
                next_layer.add(v)
                if is_test_node(v, nodes[v]):
                    impacted_test_nodes.add(v)
    current_layer = next_layer

impacted_files = set()
for tid in impacted_test_nodes:
    sf = nodes[tid].get("source_file")
    if sf:
        impacted_files.add(sf)

result = {
    "target_nodes": list(initial_nodes),
    "max_hops": max_hops,
    "total_impacted_tests": len(impacted_test_nodes),
    "impacted_test_nodes": list(impacted_test_nodes),
    "impacted_test_files": sorted(list(impacted_files))
}

print(json.dumps(result, indent=2))
'@

$tempPy = [System.IO.Path]::GetTempFileName() + ".py"
[System.IO.File]::WriteAllText($tempPy, $pyScript, [System.Text.Encoding]::UTF8)

try {
    $raw = & $pythonCmd $tempPy $GraphPath $targetNode $MaxHops | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test impact analysis execution failed."
        exit 1
    }

    if ($Json) {
        Write-Output $raw.Trim()
    } else {
        $res = $raw | ConvertFrom-Json
        Write-Output "================================================================="
        Write-Output "GSS-BRIDGE GRAPH-GUIDED TEST IMPACT ANALYSIS (TIA)"
        Write-Output "================================================================="
        Write-Output "Target Components: $($res.target_nodes -join ', ')"
        Write-Output "Graph Max Hops:    $($res.max_hops)"
        Write-Output "ImpactedTestNodes: $($res.total_impacted_tests)"
        Write-Output "-----------------------------------------------------------------"
        Write-Output "Impacted Test Suites to Execute:"
        if ($res.impacted_test_files.Count -eq 0) {
            Write-Output "  (No topologically coupled test suites found within $($res.max_hops) hops)"
        } else {
            foreach ($tf in $res.impacted_test_files) {
                Write-Output "  - $tf"
            }
        }
        Write-Output "================================================================="
    }
} finally {
    Remove-Item -Force -LiteralPath $tempPy -ErrorAction SilentlyContinue
}
