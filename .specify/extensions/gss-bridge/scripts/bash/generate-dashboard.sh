#!/usr/bin/env bash
# generate-dashboard.sh -- Generates standalone interactive HTML visual dashboard for GSS-Bridge.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

OUT_FILE=""
OPEN_BROWSER=0
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --out-file)
            OUT_FILE="$2"
            shift 2
            ;;
        --open)
            OPEN_BROWSER=1
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
DASH_DIR="$SPECIFY_DIR/dashboard"
mkdir -p "$DASH_DIR"

if [ -z "$OUT_FILE" ]; then
    TARGET_OUT="$DASH_DIR/index.html"
else
    TARGET_OUT="$OUT_FILE"
fi

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
        PYTHON_CMD="$cmd"
        break
    fi
done

if [ -z "$PYTHON_CMD" ]; then
    echo "Error: Python 3 runtime is required for generate-dashboard.sh" >&2
    exit 2
fi

export GSS_REPO_ROOT="$REPO_ROOT"
export GSS_DASH_OUT="$TARGET_OUT"
export GSS_DASH_JSON="$OUTPUT_JSON"

$PYTHON_CMD - <<'PYEOF'
import os, sys, json, glob, html

repo_root = os.environ.get("GSS_REPO_ROOT", os.getcwd())
dash_out = os.environ.get("GSS_DASH_OUT", os.path.join(repo_root, ".specify", "dashboard", "index.html"))
output_json = (os.environ.get("GSS_DASH_JSON", "0") == "1")

# 1. Handoff state
handoff_file = os.path.join(repo_root, ".specify", "gss-handoff.json")
handoff_data = {}
if os.path.isfile(handoff_file):
    try:
        with open(handoff_file, "r", encoding="utf-8-sig") as f:
            handoff_data = json.load(f)
    except Exception:
        pass

status = handoff_data.get("status", "unknown").upper()
actor = handoff_data.get("last_actor", handoff_data.get("actor_assigned", "unknown"))
feature_dir = handoff_data.get("feature_directory", "")
timestamp = handoff_data.get("last_updated", "")

# 2. Tasks progress
tasks = []
total_tasks = 0
done_tasks = 0
parallel_tasks = 0

tasks_path = ""
if feature_dir:
    tasks_path = os.path.join(repo_root, feature_dir, "tasks.md")
    if not os.path.isfile(tasks_path):
        tasks_path = os.path.join(repo_root, "specs", os.path.basename(feature_dir), "tasks.md")

if not os.path.isfile(tasks_path):
    spec_tasks = glob.glob(os.path.join(repo_root, "specs", "*", "tasks.md"))
    if spec_tasks:
        tasks_path = spec_tasks[0]

if os.path.isfile(tasks_path):
    try:
        with open(tasks_path, "r", encoding="utf-8-sig") as f:
            for line in f:
                s = line.strip()
                if s.startswith("- [x]") or s.startswith("- [ ]"):
                    total_tasks += 1
                    is_done = s.startswith("- [x]")
                    if is_done:
                        done_tasks += 1
                    is_p = "[P]" in s
                    if is_p:
                        parallel_tasks += 1
                    title = s[5:].strip()
                    tasks.append({"title": title, "done": is_done, "parallel": is_p})
    except Exception:
        pass

task_pct = int((done_tasks / total_tasks * 100)) if total_tasks > 0 else 0

# 3. Graphify metrics
graph_file = os.path.join(repo_root, "graphify-out", "graph.json")
graph_nodes_count = 0
graph_edges_count = 0
god_nodes = []

if os.path.isfile(graph_file):
    try:
        with open(graph_file, "r", encoding="utf-8-sig") as f:
            gdata = json.load(f)
        nodes = gdata.get("nodes", [])
        edges = gdata.get("links", gdata.get("edges", []))
        graph_nodes_count = len(nodes)
        graph_edges_count = len(edges)
        
        deg = {}
        node_labels = {}
        for n in nodes:
            nid = str(n.get("id", ""))
            lbl = n.get("label", nid)
            deg[nid] = 0
            node_labels[nid] = lbl
            
        for e in edges:
            u = str(e.get("source", ""))
            v = str(e.get("target", ""))
            if u in deg:
                deg[u] += 1
            if v in deg:
                deg[v] += 1
                
        sorted_nodes = sorted(deg.items(), key=lambda x: x[1], reverse=True)[:10]
        for nid, count in sorted_nodes:
            god_nodes.append({"id": nid, "label": node_labels.get(nid, nid), "degree": count})
    except Exception:
        pass

