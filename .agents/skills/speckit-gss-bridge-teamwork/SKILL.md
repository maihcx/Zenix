---
name: speckit-gss-bridge-teamwork
description: Orchestrate autonomous multi-agent teams (Orchestrator, Parallel Coders in Worktrees, and Sentinel Reviewer) using GSS-Bridge
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.teamwork.md
---

# GSS-Bridge Autonomous Teamwork Protocol

Integrates native multi-agent team capabilities (such as Google Antigravity `/teamwork` and autonomous agent swarms) with the GSS-Bridge Trinity Protocol: **Spec-Kit** requirements governance, **Graphify** topological independence, and **Superpowers** execution discipline.

## Architecture & Role Matrix

```
                      ┌──────────────────────────────────────┐
                      │        Lead Orchestrator             │
                      │  Coordinates waves & gss-handoff     │
                      └──────────────────┬───────────────────┘
                                         │
                 ┌───────────────────────┴───────────────────────┐
                 ▼                                               ▼
  ┌──────────────────────────────┐                ┌──────────────────────────────┐
  │   Parallel Coder Subagents   │                │  Sentinel Adversarial Gate   │
  │  Isolated wave-worktrees     │───────────────►│  Test Impact Analysis (TIA)  │
  │  Strict TDD (Red -> Green)   │   PR Package   │  drift-detector & cycle gate │
  └──────────────────────────────┘                └──────────────────────────────┘
```

1. **Lead Orchestrator**: Maintains global contract truth from `spec.md` and `plan.md`, tracks lifecycle state in `.specify/gss-handoff.json`, generates isolated git worktrees, and dispatches parallel subagent waves.
2. **Coder Subagents**: Execute parallel tasks `[P]` in dedicated worktrees (`.specify/worktrees/<name>`), strictly scoped to 1-2 hop AST skeletons from Graphify, under strict TDD Red-Green-Refactor.
3. **Sentinel Adversarial Reviewer**: Independent verification gate that validates each wave before merging. Runs `test-impact.ps1` to detect regressions and `drift-detector.ps1` to prevent circular dependencies and layer boundary violations.

## Invocation

```powershell
# Generate multi-agent teamwork plan with JSON output
.\.specify\extensions\gss-bridge\scripts\powershell\teamwork-plan.ps1 -Json

# Generate human-readable multi-agent roster and verification commands
.\.specify\extensions\gss-bridge\scripts\powershell\teamwork-plan.ps1
```

```bash
# Generate multi-agent teamwork plan with JSON output
bash .specify/extensions/gss-bridge/scripts/bash/teamwork-plan.sh --json

# Generate human-readable multi-agent roster and verification commands
bash .specify/extensions/gss-bridge/scripts/bash/teamwork-plan.sh
```

### Agent Team Commands
- **Google Antigravity**: `/teamwork` or `/gss-bridge-teamwork`
- **Claude Code**: `/gss-bridge-teamwork` or `/speckit-gss-bridge-teamwork`
- **OpenAI Codex**: `$gss-bridge-teamwork` or `$speckit-gss-bridge-teamwork`
