# Developer & Tester Quickstart Guide: Zalo WinUI Port

**Feature Identifier**: `zalo-winui-port`  
**Milestone**: R2 (speckit-plan)  
**Date**: 2026-11-11  
**Status**: Approved (Production-Ready Architecture)  
**Spec Reference**: [.specify/specs/spec.md](spec.md)  
**Plan Reference**: [.specify/specs/plan.md](plan.md)  

---

## 1. Prerequisites & Environment Setup

Before building, running, or testing the Zenix Zalo component on Windows 10/11, ensure the following developer toolchains and system prerequisites are installed:

### 1.1 Toolchain Requirements
1. **.NET 10 SDK**: .NET 10.0.100 or newer.
   ```powershell
   dotnet --version
   ```
2. **Windows App SDK**: Version 1.6+ (`net10.0-windows10.0.26100.0` workload).
3. **Developer Mode**: Enabled in Windows Settings (`ms-settings:developers`) to allow unpackaged app deployment.
4. **Python 3.10+**: Required for `agent-desktop` desktop bridge automation.
   ```powershell
   python --version
   ```
5. **Git Line Endings**: Ensure Windows CRLF (`\r\n`) line ending discipline:
   ```powershell
   git config core.autocrlf true
   ```

---

## 2. Solution Structure & Build Commands

### 2.1 Solution File Hierarchy
```text
d:\DEV\WinUI\Zenix\
├── Zenix.sln                           # Master Visual Studio solution
├── Zenix\                              # Main WinUI 3 Desktop Application project
│   └── Zenix.csproj                    # Targets net10.0-windows10.0.26100.0
└── tests\
    └── Zenix.Tests\                    # Unit & Integration Test Suite project
        └── Zenix.Tests.csproj          # Targets net10.0-windows10.0.26100.0
```

### 2.2 Building the Application
Restore NuGet dependencies and build the entire solution in Debug configuration:

```powershell
# Navigate to repository root
cd d:\DEV\WinUI\Zenix

# Restore packages
dotnet restore Zenix.sln

# Build solution in Debug mode
dotnet build Zenix.sln -c Debug --no-restore
```

To produce an optimized Release build:
```powershell
dotnet build Zenix.sln -c Release
```

---

## 3. Running Unit & Integration Test Suites

Execute targeted test suites using the `dotnet test` CLI with qualification filters:

### Scenario 1: Cryptographic Primitives & Media De-noising Verification
Validates hardware-accelerated AES-256-CBC, parameter Base64 encryption, trusted device linking HKDF-SHA256 keychain envelopes, constant-time HMAC-SHA256 verification, and XOR 147 media de-noising.
```powershell
dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "FullyQualifiedName~ZaloCryptoTests" --logger "console;verbosity=normal"
```

### Scenario 2: Realtime Gateway Binary Framing
Validates 4-byte header encoding/decoding `[v: 1B][cmd: 2B LE][subCmd: 1B]`, zero-allocation span slicing, and heartbeat framing.
```powershell
dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "FullyQualifiedName~ZaloGatewayFramingTests" --logger "console;verbosity=normal"
```

### Scenario 3: SQLite DAL 4.0 & Schema Migration
Validates database initialization, WAL journal mode (`PRAGMA journal_mode = WAL`), 5000ms busy timeout, constraint tolerance (returning `{ LastInsertedId = 0, RowsAffected = 0 }` on UNIQUE conflicts), and schema migration handshake (`PRAGMA user_version = 400`).
```powershell
dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "FullyQualifiedName~ZaloSqliteDalTests" --logger "console;verbosity=normal"
```

### Scenario 4: Clean MVVM UI & Localization Compliance
Validates that ViewModels contain zero UI references, all views use compiled `{x:Bind}`, and zero hardcoded user-facing string literals exist in XAML.
```powershell
dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "FullyQualifiedName~LocalizationVerificationTests" --logger "console;verbosity=normal"
```

---

## 4. Live Desktop UI Automation via Agent-Desktop

The Zenix project strictly enforces **Principle VIII: Non-Intrusive Agent Desktop Automation & Visual Verification Gate**. In agent terminal environments, interactions with the live desktop must route through the `WinSta0\default` bridge (`bridge.py`) to prevent sandbox desktop enumeration errors.

### 4.1 Launching the Application
Launch the compiled application onto the interactive desktop:
```powershell
python .agents/skills/agent-desktop/scripts/bridge.py launch-app "d:\DEV\WinUI\Zenix\bin\Debug\net10.0-windows10.0.26100.0\Zenix.exe"
```

### 4.2 Inspecting UI State (Accessibility Tree Snapshot)
Read the Windows UI Automation accessibility tree in compact JSON format:
```powershell
python .agents/skills/agent-desktop/scripts/bridge.py snapshot --app Zenix -i --compact
```
*Note*: Look for qualified `@ref` IDs in the output (e.g. `'@s8f3k2p9:e1'`). In PowerShell, always enclose refs in single quotes.

### 4.3 Interacting with Controls via Semantic Ref
Interact with controls without moving the physical mouse cursor:
```powershell
# Click a button or conversation item
python .agents/skills/agent-desktop/scripts/bridge.py click '@s8f3k2p9:e1'

# Type into a message composer text box
python .agents/skills/agent-desktop/scripts/bridge.py set-value '@s8f3k2p9:e5' "Hello from automated test!"
```

### 4.4 Capturing Verification Screenshots
Capture a high-fidelity visual screenshot of the application window for attestation:
```powershell
python .agents/skills/agent-desktop/scripts/bridge.py screenshot --app Zenix "d:\DEV\WinUI\Zenix\artifacts\zenix_chat_verified.png"
```

---

## 5. Developer Workflow & Troubleshooting

### 5.1 Realtime Gateway Reconnection
If the WebSocket channel disconnects unexpectedly:
- Check that session cookie `zpw_sek` is valid and prefixed with domain `.zalo.me`.
- Verify that `ZaloWireFingerprintHandler` is active, injecting `User-Agent: ZaloPC-win32-23v694` and omitting `Origin`/`Referer` on REST endpoints.

### 5.2 SQLite Concurrency Contention (`SQLITE_BUSY`)
- Ensure all repository write methods operate within a `SemaphoreSlim(1,1)` lock.
- Confirm `PRAGMA busy_timeout = 5000;` is executed on connection initialization.

### 5.3 Modifying User-Facing Strings
- Never write raw literals in XAML! Add the string key to:
  - `Zenix/Strings/String.resx` (default)
  - `Zenix/Strings/String.en.resx` (English)
  - `Zenix/Strings/String.vi.resx` (Vietnamese)
- In XAML, bind using:
  `Text="{Binding [YourStringKey], Source={StaticResource Localization}, Mode=OneWay}"`
- Run `LocalizationVerificationTests` to ensure zero missing translation keys.
