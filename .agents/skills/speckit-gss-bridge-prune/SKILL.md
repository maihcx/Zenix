---
name: speckit-gss-bridge-prune
description: Extract AST structural skeletons (classes, functions, signatures) to prune token load for subagents
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.prune.md
---


# GSS-Bridge AST Skeleton Pruner

Extract concise AST structural skeletons from source code files, stripping implementation bodies while retaining imports, class declarations, function signatures, docstrings, and export shapes. Saves 70–80% of token window load in subagent prompts.

## Workflow

1. **Target Selection**:
   Select a target source file or entire directory to prune.
2. **AST Parsing**:
   Uses Python's native AST engine for `.py` and regex heuristic parser for `.js`, `.ts`, `.ps1`, `.sh`.
3. **Skeleton Extraction**:
   Retains module docstrings, class signatures, method definitions, type annotations, and parameters.
4. **Token Savings Telemetry**:
   Outputs the compressed skeleton alongside original vs pruned line/character counts and percentage saved.

## Invocation

```powershell
# Extract skeleton for a single source file
.\.specify\extensions\gss-bridge\scripts\powershell\ast-skeleton.ps1 -Path "src/auth.py"

# Output in JSON format with compression metrics
.\.specify\extensions\gss-bridge\scripts\powershell\ast-skeleton.ps1 -Path "src/auth.py" -Json
```

```bash
# Extract skeleton for a single source file
bash .specify/extensions/gss-bridge/scripts/bash/ast-skeleton.sh --path "src/auth.py"

# Output in JSON format with compression metrics
bash .specify/extensions/gss-bridge/scripts/bash/ast-skeleton.sh --path "src/auth.py" --json
```
