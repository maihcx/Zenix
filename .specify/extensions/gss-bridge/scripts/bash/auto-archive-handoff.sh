#!/usr/bin/env bash
# auto-archive-handoff.sh — auto-archive complete handoff before new feature.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

ACTOR_ARG=""
REASON="Auto-archive complete handoff before new feature."

while [ $# -gt 0 ]; do
    case "$1" in
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --reason)
            REASON="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done

REPO_ROOT="$(get_bridge_repo_root)"
ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

HANDOFF_PATH="$REPO_ROOT/.specify/gss-handoff.json"
if [ ! -f "$HANDOFF_PATH" ]; then
    HANDOFF_PATH="$REPO_ROOT/.specify/superpowers-handoff.json"
fi

if [ ! -f "$HANDOFF_PATH" ]; then
    echo "No handoff file at $HANDOFF_PATH; nothing to archive."
    exit 0
fi

CURRENT_STATUS="$(jq -r '.status // empty' "$HANDOFF_PATH" 2>/dev/null || true)"
if [ "$CURRENT_STATUS" != "complete" ]; then
    echo "No complete handoff to archive (current status: '$CURRENT_STATUS')."
    exit 0
fi

PRIOR_FEATURE_DIR="$(jq -r '.feature_directory // empty' "$HANDOFF_PATH" 2>/dev/null || true)"

# Delegate to update-handoff.sh
bash "$SCRIPT_DIR/update-handoff.sh" --status ready --clear-feature-directory --artifact-owner unknown --reason "$REASON" --actor "$ACTOR" >/dev/null

SNAPSHOT_ID="$(jq -r '.last_snapshot_id // empty' "$HANDOFF_PATH" 2>/dev/null || true)"
TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

if command -v jq >/dev/null 2>&1; then
    jq -c -n \
        --arg ts "$TIMESTAMP" \
        --arg act "archive" \
        --arg st "archived" \
        --arg fd "$PRIOR_FEATURE_DIR" \
        --arg dec "archive" \
        --arg rsn "$REASON" \
        --arg actor "$ACTOR" \
        --arg snap "$SNAPSHOT_ID" \
        '{
            timestamp: $ts,
            action: $act,
            status: $st,
            feature_directory: $fd,
            decision: $dec,
            reason: $rsn,
            actor: $actor,
            snapshot_id: (if $snap == "" then null else $snap end)
        }' >> "$REPO_ROOT/.specify/bridge-events.jsonl"
fi

echo "Auto-archived handoff for '$PRIOR_FEATURE_DIR' (snapshot: $SNAPSHOT_ID)."
exit 0
