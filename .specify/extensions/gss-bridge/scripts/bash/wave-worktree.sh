#!/usr/bin/env bash
# wave-worktree.sh — Manages isolated git worktrees for parallel subagent wave execution.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

ACTION="list"
NAME=""
BRANCH=""
BASE="HEAD"
TARGET=""
FORCE=0
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --action)
            ACTION="$2"
            shift 2
            ;;
        --name)
            NAME="$2"
            shift 2
            ;;
        --branch)
            BRANCH="$2"
            shift 2
            ;;
        --base)
            BASE="$2"
            shift 2
            ;;
        --target)
            TARGET="$2"
            shift 2
            ;;
        --force)
            FORCE=1
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
WORKTREE_BASE_DIR="$SPECIFY_DIR/worktrees"
mkdir -p "$WORKTREE_BASE_DIR"

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
        PYTHON_CMD="$cmd"
        break
    fi
done

if [ "$ACTION" = "list" ]; then
    RAW_LIST="$(git worktree list --porcelain 2>/dev/null || true)"
    if [ -z "$PYTHON_CMD" ]; then
        if [ "$OUTPUT_JSON" -eq 1 ]; then
            echo "[]"
        else
            echo "No active worktrees or Python not available."
        fi
        exit 0
    fi
    
    export GSS_WT_RAW="$RAW_LIST"
    export GSS_WT_JSON="$OUTPUT_JSON"
    $PYTHON_CMD - <<'PYEOF'
import os, sys, json

raw = os.environ.get("GSS_WT_RAW", "")
output_json = (os.environ.get("GSS_WT_JSON", "0") == "1")

items = []
current = {}
for line in raw.splitlines():
    line = line.strip()
    if not line:
        if current.get("worktree"):
            items.append(current)
            current = {}
        continue
    if line.startswith("worktree "):
        current["worktree"] = line[9:].strip()
    elif line.startswith("HEAD "):
        current["head"] = line[5:].strip()
    elif line.startswith("branch "):
        current["branch"] = line[7:].strip()

if current.get("worktree"):
    items.append(current)

filtered = []
for it in items:
    wt = it.get("worktree", "").replace("\\", "/")
    if "/.specify/worktrees/" in wt:
        name = os.path.basename(wt)
        br = it.get("branch", "").replace("refs/heads/", "")
        filtered.append({
            "name": name,
            "path": it.get("worktree"),
            "branch": br,
            "head": it.get("head")
        })

if output_json:
    print(json.dumps(filtered, ensure_ascii=False))
else:
    print("=================================================================")
    print("GSS-BRIDGE PARALLEL WAVE WORKTREES")
    print("=================================================================")
    if not filtered:
        print("No active worktrees in .specify/worktrees/")
    else:
        for w in filtered:
            print(f"- Wave Worktree: {w['name']}")
            print(f"  Path:   {w['path']}")
            print(f"  Branch: {w['branch']}")
            print(f"  HEAD:   {w['head']}")
    print("=================================================================")
PYEOF
    exit 0
fi

if [ -z "$NAME" ]; then
    echo "Error: --name is required for action '$ACTION'." >&2
    exit 2
fi

SANITIZED_NAME="$(printf '%s' "$NAME" | tr -c 'a-zA-Z0-9_-' '-')"
WT_PATH="$WORKTREE_BASE_DIR/$SANITIZED_NAME"
BRANCH_NAME="${BRANCH:-gss-worktree/$SANITIZED_NAME}"

if [ "$ACTION" = "create" ]; then
    if [ -d "$WT_PATH" ]; then
        if [ "$FORCE" -eq 1 ]; then
            git worktree remove --force "$WT_PATH" 2>/dev/null || true
            git branch -D "$BRANCH_NAME" 2>/dev/null || true
        else
            echo "Error: Worktree directory '$WT_PATH' already exists. Use --force to overwrite." >&2
            exit 1
        fi
    fi

    if git branch --list "$BRANCH_NAME" | grep -q "$BRANCH_NAME"; then
        if [ "$FORCE" -eq 1 ]; then
            git branch -D "$BRANCH_NAME" 2>/dev/null || true
        else
            echo "Error: Branch '$BRANCH_NAME' already exists. Use --force to replace." >&2
            exit 1
        fi
    fi

    if ! git worktree add -b "$BRANCH_NAME" "$WT_PATH" "$BASE" >/dev/null 2>&1; then
        echo "Error: Failed to create git worktree at '$WT_PATH'." >&2
        exit 1
    fi

    if [ "$OUTPUT_JSON" -eq 1 ]; then
        printf '{"action":"create","name":"%s","path":"%s","branch":"%s","base":"%s","status":"created"}\n' \
            "$SANITIZED_NAME" "$WT_PATH" "$BRANCH_NAME" "$BASE"
    else
        echo "[gss-bridge] Isolated worktree created for wave '$SANITIZED_NAME':"
        echo "  Path:   $WT_PATH"
        echo "  Branch: $BRANCH_NAME"
        echo "  Base:   $BASE"
    fi
    exit 0
fi

if [ "$ACTION" = "merge" ]; then
    if ! git branch --list "$BRANCH_NAME" | grep -q "$BRANCH_NAME"; then
        echo "Error: Branch '$BRANCH_NAME' does not exist to merge." >&2
        exit 1
    fi

    MERGE_MSG="Merge isolated wave worktree: $SANITIZED_NAME ($BRANCH_NAME)"
    if ! git merge --no-ff "$BRANCH_NAME" -m "$MERGE_MSG" >/dev/null 2>&1; then
        echo "Error: Merge conflict or failure merging '$BRANCH_NAME'." >&2
        exit 1
    fi

    if [ "$OUTPUT_JSON" -eq 1 ]; then
        printf '{"action":"merge","name":"%s","branch":"%s","status":"merged"}\n' \
            "$SANITIZED_NAME" "$BRANCH_NAME"
    else
        echo "[gss-bridge] Successfully merged wave branch '$BRANCH_NAME'."
    fi
    exit 0
fi

if [ "$ACTION" = "cleanup" ]; then
    if [ -d "$WT_PATH" ]; then
        git worktree remove --force "$WT_PATH" >/dev/null 2>&1 || true
    fi
    git branch -D "$BRANCH_NAME" >/dev/null 2>&1 || true

    if [ "$OUTPUT_JSON" -eq 1 ]; then
        printf '{"action":"cleanup","name":"%s","branch":"%s","status":"cleaned"}\n' \
            "$SANITIZED_NAME" "$BRANCH_NAME"
    else
        echo "[gss-bridge] Cleaned up worktree and branch for '$SANITIZED_NAME'."
    fi
    exit 0
fi

echo "Error: Unknown action '$ACTION'" >&2
exit 2
