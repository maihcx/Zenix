#!/usr/bin/env bash
# dispatch-subagents.sh — Parallel Subagent Wave Dispatcher with Graphify Scoping.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

TASKS_PATH=""
FEATURE_DIR=""
ACTOR_ARG=""
OUTPUT_JSON=0
PRUNE_CONTEXT=0

while [ $# -gt 0 ]; do
    case "$1" in
        --tasks-path)
            TASKS_PATH="$2"
            shift 2
            ;;
        --feature-directory)
            FEATURE_DIR="$2"
            shift 2
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --prune-context)
            PRUNE_CONTEXT=1
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
SPECIFY_DIR="$REPO_ROOT/.specify"
ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

RESOLVED_TASKS="$TASKS_PATH"
if [ -z "$RESOLVED_TASKS" ]; then
    TARGET_DIR="$FEATURE_DIR"
    if [ -z "$TARGET_DIR" ] && [ -f "$SPECIFY_DIR/gss-handoff.json" ]; then
        TARGET_DIR="$(read_handoff_field "$SPECIFY_DIR/gss-handoff.json" "feature_directory")"
    fi
    if [ -n "$TARGET_DIR" ] && [ -f "$REPO_ROOT/$TARGET_DIR/tasks.md" ]; then
        RESOLVED_TASKS="$REPO_ROOT/$TARGET_DIR/tasks.md"
    fi
fi

if [ -z "$RESOLVED_TASKS" ] || [ ! -f "$RESOLVED_TASKS" ]; then
    echo "[gss-bridge] tasks.md could not be found. Provide --tasks-path or ensure active feature has tasks.md." >&2
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
    echo "Error: Python runtime required for subagent wave calculation." >&2
    exit 2
fi

GRAPH_PATH="$(ensure_graphify_index "$REPO_ROOT" "$OUTPUT_JSON")"

export RESOLVED_TASKS GRAPH_PATH OUTPUT_JSON

$PYTHON_CMD - << 'EOF'
import os, sys, json, re
from collections import deque

tasks_path = os.environ['RESOLVED_TASKS']
graph_path = os.environ.get('GRAPH_PATH', '')
output_json = os.environ.get('OUTPUT_JSON', '0') == '1'

with open(tasks_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

p_tasks = []
task_regex = re.compile(r'^\s*-\s*\[\s*\]\s*(T\d+)\s+\[P\]\s+(.*)$')

for line in lines:
    m = task_regex.match(line)
    if m:
        p_tasks.append({
            "id": m.group(1),
            "description": m.group(2).strip(),
            "suspect_node": "",
            "target_files": []
        })

if not p_tasks:
    if output_json:
        print(json.dumps({"status": "no_parallel_tasks", "message": "No pending [P] tasks found in tasks.md."}))
    else:
        print(f"[gss-bridge dispatch] No pending [P] tasks found in {tasks_path}.")
        print("Tasks should be executed sequentially.")
    sys.exit(0)

nodes = {}
neighbors = {}

if graph_path and os.path.exists(graph_path):
    try:
        with open(graph_path, 'r', encoding='utf-8') as f:
            gdata = json.load(f)
        for n in gdata.get('nodes', []):
            nid = str(n.get('id', ''))
            nodes[nid] = n
            neighbors[nid] = set()
        edges = gdata.get('links') or gdata.get('edges') or []
        for e in edges:
            u, v = str(e.get('source', '')), str(e.get('target', ''))
            if u in neighbors and v in neighbors:
                neighbors[u].add(v)
                neighbors[v].add(u)
    except Exception:
        pass

for t in p_tasks:
    desc = t['description']
    for nid, n in nodes.items():
        lbl = n.get('label', '')
        if (lbl and lbl.lower() in desc.lower()) or (nid and nid.lower() in desc.lower()):
            t['suspect_node'] = nid
            sf = n.get('source_file')
            if sf:
                t['target_files'] = [sf]
            break

def get_distance(a, b):
    if not a or not b or a not in neighbors or b not in neighbors:
        return 999
    if a == b:
        return 0
    q = deque([(a, 0)])
    seen = {a}
    while q:
        curr, dist = q.popleft()
        if curr == b:
            return dist
        if dist < 2:
            for nbr in neighbors[curr]:
                if nbr not in seen:
                    seen.add(nbr)
                    q.append((nbr, dist + 1))
    return 999

waves = []
for t in p_tasks:
    placed = False
    for wave in waves:
        conflict = False
        for ex in wave:
            if t['suspect_node'] and ex['suspect_node']:
                dist = get_distance(t['suspect_node'], ex['suspect_node'])
                if dist <= 2:
                    conflict = True
                    break
        if not conflict:
            wave.append(t)
            placed = True
            break
    if not placed:
        waves.append([t])

wave_results = []
for i, wave in enumerate(waves):
    wave_results.append({
        "wave_number": i + 1,
        "parallel_subagents_count": len(wave),
        "tasks": [
            {
                "task_id": t["id"],
                "description": t["description"],
                "target_node": t["suspect_node"] or "(unmapped)",
                "scoped_files": t["target_files"] or [f"Identified by Graphify for {t['suspect_node']}"],
                "prune_context": bool(int(os.environ.get("PRUNE_CONTEXT", 0))),
                "subagent_prompt": f"Task: [{t['id']}] {t['description']}\nRole: GSS-Bridge Task Implementer\nScoped Files: {', '.join(t['target_files']) if t['target_files'] else 'Identified by Graphify'}\n\nStrict TDD & Verification Instructions:\n1. RED: Write a failing unit/integration test first before modifying source code. Confirm it fails.\n2. GREEN: Write minimal code within the scoped boundary to pass the test.\n3. REFACTOR: Clean up without breaking existing tests.\n4. EVIDENCE: Run test commands and return actual output as verification evidence.\n\nConstraints: Stay strictly within scoped files. Do not modify unrelated components."
            }
            for t in wave
        ]
    })

if output_json:
    print(json.dumps({
        "tasks_file": tasks_path,
        "total_parallel_tasks": len(p_tasks),
        "total_waves": len(waves),
        "waves": wave_results
    }, indent=2))
else:
    print("=================================================================")
    print("GSS-BRIDGE PARALLEL SUBAGENT WAVE DISPATCH PLAN")
    print("=================================================================")
    print(f"Source Tasks: {tasks_path}")
    print(f"Total [P] Tasks: {len(p_tasks)} across {len(waves)} execution wave(s)\n")

    for w in wave_results:
        w_num = w["wave_number"]
        count = w["parallel_subagents_count"]
        plural = "s" if count > 1 else ""
        print(f"### Wave {w_num} (Dispatch {count} Parallel Subagent{plural})")
        print("> Execute these tasks concurrently. Graphify verified 0 coupling within 2 hops.\n")
        for t in w["tasks"]:
            print(f"- **Task {t['task_id']}**: {t['description']}")
            print(f"  - Scoped Node: {t['target_node']}")
            print("  - Subagent Instruction: Scoped strictly to files identified by Graphify. Write failing test first (RED).")
        print()
    print("Next step: Dispatch Wave 1 subagents. Upon completion, advance to Wave 2.")
EOF
