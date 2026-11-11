#!/usr/bin/env bash
# log-decision.sh — Cross-agent reasoning and architectural decision journal.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

DECISION=""
RATIONALE=""
ALTERNATIVES=""
TOPIC="general"
FEATURE=""
ACTOR_ARG=""
QUERY=""
RECENT=0
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --decision)
            DECISION="$2"
            shift 2
            ;;
        --rationale)
            RATIONALE="$2"
            shift 2
            ;;
        --alternatives)
            ALTERNATIVES="$2"
            shift 2
            ;;
        --topic)
            TOPIC="$2"
            shift 2
            ;;
        --feature)
            FEATURE="$2"
            shift 2
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --query)
            QUERY="$2"
            shift 2
            ;;
        --recent)
            RECENT="$2"
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
SPECIFY_DIR="$REPO_ROOT/.specify"
mkdir -p "$SPECIFY_DIR"
JOURNAL_PATH="$SPECIFY_DIR/agent-decisions.jsonl"
ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
        PYTHON_CMD="$cmd"
        break
    fi
done

if [ -z "$PYTHON_CMD" ]; then
    echo "Error: Python 3 runtime is required for log-decision.sh" >&2
    exit 2
fi

export GSS_DEC_JOURNAL="$JOURNAL_PATH"
export GSS_DEC_ACTOR="$ACTOR"
export GSS_DEC_FEATURE="$FEATURE"
export GSS_DEC_TOPIC="$TOPIC"
export GSS_DEC_DECISION="$DECISION"
export GSS_DEC_RATIONALE="$RATIONALE"
export GSS_DEC_ALTERNATIVES="$ALTERNATIVES"
export GSS_DEC_QUERY="$QUERY"
export GSS_DEC_RECENT="$RECENT"
export GSS_DEC_JSON="$OUTPUT_JSON"

$PYTHON_CMD - <<'PYEOF'
import os, sys, json, time, random

journal_path = os.environ.get("GSS_DEC_JOURNAL", "")
actor = os.environ.get("GSS_DEC_ACTOR", "unknown")
feature = os.environ.get("GSS_DEC_FEATURE", "")
topic = os.environ.get("GSS_DEC_TOPIC", "general")
decision = os.environ.get("GSS_DEC_DECISION", "")
rationale = os.environ.get("GSS_DEC_RATIONALE", "")
alternatives = os.environ.get("GSS_DEC_ALTERNATIVES", "")
query = os.environ.get("GSS_DEC_QUERY", "")
recent = int(os.environ.get("GSS_DEC_RECENT", 0) or 0)
output_json = (os.environ.get("GSS_DEC_JSON", "0") == "1")

if decision:
    now_ts = int(time.time())
    rnd = random.randint(1000, 9999)
    dec_id = f"dec-{now_ts}-{rnd}"
    iso_time = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(now_ts))
    
    record = {
        "id": dec_id,
        "timestamp": iso_time,
        "actor": actor,
        "feature": feature,
        "topic": topic,
        "decision": decision,
        "rationale": rationale,
        "alternatives": alternatives
    }
    
    with open(journal_path, "a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")
        
    if output_json:
        print(json.dumps(record, ensure_ascii=False))
    else:
        print(f"[gss-bridge] Decision logged: {dec_id}")
        print(f"  Actor:        {actor}")
        print(f"  Topic:        {topic}")
        if feature:
            print(f"  Feature:      {feature}")
        print(f"  Decision:     {decision}")
        if rationale:
            print(f"  Rationale:    {rationale}")
        if alternatives:
            print(f"  Alternatives: {alternatives}")
    sys.exit(0)

results = []
if os.path.isfile(journal_path):
    with open(journal_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                results.append(json.loads(line))
            except Exception:
                pass

if feature:
    results = [r for r in results if r.get("feature") == feature]
if topic and topic != "general":
    results = [r for r in results if r.get("topic") == topic]
if query:
    q = query.lower()
    results = [
        r for r in results
        if q in (r.get("decision") or "").lower()
        or q in (r.get("rationale") or "").lower()
        or q in (r.get("alternatives") or "").lower()
        or q in (r.get("topic") or "").lower()
        or q in (r.get("feature") or "").lower()
        or q in (r.get("actor") or "").lower()
    ]

if recent > 0 and len(results) > recent:
    results = results[-recent:]

if output_json:
    print(json.dumps(results, ensure_ascii=False))
else:
    print(f"[gss-bridge] Retrieved {len(results)} decision record(s):")
    for r in results:
        feat = f" [{r['feature']}]" if r.get("feature") else ""
        print(f"- [{r.get('timestamp')}] ({r.get('actor')} | {r.get('topic')}){feat}")
        print(f"  Decision: {r.get('decision')}")
        if r.get("rationale"):
            print(f"  Rationale: {r.get('rationale')}")
        if r.get("alternatives"):
            print(f"  Alternatives: {r.get('alternatives')}")
PYEOF
