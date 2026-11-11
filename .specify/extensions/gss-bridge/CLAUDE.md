# CLAUDE.md: GSS-Bridge Developer Guidelines

## Core Principles

- **Spec-Kit**: Single source of truth for requirements & tasks (`specs/<feature>/`). Never create files in `docs/plans/`.
- **Graphify**: Single source of truth for codebase topology (`graphify-out/graph.json`). Prefer `graphify query`, `graphify explain`, and `graphify path` over blind grepping. If `graphify-out/graph.json` does not exist, execute `/graphify .` to initialize the knowledge graph before running any Graphify-dependent command or triage step; never skip or bypass Graphify. AI governance directories (`.specify/`, `.agents/`, `.claude/`, etc.) and Graphify outputs (`graphify-out/`, `raw/`, `.graphify*`) must always be excluded via `.graphifyignore`.
- **Superpowers**: Single standard for execution discipline. TDD Red-Green-Refactor, systematic debugging via graph path tracing, verified evidence before marking `- [x] T###`.
- **Formatting Standard**: All structural files (`.md`, `.yml`, `.yaml`, `.json`, `.jsonl`, `.ps1`, `.py`, manifests, templates) must strictly use CRLF (`\r\n`) line endings. All POSIX shell scripts (`*.sh`) must use LF (`\n`) for Linux and macOS execution compatibility.

## Essential Commands

```bash
# Check status and readiness
bash .specify/extensions/gss-bridge/scripts/bash/bridge-status.sh --readiness

# Calculate component blast radius with Mermaid diagram
bash .specify/extensions/gss-bridge/scripts/bash/graphify-blast-radius.sh --node "<ComponentName>" --mermaid

# Verify task independence for parallel [P] execution
bash .specify/extensions/gss-bridge/scripts/bash/graphify-blast-radius.sh --check-coupling --node-a "CompA" --node-b "CompB"

# Assess a bug with Graphify call graph & blast radius
bash .specify/extensions/gss-bridge/scripts/bash/bug-assess.sh --slug "auth-null-token" --target-node "AuthService"

# Remediate a bug with Superpowers TDD & automatic Graphify sync
bash .specify/extensions/gss-bridge/scripts/bash/bug-fix.sh --slug "auth-null-token"

# Partition parallel [P] tasks into conflict-free execution waves with AST pruning
bash .specify/extensions/gss-bridge/scripts/bash/dispatch-subagents.sh --tasks "specs/001-feature/tasks.md" --prune-context

# Extract AST structural skeleton to reduce token load
bash .specify/extensions/gss-bridge/scripts/bash/ast-skeleton.sh --path "src/core.py"

# Select impacted tests based on Graphify topology
bash .specify/extensions/gss-bridge/scripts/bash/test-impact.sh --detect-changed

# Audit architectural drift and circular dependency cycles
bash .specify/extensions/gss-bridge/scripts/bash/drift-detector.sh --fail-on-cycles

# Generate interactive HTML dashboard
bash .specify/extensions/gss-bridge/scripts/bash/generate-dashboard.sh --open

# Record cross-agent decision
bash .specify/extensions/gss-bridge/scripts/bash/log-decision.sh --decision "Adopt X" --rationale "Because Y" --topic "arch"

# Manage isolated git worktrees for subagents
bash .specify/extensions/gss-bridge/scripts/bash/wave-worktree.sh --action list

# Run tests
pwsh -File tests/run-all.ps1
bash tests/run-all.sh
```
