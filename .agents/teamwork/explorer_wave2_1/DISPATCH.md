# Task Assignment: Codebase Baseline & Project Infrastructure Survey (Wave 2)

## Context
We are beginning Wave 2: Storage & Cryptography Core (Tasks T-008 to T-018, heading towards GATE_02). Wave 1 established foundation, domain models, contracts, and localization.

## Objective
Investigate the existing codebase state and verify baseline health:
1. Examine `Zenix.sln`, `Zenix/Zenix.csproj`, `tests/Zenix.Tests/Zenix.Tests.csproj`.
2. Check existing packages (e.g. `Microsoft.Data.Sqlite`, `CommunityToolkit.Mvvm`, `System.Security.Cryptography`, etc.) to see if all necessary dependencies for SQLite DAL and Cryptography are already referenced or if anything is missing.
3. Check existing models in `Zenix/Core/Zalo/Models/` and contracts in `Zenix/Core/Zalo/Contracts/` (especially `IZaloStorageService`, `IZaloCryptoService`, `ISignalE2eeService`).
4. Run `dotnet test Zenix.sln` to record the exact passing baseline test count (expected: 83/83 passing).
5. Identify target file paths and namespace conventions for Storage (T-008..T-013) and Cryptography (T-014..T-018).
6. Note any build or analyzer warnings, Roslyn rules (`IDE0065`), and Git CRLF line ending constraints.

## Inputs
- `ORIGINAL_REQUEST.md`: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`
- `PROJECT.md`: `d:\DEV\WinUI\Zenix\.agents\teamwork\orchestrator\PROJECT.md`
- Solution files: `Zenix.sln`, `Zenix/Zenix.csproj`, `tests/Zenix.Tests/Zenix.Tests.csproj`

## Deliverables
Write your comprehensive report and handoff to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_1\handoff.md`
Update your `progress.md` with liveness timestamps.
Once finished, send a brief message to the orchestrator: "I'm done, check my report".
