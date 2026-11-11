#!/usr/bin/env bash
# bug-assess.sh — Diagnose and triage bugs using Graphify knowledge graph context.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

SLUG=""
SYMPTOM=""
STACK_FILE=""
SUSPECT_NODE=""
ACTOR_ARG=""
OUTPUT_JSON=0
GENERATE_REPRO=0

while [ $# -gt 0 ]; do
    case "$1" in
        --slug)
            SLUG="$2"
            shift 2
            ;;
        --symptom)
            SYMPTOM="$2"
            shift 2
            ;;
        --stack-file)
            STACK_FILE="$2"
            shift 2
            ;;
        --suspect-node)
            SUSPECT_NODE="$2"
            shift 2
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --generate-repro)
            GENERATE_REPRO=1
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

if [ -z "$SLUG" ]; then
    echo "Error: --slug is required" >&2
    exit 2
fi

REPO_ROOT="$(get_bridge_repo_root)"
SPECIFY_DIR="$REPO_ROOT/.specify"

if [ ! -d "$SPECIFY_DIR" ]; then
    echo "[gss-bridge] Not inside a Spec-Kit repository (.specify missing)." >&2
    exit 2
fi

ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"
GRAPH_PATH="$(ensure_graphify_index "$REPO_ROOT")"

SYMPTOM_TEXT="$SYMPTOM"
if [ -n "$STACK_FILE" ] && [ -f "$STACK_FILE" ]; then
    FILE_TEXT="$(cat "$STACK_FILE")"
    if [ -n "$SYMPTOM_TEXT" ]; then
        SYMPTOM_TEXT="$SYMPTOM_TEXT"$'\n\n'"Stack Trace:"$'\n'"$FILE_TEXT"
    else
        SYMPTOM_TEXT="$FILE_TEXT"
    fi
fi
if [ -z "$SYMPTOM_TEXT" ]; then
    SYMPTOM_TEXT="(No detailed symptom text provided. Triage based on target symbols.)"
fi

TARGET_NODE="$SUSPECT_NODE"
MERMAID_BLOCK=""
RISK_LEVEL="UNKNOWN"
SOURCE_LOCATION=""
CALLERS_STR=""
CALLEES_STR=""
TRANS_COUNT=0

if [ -f "$GRAPH_PATH" ]; then
    PYTHON_CMD=""
    for cmd in python3 python py; do
        if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
            PYTHON_CMD="$cmd"
            break
        fi
    done

    if [ -n "$PYTHON_CMD" ]; then
        DETECTED_NODE="$($PYTHON_CMD -c '
import os, sys, json
graph_path = sys.argv[1]
suspect = sys.argv[2]
symptom = sys.argv[3]

if suspect:
    print(suspect)
    sys.exit(0)

