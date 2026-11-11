---
name: speckit-gss-bridge-dashboard
description: Generate a standalone interactive HTML visual dashboard summarizing tasks, graph topology, bugs, and decisions
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.dashboard.md
---


# GSS-Bridge Interactive Visual Dashboard

Generate a standalone, zero-dependency HTML cockpit at `.specify/dashboard/index.html` displaying real-time metrics across Spec-Kit feature progress, Graphify AST hub nodes, triaged bug assessments, and cross-agent decision timelines.

## Workflow

1. **Telemetry Aggregation**:
   Collects state from `.specify/gss-handoff.json`, feature `tasks.md`, `graphify-out/graph.json`, `.specify/bugs/`, and `.specify/agent-decisions.jsonl`.
2. **Offline-Ready Visual UI**:
   Renders a responsive dark-mode cockpit with metrics cards, task progress bars, top God node degrees, risk tables, and decision feeds.
3. **Instant Browser Launch**:
   Optional `-Open` flag opens the visual report in your default browser.

## Invocation

```powershell
# Generate dashboard
.\.specify\extensions\gss-bridge\scripts\powershell\generate-dashboard.ps1

# Generate and immediately open in default browser
.\.specify\extensions\gss-bridge\scripts\powershell\generate-dashboard.ps1 -Open

# Export aggregated telemetry in JSON
.\.specify\extensions\gss-bridge\scripts\powershell\generate-dashboard.ps1 -Json
```

```bash
# Generate dashboard
bash .specify/extensions/gss-bridge/scripts/bash/generate-dashboard.sh

# Generate and immediately open in default browser
bash .specify/extensions/gss-bridge/scripts/bash/generate-dashboard.sh --open

# Export aggregated telemetry in JSON
bash .specify/extensions/gss-bridge/scripts/bash/generate-dashboard.sh --json
```
