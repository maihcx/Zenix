#!/usr/bin/env bash
# teamwork-plan.sh — Autonomous Multi-Agent Teamwork Planner with Graphify Scoping & Sentinel Gate.
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
    echo "Error: Python runtime required for teamwork plan calculation." >&2
    exit 2
fi

GRAPH_PATH="$(ensure_graphify_index "$REPO_ROOT" "$OUTPUT_JSON")"

export RESOLVED_TASKS GRAPH_PATH OUTPUT_JSON PRUNE_CONTEXT

$PYTHON_CMD - << 'EOF'
import os, sys, json, re
from collections import deque

tasks_path = os.environ['RESOLVED_TASKS']
graph_path = os.environ.get('GRAPH_PATH', '')
output_json = os.environ.get('OUTPUT_JSON', '0') == '1'
prune_ctx = os.environ.get('PRUNE_CONTEXT', '0') == '1'

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
        print(f"[gss-bridge teamwork] No pending [P] tasks found in {tasks_path}.")
        print("Tasks should be executed sequentially.")
    sys.exit(0)

nodes = {}
neighbors = {}

if os.path.isfile(graph_path):
    with open(graph_path, 'r', encoding='utf-8') as f:
        graph_data = json.load(f)
    for n in graph_data.get('nodes', []):
        nid = str(n.get('id', ''))
        nodes[nid] = n
        neighbors[nid] = set()
    edges = graph_data.get('links', []) or graph_data.get('edges', [])
    for e in edges:
        u = str(e.get('source', ''))
        v = str(e.get('target', ''))
        if u in neighbors and v in neighbors:
            neighbors[u].add(v)
            neighbors[v].add(u)

for t in p_tasks:
    desc = t['description']
    matched_id = None
    for nid, node in nodes.items():
        lbl = str(node.get('label', ''))
        if lbl and lbl.lower() in desc.lower():
            matched_id = nid
            break
        elif nid.lower() in desc.lower():
            matched_id = nid
            break
    if matched_id:
        t['suspect_node'] = matched_id
        src = nodes[matched_id].get('source_file')
        if src:
            t['target_files'] = [src]

def get_distance(a, b):
    if not a or not b or a not in neighbors or b not in neighbors:
        return 999
    if a == b:
        return 0
    seen = {a}
    q = deque([(a, 0)])
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

all_nodes = [t['suspect_node'] for t in p_tasks if t['suspect_node']]
all_nodes_str = ", ".join(sorted(set(all_nodes))) if all_nodes else "all modified components"

orchestrator_role = {
    "role": "GSS Lead Orchestrator",
    "description": "Orchestrates multi-agent wave execution, manages .specify/gss-handoff.json, spins up isolated worktrees, and finalizes merge verification.",
    "responsibilities": [
        "Read spec.md, plan.md, and tasks.md to preserve global architecture contracts",
        "Manage isolated git worktrees via wave-worktree.sh (--action create, merge, cleanup)",
        "Dispatch parallel Coder subagents per wave via invoke_subagent",
        "Trigger Sentinel Reviewer subagent for verification gate before merging worktrees",
        "Update knowledge graph via graphify update . and log decisions in .specify/agent-decisions.jsonl"
    ]
}

sentinel_role = {
    "role": "GSS Sentinel Adversarial Reviewer",
    "description": "Independent verification gate guarding against regression, circular dependency cycles, and architectural drift before worktree merge.",
    "verification_commands": [
        f"bash .specify/extensions/gss-bridge/scripts/bash/test-impact.sh --node \"{all_nodes_str}\" --detect-changed",
        "bash .specify/extensions/gss-bridge/scripts/bash/drift-detector.sh --fail-on-cycles"
    ],
    "subagent_prompt": f"""Role: GSS Sentinel Adversarial Reviewer
Task: Perform independent verification gate on completed wave worktrees.

Verification Steps:
1. Run Test Impact Analysis:
   bash .specify/extensions/gss-bridge/scripts/bash/test-impact.sh --node "{all_nodes_str}" --detect-changed
2. Run Architectural Drift & Circular Dependency check:
   bash .specify/extensions/gss-bridge/scripts/bash/drift-detector.sh --fail-on-cycles
3. Execute targeted test suites and examine test runner output.
4. If any test fails or architecture cycles are detected, REJECT with actionable feedback.
5. If all tests pass with evidence, APPROVE the wave for merge."""
}

