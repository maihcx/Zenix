---
description: "Record and query architectural choices, trade-offs, and reasoning in a shared cross-agent journal"
---

# GSS-Bridge Cross-Agent Decision Journal

Maintain a persistent chronological log of architectural choices, hypotheses, and trade-off rationales in `.specify/agent-decisions.jsonl`. Prevents circular refactorings, forgotten alternatives, and knowledge loss across multiple AI agents (Claude Code, OpenAI Codex, Google Antigravity, Gemini CLI).

## Workflow

1. **Structured Recording**:
   Agents log major architectural pivots, framework selections, algorithm trade-offs, or rejected alternatives.
2. **Context Persistence**:
   Appends structured JSON records with unique IDs, ISO timestamps, agent identity, topic tags, and feature references.
3. **Cross-Agent Querying**:
   Any subsequent agent can query past decisions by keyword, feature, actor, or topic before altering code.

## Invocation

```powershell
# Record a new architectural decision
.\.specify\extensions\gss-bridge\scripts\powershell\log-decision.ps1 -Decision "Use AST skeleton pruning" -Rationale "Saves 70-80% token load in subagent prompts" -Alternatives "Pass full raw source files" -Topic "performance" -Actor antigravity

# Query past decisions by topic or keyword
.\.specify\extensions\gss-bridge\scripts\powershell\log-decision.ps1 -Query "skeleton"
```

```bash
# Record a new architectural decision
bash .specify/extensions/gss-bridge/scripts/bash/log-decision.sh --decision "Use AST skeleton pruning" --rationale "Saves 70-80% token load in subagent prompts" --alternatives "Pass full raw source files" --topic "performance" --actor antigravity

# Query past decisions by topic or keyword
bash .specify/extensions/gss-bridge/scripts/bash/log-decision.sh --query "skeleton"
```
