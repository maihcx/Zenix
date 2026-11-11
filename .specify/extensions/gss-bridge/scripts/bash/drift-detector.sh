#!/usr/bin/env bash
# drift-detector.sh — Detect architectural drift, circular dependencies, and layer violations in GSS-Bridge.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

GRAPH_ARG=""
RULES_FILE=""
FAIL_ON_CYCLES=0
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --graph|--graph-path)
            GRAPH_ARG="$2"
            shift 2
            ;;
        --rules-file)
            RULES_FILE="$2"
            shift 2
            ;;
        --fail-on-cycles)
            FAIL_ON_CYCLES=1
            shift
            ;;
        --json)
            OUTPUT_JSON=1
            shift
            ;;
        *)
            shift
            ;;
    esac
done

REPO_ROOT="$(get_bridge_repo_root)"

if [ -n "$GRAPH_ARG" ]; then
    if [[ "$GRAPH_ARG" = /* ]] || [[ "$GRAPH_ARG" =~ ^[A-Za-z]: ]]; then
        GRAPH_PATH="$GRAPH_ARG"
    else
        GRAPH_PATH="$REPO_ROOT/$GRAPH_ARG"
    fi
else
    GRAPH_PATH="$(ensure_graphify_index "$REPO_ROOT" "$OUTPUT_JSON")"
fi

if [ ! -f "$GRAPH_PATH" ]; then
    echo "Error: Graphify index missing at '$GRAPH_PATH'. Run /graphify first." >&2
    exit 2
fi

if [ -n "$RULES_FILE" ]; then
    if [[ "$RULES_FILE" = /* ]] || [[ "$RULES_FILE" =~ ^[A-Za-z]: ]]; then
        RULES_PATH="$RULES_FILE"
    else
        RULES_PATH="$REPO_ROOT/$RULES_FILE"
    fi
else
    RULES_PATH="$REPO_ROOT/.specify/memory/architecture-rules.json"
fi

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
        PYTHON_CMD="$cmd"
        break
    fi
done

if [ -z "$PYTHON_CMD" ]; then
    echo "Error: Python 3 runtime is required for drift-detector.sh" >&2
    exit 2
fi

RAW_RESULT="$($PYTHON_CMD - "$GRAPH_PATH" "$RULES_PATH" <<'PYEOF'
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
PYEOF
)"

if [ "$OUTPUT_JSON" -eq 1 ]; then
    echo "$RAW_RESULT"
else
    echo "================================================================="
    echo "GSS-BRIDGE ARCHITECTURAL DRIFT & CYCLE AUDITOR"
    echo "================================================================="
    echo "$RAW_RESULT" | $PYTHON_CMD -c '
import sys, json
d = json.load(sys.stdin)
print(f"Status:          {d.get(\"status\")}")
print(f"Graph Topology:  {d.get(\"total_nodes\")} nodes, {d.get(\"total_edges\")} edges")
print(f"Circular Cycles: {d.get(\"cycles_count\")}")
print(f"Rule Violations: {d.get(\"violations_count\")}")
print("-----------------------------------------------------------------")
cycles = d.get("cycles", [])
if cycles:
    print("Detected Circular Dependencies:")
    for c in cycles:
        print(f"  [CYCLE] {c}")
    print("")

violations = d.get("violations", [])
if violations:
    print("Layer Boundary Violations:")
    for v in violations:
        print(f"  [VIOLATION] {v[\"from_node\"]} ({v[\"from_layer\"]}) -> {v[\"to_node\"]} ({v[\"to_layer\"]}): {v[\"rule\"]}")
    print("")

if d.get("status") == "HEALTHY":
    print("Architecture is clean. No forbidden upward dependencies or cycles detected.")
'
    echo "================================================================="
fi

if [ "$FAIL_ON_CYCLES" -eq 1 ]; then
    STATUS="$($PYTHON_CMD -c 'import sys, json; print(json.loads(sys.argv[1]).get("status"))' "$RAW_RESULT")"
    if [ "$STATUS" != "HEALTHY" ]; then
        exit 1
    fi
fi

exit 0
