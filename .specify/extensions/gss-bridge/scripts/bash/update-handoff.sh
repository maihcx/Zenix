#!/usr/bin/env bash
# update-handoff.sh — update GSS-Bridge handoff state.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

STATUS="ready"
FEATURE_DIR=""
REASON=""
ARTIFACT_OWNER=""
ACTOR_ARG=""
CLEAR_FEATURE_DIR=0

while [ $# -gt 0 ]; do
    case "$1" in
        --status)
            STATUS="$2"
            shift 2
            ;;
        --feature-directory)
            FEATURE_DIR="$2"
            shift 2
            ;;
        --reason)
            REASON="$2"
            shift 2
            ;;
        --artifact-owner)
            ARTIFACT_OWNER="$2"
            shift 2
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --clear-feature-directory)
            CLEAR_FEATURE_DIR=1
            shift
            ;;
        *)
            shift
            ;;
    esac
done

REPO_ROOT="$(get_bridge_repo_root)"
ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

ensure_graphify_exclusions() {
    local target_root="$1"
    [ -z "$target_root" ] || [ ! -d "$target_root" ] && return 0
    local ignore_path="$target_root/.graphifyignore"
    local mandatory=(
        ".specify/"
        ".agents/"
        ".claude/"
        ".gemini/"
        ".cursor/"
        ".windsurf/"
        ".kilo/"
        ".devin/"
        ".codex/"
        ".copilot/"
        ".ai/"
        "graphify-out/"
        "raw/"
        ".graphify*"
    )
    if [ -f "$ignore_path" ]; then
        local missing=()
        for p in "${mandatory[@]}"; do
            if ! grep -qF "$p" "$ignore_path" 2>/dev/null; then
                missing+=("$p")
            fi
        done
        if [ ${#missing[@]} -gt 0 ]; then
            printf "
# GSS-Bridge Meta-Tooling Exclusions
" >> "$ignore_path"
            for m in "${missing[@]}"; do
                printf "%s
" "$m" >> "$ignore_path"
            done
        fi
    else
        cat << 'EOF' > "$ignore_path"
# =====================================================================
# Graphify Ignore Rules: AI Knowledge & Meta-Tooling Exclusions
# Generated and maintained by GSS-Bridge Protocol
# =====================================================================

# 1. Spec-Kit & Agent Governance Frameworks (AI Knowledge & Directives)
.specify/
.agents/
.claude/
.gemini/
.cursor/
.windsurf/
.kilo/
.devin/
.codex/
.copilot/
.ai/

# 2. Graphify Internal Engine, Intermediate Processing & Artifacts
graphify-out/
raw/
.graphify*

# 3. Transpiled Bundles, Minified Workers & Build Artifacts
node_modules/
dist/
build/
bin/
obj/
out/
target/
.cache/
*.min.js
*.min.css
*.worker.js
*.worker-*.js
*.bundle.js
editorWorker*.js
EOF
    fi

    local out_dir="$target_root/graphify-out"
    if [ -d "$out_dir" ]; then
        local build_cfg="$out_dir/.graphify_build.json"
        cat << 'EOF' > "$build_cfg"
{"excludes": [".specify/", ".agents/", ".claude/", ".gemini/", ".cursor/", ".windsurf/", ".kilo/", ".devin/", ".codex/", ".copilot/", ".ai/", "graphify-out/", "raw/", ".graphify*"], "gitignore": true}
EOF
    fi
}

ensure_graphify_exclusions "$REPO_ROOT"

SPECIFY_DIR="$REPO_ROOT/.specify"
mkdir -p "$SPECIFY_DIR"

HANDOFF_PATH="$SPECIFY_DIR/gss-handoff.json"
PRIOR_FEATURE_DIR=""
PRIOR_OWNER=""
PRIOR_ACTOR=""

if [ -f "$HANDOFF_PATH" ]; then
    PRIOR_FEATURE_DIR="$(read_handoff_field "$HANDOFF_PATH" "feature_directory")"
    PRIOR_OWNER="$(read_handoff_field "$HANDOFF_PATH" "artifact_owner")"
fi

TARGET_FEATURE_DIR="$FEATURE_DIR"
if [ "$CLEAR_FEATURE_DIR" -eq 1 ]; then
    TARGET_FEATURE_DIR=""
elif [ -z "$TARGET_FEATURE_DIR" ]; then
    TARGET_FEATURE_DIR="$PRIOR_FEATURE_DIR"
fi

OWNER="$ARTIFACT_OWNER"
if [ -z "$OWNER" ]; then
    OWNER="${PRIOR_OWNER:-$ACTOR}"
fi
[ -z "$OWNER" ] && OWNER="unknown"

TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
SNAPSHOT_ID=""

if [ -n "$TARGET_FEATURE_DIR" ] && [ -d "$REPO_ROOT/$TARGET_FEATURE_DIR" ]; then
    SNAP_TS="$(date -u +"%Y%m%d%H%M%S")"
    SNAPSHOT_ID="${SNAP_TS}-${STATUS}"
    SNAP_DIR="$SPECIFY_DIR/bridge-snapshots/$SNAPSHOT_ID"
    mkdir -p "$SNAP_DIR"
    for art in spec.md plan.md tasks.md; do
        if [ -f "$REPO_ROOT/$TARGET_FEATURE_DIR/$art" ]; then
            cp -p "$REPO_ROOT/$TARGET_FEATURE_DIR/$art" "$SNAP_DIR/"
        fi
    done
    if [ -f "$SPECIFY_DIR/memory/constitution.md" ]; then
        cp -p "$SPECIFY_DIR/memory/constitution.md" "$SNAP_DIR/"
    fi
fi

SHA_MAP="null"
if [ "$STATUS" = "executing" ] || [ "$STATUS" = "complete" ]; then
    if [ -n "$TARGET_FEATURE_DIR" ] && [ -d "$REPO_ROOT/$TARGET_FEATURE_DIR" ]; then
        SHA_MAP="$(get_artifacts_sha256_json "$REPO_ROOT/$TARGET_FEATURE_DIR")"
    fi
fi

if command -v jq >/dev/null 2>&1; then
    jq -n \
        --arg ts "$TIMESTAMP" \
        --arg fd "$TARGET_FEATURE_DIR" \
        --arg st "$STATUS" \
        --arg rsn "$REASON" \
        --arg owner "$OWNER" \
        --arg snap "$SNAPSHOT_ID" \
        --argjson shas "$SHA_MAP" \
        '{
            schema_version: 1,
            updated_at: $ts,
            feature_directory: (if $fd == "" then null else $fd end),
            source_of_truth: (if ($fd | test("bugs")) then {
                constitution: ".specify/memory/constitution.md",
                assessment: ($fd + "/assessment.md"),
                fix: ($fd + "/fix.md")
            } else {
                constitution: ".specify/memory/constitution.md",
                spec: (if $fd == "" then null else ($fd + "/spec.md") end),
                plan: (if $fd == "" then null else ($fd + "/plan.md") end),
                tasks: (if $fd == "" then null else ($fd + "/tasks.md") end)
            } end),
            supersedes: ["speckit.implement"],
            executor: (if $st == "ready" then "speckit" else "gss-bridge" end),
            capabilities: [
                "graphify-codebase-intelligence",
                "blast-radius-analysis",
                "task-parallelism-verification",
                "executing-plans",
                "test-driven-development",
                "subagent-scoping",
                "systematic-debugging",
                "verification-before-completion",
                "requesting-code-review",
                "finishing-a-development-branch"
            ],
            status: $st,
            blocked_reason: (if $st == "blocked" then (if $rsn == "" then "(no reason provided)" else $rsn end) else null end),
            artifact_owner: $owner,
            review_only_agents: [],
            last_snapshot_id: (if $snap == "" then null else $snap end),
            artifacts_sha256: $shas,
            instructions: "Use /gss-bridge or /speckit-gss-bridge-execute. GSS-Bridge combines Spec-Kit governance, Graphify topology context, and Superpowers TDD execution discipline."
        }' > "$SPECIFY_DIR/gss-handoff.json"

    # Dual write for backward compatibility
    cp -p "$SPECIFY_DIR/gss-handoff.json" "$SPECIFY_DIR/superpowers-handoff.json"

    # Append to event log
    jq -c -n \
        --arg ts "$TIMESTAMP" \
        --arg act "handoff" \
        --arg st "$STATUS" \
        --arg fd "$TARGET_FEATURE_DIR" \
        --arg dec "updated" \
        --arg rsn "$REASON" \
        --arg actor "$ACTOR" \
        --arg snap "$SNAPSHOT_ID" \
        '{
            timestamp: $ts,
            action: $act,
            status: $st,
            feature_directory: (if $fd == "" then null else $fd end),
            decision: $dec,
            reason: $rsn,
            actor: $actor,
            snapshot_id: (if $snap == "" then null else $snap end)
        }' >> "$SPECIFY_DIR/bridge-events.jsonl"
