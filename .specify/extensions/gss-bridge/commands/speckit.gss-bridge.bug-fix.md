---
description: "Implement and verify bug remediation under strict Superpowers TDD and verification discipline"
---

# GSS-Bridge Bug Fix

Executes bug remediation based on `.specify/bugs/<slug>/assessment.md` under strict **Superpowers TDD** discipline.

## Workflow

1. **Precondition Check (Guard)**:
   Verifies that `assessment.md` exists and contains identified root causes. Direct unverified patching is blocked.
2. **Active Remediation State**:
   Sets `.specify/gss-handoff.json` to `executing` scoped to `.specify/bugs/<slug>`.
3. **Red-Green-Refactor Cycle**:
   - **RED**: Write a minimal failing test reproducing the reported symptom. Run the test and confirm failure.
   - **GREEN**: Make minimal code modifications strictly within the blast radius boundary. Run test and confirm pass.
   - **REFACTOR**: Clean implementation without changing observable invariants.
4. **Verification & Completion**:
   - Run verification commands and record actual output.
   - Automatically trigger `graphify --update` to synchronize the knowledge graph.
   - Generate `.specify/bugs/<slug>/fix.md`.
   - Transition handoff state to `complete`.

## Invocation

```powershell
# Start active remediation
.\.specify\extensions\gss-bridge\scripts\powershell\bug-fix.ps1 -Slug "login-crash" -Actor <actor>

# Complete and verify bug remediation
.\.specify\extensions\gss-bridge\scripts\powershell\bug-fix.ps1 -Slug "login-crash" -Complete -RegressionTest "tests/test-login-regression.ps1" -VerificationEvidence "PASS: All 24 tests passed" -Actor <actor>
```

```bash
# Start active remediation
bash .specify/extensions/gss-bridge/scripts/bash/bug-fix.sh --slug "login-crash" --actor <actor>

# Complete and verify bug remediation
bash .specify/extensions/gss-bridge/scripts/bash/bug-fix.sh --slug "login-crash" --complete --regression-test "tests/test-login-regression.sh" --verification-evidence "PASS: All 24 tests passed" --actor <actor>
```
