---
name: speckit-gss-bridge-dispatch
description: Partition tasks tagged [P] into independent execution waves and generate Graphify-scoped subagent prompts
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.dispatch.md
---


# GSS-Bridge Parallel Subagent Wave Dispatcher

Automates the grouping and context-scoping of parallel tasks `[P]` from `tasks.md` using **Graphify** topological distance analysis.

## Knowledge Graph Pre-requisite & Auto-Initialization

If `graphify-out/graph.json` has not yet been generated in the workspace, the wave dispatcher automatically executes `graphify .` to initialize and build the knowledge graph before computing topological graph distance ($d \ge 3$), preventing erroneous parallel collisions.

## Capabilities

1. **Topological Coupling Detection**:
   Inspects all `[P]` tasks in `tasks.md` and checks shortest paths between target components in `graphify-out/graph.json`.
2. **Execution Wave Partitioning**:
   Applies graph coloring to partition tasks into safe, non-interfering parallel waves (Wave 1, Wave 2, etc.).
3. **Subagent Context Boundary Scoping**:
   Generates isolated subagent prompt instructions restricted exclusively to the 1-hop / 2-hop file neighborhood identified by Graphify. Prevents repo-wide token dumping and merge collisions.

## Invocation

```powershell
# Analyze active feature tasks and generate dispatch plan
.\.specify\extensions\gss-bridge\scripts\powershell\dispatch-subagents.ps1 -Actor <actor>

# Analyze specific tasks file with JSON output
.\.specify\extensions\gss-bridge\scripts\powershell\dispatch-subagents.ps1 -TasksPath "specs/001-payment-flow/tasks.md" -Json
```

```bash
# Analyze active feature tasks and generate dispatch plan
bash .specify/extensions/gss-bridge/scripts/bash/dispatch-subagents.sh --actor <actor>

# Analyze specific tasks file with JSON output
bash .specify/extensions/gss-bridge/scripts/bash/dispatch-subagents.sh --tasks-path "specs/001-payment-flow/tasks.md" --json
```
