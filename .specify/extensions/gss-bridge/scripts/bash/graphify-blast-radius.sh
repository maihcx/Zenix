#!/usr/bin/env bash
# graphify-blast-radius.sh — Calculate blast radius and check task coupling for GSS-Bridge.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

REPO_ROOT="$(get_bridge_repo_root)"
GRAPH_PATH="$REPO_ROOT/graphify-out/graph.json"

NODE=""
CHECK_COUPLING=0
NODE_A=""
NODE_B=""
MAX_HOPS=2
OUTPUT_JSON=0
MERMAID=0

while [ $# -gt 0 ]; do
    case "$1" in
        --node)
            NODE="$2"
            shift 2
            ;;
        --check-coupling)
            CHECK_COUPLING=1
            shift
            ;;
        --node-a)
            NODE_A="$2"
            shift 2
            ;;
        --node-b)
            NODE_B="$2"
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
        --mermaid)
            MERMAID=1
            shift
            ;;
        *)
            shift
            ;;
    esac
done

GRAPH_PATH="$(ensure_graphify_index "$REPO_ROOT" "$OUTPUT_JSON")"

if [ ! -f "$GRAPH_PATH" ]; then
    if [ "$OUTPUT_JSON" -eq 1 ]; then
        printf '{"status":"error","message":"graphify-out/graph.json not found and auto-initialization failed. Run /graphify first."}\n'
    else
        echo "[graphify] ERROR: graphify-out/graph.json not found and auto-initialization failed."
        echo "Please run '/graphify' on the workspace before calculating blast radius."
    fi
    exit 2
fi

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1; then
        if "$cmd" -c "import sys" >/dev/null 2>&1; then
            PYTHON_CMD="$cmd"
            break
        fi
    fi
done

if [ -z "$PYTHON_CMD" ]; then
    echo "Error: a functional python3 or python runtime is required" >&2
    exit 2
fi

export GRAPH_PATH CHECK_COUPLING NODE NODE_A NODE_B MAX_HOPS OUTPUT_JSON MERMAID

$PYTHON_CMD - << 'EOF'
import os, sys, json
from collections import deque

graph_path = os.environ['GRAPH_PATH']
check_coupling = os.environ.get('CHECK_COUPLING', '0') == '1'
node_term = os.environ.get('NODE', '')
node_a_term = os.environ.get('NODE_A', '')
node_b_term = os.environ.get('NODE_B', '')
max_hops = int(os.environ.get('MAX_HOPS', '2'))
output_json = os.environ.get('OUTPUT_JSON', '0') == '1'
mermaid_only = os.environ.get('MERMAID', '0') == '1'

