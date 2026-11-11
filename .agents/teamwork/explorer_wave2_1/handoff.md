# Handoff Report: Codebase Baseline & Project Infrastructure Survey (Wave 2)

**Author**: `explorer_wave2_1`  
**Date**: 2026-11-11  
**Milestone**: Wave 2: Storage & Cryptography Core (Tasks T-008 to T-018, GATE_02)  
**State Folder**: `d:\DEV\WinUI\Zenix\.agents\teamwork\explorer_wave2_1`  

---

## 1. Observation

### 1.1 Solution & Project Infrastructure
- **Solution File**: `d:\DEV\WinUI\Zenix\Zenix.sln`
  - Targets two projects: `Zenix\Zenix.csproj` (GUI App, GUID `{057CBCB9-6C93-48CA-962C-8F4455B71629}`) and `tests\Zenix.Tests\Zenix.Tests.csproj` (Unit Test Suite, GUID `{DEB58FAA-79ED-433A-96AC-A47FDE8D5A54}`).
- **Project File**: `Zenix/Zenix.csproj`
  - Target framework: `net10.0-windows10.0.26100.0`
  - WindowsPackageType: `None` (unpackaged desktop)
  - Platforms: `x64;ARM64`, Default `win-x64`
  - Nullable: `enable`, LangVersion: `preview`
  - Packages:
    ```xml
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.6.5" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.28000.2957" />
    <PackageReference Include="WinUIEx" Version="2.9.3" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.14" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.14" />
    <PackageReference Include="System.IO.Hashing" Version="10.0.13" />
    <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.13" />
    ```
  - InternalsVisibleTo: `Zenix.Tests` (lines 38-40)
- **Test Project File**: `tests/Zenix.Tests/Zenix.Tests.csproj`
  - Target framework: `net10.0-windows10.0.26100.0`
  - Packages: `FluentAssertions` (6.12.2), `Microsoft.NET.Test.Sdk` (17.12.0), `xunit` (2.9.2), `xunit.runner.visualstudio` (2.8.2), `Moq` (4.20.72)
  - ProjectReference: `..\..\Zenix\Zenix.csproj`

### 1.2 Baseline Build and Test Status
- **Build Command**: `dotnet build Zenix.sln -c Debug`
  - Verbatim Output:
    ```text
    Build succeeded.
        0 Warning(s)
        0 Error(s)
    Time Elapsed 00:00:01.27
    ```
- **Test Command**: `dotnet test Zenix.sln`
  - Verbatim Output:
    ```text
    Passed!  - Failed:     0, Passed:    83, Skipped:     0, Total:    83, Duration: 66 ms - Zenix.Tests.dll (net10.0)
    ```
  - Baseline Test Inventory: 83 tests passing cleanly across:
    1. `SmokeTest.cs` (1 test)
    2. `Zalo/LocalizationVerificationTests.cs` (6 tests, Category: Localization)
    3. `Zalo/Contracts/ContractsArchitectureTests.cs` (7 tests, Category: Contracts)
    4. `Zalo/Models/DomainEnumTests.cs` (9 tests, Category: Models)
    5. `Zalo/Models/DtoSerializationTests.cs` (7 tests, Category: Models)
    6. `Zalo/Models/MessagePayloadSerializationTests.cs` (43 tests, Category: Models)
    7. `Zalo/Models/ProfileModelTests.cs` (10 tests, Category: Models)

### 1.3 Existing Contracts and Reflection Invariants
- **Contracts in `Zenix/Core/Zalo/Contracts/`**:
  - `IZaloClient.cs`, `IZaloAuthService.cs`, `IZaloGatewayService.cs`, `IZaloCryptoService.cs`, `IZaloMessageService.cs`, `IZaloMediaService.cs`, `IZaloContactService.cs`, `IZaloStorageService.cs`, `IConversationRepository.cs` (declared in `IZaloStorageService.cs:17-50`), `IMessageRepository.cs` (declared in `IZaloStorageService.cs:55-100`), `IContactRepository.cs` (declared in `IZaloStorageService.cs:105-121`), `IFilePickerService.cs`, `IDialogService.cs`, `IChatViewModel.cs`, `IConversationListViewModel.cs`.
