# Changelog

All notable changes to **GSS-Bridge** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-11-09

### Added
- **GSS-Teamwork Protocol & Autonomous Multi-Agent Swarm Orchestrator**:
  - Integrated native support for autonomous agent teams (including Google Antigravity `/teamwork` command and multi-agent systems).
  - Implemented 3-tier Role Matrix:
    1. **Lead Orchestrator**: Coordinates wave progression, monitors `.specify/gss-handoff.json`, generates isolated worktrees, and finalizes merge verification.
    2. **Coder Subagents**: Parallel agents executing `[P]` tasks across isolated Git worktrees (`.specify/worktrees/<name>`) with AST-pruned skeletons under strict TDD (Red-Green-Refactor).
    3. **Sentinel Adversarial Reviewer**: Independent verification gate running Test Impact Analysis (`test-impact.ps1`) and Architectural Drift / Circular Dependency audits (`drift-detector.ps1 -FailOnCycles`) before worktree merge.
  - Implemented teamwork planners:
    - `.specify/extensions/gss-bridge/scripts/powershell/teamwork-plan.ps1`
    - `.specify/extensions/gss-bridge/scripts/bash/teamwork-plan.sh`
  - Added command specification `.specify/extensions/gss-bridge/commands/speckit.gss-bridge.teamwork.md` (aliases: `speckit-gss-bridge-teamwork`, `gss-bridge-teamwork`, `gss-teamwork`, `/teamwork`).
  - Added test suites `tests/test-teamwork.ps1` and `tests/test-teamwork.sh`.

## [1.2.3] - 2026-11-05

### Added
- **Automated Subagent Prompt Synthesis in Wave Dispatcher**:
  - Enhanced `dispatch-subagents.ps1` and `dispatch-subagents.sh` with automatic generation of pre-scoped `subagent_prompt` payloads in JSON output.
  - Subagent prompts automatically embed task identification, Graphify 1-2 hop scoped files, strict TDD Red-Green-Refactor discipline, and test evidence reporting requirements.
  - Eliminates prompt composition cognitive load and friction for LLM runtimes, allowing direct mapping into `invoke_subagent`.
- **The Iron Law of Automated Subagent Wave Dispatch**:
  - Codified the Iron Law of Subagent Dispatch across `AGENTS.md` and `.agents/skills/gss-bridge/SKILL.md`.
  - Added anti-rationalization table entries and red flag indicators strictly prohibiting sequential or inline execution of `[P]` tasks in parent agent sessions.
- **Automated Subagent-Driven Execution Protocol**:
  - Updated `commands/speckit.gss-bridge.execute.md` to register `superpowers:subagent-driven-development` with the guard.
  - Standardized Execution Loop Step 5 into automated pre-flight wave partitioning via `dispatch-subagents.ps1 -Json` followed by simultaneous parallel subagent dispatch per wave.

## [1.2.2] - 2026-11-04

### Added
- **Mandatory Graphify Pre-requisite & Auto-Initialization Engine**:
  - Implemented automated knowledge graph initialization across all Graphify-dependent bridge scripts (`graphify-blast-radius`, `bug-assess`, `dispatch-subagents`, `drift-detector`, `test-impact`).
  - Added `Ensure-GraphifyIndex` in `common-actor-resolution.ps1` and `ensure_graphify_index` in `common-actor-resolution.sh`. When `graphify-out/graph.json` is missing, commands automatically trigger `graphify .` (or python module fallback) to initialize and build the knowledge graph before proceeding, eliminating blind or degraded ungrounded triage.
  - Added test suites `tests/test-graphify-auto-init.ps1` and `tests/test-graphify-auto-init.sh` to verify automatic graph construction in fresh/un-indexed workspaces. Total test suites: 15 PowerShell, 14 Bash.
- **Cross-Agent Iron Rule for Graph Pre-requisites**:
  - Updated GSS-Bridge specifications, skills (`.agents/skills/gss-bridge/SKILL.md`, `.claude/skills/gss-bridge/SKILL.md`, global `graphify` skill), and multi-agent guides (`AGENTS.md`, `CLAUDE.md`, `commands/*.md`).
  - Enforced strict prohibitions and rationalization table counters against skipping Graphify analysis or falling back to raw file reading/grep when `graphify-out/graph.json` has not yet been built.


## [1.2.1] - 2026-11-03