with open(graph_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

nodes = {n['id']: n for n in data.get('nodes', [])}
edges = data.get('links') or data.get('edges') or []

neighbors = {nid: set() for nid in nodes}
in_edges = {nid: [] for nid in nodes}
out_edges = {nid: [] for nid in nodes}

for e in edges:
    u = str(e.get('source'))
    v = str(e.get('target'))
    if u in nodes and v in nodes:
        neighbors[u].add(v)
        neighbors[v].add(u)
        out_edges[u].append(e)
        in_edges[v].append(e)

def find_node(term):
    if not term:
        return None
    clean = term.strip().lower()
    if term in nodes:
        return term
    for nid, d in nodes.items():
        if d.get('label', '').lower() == clean:
            return nid
    for nid, d in nodes.items():
        if clean in d.get('label', '').lower() or clean in nid.lower():
            return nid
    return None

if check_coupling:
    id_a = find_node(node_a_term)
    id_b = find_node(node_b_term)
    if not id_a or not id_b:
        missing = []
        if not id_a: missing.append(node_a_term)
        if not id_b: missing.append(node_b_term)
        msg = f"Could not locate node(s) in graph: {', '.join(missing)}"
        if output_json:
            print(json.dumps({"status": "unknown", "message": msg}))
        else:
            print(f"[graphify] WARNING: {msg}")
        sys.exit(0)

    # BFS
    queue = deque([(id_a, 0, [id_a])])
    visited = {id_a}
    found = None
    while queue:
        curr, dist, path = queue.popleft()
        if curr == id_b:
            found = (dist, path)
            break
        if dist < max_hops:
            for nbr in neighbors[curr]:
                if nbr not in visited:
                    visited.add(nbr)
                    queue.append((nbr, dist + 1, path + [nbr]))

    if found:
        dist, path = found
        path_labels = [nodes[x].get('label', x) for x in path]
        res = {
            "status": "coupled",
            "safe_for_parallel": False,
            "distance": dist,
            "path": path_labels,
            "recommendation": f"Execute sequentially. Direct coupling detected within {dist} hops."
        }
        if output_json:
            print(json.dumps(res, indent=2))
        else:
            print(f"[graphify blast-radius] COUPLED: {node_a_term} <--> {node_b_term}")
            print(f"  Hops: {dist}")
            print(f"  Path: {' -> '.join(path_labels)}")
            print("  Verdict: Unsafe for parallel [P] execution. Execute sequentially!")
        sys.exit(1)
    else:
        res = {
            "status": "independent",
            "safe_for_parallel": True,
            "distance": -1,
            "recommendation": f"Safe for parallel [P] subagent execution. No path within {max_hops} hops."
        }
        if output_json:
            print(json.dumps(res, indent=2))
        else:
            print(f"[graphify blast-radius] INDEPENDENT: {node_a_term} and {node_b_term}")
            print(f"  No coupling found within {max_hops} hops.")
            print("  Verdict: Safe for parallel [P] subagent execution.")
        sys.exit(0)

# Mode 2: Blast radius
target_id = find_node(node_term)
if not target_id:
    if output_json:
        print(json.dumps({"status": "error", "message": f"Node '{node_term}' not found in graph."}))
    else:
        print(f"[graphify] Node '{node_term}' was not found in graphify-out/graph.json.")
    sys.exit(1)

target = nodes[target_id]
lbl = target.get('label', target_id)

callers = list({nodes[str(e['source'])].get('label', str(e['source'])) for e in in_edges[target_id]})
callees = list({nodes[str(e['target'])].get('label', str(e['target'])) for e in out_edges[target_id]})

hop2 = set()
for nbr in neighbors[target_id]:
    for nbr2 in neighbors[nbr]:
        if nbr2 != target_id and nbr2 not in neighbors[target_id]:
            hop2.add(nodes[nbr2].get('label', nbr2))

risk = "HIGH" if len(callers) > 5 or len(hop2) > 15 else ("MEDIUM" if len(callers) > 2 or len(hop2) > 5 else "LOW")

report = {
    "node": lbl,
    "source_file": target.get('source_file', ''),
    "source_location": target.get('source_location', ''),
    "callers_1hop": callers,
    "callees_1hop": callees,
    "transitive_2hop_count": len(hop2),
    "transitive_2hop_samples": list(hop2)[:10],
    "risk_level": risk
}

import re
def safe_id(s):
    clean = re.sub(r'[^a-zA-Z0-9_]', '_', s)
    if clean and clean[0].isdigit():
        clean = f"n_{clean}"
    return clean

target_safe = safe_id(lbl)
mermaid_lines = [
    "```mermaid",
    "flowchart LR",
    "    classDef targetNode fill:#4f46e5,stroke:#312e81,stroke-width:2px,color:#ffffff;",
    "    classDef callerNode fill:#0ea5e9,stroke:#0369a1,stroke-width:1px,color:#ffffff;",
    "    classDef calleeNode fill:#10b981,stroke:#047857,stroke-width:1px,color:#ffffff;",
    f"    {target_safe}[\"{lbl}\"]:::targetNode"
]

for c in callers:
    c_safe = safe_id(c)
    mermaid_lines.append(f"    {c_safe}[\"{c}\"]:::callerNode --> {target_safe}")
for c in callees:
    c_safe = safe_id(c)
    mermaid_lines.append(f"    {target_safe} --> {c_safe}[\"{c}\"]:::calleeNode")
mermaid_lines.append("```")
mermaid_block = "\n".join(mermaid_lines)

report["mermaid"] = mermaid_block

if output_json:
    print(json.dumps(report, indent=2))
elif mermaid_only:
    print(mermaid_block)
else:
    print("## Blast Radius & Architecture Context (Graphify)")
    print(f"- **Target Node**: {lbl}")
    print(f"- **Source Location**: {target.get('source_file', '')} : {target.get('source_location', '')}")
    print(f"- **Direct Callers (1-hop)**: {', '.join(callers) if callers else '(none)'}")
    print(f"- **Direct Callees (1-hop)**: {', '.join(callees) if callees else '(none)'}")
    print(f"- **Transitive Impact (2-hop)**: {len(hop2)} node(s) impacted")
    print(f"- **Coupling / Risk Assessment**: {risk}")
    print()
    print("### Architecture Call Flow")
    print(mermaid_block)
EOF
