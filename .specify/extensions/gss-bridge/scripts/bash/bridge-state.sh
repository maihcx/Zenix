#!/usr/bin/env bash
# bridge-state.sh — shared helper for state-summary + pending-task counting in GSS-Bridge.
set -euo pipefail

BRIDGE_TASK_ID_REGEX='^[[:space:]]*- \[ \] T[0-9]+'
BRIDGE_DEFERRED_HEADER_REGEX='^#+[[:space:]]+.*\b(deferred|optional|out of scope|won.?t do|future|wontfix|backlog)\b'

get_pending_task_count() {
    local tasks_path="$1"
    if [ ! -f "$tasks_path" ]; then
        echo "-1"
        return 0
    fi

    local in_exempt=0
    local count=0
    while IFS= read -r line || [ -n "$line" ]; do
        if [[ "$line" =~ ^#+[[:space:]]+ ]]; then
            if echo "$line" | grep -Ei "$BRIDGE_DEFERRED_HEADER_REGEX" >/dev/null 2>&1; then
                in_exempt=1
            else
                in_exempt=0
            fi
            continue
        fi

        if [ "$in_exempt" -eq 0 ]; then
            if [[ "$line" =~ $BRIDGE_TASK_ID_REGEX ]]; then
                count=$((count + 1))
            fi
        fi
    done < "$tasks_path"

    echo "$count"
}

get_artifacts_sha256_json() {
    local feature_full="$1"
    local spec_sha="null"
    local plan_sha="null"
    local tasks_sha="null"

    if [ -f "$feature_full/spec.md" ]; then
        spec_sha="\"$(sha256sum "$feature_full/spec.md" | awk '{print $1}')\""
    fi
    if [ -f "$feature_full/plan.md" ]; then
        plan_sha="\"$(sha256sum "$feature_full/plan.md" | awk '{print $1}')\""
    fi
    if [ -f "$feature_full/tasks.md" ]; then
        tasks_sha="\"$(sha256sum "$feature_full/tasks.md" | awk '{print $1}')\""
    fi

    printf '{"spec.md":%s,"plan.md":%s,"tasks.md":%s}' "$spec_sha" "$plan_sha" "$tasks_sha"
}

read_handoff_field() {
    local file="$1"
    local key="$2"
    if [ ! -f "$file" ]; then return 0; fi
    if command -v jq >/dev/null 2>&1; then
        jq -r ".$key // empty" "$file" 2>/dev/null || true
    else
        awk -v key="$key" -F'"' '
            $0 ~ "\"" key "\"" {
                for (i = 1; i <= NF; i++) {
                    if ($i == key) {
                        val = $(i+2)
                        if (val != "null" && val != "") {
                            print val
                        }
                        exit
                    }
                }
            }
        ' "$file"
    fi
}

write_bridge_state_summary_full() {
    local handoff_path="$1"
    local repo_root="$2"
    local actor="$3"
    local prior_actor="${4:-}"
    local emit_complete_warning="${5:-0}"

    local feature_dir=""
    local status=""
    local owner=""
    local pending_count="-1"

    if [ -f "$handoff_path" ]; then
        feature_dir="$(read_handoff_field "$handoff_path" "feature_directory")"
        status="$(read_handoff_field "$handoff_path" "status")"
        owner="$(read_handoff_field "$handoff_path" "artifact_owner")"

        if [ -n "$feature_dir" ]; then
            local feature_full="$repo_root/$feature_dir"
            if [ -f "$feature_full/tasks.md" ]; then
                pending_count="$(get_pending_task_count "$feature_full/tasks.md")"
            fi
        fi
    fi

    echo "[bridge state]"
    if [ -n "$feature_dir" ]; then
        echo "  Feature directory: $feature_dir"
    else
        echo "  Feature directory: (none)"
    fi

    if [ -n "$status" ]; then
        echo "  Status: $status"
    else
        echo "  Status: (unknown)"
    fi

    if [ -n "$owner" ]; then
        echo "  Artifact owner: $owner"
    else
        echo "  Artifact owner: unknown"
    fi

    if [ -n "$prior_actor" ] && [ "$prior_actor" != "$actor" ]; then
        echo "  Actor: $prior_actor → $actor"
    else
        echo "  Actor: ${actor:-unknown}"
    fi

    if [ "$pending_count" -ge 0 ]; then
        echo "  Pending tasks: $pending_count"
    elif [ -n "$feature_dir" ]; then
        echo "  Pending tasks: (no tasks.md)"
    fi

    if [ -f "$repo_root/graphify-out/graph.json" ]; then
        echo "  Graphify: indexed (graph.json)"
    else
        echo "  Graphify: not built (run /graphify)"
    fi

    if [ "$emit_complete_warning" -eq 1 ] && [ "$status" = "complete" ] && [ "$pending_count" -gt 0 ]; then
        printf '[bridge] WARNING: Feature marked complete but %d task(s) remain unchecked in tasks.md\n' "$pending_count" >&2
    fi
}
