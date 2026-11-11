---
name: speckit-gss-bridge-repro-gen
description: Auto-synthesize minimal failing reproduction test cases (RED) during bug triage
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.repro-gen.md
---


# GSS-Bridge Auto-Synthesizing Bug Repro Harness

Automatically synthesizes an isolated, minimal reproducing test template during bug assessment, guaranteeing the mandatory first RED phase of Superpowers Test-Driven Development (TDD).

## Workflow

1. **Stack & Symptom Extraction**:
   Captures failing symbols, expected vs actual behavior, and context from bug descriptions or stack traces.
2. **Framework Detection**:
   Detects repo test harness (Python `pytest` / `unittest`, Node `jest` / `vitest`, PowerShell `Pester`, or Bash `assert`).
3. **Scaffold Generation**:
   Generates a standalone failing test in `tests/repro/test_<slug>__repro.<ext>`.
4. **Assessment Embedding**:
   Injects the verification command into the bug assessment checklist: `- [ ] **Auto-Synthesized Repro**: Run ... to verify initial failure (RED).`

## Invocation

```powershell
# Synthesize repro harness for a bug
.\.specify\extensions\gss-bridge\scripts\powershell\synthesize-repro.ps1 -Slug "cache-overflow" -SuspectNode "CacheManager" -Symptom "Key evicted prematurely" -Json
```

```bash
# Synthesize repro harness for a bug
bash .specify/extensions/gss-bridge/scripts/bash/synthesize-repro.sh --slug "cache-overflow" --suspect-node "CacheManager" --symptom "Key evicted prematurely" --json
```
