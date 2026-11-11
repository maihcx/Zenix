---
description: "Diagnose and triage bug reports using Graphify knowledge graph context and blast radius analysis"
---

# GSS-Bridge Bug Assessment

Diagnose an incoming bug report, stack trace, or issue using **Graphify** codebase intelligence without modifying production code.

## Workflow

0. **Mandatory Knowledge Graph Pre-requisite**:
   Verifies that `graphify-out/graph.json` exists. If not yet created, automatically executes `graphify .` to initialize and build the knowledge graph first. Never skips or triages blindly without graph context.
1. **Extract Root Cause Candidates**:
   Parses error stack traces, symbols, or exception logs against `graphify-out/graph.json`.
2. **Compute Blast Radius**:
   Traces 1-hop callers, 2-hop transitive impact, and risk classification.
3. **Generate Mermaid Architecture Flow**:
   Visualizes call paths into the suspect component.
4. **Enforce Superpowers TDD Contract**:
   Generates `.specify/bugs/<slug>/assessment.md` mandating that a failing regression test case be written *before* touching source code.

## Invocation

```powershell
# Triage a bug with symptom description
.\.specify\extensions\gss-bridge\scripts\powershell\bug-assess.ps1 -Slug "login-crash" -Symptom "Error: Token expired in AuthService.validateToken called by UserController" -Actor <actor>

# Triage using an existing stack trace file
.\.specify\extensions\gss-bridge\scripts\powershell\bug-assess.ps1 -Slug "db-timeout" -StackFile "logs/error.log" -Actor <actor>
```

```bash
# Triage a bug with symptom description
bash .specify/extensions/gss-bridge/scripts/bash/bug-assess.sh --slug "login-crash" --symptom "Error: Token expired in AuthService.validateToken called by UserController" --actor <actor>

# Triage using an existing stack trace file
bash .specify/extensions/gss-bridge/scripts/bash/bug-assess.sh --slug "db-timeout" --stack-file "logs/error.log" --actor <actor>
```
