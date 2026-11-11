---
description: "Select impacted test suites topologically linked to modified components within 1-2 hops via Graphify"
---

# GSS-Bridge Graph-Guided Test Impact Analysis (TIA)

Analyze `graphify-out/graph.json` to identify test suites and test files topologically connected within 1 to 2 hops of target symbols, functions, or modified files. Accelerates Superpowers TDD cycles by running only relevant tests before full suite validation.

## Knowledge Graph Pre-requisite

If `graphify-out/graph.json` is missing, the selector executes `graphify .` to initialize the knowledge graph before calculating test impact.

## Workflow

1. **Input Identification**:
   Provide a target node symbol or let the tool inspect `git status` / `git diff` for modified files.
2. **Graph Traversal**:
   Traverses incoming and outgoing edges in `graphify-out/graph.json` up to 2 hops away.
3. **Test Filter**:
   Identifies all connected nodes whose file path or symbol matches test patterns (`test_`, `_test`, `tests/`, `.spec.`, `.test.`).
4. **Execution Selection**:
   Returns the deduplicated list of impacted test files ready for targeted test runner execution.

## Invocation

```powershell
# Find impacted tests for a specific symbol
.\.specify\extensions\gss-bridge\scripts\powershell\test-impact.ps1 -Node "AuthService"

# Detect impacted tests based on current git modifications
.\.specify\extensions\gss-bridge\scripts\powershell\test-impact.ps1 -DetectChanged -Json
```

```bash
# Find impacted tests for a specific symbol
bash .specify/extensions/gss-bridge/scripts/bash/test-impact.sh --node "AuthService"

# Detect impacted tests based on current git modifications
bash .specify/extensions/gss-bridge/scripts/bash/test-impact.sh --detect-changed --json
```