- **Architectural Tests in `tests/Zenix.Tests/Zalo/Contracts/ContractsArchitectureTests.cs`**:
  - Line 50-61: Asserts all required contracts are `public interface` and reside in `Zenix.Core.Zalo.Contracts`.
  - Line 68-77: Asserts all interfaces in `Zenix.Core.Zalo.Contracts` start with `'I'`.
  - Line 84-151: Asserts zero references to visual UI elements (`UIElement`, `FrameworkElement`, `Window`, `ContentDialog`, `Microsoft.UI.Xaml.*`, `Windows.UI.Xaml.*`).
  - Line 158-198: Asserts **100% of asynchronous methods** in `Zenix.Core.Zalo.Contracts` accept a `CancellationToken` parameter with default value `cancellationToken = default`.

### 1.4 Code Formatting and Roslyn Rules
- **Line Endings**: `.gitattributes` line 2: `* text=auto eol=crlf`. All `.cs`, `.xaml`, `.md`, `.json`, `.csproj`, `.sln` files are enforced CRLF (`\r\n`).
- **Roslyn IDE0065**: `.editorconfig` line 124: `csharp_using_directive_placement = outside_namespace:warning`. All `using` directives must strictly reside outside and above the namespace.

### 1.5 Reference Test Vectors Mined from `zalo-swift-re`
- **AES-256-CBC `zcid` Encryption** (`zalo-swift-re/Tests/OlazTests/RuntimeTests.swift:24-30`):
  - Fixed Key: `"3FC4F0D2AB50057BCE0D90D9187A22B1"` (UTF-8 bytes as 32-byte key)
  - IV: 16 zero bytes (`0x00` * 16)
  - Input: `"23,abc-imei,1792963077777"`
  - Ciphertext (lowercase hex): `"a1a69db6a344da4d883865c637f979495dd86112a4534347b462259bfdeb641b"`
- **AES-256-CBC API Params Encryption** (`RuntimeTests.swift:32-43`):
  - Fixed Key: `"DE71B7D4C57C0749158CB90D5E431AE3"`
  - IV: 16 zero bytes
  - Input: `"{\"language\":\"vi\"}"`
  - Ciphertext (Base64): `"V5p1S6tTwaE91W8UDCagn9vVP9wUQ3QzA04wG7xh+5E="`
- **Device Linking Keychain Envelope** (`tasks.md:441`):
  - Sealed format: `[version: 0x49 (1B)][iv: 16B][salt: 16B][ciphertext: NB][hmac: 32B]` (minimum length: 65 bytes)
  - HKDF derivation: `HKDF-SHA256(rootSecret, salt, info: "ZaloDeviceLink", length: 64)` -> 32B AES key + 32B HMAC key
  - Constant-time verification: `CryptographicOperations.FixedTimeEquals(computedHmac, envelopeHmac)`
- **Media De-noising (XOR 147)** (`tasks.md:441`, `IZaloCryptoService.cs:53-58`):
  - Prefix signature: `077a416c4f07` (6 bytes: `0x07, 0x7a, 0x41, 0x6c, 0x4f, 0x07`)
  - Filter: byte-wise XOR `147` (`0x93`) to restore clean media buffer.
- **Double Ratchet Info String** (`zalo-swift-re/Sources/Olaz/JSModules/vendor/libsignal-protocol.js`):
  - HKDF info parameter: `"ZaloRatchet"`
- **60-Digit Safety Number** (`zalo-swift-re/Sources/Olaz/JSModules/zalo-e2ee.js:21-22, 219-224`):
  - Iterations: `5200`
  - Hash primitive: `SHA-512`
  - Formatting: 12 groups of 5 decimal digits separated by single spaces (e.g., `XXXXX XXXXX XXXXX ...`), symmetric between Alice and Bob.
- **SQLite Concurrency & UNIQUE Handling** (`zalo-swift-re/Tests/OlazTests/SQLiteHostTests.swift:18-35`):
  - UNIQUE constraint failure in `run` returns `{ lastID = 0, changes = 0 }` without throwing fatal exceptions.

---

## 2. Logic Chain

1. **Dependency Sufficiency**:
   - `Zenix.csproj` references `Microsoft.Data.Sqlite` (10.0.14) and `System.Security.Cryptography.ProtectedData` (10.0.13).
   - .NET 10 BCL natively provides hardware-accelerated `System.Security.Cryptography.Aes`, `AesGcm`, `HKDF`, `HMACSHA256`, `SHA256`, `SHA512`, and `CryptographicOperations.FixedTimeEquals`.
   - Therefore, no additional NuGet packages or external libraries are required to implement Wave 2 tasks (T-008 through T-018).