# 4. Bugs
bugs = []
bugs_dir = os.path.join(repo_root, ".specify", "bugs")
if os.path.isdir(bugs_dir):
    for slug in sorted(os.listdir(bugs_dir)):
        bdir = os.path.join(bugs_dir, slug)
        if not os.path.isdir(bdir):
            continue
        ass_path = os.path.join(bdir, "assessment.md")
        fix_path = os.path.join(bdir, "fix.md")
        b_status = "UNKNOWN"
        suspect = "-"
        risk = "UNKNOWN"
        if os.path.isfile(fix_path):
            b_status = "FIXED"
        elif os.path.isfile(ass_path):
            b_status = "ASSESSED"
        
        if os.path.isfile(ass_path):
            try:
                with open(ass_path, "r", encoding="utf-8-sig") as f:
                    for line in f:
                        if "Primary Suspect Component" in line:
                            suspect = line.split(":", 1)[-1].strip().replace("`", "")
                        elif "Blast Radius Risk Assessment" in line:
                            risk = line.split(":", 1)[-1].strip().replace("**", "").replace("`", "")
            except Exception:
                pass
        bugs.append({"slug": slug, "status": b_status, "suspect": suspect, "risk": risk})

# 5. Decisions
decisions = []
dec_path = os.path.join(repo_root, ".specify", "agent-decisions.jsonl")
if os.path.isfile(dec_path):
    try:
        with open(dec_path, "r", encoding="utf-8-sig") as f:
            for line in f:
                line = line.strip()
                if line:
                    decisions.append(json.loads(line))
    except Exception:
        pass
decisions.reverse()

telemetry = {
    "status": status,
    "actor": actor,
    "feature_directory": feature_dir,
    "last_updated": timestamp,
    "tasks": {
        "total": total_tasks,
        "done": done_tasks,
        "parallel": parallel_tasks,
        "percentage": task_pct,
        "items": tasks
    },
    "graph": {
        "nodes": graph_nodes_count,
        "edges": graph_edges_count,
        "god_nodes": god_nodes
    },
    "bugs": bugs,
    "decisions": decisions
}

if output_json:
    print(json.dumps(telemetry, ensure_ascii=False))
    sys.exit(0)

