---
name: speckit-gss-bridge-drift
description: Detect architectural drift, layer boundary violations, and circular dependencies using Graphify topology
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.drift.md
---


# GSS-Bridge Architectural Drift & Cycle Auditor

Audit the codebase knowledge graph against architectural layer boundaries and detect circular dependency cycles. Enforces clean architecture contracts across agents during implementation.

## Knowledge Graph Pre-requisite & Auto-Initialization

If `graphify-out/graph.json` has not yet been generated in the workspace, the auditor automatically executes `graphify .` to initialize and build the knowledge graph before analyzing layer boundaries or dependency cycles.

## Workflow

1. **Rule Configuration**:
   Reads layering rules from `.specify/memory/architecture-rules.json` (defining layer tiers like `domain`, `core`, `adapters`, `ui`, and forbidden upward dependencies).
2. **Cycle Detection (Tarjan's SCC)**:
   Discovers strongly connected components in the knowledge graph to detect recursive dependency loops.
3. **Layer Boundary Check**:
   Verifies that edges do not cross from lower abstraction layers into higher layers.
4. **CI/Gatekeeper Enforcement**:
   Optional `--fail-on-cycles` exits with non-zero status on violations, blocking invalid PRs or branches.

## Invocation

```powershell
# Audit codebase for circular cycles and architectural drift
.\.specify\extensions\gss-bridge\scripts\powershell\drift-detector.ps1

# Strict enforcement in CI/CD pipelines
.\.specify\extensions\gss-bridge\scripts\powershell\drift-detector.ps1 -FailOnCycles -Json
```

```bash
# Audit codebase for circular cycles and architectural drift
bash .specify/extensions/gss-bridge/scripts/bash/drift-detector.sh

# Strict enforcement in CI/CD pipelines
bash .specify/extensions/gss-bridge/scripts/bash/drift-detector.sh --fail-on-cycles --json
```
