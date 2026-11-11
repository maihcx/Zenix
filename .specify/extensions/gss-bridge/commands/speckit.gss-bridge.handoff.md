---
description: "Create or update the GSS-Bridge implementation handoff state"
---

# GSS-Bridge Handoff

Create, transition, and inspect the handoff contract (`.specify/gss-handoff.json` and compatible `.specify/superpowers-handoff.json`).

## Behavior

- Automatically invoked by the `after_tasks` hook when Spec-Kit generates `tasks.md`.
- Verifies that `spec.md`, `plan.md`, and `tasks.md` exist in the active feature directory.
- Snapshots feature artifacts to `.specify/bridge-snapshots/` on each transition.
- Appends audit events to `.specify/bridge-events.jsonl`.
- Calculates artifact SHA256 hashes (`artifacts_sha256`) to detect drift.

## Invocation

```powershell
.\.specify\extensions\gss-bridge\scripts\powershell\update-handoff.ps1 -Status <ready|executing|blocked|complete> -FeatureDirectory <path> -Actor <actor>
```

```bash
bash .specify/extensions/gss-bridge/scripts/bash/update-handoff.sh --status <ready|executing|blocked|complete> --feature-directory <path> --actor <actor>
```
