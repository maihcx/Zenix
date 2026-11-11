#!/usr/bin/env bash
# guard-command.sh — Guard Spec-Kit, Graphify, and Superpowers boundaries in GSS-Bridge.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

ACTION=""
REASON=""
ACTOR_ARG=""
TARGET_FEATURE_DIR=""

while [ $# -gt 0 ]; do
    case "$1" in
        --action)
            ACTION="$2"
            shift 2
            ;;
        --reason)
            REASON="$2"
            shift 2
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --target-feature-directory)
            TARGET_FEATURE_DIR="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done

if [ -z "$ACTION" ]; then
    echo "Error: --action is required" >&2
    exit 2
fi

REPO_ROOT="$(get_bridge_repo_root)"
ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

HANDOFF_PATH="$REPO_ROOT/.specify/gss-handoff.json"
if [ ! -f "$HANDOFF_PATH" ]; then
    HANDOFF_PATH="$REPO_ROOT/.specify/superpowers-handoff.json"
fi

HANDOFF_STATUS=""
HANDOFF_FEATURE_DIR=""

if [ -f "$HANDOFF_PATH" ]; then
    HANDOFF_STATUS="$(read_handoff_field "$HANDOFF_PATH" "status")"
    HANDOFF_FEATURE_DIR="$(read_handoff_field "$HANDOFF_PATH" "feature_directory")"
fi

DECISION="allow"
DENY_REASON=""

# Rule 0: Bug fix guard — assessment contract mandatory before fix
if [ "$ACTION" = "speckit.bug.fix" ] || [ "$ACTION" = "speckit.gss-bridge.bug-fix" ] || [ "$ACTION" = "speckit-bug-fix" ] || [ "$ACTION" = "speckit-gss-bridge-bug-fix" ] || [ "$ACTION" = "gss-bridge.bug-fix" ] || [ "$ACTION" = "gss-bridge-bug-fix" ]; then
    target_bug_dir="${TARGET_FEATURE_DIR:-}"
    if [ -z "$target_bug_dir" ] && [[ "$HANDOFF_FEATURE_DIR" == *"bugs"* ]]; then
        target_bug_dir="$HANDOFF_FEATURE_DIR"
    fi
    if [ -n "$target_bug_dir" ] && [ ! -f "$REPO_ROOT/$target_bug_dir/assessment.md" ]; then
        DECISION="deny"
        DENY_REASON="$ACTION blocked: assessment contract '$target_bug_dir/assessment.md' not found. Run /speckit-gss-bridge-bug-assess with Graphify blast radius first."
    fi
# Rule 1: deny speckit.implement when handoff status is executing
elif [ "$ACTION" = "speckit.implement" ] && [ "$HANDOFF_STATUS" = "executing" ]; then
    DECISION="deny"
    DENY_REASON="speckit.implement blocked while GSS-Bridge handoff is executing; use /speckit-gss-bridge-execute (or /gss-bridge)"
# Rule 2: deny superpowers:writing-plans when active feature has spec.md + plan.md
elif [ "$ACTION" = "superpowers:writing-plans" ] && [ -n "$HANDOFF_FEATURE_DIR" ]; then
    if [ -f "$REPO_ROOT/$HANDOFF_FEATURE_DIR/spec.md" ] && [ -f "$REPO_ROOT/$HANDOFF_FEATURE_DIR/plan.md" ]; then
        DECISION="deny"
        DENY_REASON="Standalone planning in docs/plans/ is prohibited. Record TDD test matrices directly into $HANDOFF_FEATURE_DIR/plan.md and tasks.md."
    fi
# Rule 3: deny superpowers:brainstorming when active feature has spec.md + plan.md
elif [ "$ACTION" = "superpowers:brainstorming" ] && [ -n "$HANDOFF_FEATURE_DIR" ]; then
    if [ -f "$REPO_ROOT/$HANDOFF_FEATURE_DIR/spec.md" ] && [ -f "$REPO_ROOT/$HANDOFF_FEATURE_DIR/plan.md" ]; then
        DECISION="deny"
        DENY_REASON="Native superpowers brainstorming is forbidden while Spec-Kit owns active design artifacts"
    fi
# Rule 4: deny speckit.constitution while handoff is executing
elif [ "$ACTION" = "speckit.constitution" ] && [ "$HANDOFF_STATUS" = "executing" ]; then
    DECISION="deny"
    DENY_REASON="constitution edits blocked during active handoff; mark handoff blocked first"
# Rule 5: allow any other speckit.*
elif [[ "$ACTION" =~ ^speckit\. ]]; then
    DECISION="allow"
else
    DECISION="allow"
fi

# Write event log
EVENT_PATH="$REPO_ROOT/.specify/bridge-events.jsonl"
TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
if command -v jq >/dev/null 2>&1; then
    jq -c -n \
        --arg ts "$TIMESTAMP" \
        --arg act "guard" \
        --arg st "$DECISION" \
        --arg fd "$HANDOFF_FEATURE_DIR" \
        --arg dec "$DECISION" \
        --arg rsn "$DENY_REASON" \
        --arg actor "$ACTOR" \
        --arg chk "$ACTION" \
        '{
            timestamp: $ts,
            action: $act,
            status: $st,
            feature_directory: $fd,
            decision: $dec,
            reason: $rsn,
            actor: $actor,
            checked_action: $chk
        }' >> "$EVENT_PATH"
else
    local_fd_val="null"
    local_rsn_val="null"
    if [ -n "$HANDOFF_FEATURE_DIR" ]; then local_fd_val="\"$HANDOFF_FEATURE_DIR\""; fi
    if [ -n "$DENY_REASON" ]; then local_rsn_val="\"$DENY_REASON\""; fi
    printf '{"timestamp":"%s","action":"guard","status":"%s","feature_directory":%s,"decision":"%s","reason":%s,"actor":"%s","checked_action":"%s"}\n' \
        "$TIMESTAMP" "$DECISION" "$local_fd_val" "$DECISION" "$local_rsn_val" "$ACTOR" "$ACTION" >> "$EVENT_PATH"
fi

write_bridge_state_summary_full "$HANDOFF_PATH" "$REPO_ROOT" "$ACTOR"

if [ "$DECISION" = "deny" ]; then
    echo "Guard denied $ACTION."
    if [ -n "$DENY_REASON" ]; then
        echo "Reason: $DENY_REASON"
    fi
    exit 1
fi

echo "Guard allowed $ACTION."
exit 0
