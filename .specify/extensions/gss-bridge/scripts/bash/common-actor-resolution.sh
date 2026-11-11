#!/usr/bin/env bash
set -euo pipefail

get_bridge_repo_root() {
    if command -v git >/dev/null 2>&1; then
        local root
        root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
        if [ -n "$root" ]; then
            echo "$root"
            return 0
        fi
    fi
    pwd
}

resolve_bridge_actor() {
    local arg="${1:-}"
    local valid=" codex claude antigravity gemini unknown "

    if [ -n "$arg" ]; then
        local lower_arg
        lower_arg="$(echo "$arg" | tr '[:upper:]' '[:lower:]')"
        if [[ "$valid" =~ [[:space:]]"$lower_arg"[[:space:]] ]]; then
            echo "$lower_arg"
            return 0
        fi
        echo "unknown"
        return 0
    fi

    if [ -n "${GSS_BRIDGE_ACTOR:-}" ]; then
        local env_actor
        env_actor="$(echo "$GSS_BRIDGE_ACTOR" | tr '[:upper:]' '[:lower:]')"
        if [[ "$valid" =~ [[:space:]]"$env_actor"[[:space:]] ]]; then
            echo "$env_actor"
            return 0
        fi
    fi

    if [ -n "${SPECKIT_BRIDGE_ACTOR:-}" ]; then
        local env_actor
        env_actor="$(echo "$SPECKIT_BRIDGE_ACTOR" | tr '[:upper:]' '[:lower:]')"
        if [[ "$valid" =~ [[:space:]]"$env_actor"[[:space:]] ]]; then
            echo "$env_actor"
            return 0
        fi
    fi

    echo "unknown"
}
ensure_graphify_index() {
    local repo_root="${1:-}"
    local silent="${2:-0}"
    if [ -z "$repo_root" ]; then
        repo_root="$(get_bridge_repo_root)"
    fi
    local graph_path="$repo_root/graphify-out/graph.json"

    if [ -f "$graph_path" ]; then
        echo "$graph_path"
        return 0
    fi

    # Graphify index missing: execute initialization command instead of skipping
    if command -v graphify >/dev/null 2>&1; then
        if [ "$silent" -ne 1 ]; then
            echo "[graphify] Notice: graphify-out/graph.json not found. Executing graphify to initialize knowledge graph..." >&2
        fi
        (cd "$repo_root" && graphify . >/dev/null 2>&1 || true)
    elif command -v python3 >/dev/null 2>&1 && python3 -c "import graphify" >/dev/null 2>&1; then
        if [ "$silent" -ne 1 ]; then
            echo "[graphify] Notice: graphify-out/graph.json not found. Executing graphify to initialize knowledge graph..." >&2
        fi
        (cd "$repo_root" && python3 -m graphify . >/dev/null 2>&1 || true)
    elif command -v python >/dev/null 2>&1 && python -c "import graphify" >/dev/null 2>&1; then
        if [ "$silent" -ne 1 ]; then
            echo "[graphify] Notice: graphify-out/graph.json not found. Executing graphify to initialize knowledge graph..." >&2
        fi
        (cd "$repo_root" && python -m graphify . >/dev/null 2>&1 || true)
    fi

    echo "$graph_path"
    return 0
}