else
    fb_fd_val="null"
    fb_spec_val="null"
    fb_plan_val="null"
    fb_tasks_val="null"
    fb_blocked_val="null"
    fb_snap_val="null"
    fb_exec_val="gss-bridge"

    if [ "$STATUS" = "ready" ]; then fb_exec_val="speckit"; fi
    if [ -n "$TARGET_FEATURE_DIR" ]; then
        fb_fd_val="\"$TARGET_FEATURE_DIR\""
        fb_spec_val="\"$TARGET_FEATURE_DIR/spec.md\""
        fb_plan_val="\"$TARGET_FEATURE_DIR/plan.md\""
        fb_tasks_val="\"$TARGET_FEATURE_DIR/tasks.md\""
    fi
    if [ "$STATUS" = "blocked" ]; then
        if [ -z "$REASON" ]; then
            fb_blocked_val='"(no reason provided)"'
        else
            fb_blocked_val="\"$REASON\""
        fi
    fi
    if [ -n "$SNAPSHOT_ID" ]; then
        fb_snap_val="\"$SNAPSHOT_ID\""
    fi

    cat <<EOF > "$SPECIFY_DIR/gss-handoff.json"
{
  "schema_version": 1,
  "updated_at": "$TIMESTAMP",
  "feature_directory": $fb_fd_val,
  "source_of_truth": {
    "constitution": ".specify/memory/constitution.md",
    "spec": $fb_spec_val,
    "plan": $fb_plan_val,
    "tasks": $fb_tasks_val
  },
  "supersedes": [
    "speckit.implement"
  ],
  "executor": "$fb_exec_val",
  "capabilities": [
    "graphify-codebase-intelligence",
    "blast-radius-analysis",
    "task-parallelism-verification",
    "executing-plans",
    "test-driven-development",
    "subagent-scoping",
    "systematic-debugging",
    "verification-before-completion",
    "requesting-code-review",
    "finishing-a-development-branch"
  ],
  "status": "$STATUS",
  "blocked_reason": $fb_blocked_val,
  "artifact_owner": "$OWNER",
  "review_only_agents": [],
  "last_snapshot_id": $fb_snap_val,
  "artifacts_sha256": $SHA_MAP,
  "instructions": "Use /gss-bridge or /speckit-gss-bridge-execute. GSS-Bridge combines Spec-Kit governance, Graphify topology context, and Superpowers TDD execution discipline."
}
EOF

    # Dual write for backward compatibility
    cp -p "$SPECIFY_DIR/gss-handoff.json" "$SPECIFY_DIR/superpowers-handoff.json"

    # Append to event log
    fb_ev_rsn="null"
    if [ -n "$REASON" ]; then fb_ev_rsn="\"$REASON\""; fi
    printf '{"timestamp":"%s","action":"handoff","status":"%s","feature_directory":%s,"decision":"updated","reason":%s,"actor":"%s","snapshot_id":%s}\n' \
        "$TIMESTAMP" "$STATUS" "$fb_fd_val" "$fb_ev_rsn" "$ACTOR" "$fb_snap_val" >> "$SPECIFY_DIR/bridge-events.jsonl"
fi

# Auto Graphify Sync upon completion
if [ "$STATUS" = "complete" ] && [ -f "$REPO_ROOT/graphify-out/graph.json" ]; then
    ensure_graphify_exclusions "$REPO_ROOT"
    if command -v graphify >/dev/null 2>&1; then
        echo "[bridge] Triggering automatic Graphify sync (graphify update . --force)..."
        GRAPHIFY_FORCE=1 graphify update . --force >/dev/null 2>&1 || true
        echo "[bridge] Knowledge graph synchronized."
    fi
fi

WARN_COMPLETE=0
if [ "$STATUS" = "complete" ]; then WARN_COMPLETE=1; fi
write_bridge_state_summary_full "$SPECIFY_DIR/gss-handoff.json" "$REPO_ROOT" "$ACTOR" "$PRIOR_ACTOR" "$WARN_COMPLETE"

echo "Wrote .specify/gss-handoff.json with status '$STATUS'."
exit 0