### Added
- **Hermetic Graphify Exclusion Boundary (`.graphifyignore`)**:
  - Implemented automatic generation and enforcement of `.graphifyignore` across all GSS-Bridge handoff and bug remediation lifecycles.
  - Strictly excludes AI knowledge, agent directives, and governance directories (`.specify/`, `.agents/`, `.claude/`, `.gemini/`, `.cursor/`, `.windsurf/`, `.kilo/`, `.devin/`, `.codex/`, `.copilot/`, `.ai/`).
  - Strictly excludes Graphify's internal engine intermediate processing states and artifacts (`graphify-out/`, `raw/`, `.graphify*`).
  - Excludes transpiled web worker scripts (`*.worker.js`, `editorWorker*.js`), minified assets (`*.min.js`, `*.min.css`), and build outputs (`dist/`, `build/`, `bin/`, `obj/`, `target/`), preventing spurious God nodes in architecture graphs.
  - Dual-layer protection: persists `excludes` into `graphify-out/.graphify_build.json` to enforce exclusions across both `graphify update` and `graphify watch` runs.
  - Safe automatic graph pruning with `--force` and `GRAPHIFY_FORCE=1` upon handoff completion and bug remediation.
  - Added template at `.specify/templates/.graphifyignore` and bundled `.graphifyignore` into release packages.
- **Automated Verification Suites**:
  - Added `tests/test-graphify-ignore.ps1` and `tests/test-graphify-ignore.sh` verifying automatic scaffolding, pattern completeness, `.graphify_build.json` synchronization, and preservation of custom user ignore rules. Total test suites: 14 PowerShell, 13 Bash.

### Fixed
- **Dashboard Text Mojibake**:
  - Converted raw UTF-8 emoji literals in `generate-dashboard.ps1` and `generate-dashboard.sh` to standard HTML entities, resolving Windows-1252 character corruption on Windows PowerShell hosts.

## [1.2.0] - 2026-11-03

### Added
- **AST Skeleton Context Pruner (`speckit-gss-bridge-prune`)**:
  - Extracts structural class/function signatures, type annotations, and docstrings while pruning implementation bodies.
  - Achieves 70–80% token load reduction for subagent prompts; integrated into `dispatch-subagents` via `-PruneContext` / `--prune-context`.
  - Implemented in `.specify/extensions/gss-bridge/scripts/powershell/ast-skeleton.ps1` and `scripts/bash/ast-skeleton.sh`.
- **Graph-Guided Test Impact Analysis (`speckit-gss-bridge-test-select`)**:
  - Traverses `graphify-out/graph.json` up to 2 hops to pinpoint test suites and files topologically coupled to modified components or git changes.
  - Accelerates Superpowers TDD cycles by running only affected tests during iteration.
  - Implemented in `scripts/powershell/test-impact.ps1` and `scripts/bash/test-impact.sh`.
- **Architectural Drift & Cycle Auditor (`speckit-gss-bridge-drift`)**:
  - Audits knowledge graphs against layer boundary contracts (`.specify/memory/architecture-rules.json`).
  - Employs Tarjan's Strongly Connected Components (SCC) algorithm to detect circular dependency loops.
  - Includes `--fail-on-cycles` for strict CI/CD gatekeeping.
  - Implemented in `scripts/powershell/drift-detector.ps1` and `scripts/bash/drift-detector.sh`.
- **Auto-Synthesizing Bug Repro Harness (`speckit-gss-bridge-repro-gen`)**:
  - Automatically synthesizes isolated, minimal failing reproduction tests (RED phase) during bug assessment (`bug-assess -GenerateRepro`).
  - Generates framework-appropriate tests (`pytest`, `jest`, `pester`, or bash `assert`) into `tests/repro/`.
  - Implemented in `scripts/powershell/synthesize-repro.ps1` and `scripts/bash/synthesize-repro.sh`.
- **Cross-Agent Reasoning & Decision Journal (`speckit-gss-bridge-decision`)**:
  - Maintains persistent chronological log of architectural choices, trade-offs, and hypotheses in `.specify/agent-decisions.jsonl`.
  - Enables seamless cross-agent memory across Claude Code, OpenAI Codex, Antigravity, and Gemini CLI.
  - Implemented in `scripts/powershell/log-decision.ps1` and `scripts/bash/log-decision.sh`.
- **Interactive Visual Cockpit Dashboard (`speckit-gss-bridge-dashboard`)**:
  - Standalone, zero-dependency HTML dashboard at `.specify/dashboard/index.html`.
  - Visualizes Spec-Kit tasks, Graphify top God nodes/degrees, triaged bug risk levels, and decision timelines.
  - Implemented in `scripts/powershell/generate-dashboard.ps1` and `scripts/bash/generate-dashboard.sh`.
