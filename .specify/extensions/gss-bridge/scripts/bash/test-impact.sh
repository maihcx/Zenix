#!/usr/bin/env bash
# test-impact.sh — Graph-Guided Test Impact Analysis (TIA) for GSS-Bridge.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

NODE=""
DETECT_CHANGED=0
GRAPH_ARG=""
MAX_HOPS=2
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --node)
            NODE="$2"
            shift 2
            ;;
        --detect-changed)
            DETECT_CHANGED=1
            shift
            ;;
        --graph)
            GRAPH_ARG="$2"
            shift 2
            ;;
        --max-hops)
            MAX_HOPS="$2"
            shift 2
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

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
        PYTHON_CMD="$cmd"
        break
    fi
done

if [ -z "$PYTHON_CMD" ]; then
    echo "Error: Python 3 runtime is required for test-impact.sh" >&2
    exit 2
fi

TARGET_NODE="$NODE"
if [ "$DETECT_CHANGED" -eq 1 ]; then
    DIFF_FILES="$(git status --porcelain 2>/dev/null | awk '{print $2}' | paste -sd, - || true)"
    if [ -n "$DIFF_FILES" ]; then
        TARGET_NODE="$DIFF_FILES"
    fi
fi

RAW_RESULT="$($PYTHON_CMD - "$GRAPH_PATH" "$TARGET_NODE" "$MAX_HOPS" <<'PYEOF'
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
PYEOF
)"

if [ "$OUTPUT_JSON" -eq 1 ]; then
    echo "$RAW_RESULT"
else
    echo "================================================================="
    echo "GSS-BRIDGE GRAPH-GUIDED TEST IMPACT ANALYSIS (TIA)"
    echo "================================================================="
    echo "$RAW_RESULT" | $PYTHON_CMD -c '
import sys, json
d = json.load(sys.stdin)
target_nodes_str = ", ".join(d.get("target_nodes", []))
print(f"Target Components: {target_nodes_str}")
print(f"Graph Max Hops:    {d.get(\"max_hops\")}")
print(f"ImpactedTestNodes: {d.get(\"total_impacted_tests\")}")
print("-----------------------------------------------------------------")
print("Impacted Test Suites to Execute:")
files = d.get("impacted_test_files", [])
if not files:
    print("  (No topologically coupled test suites found)")
else:
    for f in files:
        print(f"  - {f}")
'
    echo "================================================================="
fi
exit 0