try:
    with open(graph_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    for n in data.get("nodes", []):
        lbl = n.get("label", "")
        nid = str(n.get("id", ""))
        if lbl and lbl in symptom:
            print(lbl)
            sys.exit(0)
        elif nid and nid in symptom:
            print(nid)
            sys.exit(0)
except Exception:
    pass
print("")
' "$GRAPH_PATH" "$TARGET_NODE" "$SYMPTOM_TEXT")"

        if [ -n "$DETECTED_NODE" ]; then
            TARGET_NODE="$DETECTED_NODE"
            BLAST_SCRIPT="$SCRIPT_DIR/graphify-blast-radius.sh"
            if [ -f "$BLAST_SCRIPT" ]; then
                BLAST_JSON="$(bash "$BLAST_SCRIPT" --node "$TARGET_NODE" --json 2>/dev/null || true)"
                if [ -n "$BLAST_JSON" ]; then
                    eval "$(echo "$BLAST_JSON" | $PYTHON_CMD -c '
import sys, json, shlex
try:
    d = json.load(sys.stdin)
    risk = d.get("risk_level", "UNKNOWN")
    sf = d.get("source_file", "")
    sl = d.get("source_location", "")
    src = f"{sf} : {sl}"
    callers = ", ".join(d.get("callers_1hop", []))
    callees = ", ".join(d.get("callees_1hop", []))
    trans = str(d.get("transitive_2hop_count", 0))
    mermaid = d.get("mermaid", "")

    print(f"RISK_LEVEL={shlex.quote(risk)}")
    print(f"SOURCE_LOCATION={shlex.quote(src)}")
    print(f"CALLERS_STR={shlex.quote(callers)}")
    print(f"CALLEES_STR={shlex.quote(callees)}")
    print(f"TRANS_COUNT={shlex.quote(trans)}")
    print(f"MERMAID_BLOCK={shlex.quote(mermaid)}")
except Exception:
    pass
')"
                fi
            fi
        fi
    fi
fi

BUG_DIR="$SPECIFY_DIR/bugs/$SLUG"
mkdir -p "$BUG_DIR"
ASSESSMENT_PATH="$BUG_DIR/assessment.md"
TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

REPRO_LINE=""
if [ "$GENERATE_REPRO" -eq 1 ]; then
    SYNTH_SCRIPT="$SCRIPT_DIR/synthesize-repro.sh"
    if [ -f "$SYNTH_SCRIPT" ]; then
        SYNTH_OUT="$(bash "$SYNTH_SCRIPT" --slug "$SLUG" --suspect-node "$TARGET_NODE" --symptom "$SYMPTOM_TEXT" --json 2>/dev/null || true)"
        if [ -n "$SYNTH_OUT" ] && [ -n "$PYTHON_CMD" ]; then
            REPRO_FILE="$(echo "$SYNTH_OUT" | $PYTHON_CMD -c 'import sys, json; print(json.load(sys.stdin).get("repro_test_file", ""))' 2>/dev/null || true)"
            if [ -n "$REPRO_FILE" ]; then
                REPRO_LINE="- [ ] **Auto-Synthesized Repro**: Run \`$REPRO_FILE\` to verify initial failure (RED)."
            fi
        fi
    fi
fi

cat <<EOF > "$ASSESSMENT_PATH"
# Bug Assessment: $SLUG

> **Status**: ASSESSED
> **Triage Agent**: $ACTOR
> **Assessed At**: $TIMESTAMP

## 1. Reported Symptom & Context
\`\`\`text
$SYMPTOM_TEXT
\`\`\`

## 2. Graphify Root Cause Candidates & Architecture Topology
- **Primary Suspect Component**: $(if [ -n "$TARGET_NODE" ]; then echo "\`$TARGET_NODE\`"; else echo "(None detected automatically from graph; manual triage required)"; fi)
$(if [ -n "$SOURCE_LOCATION" ]; then
echo "- **Source Location**: \`$SOURCE_LOCATION\`"
echo "- **Direct Callers (1-hop)**: ${CALLERS_STR:-'(none)'}"
echo "- **Direct Callees (1-hop)**: ${CALLEES_STR:-'(none)'}"
echo "- **Transitive Impact (2-hop)**: $TRANS_COUNT node(s)"
echo "- **Blast Radius Risk Assessment**: **$RISK_LEVEL**"
echo ""
echo "### Architecture Call Flow (Graphify)"
echo "$MERMAID_BLOCK"
else
echo "- *Graphify context not available or node not found in graphify-out/graph.json.*"
echo "- Run \`/graphify\` to index the workspace if not yet indexed."
fi)

## 3. Mandatory Superpowers TDD Preconditions
- [ ] **TDD Failing Test Required**: You MUST write a minimal reproducing test case in the test suite that FAILS (Red) reproducing the symptom before touching any implementation code.
- [ ] **Boundary Scoping**: Code modifications MUST be restricted to the immediate suspect component and directly coupled callers/callees identified above.

## 4. Next Step
Execute fix with Superpowers discipline: \`/speckit-gss-bridge-bug-fix slug=$SLUG\`
EOF

UPDATE_SCRIPT="$SCRIPT_DIR/update-handoff.sh"
if [ -f "$UPDATE_SCRIPT" ]; then
    bash "$UPDATE_SCRIPT" \
        --status "ready" \
        --feature-directory ".specify/bugs/$SLUG" \
        --artifact-owner "$ACTOR" \
        --actor "$ACTOR" >/dev/null
fi

if [ "$OUTPUT_JSON" -eq 1 ]; then
    printf '{"status":"assessed","slug":"%s","assessment_file":".specify/bugs/%s/assessment.md","suspect_node":"%s","blast_risk":"%s"}\n' \
        "$SLUG" "$SLUG" "$TARGET_NODE" "$RISK_LEVEL"
else
    echo "[gss-bridge] Bug assessment created for '$SLUG'."
    echo "  Assessment: .specify/bugs/$SLUG/assessment.md"
    echo "  Suspect:    ${TARGET_NODE:-'(manual identification needed)'}"
    echo "  Blast Risk: $RISK_LEVEL"
    echo "  Next step:  Run /speckit-gss-bridge-bug-fix slug=$SLUG"
fi
exit 0