2. **Model and Contract Alignment**:
   - Wave 1 established `UserProfile`, `UserContact`, `GroupProfile`, `GroupMember`, `ConversationSummary`, and `ChatMessage` with all 14 payload types in `Zenix/Core/Zalo/Models/`.
   - `IZaloStorageService` in `Zenix/Core/Zalo/Contracts/IZaloStorageService.cs` already defines the sub-interfaces `IConversationRepository`, `IMessageRepository`, and `IContactRepository`.
   - `IZaloCryptoService` in `Zenix/Core/Zalo/Contracts/IZaloCryptoService.cs` already defines `EncryptZcid`, `EncryptApiParams`, `DecryptKeychainData`, `EncryptKeychainData`, `DenoiseMedia`, and `ComputeSafetyNumber`.
   - Task T-018 calls for `SignalE2eeService` implementing `ISignalE2eeService`. An interface `ISignalE2eeService.cs` must be added to `Zenix/Core/Zalo/Contracts/` adhering strictly to `ContractsArchitectureTests` invariants (name starting with `'I'`, no UI types, all async methods with `CancellationToken cancellationToken = default`).

3. **Regression Safety Baseline**:
   - Solution compiles cleanly with 0 errors and 0 warnings.
   - All 83 existing unit tests pass in 66 ms.
   - Any implementation of Wave 2 must maintain 0 warnings and 83/83 existing test pass rate alongside new tests with `[Trait("Category", "Storage")]` and `[Trait("Category", "Crypto")]`.

4. **Target Layout Formulation**:
   - Storage implementations should reside in:
     - `Zenix/Core/Zalo/Storage/ZaloSqliteDalService.cs`
     - `Zenix/Core/Zalo/Storage/SqliteExecutionResult.cs`
     - `Zenix/Core/Zalo/Storage/ZaloSqliteMigrator.cs`
     - `Zenix/Core/Zalo/Storage/Scripts/Schema_v400.sql`
     - `Zenix/Core/Zalo/Storage/Repositories/ConversationRepository.cs`
     - `Zenix/Core/Zalo/Storage/Repositories/MessageRepository.cs`
     - `Zenix/Core/Zalo/Storage/Repositories/FailedDraftStore.cs`
     - `Zenix/Core/Zalo/Storage/Repositories/ContactRepository.cs`
     - `Zenix/Core/Zalo/Storage/Repositories/GroupRepository.cs`
     - `Zenix/Core/Zalo/Storage/ZaloCredentialStorage.cs`
     - `Zenix/Core/Zalo/Storage/ZaloStorageService.cs`
   - Cryptography implementations should reside in:
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
   - Test suites should reside in:
     - `tests/Zenix.Tests/Zalo/Storage/*.cs` with `[Trait("Category", "Storage")]`
     - `tests/Zenix.Tests/Zalo/Crypto/*.cs` with `[Trait("Category", "Crypto")]`

---

## 3. Caveats

- **No Caveats**: The codebase state is completely pristine, all dependencies are in place, the solution compiles with 0 errors/warnings, and 83/83 existing unit tests pass cleanly.

---

## 4. Conclusion

1. **Codebase Baseline**: Healthy and fully operational. .NET 10 WinUI 3 toolchain is functional and fast (66 ms test run, 1.27 s build).
2. **Dependencies**: 100% sufficient. No package additions needed for Wave 2.
3. **Contracts & Architecture**: Pre-established in Wave 1. Implementers must ensure `ISignalE2eeService.cs` complies with `ContractsArchitectureTests` reflection rules.
4. **Test Vectors**: Fully mined and documented from `zalo-swift-re` for immediate test-driven development (TDD) by coder agents.

---

## 5. Verification Method

To independently verify the baseline findings:
1. Run `dotnet build Zenix.sln -c Debug` -> Confirm `0 Warning(s)`, `0 Error(s)`.
2. Run `dotnet test Zenix.sln` -> Confirm `Passed: 83, Failed: 0, Total: 83`.
3. Inspect `Zenix/Zenix.csproj` -> Confirm packages `Microsoft.Data.Sqlite` (10.0.14) and `System.Security.Cryptography.ProtectedData` (10.0.13).
4. Run `git status` -> Confirm working tree is on branch `master` with no conflicting changes in `Zenix/` or `tests/`.
