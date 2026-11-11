# Dispatch: Contracts Worker (Wave 1: T-006 & GATE_01 Tests)

**Role**: Contracts Worker
**Task**: Implement Task T-006 and complete GATE_01 unit tests for Wave 1.
**Working Directory**: `d:\DEV\WinUI\Zenix`
**State Folder**: `d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave1_contracts\`
**Scope Document**: `d:\DEV\WinUI\Zenix\.agents\teamwork\sub_orch_wave1\SCOPE.md`
**Original Request**: `d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md`

## Invariants & Warnings
> DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

- Windows CRLF (`\r\n`) line endings on ALL created files.
- Roslyn rule `IDE0065` clean: All `using` directives outside and above `namespace`.
- File-scoped namespaces (`IDE0161`): `namespace Zenix.Core.Zalo.Contracts;`.
- 100% XML doc comments in technical American English.
- IMPORTANT ENCODING INVARIANT: Target files in `Zenix/Core/Zalo/Contracts/` must be written in **UTF-8 without BOM**. (Note: the reference files in `.specify/specs/contracts/` have UTF-8 BOM, you must strip the BOM when writing to `Zenix/Core/Zalo/Contracts/`).
- Zero references to UI visual types (`Microsoft.UI.Xaml.*`, `UIElement`, `Window`, `ContentDialog`).
- 100% async methods must accept `CancellationToken cancellationToken = default`.
- Do NOT use `open(f, 'wb').write(open(f, 'rb').read())` in Python because opening 'wb' truncates files to 0 bytes! Read into memory first if using Python scripts.

## File Ownership (Exclusively owned by this worker)
1. `Zenix/Core/Zalo/Contracts/`:
   - `IZaloClient.cs`
   - `IZaloAuthService.cs`
   - `IZaloGatewayService.cs`
   - `IZaloCryptoService.cs`
   - `IZaloMessageService.cs`
   - `IZaloMediaService.cs`
   - `IZaloContactService.cs`
   - `IZaloStorageService.cs`
   - `IFilePickerService.cs`
   - `IDialogService.cs`
   - `IChatViewModel.cs`
   - `IConversationListViewModel.cs`
2. `tests/Zenix.Tests/Zalo/Contracts/`:
   - `ContractsArchitectureTests.cs` (decorated with `[Trait("Category", "Contracts")]`)

## Reference Inputs
- Existing contract files in `.specify/specs/contracts/`
- `.specify/specs/contracts/service-interfaces.md`
- `d:\DEV\WinUI\Zenix\.agents\teamwork\spec_miner_wave1_3\analysis.md`
- Implemented models in `Zenix/Core/Zalo/Models/` (Profiles, Messages, Payloads, Dto, Enums)

## Verification
- Run: `dotnet build Zenix.sln -c Debug`
- Run: `dotnet test Zenix.sln --filter "Category=Models|Category=Contracts"`
- Confirm 0 errors, 0 warnings, all tests pass.
- Write `changes.md` and `handoff.md` in your state folder.
- Report completion via `send_message`.

## 2026-11-11T07:17:51Z
You are the Contracts Worker for Wave 1.
Read your dispatch instructions in d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave1_contracts\DISPATCH.md
Read the scope document in d:\DEV\WinUI\Zenix\.agents\teamwork\sub_orch_wave1\SCOPE.md
Read the original request in d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md
Your state folder is d:\DEV\WinUI\Zenix\.agents\teamwork\worker_wave1_contracts\

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Implement Task T-006 and GATE_01 test suite:
1. Copy and adapt the 12 C# interface contracts from .specify/specs/contracts/ into Zenix/Core/Zalo/Contracts/:
   - IZaloClient.cs, IZaloAuthService.cs, IZaloGatewayService.cs, IZaloCryptoService.cs, IZaloMessageService.cs, IZaloMediaService.cs, IZaloContactService.cs, IZaloStorageService.cs, IFilePickerService.cs, IDialogService.cs, IChatViewModel.cs, IConversationListViewModel.cs
   - Ensure target files are saved in UTF-8 without BOM (strip any incoming BOM) and Windows CRLF.
   - Ensure using statements are placed outside namespace (IDE0065).
   - Ensure zero UI types (UIElement, Window, ContentDialog).
   - Ensure all async methods accept CancellationToken cancellationToken = default.
   - Ensure imports map cleanly to Zenix.Core.Zalo.Models.*.
2. Implement ContractsArchitectureTests.cs in tests/Zenix.Tests/Zalo/Contracts/ decorated with [Trait("Category", "Contracts")] verifying reflection-based architecture rules (zero UI types in Contracts assembly/namespace, 100% async methods have CancellationToken).
3. Verify build (`dotnet build Zenix.sln -c Debug`) and tests (`dotnet test Zenix.sln --filter "Category=Models|Category=Contracts"`).
4. Update AST knowledge graph if needed (`graphify update .`).
5. Write changes.md and handoff.md in your state folder.
6. Report completion back to me via send_message.

