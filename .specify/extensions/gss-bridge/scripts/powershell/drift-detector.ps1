# drift-detector.ps1 — Detect architectural drift, circular dependencies, and layer violations in GSS-Bridge.
[CmdletBinding()]
param(
    [Alias("GraphPath")]
    [string]$Graph = "",
    [string]$RulesFile = "",
    [switch]$FailOnCycles,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')
. (Join-Path $ScriptDir 'bridge-state.ps1')

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

$rulesPath = if ($RulesFile) {
    if ([System.IO.Path]::IsPathRooted($RulesFile)) { $RulesFile } else { Join-Path $RepoRoot $RulesFile }
} else {
    Join-Path $RepoRoot ".specify\memory\architecture-rules.json"
}

$pythonCmd = "python"
if (-not (Get-Command "python" -ErrorAction SilentlyContinue)) {
    if (Get-Command "python3" -ErrorAction SilentlyContinue) { $pythonCmd = "python3" }
    elseif (Get-Command "py" -ErrorAction SilentlyContinue) { $pythonCmd = "py" }
}

$pyScript = @'
import sys, os, json

graph_path = sys.argv[1] if len(sys.argv) > 1 else ""
rules_path = sys.argv[2] if len(sys.argv) > 2 else ""

with open(graph_path, "r", encoding="utf-8-sig") as f:
    gdata = json.load(f)

nodes = {}
for n in gdata.get("nodes", []):
    nid = str(n.get("id", ""))
    nodes[nid] = n

edges = gdata.get("links") or gdata.get("edges") or []

# 1. Detect cycles using Tarjan SCC
adj = {}
for nid in nodes:
    adj[nid] = []

for e in edges:
    u = str(e.get("source", ""))
    v = str(e.get("target", ""))
    if u in adj and v in adj:
        adj[u].append(v)

index = 0
indices = {}
lowlink = {}
on_stack = {}
stack = []
sccs = []

def strongconnect(v):
    global index
    indices[v] = index
    lowlink[v] = index
    index += 1
    stack.append(v)
    on_stack[v] = True

    for w in adj.get(v, []):
        if w not in indices:
            strongconnect(w)
            lowlink[v] = min(lowlink[v], lowlink[w])
        elif on_stack.get(w, False):
            lowlink[v] = min(lowlink[v], indices[w])

    if lowlink[v] == indices[v]:
        scc = []
        while True:
            w = stack.pop()
            on_stack[w] = False
            scc.append(w)
            if w == v:
                break
        if len(scc) > 1:
            sccs.append(scc)

for v in list(nodes.keys()):
    if v not in indices:
        strongconnect(v)

# 2. Check architecture rules
rules = {}
if os.path.isfile(rules_path):
    try:
        with open(rules_path, "r", encoding="utf-8-sig") as rf:
            rules = json.load(rf)
    except Exception:
        pass

layer_map = {}
layers = rules.get("layers", [])
for l in layers:
    lname = l.get("name")
    for pat in l.get("patterns", []):
        for nid, nobj in nodes.items():
            sf = nobj.get("source_file", "")
            if pat in sf or pat in nid:
                layer_map[nid] = lname

violations = []
forbidden = rules.get("forbidden_dependencies", [])
for e in edges:
    u = str(e.get("source", ""))
    v = str(e.get("target", ""))
    lu = layer_map.get(u)
    lv = layer_map.get(v)
    if lu and lv and lu != lv:
        for frule in forbidden:
            if frule.get("from") == lu and frule.get("to") == lv:
                violations.append({
                    "from_node": u,
                    "from_layer": lu,
                    "to_node": v,
                    "to_layer": lv,
                    "rule": frule.get("reason", "Forbidden cross-layer edge")
                })

result = {
    "status": "HEALTHY" if (len(sccs) == 0 and len(violations) == 0) else "DRIFT_DETECTED",
    "total_nodes": len(nodes),
    "total_edges": len(edges),
    "cycles_count": len(sccs),
    "cycles": [" -> ".join(c[:4]) + ("..." if len(c) > 4 else "") for c in sccs],
    "violations_count": len(violations),
    "violations": violations
}

print(json.dumps(result, indent=2))
'@

$tempPy = [System.IO.Path]::GetTempFileName() + ".py"
[System.IO.File]::WriteAllText($tempPy, $pyScript, [System.Text.Encoding]::UTF8)

try {
    $argsList = @($tempPy, $GraphPath, $rulesPath)
    $resRaw = & $pythonCmd $argsList | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Architectural drift detection execution failed."
        exit 1
    }

    if ($Json) {
        Write-Output $resRaw
    } else {
        $res = $resRaw | ConvertFrom-Json
        Write-Output "================================================================="
        Write-Output "GSS-BRIDGE ARCHITECTURAL DRIFT & CYCLE AUDITOR"
        Write-Output "================================================================="
        Write-Output "Status:          $($res.status)"
        Write-Output "Graph Topology:  $($res.total_nodes) nodes, $($res.total_edges) edges"
        Write-Output "Circular Cycles: $($res.cycles_count)"
        Write-Output "Rule Violations: $($res.violations_count)"
        Write-Output "-----------------------------------------------------------------"

        if ($res.cycles_count -gt 0) {
            Write-Output "Detected Circular Dependencies:"
            foreach ($c in $res.cycles) {
                Write-Output "  [CYCLE] $c"
            }
            Write-Output ""
        }

        if ($res.violations_count -gt 0) {
            Write-Output "Layer Boundary Violations:"
            foreach ($v in $res.violations) {
                Write-Output "  [VIOLATION] $($v.from_node) ($($v.from_layer)) -> $($v.to_node) ($($v.to_layer)): $($v.rule)"
            }
            Write-Output ""
        }

        if ($res.status -eq "HEALTHY") {
            Write-Output "Architecture is clean. No forbidden upward dependencies or cycles detected."
        }
        Write-Output "================================================================="
    }

    if ($FailOnCycles -and ($res.cycles_count -gt 0 -or $res.violations_count -gt 0)) {
        exit 1
    }
} finally {
    Remove-Item -Force -LiteralPath $tempPy -ErrorAction SilentlyContinue
}

exit 0