# Generate HTML
html_content = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>GSS-Bridge Architecture & Governance Cockpit</title>
<style>
:root {{
  --bg: #090d16;
  --card: #131b2e;
  --card-border: #1e293b;
  --text: #f1f5f9;
  --text-muted: #94a3b8;
  --accent: #38bdf8;
  --accent-glow: rgba(56, 189, 248, 0.15);
  --green: #10b981;
  --amber: #f59e0b;
  --red: #ef4444;
  --purple: #a855f7;
}}
* {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }}
body {{ background-color: var(--bg); color: var(--text); padding: 24px; line-height: 1.5; }}
.container {{ max-width: 1280px; margin: 0 auto; display: flex; flex-direction: column; gap: 24px; }}
header {{ display: flex; justify-content: space-between; align-items: center; border-b: 1px solid var(--card-border); padding-bottom: 20px; }}
.title-group h1 {{ font-size: 24px; font-weight: 700; display: flex; align-items: center; gap: 10px; }}
.title-group p {{ color: var(--text-muted); font-size: 14px; margin-top: 4px; }}
.badges {{ display: flex; gap: 10px; align-items: center; }}
.badge {{ padding: 6px 12px; border-radius: 9999px; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; }}
.badge-ready {{ background: #064e3b; color: #34d399; border: 1px solid #059669; }}
.badge-executing {{ background: #1e3a8a; color: #60a5fa; border: 1px solid #2563eb; }}
.badge-complete {{ background: #064e3b; color: #6ee7b7; border: 1px solid #10b981; }}
.badge-blocked {{ background: #7f1d1d; color: #fca5a5; border: 1px solid #dc2626; }}
.badge-actor {{ background: #312e81; color: #c084fc; border: 1px solid #6366f1; }}

.metrics-grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 16px; }}
.card {{ background: var(--card); border: 1px solid var(--card-border); border-radius: 12px; padding: 20px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.2); }}
.card-header {{ font-size: 13px; font-weight: 600; color: var(--text-muted); text-transform: uppercase; margin-bottom: 8px; }}
.card-val {{ font-size: 28px; font-weight: 700; color: var(--text); }}
.card-sub {{ font-size: 13px; color: var(--text-muted); margin-top: 4px; }}
.progress-bar-bg {{ background: #1e293b; height: 8px; border-radius: 4px; overflow: hidden; margin-top: 10px; }}
.progress-bar-fill {{ background: var(--accent); height: 100%; transition: width 0.3s ease; }}

.section-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }}
@media (max-width: 900px) {{ .section-grid {{ grid-template-columns: 1fr; }} }}

h2 {{ font-size: 18px; font-weight: 600; margin-bottom: 16px; display: flex; align-items: center; gap: 8px; }}
.task-list {{ display: flex; flex-direction: column; gap: 8px; max-height: 380px; overflow-y: auto; padding-right: 6px; }}
.task-item {{ display: flex; align-items: center; gap: 12px; padding: 10px 14px; background: #0f172a; border-radius: 8px; border: 1px solid #1e293b; font-size: 14px; }}
.task-item.done {{ opacity: 0.6; text-decoration: line-through; }}
.badge-p {{ background: #4c1d95; color: #d8b4fe; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }}

table {{ width: 100%; border-collapse: collapse; font-size: 14px; text-align: left; }}
th {{ background: #0f172a; padding: 10px 12px; color: var(--text-muted); font-size: 12px; text-transform: uppercase; border-bottom: 1px solid var(--card-border); }}
td {{ padding: 10px 12px; border-bottom: 1px solid #1e293b; }}
tr:hover td {{ background: rgba(255,255,255,0.02); }}

.risk-HIGH {{ color: var(--red); font-weight: 600; }}
.risk-MEDIUM {{ color: var(--amber); font-weight: 600; }}
.risk-LOW {{ color: var(--green); font-weight: 600; }}

.decision-feed {{ display: flex; flex-direction: column; gap: 12px; max-height: 380px; overflow-y: auto; }}
.decision-card {{ background: #0f172a; border: 1px solid #1e293b; border-radius: 8px; padding: 14px; }}
.decision-top {{ display: flex; justify-content: space-between; font-size: 12px; color: var(--text-muted); margin-bottom: 6px; }}
.decision-title {{ font-size: 14px; font-weight: 600; color: #e2e8f0; }}
.decision-body {{ font-size: 13px; color: var(--text-muted); margin-top: 4px; }}
</style>
</head>
<body>
<div class="container">
  <header>
    <div class="title-group">
      <h1>&#128737;&#65039; GSS-Bridge Cockpit</h1>
      <p>Spec-Kit Governance &middot; Graphify Context &middot; Superpowers TDD Discipline</p>
    </div>
    <div class="badges">
      <span class="badge badge-actor">Agent: {html.escape(actor)}</span>
      <span class="badge badge-{status.lower()}">{html.escape(status)}</span>
    </div>
  </header>

  <div class="metrics-grid">
    <div class="card">
      <div class="card-header">Feature TDD Progress</div>
      <div class="card-val">{done_tasks} / {total_tasks} <span style="font-size:16px;font-weight:400;color:var(--text-muted)">({task_pct}%)</span></div>
      <div class="progress-bar-bg"><div class="progress-bar-fill" style="width: {task_pct}%;"></div></div>
      <div class="card-sub">{parallel_tasks} parallel [P] task(s) scoped</div>
    </div>
    <div class="card">
      <div class="card-header">Knowledge Graph Size</div>
      <div class="card-val">{graph_nodes_count} <span style="font-size:16px;font-weight:400;color:var(--text-muted)">nodes</span></div>
      <div class="card-sub">{graph_edges_count} edges &middot; AST neighborhood indexed</div>
    </div>
    <div class="card">
      <div class="card-header">Active Bugs / Incidents</div>
      <div class="card-val">{len(bugs)} <span style="font-size:16px;font-weight:400;color:var(--text-muted)">triaged</span></div>
      <div class="card-sub">{sum(1 for b in bugs if b['status'] == 'FIXED')} resolved &middot; {sum(1 for b in bugs if b['status'] == 'ASSESSED')} active</div>
    </div>
    <div class="card">
      <div class="card-header">Cross-Agent Decisions</div>
      <div class="card-val">{len(decisions)} <span style="font-size:16px;font-weight:400;color:var(--text-muted)">records</span></div>
      <div class="card-sub">Ledger: .specify/agent-decisions.jsonl</div>
    </div>
  </div>

  <div class="section-grid">
    <div class="card">
      <h2>&#128203; Feature Tasks &amp; Execution Waves</h2>
      <div class="task-list">
"""

if tasks:
    for t in tasks:
        d_class = "done" if t["done"] else ""
        icon = "&#9989;" if t["done"] else "&#11036;"
        p_badge = '<span class="badge-p">[P]</span>' if t["parallel"] else ""
        html_content += f"""        <div class="task-item {d_class}">
          <span>{icon}</span>
          <span style="flex:1">{html.escape(t["title"])}</span>
          {p_badge}
        </div>\n"""
else:
    html_content += """        <div style="color:var(--text-muted);padding:12px;text-align:center;">No active feature tasks found in specs/</div>\n"""

html_content += f"""      </div>
    </div>

    <div class="card">
      <h2>&#127760; Top AST Hubs &amp; God Nodes</h2>
      <table>
        <thead>
          <tr>
            <th>Component / Symbol</th>
            <th style="text-align:right">Graph Degree</th>
          </tr>
        </thead>
        <tbody>
"""

if god_nodes:
    for gn in god_nodes:
        html_content += f"""          <tr>
            <td style="font-family:monospace;color:var(--accent)">{html.escape(gn["label"])}</td>
            <td style="text-align:right;font-weight:600">{gn["degree"]}</td>
          </tr>\n"""
else:
    html_content += """          <tr><td colspan="2" style="color:var(--text-muted);text-align:center;">No graph nodes indexed. Run /graphify</td></tr>\n"""

html_content += f"""        </tbody>
      </table>
    </div>
  </div>

  <div class="section-grid">
    <div class="card">
      <h2>&#128027; Bug Assessments &amp; Blast Radius</h2>
      <table>
        <thead>
          <tr>
            <th>Bug Slug</th>
            <th>Suspect Node</th>
            <th>Risk</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
"""

if bugs:
    for b in bugs:
        r_class = f"risk-{b['risk']}" if b['risk'] in ["HIGH", "MEDIUM", "LOW"] else ""
        html_content += f"""          <tr>
            <td style="font-weight:600">{html.escape(b["slug"])}</td>
            <td style="font-family:monospace">{html.escape(b["suspect"])}</td>
            <td class="{r_class}">{html.escape(b["risk"])}</td>
            <td><span class="badge badge-{'complete' if b['status'] == 'FIXED' else 'executing'}">{html.escape(b['status'])}</span></td>
          </tr>\n"""
else:
    html_content += """          <tr><td colspan="4" style="color:var(--text-muted);text-align:center;">No bug assessments found in .specify/bugs/</td></tr>\n"""

html_content += f"""        </tbody>
      </table>
    </div>

    <div class="card">
      <h2>&#129504; Cross-Agent Decision Journal</h2>
      <div class="decision-feed">
"""

if decisions:
    for d in decisions[:8]:
        html_content += f"""        <div class="decision-card">
          <div class="decision-top">
            <span><strong>{html.escape(d.get("actor", "unknown"))}</strong> &middot; {html.escape(d.get("topic", "general"))}</span>
            <span>{html.escape(d.get("timestamp", ""))}</span>
          </div>
          <div class="decision-title">{html.escape(d.get("decision", ""))}</div>
          <div class="decision-body">{html.escape(d.get("rationale", ""))}</div>
        </div>\n"""
else:
    html_content += """        <div style="color:var(--text-muted);padding:12px;text-align:center;">No decisions logged yet. Use /speckit-gss-bridge-decision</div>\n"""

html_content += """      </div>
    </div>
  </div>
</div>
</body>
</html>
"""

os.makedirs(os.path.dirname(dash_out), exist_ok=True)
with open(dash_out, "w", encoding="utf-8-sig") as f:
    f.write(html_content)

print(f"[gss-bridge] Dashboard generated: {dash_out}")
PYEOF

if [ "$OPEN_BROWSER" -eq 1 ] && [ -f "$TARGET_OUT" ]; then
    if command -v xdg-open >/dev/null 2>&1; then
        xdg-open "$TARGET_OUT" >/dev/null 2>&1 || true
    elif command -v open >/dev/null 2>&1; then
        open "$TARGET_OUT" >/dev/null 2>&1 || true
    elif command -v start >/dev/null 2>&1; then
        start "$TARGET_OUT" >/dev/null 2>&1 || true
    fi
fi
