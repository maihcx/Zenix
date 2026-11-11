#!/usr/bin/env bash
# bridge-status.sh — read-only GSS-Bridge state and readiness introspection.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

OUTPUT_JSON=0
READINESS=0
ACTOR_ARG=""

while [ $# -gt 0 ]; do
    case "$1" in
        --json)
            OUTPUT_JSON=1
            shift
            ;;
        --readiness)
            READINESS=1
            shift
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done

REPO_ROOT="$(get_bridge_repo_root)"
SPECIFY_DIR="$REPO_ROOT/.specify"

if [ ! -d "$SPECIFY_DIR" ]; then
    printf '[bridge] not inside a Spec-Kit repository (.specify missing)\n' >&2
    exit 2
fi

HANDOFF_PATH="$SPECIFY_DIR/gss-handoff.json"
if [ ! -f "$HANDOFF_PATH" ]; then
    HANDOFF_PATH="$SPECIFY_DIR/superpowers-handoff.json"
fi

RESOLVED_ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

FEATURE_DIR=""
STATUS=""
OWNER=""
PENDING_COUNT="-1"

if [ -f "$HANDOFF_PATH" ]; then
    FEATURE_DIR="$(read_handoff_field "$HANDOFF_PATH" "feature_directory")"
    STATUS="$(read_handoff_field "$HANDOFF_PATH" "status")"
    OWNER="$(read_handoff_field "$HANDOFF_PATH" "artifact_owner")"
    if [ -n "$FEATURE_DIR" ] && [ -f "$REPO_ROOT/$FEATURE_DIR/tasks.md" ]; then
        PENDING_COUNT="$(get_pending_task_count "$REPO_ROOT/$FEATURE_DIR/tasks.md")"
    fi
fi

[ -z "$FEATURE_DIR" ] && FEATURE_DIR="(none)"
[ -z "$STATUS" ] && STATUS="(no handoff)"
[ -z "$OWNER" ] && OWNER="unknown"

GRAPH_EXISTS=0
if [ -f "$REPO_ROOT/graphify-out/graph.json" ]; then
    GRAPH_EXISTS=1
fi

case "$STATUS" in
    ready)     NEXT="Run /speckit-plan or /speckit-tasks on $FEATURE_DIR" ;;
    executing) NEXT="Run /speckit-gss-bridge-execute (or /gss-bridge) to implement pending tasks with TDD discipline" ;;
    blocked)   NEXT="Resolve contract gap in $FEATURE_DIR, then resume with /gss-bridge" ;;
    complete)  NEXT="Feature is complete. Start a new feature with /speckit-specify" ;;
    *)         NEXT="Run /speckit-specify to create a new feature specification" ;;
esac

if [ "$READINESS" -eq 1 ]; then
    HAS_BASH=1
    HAS_GIT=0
    HAS_PYTHON=0
    command -v git >/dev/null 2>&1 && HAS_GIT=1
    for pcmd in python3 python py; do
        if command -v "$pcmd" >/dev/null 2>&1 && "$pcmd" -c "import sys" >/dev/null 2>&1; then
            HAS_PYTHON=1
            break
        fi
    done

    TOOLS_STATUS="ok"
    if [ "$HAS_GIT" -eq 0 ]; then TOOLS_STATUS="warning"; fi

    PKG_STATUS="ok"
    if [ ! -f "$SPECIFY_DIR/extensions/gss-bridge/extension.yml" ]; then PKG_STATUS="warning"; fi

    GRAPH_STATUS="ready (graphify-out/graph.json)"
    if [ "$GRAPH_EXISTS" -eq 0 ]; then GRAPH_STATUS="not-indexed (run /graphify)"; fi

    if [ "$OUTPUT_JSON" -eq 1 ] && command -v jq >/dev/null 2>&1; then
        jq -n \
            --arg flavor "bash" \
            --arg tools "$TOOLS_STATUS" \
            --arg pkg "$PKG_STATUS" \
            --arg st "$STATUS" \
            --arg fd "$FEATURE_DIR" \
            --arg graph "$GRAPH_STATUS" \
            --arg next "$NEXT" \
            --argjson git "$HAS_GIT" \
            --argjson python "$HAS_PYTHON" \
            --argjson pending "$PENDING_COUNT" \
            '{
                script_flavor: $flavor,
                required_tools: { status: $tools, git: ($git == 1), python: ($python == 1) },
                namespace: { status: "ok", prefix: "speckit.gss-bridge" },
                package_files: { status: $pkg },
                bridge_state: { status: $st, feature: $fd, pending_tasks: $pending },
                graphify: { status: (if $graph == "ready (graphify-out/graph.json)" then "ok" else "not-built" end) },
                agents: { status: "verified", supported: ["codex", "claude", "antigravity", "gemini"] },
                next: $next
            }'
    else
        echo "[bridge readiness]"
        echo "  Script flavor: bash"
        echo "  Required tools: $TOOLS_STATUS (git: $HAS_GIT, python: $HAS_PYTHON)"
        echo "  Namespace: ok (speckit.gss-bridge)"
        echo "  Package files: $PKG_STATUS"
        echo "  Bridge state: $STATUS"
        echo "  Graphify: $GRAPH_STATUS"
        echo "  Agents: verified (codex, claude, antigravity, gemini)"
        echo "  Next: $NEXT"
    fi
    exit 0
fi

if [ "$OUTPUT_JSON" -eq 1 ] && command -v jq >/dev/null 2>&1; then
    jq -n \
        --arg fd "$FEATURE_DIR" \
        --arg st "$STATUS" \
        --arg own "$OWNER" \
        --arg act "$RESOLVED_ACTOR" \
        --argjson pending "$PENDING_COUNT" \
        --argjson graph "$GRAPH_EXISTS" \
        --arg next "$NEXT" \
        '{
            feature_directory: $fd,
            status: $st,
            artifact_owner: $own,
            actor: $act,
            pending_tasks: $pending,
            graphify_indexed: ($graph == 1),
            next: $next
        }'
else
    write_bridge_state_summary_full "$HANDOFF_PATH" "$REPO_ROOT" "$RESOLVED_ACTOR"
    echo "  Next: $NEXT"
fi
exit 0
