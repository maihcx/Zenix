# Dispatch: Code Quality Reviewer 1 (Wave 1: GATE_01)

**Role**: Code Quality Reviewer
**Task**: Independently review Wave 1 code quality, architecture compliance, and build/test integrity.
**Working Directory**: `d:\DEV\WinUI\Zenix`
**State Folder**: `d:\DEV\WinUI\Zenix\.agents\teamwork\reviewer_wave1_1\`
**Scope Document**: `d:\DEV\WinUI\Zenix\.agents\teamwork\sub_orch_wave1\SCOPE.md`
**Original Request**: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`

## Review Focus
1. Build and test verification:
   - `dotnet build Zenix.sln -c Debug` (Must pass with 0 warnings, 0 errors).
   - `dotnet test Zenix.sln --filter "Category=Models | Category=Contracts"` (Must pass 100%).
2. Architectural Compliance (Constitution v1.19.0):
   - Zero UI types in `Zenix/Core/Zalo/Models/` and `Zenix/Core/Zalo/Contracts/`.
   - 100% of async methods accept `CancellationToken cancellationToken = default`.
   - Roslyn rule `IDE0065` (all usings outside and above namespace).
   - 100% English XML doc comments.
   - Pure Windows CRLF line endings on all repository files.
   - UTF-8 without BOM on `.resx` files and C# source files.
3. Verdict: Provide a clear verdict (`APPROVE` or `REQUEST_CHANGES`) in `handoff.md`.
4. Report back via `send_message`.
