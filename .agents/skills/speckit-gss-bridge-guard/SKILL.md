---
name: speckit-gss-bridge-guard
description: Guard Spec-Kit, Graphify, and Superpowers boundaries
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.guard.md
---


# GSS-Bridge Guard

Enforce architectural boundaries and execution rules across the Golden Triangle (Spec-Kit + Graphify + Superpowers).

## Guard Rules Evaluated

1. **Deny raw `speckit.implement`**:
   Blocked when handoff status is `executing`. Implementation must follow Superpowers TDD discipline and Graphify context scoping via `/speckit-gss-bridge-execute` (or `/gss-bridge`).
2. **Redirect `superpowers:writing-plans`**:
   Denied when an active feature directory exists and attempts to create standalone plan files in `docs/plans/`. Planning must record TDD test matrices directly into `specs/<feature>/plan.md` and `tasks.md`.
3. **Deny `speckit.constitution`**:
   Blocked while handoff status is `executing` to prevent shifting governance rules mid-flight. (Must mark handoff `blocked` first).
4. **Deny Unverified Parallelism**:
   Warns or blocks running multiple subagents on tasks `[P]` if `graphify path` detects coupling or shared mutable state.
5. **Allow Spec-Kit Design Actions**:
   Clarify, specify, plan, and tasks generation actions are always allowed.
6. **Default Allow**:
   All standard non-violating commands are permitted.

## Invocation

```powershell
.\.specify\extensions\gss-bridge\scripts\powershell\guard-command.ps1 -Action <action> -Actor <actor>
```

```bash
bash .specify/extensions/gss-bridge/scripts/bash/guard-command.sh --action <action> --actor <actor>
```