wave_results = []
for i, wave in enumerate(waves):
    w_num = i + 1
    coder_list = []
    for t in wave:
        scoped = t["target_files"] or [f"Identified by Graphify for {t['suspect_node']}"]
        safe_node = re.sub(r'[^a-zA-Z0-9_-]', '', t['suspect_node'] or "task").lower()
        wt_name = f"wave-{w_num}-{safe_node}-{t['id'].lower()}"

        prompt = f"""Task: [{t['id']}] {t['description']}
Role: GSS Coder Subagent
Assigned Worktree: .specify/worktrees/{wt_name} (branch: gss-worktree/{wt_name})
Scoped Files: {', '.join(scoped)}

Strict TDD & Verification Instructions:
1. RED: Write a failing unit/integration test first in the assigned worktree. Confirm it fails.
2. GREEN: Write minimal code within scoped files to pass the test.
3. REFACTOR: Clean up code while keeping test suite green.
4. EVIDENCE: Run verification commands and return actual output to Lead Orchestrator.

Constraints: Stay strictly within assigned worktree and scoped files. Do not modify unrelated components."""

        coder_list.append({
            "task_id": t["id"],
            "role": f"GSS Coder Subagent: {t['id']}",
            "description": t["description"],
            "target_node": t["suspect_node"] or "(unmapped)",
            "worktree_name": wt_name,
            "worktree_path": f".specify/worktrees/{wt_name}",
            "worktree_branch": f"gss-worktree/{wt_name}",
            "scoped_files": scoped,
            "prune_context": prune_ctx,
            "subagent_prompt": prompt
        })

    wave_results.append({
        "wave_number": w_num,
        "parallel_coders_count": len(wave),
        "coders": coder_list
    })

if output_json:
    print(json.dumps({
        "teamwork_protocol": "GSS-Teamwork v1.0",
        "tasks_file": tasks_path,
        "total_parallel_tasks": len(p_tasks),
        "total_waves": len(waves),
        "roster": {
            "orchestrator": orchestrator_role,
            "sentinel": sentinel_role,
            "waves": wave_results
        }
    }, indent=2))
else:
    print("=================================================================")
    print("GSS-BRIDGE AUTONOMOUS MULTI-AGENT TEAMWORK PLAN")
    print("=================================================================")
    print(f"Source Tasks: {tasks_path}")
    print(f"Total [P] Tasks: {len(p_tasks)} across {len(waves)} execution wave(s)\n")

    print("### 1. Lead Orchestrator")
    print(f"- Role: {orchestrator_role['role']}")
    print("- Responsibilities: Coordinates worktrees, subagent waves, and sentinel verification gate.\n")

    print("### 2. Sentinel Adversarial Reviewer")
    print(f"- Role: {sentinel_role['role']}")
    print("- Verification Commands:")
    for cmd in sentinel_role["verification_commands"]:
        print(f"  - {cmd}")
    print()

    print("### 3. Coder Subagents (Wave by Wave Worktree Allocations)")
    for w in wave_results:
        w_num = w["wave_number"]
        count = w["parallel_coders_count"]
        plural = "s" if count > 1 else ""
        print(f"#### Wave {w_num} (Dispatch {count} Parallel Coder Subagent{plural})")
        for c in w["coders"]:
            print(f"- **[{c['task_id']}]**: {c['description']}")
            print(f"  - Target Node: {c['target_node']}")
            print(f"  - Isolated Worktree: {c['worktree_path']} ({c['worktree_branch']})")
            print("  - Discipline: Strict TDD (RED -> GREEN -> REFACTOR).")
        print()
    print("Next step: Launch team via Antigravity /teamwork or invoke_subagent.")
EOF