- **Git Worktree Wave Isolation (`speckit-gss-bridge-worktree`)**:
  - Manages isolated git worktrees under `.specify/worktrees/` for parallel subagent waves.
  - Prevents file-lock contention and dirty working tree overwrites during concurrent execution.
  - Implemented in `scripts/powershell/wave-worktree.ps1` and `scripts/bash/wave-worktree.sh`.
- **Universal Line Ending Standardization (`/writing-skills` Discipline)**:
  - Enforced CRLF (`\r\n`) across all structural files, manifests, docs, data, and PowerShell scripts (`.md`, `.yml`, `.yaml`, `.json`, `.jsonl`, `.ps1`, `.py`).
  - Enforced strict LF (`\n`) line endings across all POSIX shell scripts (`*.sh`) to guarantee syntax validity on Linux (`ubuntu-latest`) and macOS (`macos-latest`).
  - Configured `.gitattributes` to guarantee `* text=auto eol=crlf` and `*.sh text eol=lf`.
  - Codified the invariant into `.agents/skills/gss-bridge/SKILL.md` and `.claude/skills/gss-bridge/SKILL.md`.
  - Added dual verification gates to `test-release-powershell.ps1` asserting CRLF on structural files and LF (zero CR) on shell scripts.
- **Automated Verification Suites**:
  - Added 7 new test suites in both PowerShell and POSIX Bash covering all new capabilities (total: 13 PowerShell suites, 12 Bash suites).

## [1.1.1] - 2026-11-02

### Changed
- **Command Syntax Standardization**:
  - Standardized all command suggestions and next-step recommendations across PowerShell and Bash scripts from dot notation (`/speckit.gss-bridge.*`) to slash command dash notation (`/speckit-gss-bridge-bug-fix`, `/speckit-gss-bridge-bug-assess`, `/speckit-gss-bridge-dispatch`, `/speckit-gss-bridge-execute`).
  - Added full set of dash-separated aliases (`speckit-gss-bridge-*`) in extension manifests (`extension.yml`) for seamless recognition across Spec-Kit, Claude Code, OpenAI Codex, Antigravity, and Gemini CLI.
  - Enhanced guard rules in `guard-command.ps1` and `guard-command.sh` to recognize both dot and dash formatted actions (`speckit-gss-bridge-bug-fix`, `speckit-bug-fix`, `gss-bridge-bug-fix`).
  - Updated templates (`bug-assessment-template.md`, `plan-template.md`, `bug-fix-template.md`) and documentation (`AGENTS.md`, `README.md`, `README.vi-VN.md`, `SKILL.md`) to reflect canonical dash syntax and correct `graphify update .` synchronization.

## [1.1.0] - 2026-10-30

### Added
- **The GSS-Bug Protocol**:
  - `speckit-gss-bridge-bug-assess` (`speckit.gss-bridge.bug-assess`): Combines Spec-Kit bug tracking with Graphify AST knowledge graph analysis. Parses stack traces, maps affected nodes, traces caller/callee hierarchies, computes blast radius, and embeds visual Mermaid flowcharts into `.specify/bugs/<slug>/assessment.md`.
  - `speckit-gss-bridge-bug-fix` (`speckit.gss-bridge.bug-fix`): Enforces Superpowers `systematic-debugging` and strict Test-Driven Development (TDD) Red $\rightarrow$ Green $\rightarrow$ Refactor workflow. Requires an automated reproduction test before writing fix code, executes test suites to verify fix evidence, automatically synchronizes the codebase knowledge graph via `graphify update .`, and records completion in `.specify/bugs/<slug>/fix.md`.
  - Bug templates: `.specify/templates/bug-assessment-template.md` and `.specify/templates/bug-fix-template.md`.
- **Parallel Subagent Wave Dispatcher (`speckit-gss-bridge-dispatch`)**:
  - Computes pairwise topological distances on `graphify-out/graph.json` between parallel tasks tagged `[P]` in `tasks.md`.
  - Automatically partitions independent tasks ($d \ge 3$) into concurrent execution waves (Wave 1, Wave 2, etc.) while blocking conflicting tasks from running in parallel.
  - Generates subagent dispatch prompts with context strictly bounded to 1-2 hop AST neighborhoods, reducing subagent token overhead by up to 100x.
- **Visual Mermaid Blast Radius Diagrams**:
  - Added `-Mermaid` / `--mermaid` support to `graphify-blast-radius.ps1` and `graphify-blast-radius.sh` to render GitHub-compatible caller/callee architecture diagrams.
