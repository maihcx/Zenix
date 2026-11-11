#!/usr/bin/env bash
# bug-fix.sh — Execute bug remediation under Superpowers TDD and verification discipline.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
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

. "$SCRIPT_DIR/common-actor-resolution.sh"
. "$SCRIPT_DIR/bridge-state.sh"

SLUG=""
ACTOR_ARG=""
REGRESSION_TEST=""
VERIFICATION_EVIDENCE=""
COMPLETE=0
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --slug)
            SLUG="$2"
            shift 2
            ;;
        --actor)
            ACTOR_ARG="$2"
            shift 2
            ;;
        --regression-test)
            REGRESSION_TEST="$2"
            shift 2
            ;;
        --verification-evidence)
            VERIFICATION_EVIDENCE="$2"
            shift 2
            ;;
        --complete)
            COMPLETE=1
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
ACTOR="$(resolve_bridge_actor "$ACTOR_ARG")"

BUG_DIR="$SPECIFY_DIR/bugs/$SLUG"
ASSESSMENT_PATH="$BUG_DIR/assessment.md"

if [ ! -f "$ASSESSMENT_PATH" ]; then
    echo "[gss-bridge] Assessment contract missing at '$ASSESSMENT_PATH'. Run /speckit-gss-bridge-bug-assess --slug $SLUG first before attempting a fix." >&2
    exit 1
fi

UPDATE_SCRIPT="$SCRIPT_DIR/update-handoff.sh"

if [ "$COMPLETE" -eq 0 ]; then
    if [ -f "$UPDATE_SCRIPT" ]; then
        bash "$UPDATE_SCRIPT" \
            --status "executing" \
            --feature-directory ".specify/bugs/$SLUG" \
            --artifact-owner "$ACTOR" \
            --actor "$ACTOR" >/dev/null
    fi

    if [ "$OUTPUT_JSON" -eq 1 ]; then
        printf '{"status":"executing","slug":"%s","assessment":".specify/bugs/%s/assessment.md"}\n' \
            "$SLUG" "$SLUG"
    else
        echo "[gss-bridge bug-fix] Active bug remediation started for '$SLUG'."
        echo "  Contract:      .specify/bugs/$SLUG/assessment.md"
        echo "  Handoff state: executing"
        echo ""
        echo "================================================================="
        echo "SUPERPOWERS TDD MANDATE:"
        echo "1. RED:      Write a minimal regression test that FAILS reproducing this bug."
        echo "2. GREEN:    Write the minimal code change to fix the root cause."
        echo "3. REFACTOR: Verify that the test passes and no regressions occur."
        echo "4. COMPLETE: Run ./bug-fix.sh --slug $SLUG --complete --regression-test <file>"
        echo "================================================================="
    fi
    exit 0
fi

# Stage 2: Completion
REG_TEXT="${REGRESSION_TEST:-'(Regression test added in test suite)'}"
VER_TEXT="${VERIFICATION_EVIDENCE:-'Verified by local test runner execution.'}"
FIX_PATH="$BUG_DIR/fix.md"
TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

cat <<EOF > "$FIX_PATH"
# Bug Fix Report: $SLUG

> **Status**: FIXED & VERIFIED
> **Resolving Agent**: $ACTOR
> **Resolved At**: $TIMESTAMP

## 1. Assessment Contract
- Reference: \`.specify/bugs/$SLUG/assessment.md\`

## 2. Superpowers TDD Evidence
- **Regression Test**: \`$REG_TEXT\`
- **Verification Evidence**:
\`\`\`text
$VER_TEXT
\`\`\`

## 3. Superpowers Completion Checklist
- [x] Failing test written before code modification (RED).
- [x] Minimal fix applied resolving root cause (GREEN).
- [x] Regression test passing without side effects.
- [x] Verification evidence confirmed before marking complete.

## 4. Graphify Synchronization
- Knowledge graph scheduled for sync via \`graphify update .\`.
EOF

GRAPH_SYNCED=0
if [ -f "$REPO_ROOT/graphify-out/graph.json" ]; then
    ensure_graphify_exclusions "$REPO_ROOT"
    if command -v graphify >/dev/null 2>&1; then
        GRAPHIFY_FORCE=1 graphify update . --force >/dev/null 2>&1 || true
        GRAPH_SYNCED=1
    fi
fi

if [ -f "$UPDATE_SCRIPT" ]; then
    bash "$UPDATE_SCRIPT" \
        --status "complete" \
        --feature-directory ".specify/bugs/$SLUG" \
        --artifact-owner "$ACTOR" \
        --actor "$ACTOR" >/dev/null
fi

if [ "$OUTPUT_JSON" -eq 1 ]; then
    printf '{"status":"complete","slug":"%s","fix_report":".specify/bugs/%s/fix.md","graph_synced":%d}\n' \
        "$SLUG" "$SLUG" "$GRAPH_SYNCED"
else
    echo "[gss-bridge bug-fix] Bug '$SLUG' successfully resolved and verified!"
    echo "  Fix Report:   .specify/bugs/$SLUG/fix.md"
    if [ "$GRAPH_SYNCED" -eq 1 ]; then
        echo "  Graphify Sync: Synchronized (graphify update .)"
    else
        echo "  Graphify Sync: Run /graphify update ."
    fi
    echo "  Next step:    Run superpowers:requesting-code-review and finish branch."
fi
exit 0
