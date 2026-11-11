---
name: speckit-gss-bridge-worktree
description: Manage isolated git worktrees for concurrent subagent wave execution without git lock conflicts
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: GSS Team
  source: gss-bridge:commands/speckit.gss-bridge.worktree.md
---


# GSS-Bridge Git Worktree Wave Isolation

Isolate parallel subagent wave execution into dedicated Git worktrees under `.specify/worktrees/<name>`, preventing file-lock contention and dirty working trees during concurrent task execution.

## Workflow

1. **Worktree Creation**:
   Spins up an isolated working tree on a dedicated branch (`gss-worktree/<name>`) branched from `HEAD`.
2. **Subagent Execution**:
   Parallel subagents perform implementation, TDD test runs, and verification within their isolated tree.
3. **Clean Merge**:
   After subagents pass verification, cleanly merges the wave branch back into the parent branch.
4. **Safe Cleanup**:
   Removes the worktree and prunes the temporary branch.

## Invocation

```powershell
# List active wave worktrees
.\.specify\extensions\gss-bridge\scripts\powershell\wave-worktree.ps1 -Action list

# Create an isolated worktree for a wave
.\.specify\extensions\gss-bridge\scripts\powershell\wave-worktree.ps1 -Action create -Name "wave-1-auth"

# Merge wave branch upon verification
.\.specify\extensions\gss-bridge\scripts\powershell\wave-worktree.ps1 -Action merge -Name "wave-1-auth"

# Clean up worktree and delete branch
.\.specify\extensions\gss-bridge\scripts\powershell\wave-worktree.ps1 -Action cleanup -Name "wave-1-auth"
```

```bash
# List active wave worktrees
bash .specify/extensions/gss-bridge/scripts/bash/wave-worktree.sh --action list

# Create an isolated worktree for a wave
bash .specify/extensions/gss-bridge/scripts/bash/wave-worktree.sh --action create --name "wave-1-auth"

# Merge wave branch upon verification
bash .specify/extensions/gss-bridge/scripts/bash/wave-worktree.sh --action merge --name "wave-1-auth"

# Clean up worktree and delete branch
bash .specify/extensions/gss-bridge/scripts/bash/wave-worktree.sh --action cleanup --name "wave-1-auth"
```
