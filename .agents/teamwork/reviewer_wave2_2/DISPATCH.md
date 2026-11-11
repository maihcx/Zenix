# Reviewer Dispatch: Cryptography & Signal Protocol Subsystem Review (Wave 2, Tasks T-014..T-018)

## Context
Wave 2 Cryptography and Signal Protocol subsystem has been completed by `worker_wave2_crypto`.
Source files:
- `Zenix/Core/Zalo/Contracts/ISignalE2eeService.cs`
- `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.cs`
- `Zenix/Core/Zalo/Cryptography/ZaloCryptoService.Keychain.cs`
- `Zenix/Core/Zalo/Cryptography/MediaDenoiseHelper.cs`
- `Zenix/Core/Zalo/Cryptography/Curve25519Helper.cs`
- `Zenix/Core/Zalo/Cryptography/SignalX3dhService.cs`
- `Zenix/Core/Zalo/Cryptography/DoubleRatchetSessionState.cs`
- `Zenix/Core/Zalo/Cryptography/SignalDoubleRatchetService.cs`
- `Zenix/Core/Zalo/Cryptography/SafetyNumberFormatter.cs`
- `Zenix/Core/Zalo/Cryptography/SignalE2eeService.cs`
Test files:
- `tests/Zenix.Tests/Zalo/Crypto/*.cs`

## Review Directives
1. Read `ORIGINAL_REQUEST.md`, `PROJECT.md`, and `worker_wave2_crypto/handoff.md`.
2. Inspect source files for:
   - Roslyn `IDE0065` compliance (all `using` directives outside/above namespace).
   - Windows CRLF line endings on all files.
   - 100% XML doc comments in technical American English on all public classes, methods, and properties.
   - Zero UI references (`Microsoft.UI.Xaml.*`, `UIElement`, `Window`).
   - 100% async methods accepting `CancellationToken cancellationToken = default`.
   - Reflection test compliance for `ISignalE2eeService.cs` (starts with 'I', no UI types, async parameters).
   - Constant-time HMAC verification via `CryptographicOperations.FixedTimeEquals`.
   - Zero memory leakage: key zeroing via `CryptographicOperations.ZeroMemory`.
   - Exact parity with test vectors from `zalo-swift-re` for `zcid`, `EncryptApiParams`, Keychain envelope, XOR 147 media de-noising, RFC 7748 Curve25519, X3DH master secret, Double Ratchet, and 60-digit Safety Number.
3. Run verification commands:
   - `dotnet build Zenix.sln -c Debug` -> Must succeed with 0 errors and 0 warnings.
   - `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"` -> All 11 crypto tests must pass.
4. Conclude with a clear verdict in `handoff.md`:
   - `Verdict: APPROVE` or `Verdict: REQUEST_CHANGES` (with concrete reasons).

## Deliverables
Write your review report to:
`d:\DEV\WinUI\Zenix\.agents\teamwork\reviewer_wave2_2\handoff.md`
Notify the orchestrator via `send_message`.


## 2026-11-11T09:08:42Z
Read d:\DEV\WinUI\Zenix\.agents\teamwork\reviewer_wave2_2\DISPATCH.md and execute your code review on the Cryptography & Signal Protocol subsystem (Tasks T-014..T-018).
Working directory: d:\DEV\WinUI\Zenix
State folder: d:\DEV\WinUI\Zenix\.agents\teamwork\reviewer_wave2_2
Original request: d:\DEV\WinUI\Zenix\.agents\teamwork\ORIGINAL_REQUEST.md

Inspect all crypto source and test files for constant-time HMAC, zero secret leakage, ISignalE2eeService reflection invariants, and run verification builds/tests. Deliver your verdict (APPROVE / REQUEST_CHANGES) in handoff.md and notify the orchestrator.