- **Automatic Knowledge Graph Synchronization**:
  - Automatically runs `graphify update .` when completing feature implementations (`update-handoff`) or bug fixes (`bug-fix`).
- **Multi-Agent Collision Guard**:
  - Enhanced `guard-command.ps1` and `guard-command.sh` to prevent conflicting concurrent feature implementations across multiple agents and ensure bug fixes are blocked until an approved bug assessment exists.
- **Comprehensive Test Suites**:
  - Added `tests/test-bug-workflow.ps1` and `tests/test-bug-workflow.sh` for full bug assess/fix lifecycle verification.
  - Added `tests/test-dispatch-subagents.ps1` and `tests/test-dispatch-subagents.sh` for wave partitioning and conflict detection verification.

## [1.0.1] - 2026-10-28

### Added
- **Full POSIX Bash Test Suite**: Added 1:1 bash test parity with PowerShell suites (`tests/test-graphify-blast-radius.sh`, `tests/test-guard-rules.sh`, and `tests/run-all.sh`).
- **Zero-Dependency JSON Fallback**: Added AWK parser (`read_handoff_field`) and heredoc template generators in all bash scripts (`bridge-state.sh`, `update-handoff.sh`, `guard-command.sh`, `bridge-status.sh`), allowing GSS-Bridge to run reliably in environments without `jq` (such as Windows Git Bash or minimal Linux/Docker containers).
- **Python Zipfile Fallback in Release Packager**: Enhanced `scripts/release/build-extension-zip.sh` to use Python's built-in `zipfile` module when the native `zip` command is not installed.
- **Robust Windows Python Runtime Detection**: Added multi-candidate detection in `graphify-blast-radius.sh` and `bridge-status.sh` to bypass Windows Store `python3.exe` execution alias traps and resolve functional Python runtimes (`python3`, `python`, `py`).
- **Dynamic Release Validation**: Upgraded package validation scripts to dynamically resolve target versions from manifest declarations.

### Fixed
- Fixed guard evaluation in `guard-command.sh` to ensure `speckit.implement` blocks properly when handoff is in `executing` status without external JSON tooling.
- Fixed `update-handoff.sh` to guarantee `.specify/gss-handoff.json` and dual-write mirrors are written and updated even when `jq` is unavailable.
- Standardized file line endings: strictly LF for `.sh` scripts and CRLF for `.ps1` scripts across the codebase.

## [1.0.0] - 2026-10-28

### Added
- **The Golden Triangle Protocol**: Unified workflow connecting Spec-Kit governance, Graphify codebase context, and Superpowers execution discipline.
- **Spec-Kit Centralized Truth**: Enforced single source of documentation truth in `specs/<feature>/` (`spec.md`, `plan.md`, `tasks.md`). Standalone plan creation in `docs/plans/` is strictly prohibited.
- **Graphify Codebase Intelligence**:
  - Graph-first navigation via `graphify query`, `graphify explain`, and `graphify path` on `graphify-out/graph.json`.
  - Component Blast Radius analysis integrated into `plan.md` via `graphify-blast-radius.ps1` and `.sh`.
  - Coupling verification for parallel tasks `[P]` in `tasks.md` before concurrent subagent dispatch.
  - Subagent context scoping to 1-2 hop neighborhoods, reducing token usage by 50x-100x.
  - Automatic post-implementation graph synchronization (`graphify --update`).
- **Superpowers Engineering Discipline**:
  - Test-Driven Development (TDD) Red-Green-Refactor enforcement.
  - Systematic debugging with call graph path tracing.
  - Verification evidence mandatory before checking off `- [x] T###`.
  - Code review (`superpowers:requesting-code-review`) and branch finishing (`superpowers:finishing-a-development-branch`).
- **Cross-Agent Support**:
  - First-class support for Claude Code (`/gss-bridge`), OpenAI Codex (`$gss-bridge`), Google Antigravity (`/gss-bridge`), and Gemini CLI.
  - Shared handoff contract at `.specify/gss-handoff.json` with backward-compatible mirror at `.specify/superpowers-handoff.json`.
- **Cross-Platform Scripting**:
  - Native Windows PowerShell 5.1+ and Core scripts (`scripts/powershell/`).
  - Native Linux & macOS POSIX Bash scripts (`scripts/bash/`).
- **Tooling & Release Readiness**:
  - Automated CI/CD release workflow (`.github/workflows/release.yml`) with Linux, Windows, and macOS quality gates.
  - Release packaging and validation tooling (`scripts/release/`).
- **Bilingual Documentation**:
  - English documentation in `README.md`.
  - Vietnamese documentation in `README.vi-VN.md` with bidirectional links.
