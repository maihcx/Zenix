---
name: speckit-gss-bridge-blast-radius
description: Analyze component blast radius and verify parallel task [P] independence using Graphify
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.blast-radius.md
---


# GSS-Bridge Blast Radius & Coupling Checker

Leverage `graphify-out/graph.json` to calculate component blast radius and verify that tasks tagged `[P]` are genuinely decoupled before dispatching parallel subagents.

## Knowledge Graph Auto-Initialization

If `graphify-out/graph.json` has not yet been generated in the workspace, the command automatically initializes and constructs the knowledge graph via `graphify .` before calculating blast radius, rather than skipping or failing.

## Capabilities

1. **Blast Radius Analysis**:
   Given a node/component name, inspects immediate callers (in-edges), callees (out-edges), and degree centrality.
2. **Coupling Check for Parallel Tasks**:
   Given two nodes `NodeA` and `NodeB`, traverses shortest paths in the knowledge graph. If connected within 2 hops, alerts that parallel execution has a high risk of collisions.

## Invocation

```powershell
# Analyze blast radius of a single component
.\.specify\extensions\gss-bridge\scripts\powershell\graphify-blast-radius.ps1 -Node "AuthService"

# Verify independence between two tasks / components
.\.specify\extensions\gss-bridge\scripts\powershell\graphify-blast-radius.ps1 -CheckCoupling -NodeA "OrderController" -NodeB "NotificationWorker"
```

```bash
# Analyze blast radius of a single component
bash .specify/extensions/gss-bridge/scripts/bash/graphify-blast-radius.sh --node "AuthService"

# Verify independence between two tasks / components
bash .specify/extensions/gss-bridge/scripts/bash/graphify-blast-radius.sh --check-coupling --node-a "OrderController" --node-b "NotificationWorker"
```
