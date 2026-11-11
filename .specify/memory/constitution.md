<!--
Sync Impact Report:
- Version change: 1.18.0 → 1.19.0 (Minor version bump: Add Principle X: Clean Engineering, Root-Cause Resolution & Anti-Patchwork Discipline and Anti-Patchwork & Root-Cause Quality Gate, outlawing superficial duct-tape fixes, silent exception swallowing, and technical debt accumulation)
- Added Principles:
  - Principle X: Clean Engineering, Root-Cause Resolution & Anti-Patchwork Discipline (Zero-Patchwork Invariant):
    - Mandates root-cause architectural resolution over symptomatic treatments, band-aids, and quick hacks.
    - Outlaws empty catch blocks, indiscriminate null suppression, ad-hoc global/static state, magic numbers/coordinates, and artificial thread sleeps to mask race conditions.
    - Bans patchwork anti-patterns: duct-tape hacks, logic duplication, bypassing DI containers, and leaky architectural boundaries.
    - Enforces The Boy Scout Rule: every touched file must be left cleaner, dead-code-free, fully typed, documented with English XML comments, Roslyn IDE0065 compliant, and formatted with pure CRLF line endings.
- Expanded Gates:
  - Anti-Patchwork & Root-Cause Quality Gate: Requires explicit audit confirming root-cause resolution, zero duct-tape patterns, zero logic duplication, and adherence to clean engineering before handoff.
- Removed sections: None.
- Follow-up TODOs: None.
-->

# Zenix Constitution

## Core Principles

### Principle I: Clean MVVM & Type Safety
The application MUST strictly adhere to the Clean Model-View-ViewModel (MVVM) architectural pattern using `CommunityToolkit.Mvvm`:

- **Strict View / ViewModel Decoupling & Pure ViewModels**: ViewModels MUST be completely UI-agnostic and MUST NOT import or reference UI visual types (such as `Microsoft.UI.Xaml.*`, `FrameworkElement`, `Window`, `Page`, `ContentDialog`, or `ContentPresenter`). ViewModels communicate state via data binding, properties, commands, and decoupled messaging (`WeakReferenceMessenger`).

- **Declarative Source Generators**: ViewModels MUST utilize `[ObservableProperty]`, `[RelayCommand]`, and `[NotifyPropertyChangedFor]` source generators on partial properties to maintain compile-time safety and eliminate boilerplate.

- **Zero Business Logic in Code-Behind**: Views (`*.xaml.cs`) MUST NOT contain business, database, or domain orchestration logic. Code-behind is strictly restricted to view-only lifecycle plumbing (such as setting titlebar drag regions or hooking native non-bindable events).

- **Declarative XAML Data Binding & Zero Code-Behind UI Manipulation**: Views (`*.xaml.cs`) MUST NOT perform imperative visual tree manipulation or container child management (such as manually adding, removing, or swapping elements via `Children.Add`, `Children.Remove`, swapping content hosts via `InlineHost.Content = null; FlyoutHost.Content = Panel;`, or imperatively mutating child `Visibility` in code-behind). All dynamic view presentation, child view swapping, and viewport navigation MUST be handled declaratively in XAML via data binding (e.g., `<ContentControl Content="{x:Bind ViewModel.ActiveContent}" />`, `<ContentPresenter />`, or `<ItemsControl />`). Code-behind is strictly restricted to non-bindable platform interop (e.g., title bar drag regions, Win32 window handles, or pure routed event bubbling).

- **Standardized Compiled Binding (`{x:Bind}`) Invariant**: All XAML views and controls MUST utilize WinUI 3 compiled bindings (`{x:Bind}`) for all ViewModel data, properties, commands (`Command="{x:Bind ViewModel.MyCommand}"`), and typed event bindings, ensuring compile-time type safety and high runtime performance. Traditional reflection-based `{Binding}` is restricted exclusively to indexer-based dynamic localization (`{Binding [Key], Source={StaticResource Localization}}`).

- **Dependency Injection for ViewModels & Zero Direct `new` Instantiation**: Views, pages, and dialogs MUST resolve their ViewModels via the Dependency Injection container (`Microsoft.Extensions.Hosting` `IServiceProvider`, constructor injection, or registered ViewModel factory delegates). Directly instantiating ViewModels with `new MyViewModel(...)` in code-behind or views is STRICTLY FORBIDDEN to ensure full testability, consistent lifecycle management, and mockability.

- **Abstracted OS Interop & File Dialog Services**: Views and code-behind MUST NOT directly instantiate or invoke native Windows OS dialogs (such as `FileOpenPicker`, `FileSavePicker`, or folder pickers). All OS pickers and system dialogs MUST be abstracted behind injectable services (e.g., `IFilePickerService`, `IDialogService`) and invoked asynchronously via ViewModel commands.

- **Declarative Context Menus & Flyouts**: Context menus, item flyouts, and dropdown menus MUST be declared declaratively in XAML resource dictionaries or control templates, bound to ViewModel commands via `{x:Bind}`. Imperative instantiation of `MenuFlyout`, `MenuFlyoutItem`, or `MenuFlyoutSeparator` in C# code-behind or static visual helpers is STRICTLY FORBIDDEN.

- **Declarative Hierarchical Data Binding for TreeViews**: Hierarchical tree views (`TreeView`) MUST be populated declaratively via `ItemsSource="{x:Bind ViewModel.RootNodes}"` using typed data models and `ItemTemplate` hierarchical templates. Imperatively clearing, adding, or traversing `TreeViewNode` instances in code-behind or static builders is STRICTLY PROHIBITED.

- **MVVM Compliance Verification**: Every code modification, architectural refactoring, and defect remediation MUST be evaluated against standard Clean MVVM architecture. Attempting to resolve layout, performance, or navigation issues via imperative DOM-style visual tree manipulation in code-behind is STRICTLY FORBIDDEN.

- **UI Thread Dispatching**: Any background thread seeking to update UI-bound state MUST marshal operations through the UI thread dispatcher (`DispatcherQueue`).

- **Source Code Layout & Using Directives Order (`IDE0065`)**: In all C# source files, all `using` directives MUST be declared at the top of the file (immediately following the file header) and placed strictly OUTSIDE and ABOVE the `namespace` declaration. Placing `using` directives below or inside a `namespace` declaration is STRICTLY FORBIDDEN to eliminate style drift and guarantee compliance with `.editorconfig` rules.

- **Universal Line Endings Discipline (GSS-Bridge Standard)**: Across the entire repository, all C# source files (`*.cs`), XAML markup (`*.xaml`), markdown documentation (`*.md`), PowerShell scripts (`*.ps1`), JSON schemas/configs (`*.json`, `*.jsonl`), YAML files (`*.yml`, `*.yaml`), and resource bundles (`*.resx`) MUST strictly use Windows CRLF (`\r\n`) line endings. All POSIX shell scripts (`*.sh`) MUST strictly use LF (`\n`) line endings to preserve runtime validity across Linux/macOS environments. Mixed line endings within any single file are STRICTLY FORBIDDEN.

### Principle II: Modern Fluent UI / UX Standards
The graphical user interface MUST deliver a Tier-1 Windows desktop experience built on Windows App SDK (WinUI 3) Fluent Design 2:

- **Theme & Backdrop Synchronization**: The application MUST support full, glitch-free Light and Dark themes, seamlessly synchronized with Windows system settings, utilizing Mica/Acrylic system backdrops.

- **Modern Controls & Chrome Tab Strip**: All views MUST employ modern WinUI 3 controls, Segoe UI Variable fonts, standard spacing tokens, and native browser-style Chrome tabs (`TabView`) embedded into the window titlebar.

- **Asynchronous Modal Dialogs**: Blocking Win32 message boxes (`MessageBox.Show`) are STRICTLY FORBIDDEN. All prompts, confirmations, and alerts MUST use asynchronous `ContentDialog` or InfoBar notifications.

### Principle III: Secure Database Connection & Credential Protection
Protection of user data, database credentials, and server configurations is a non-negotiable security imperative:

- **Encrypted Credential Storage**: Database connection strings, passwords, and sensitive authentication tokens MUST NEVER be stored in plain text. They MUST be encrypted at rest using `PasswordEncryptor` (AES-GCM / Windows DPAPI).

- **Zero Plain-Text Leaks**: Passwords and secrets MUST NOT appear in plaintext in log files, crash dumps (`crash.log`), debug outputs, or error dialogs.

- **SQL Injection Prevention**: All queries and database interactions via `MySqlConnector` MUST use strictly parameterized queries or prepared statements. Dynamic string concatenation of SQL statements with raw user input is STRICTLY PROHIBITED.

### Principle IV: High Performance & Thread Stewardship
The UI MUST maintain fluid 60 FPS responsiveness (<16ms per frame) under all operational workloads:

- **Asynchronous Non-Blocking I/O**: Every database connection handshake, ping, schema discovery query, and dataset retrieval MUST execute asynchronously off the UI thread (`async`/`await`, `Task.Run`).

- **Cancellation Stewardship**: All long-running asynchronous tasks and database operations MUST accept and actively monitor a `CancellationToken` to allow instant user aborts without dangling locks.

- **Deterministic Resource Management**: Database connections, readers, commands, and transactions MUST be deterministically disposed via `await using` or `using` blocks to prevent socket exhaustion and connection leaks.

- **Memory Protection**: Query results and data records MUST use paging or virtualized collections to avoid large object heap (LOH) fragmentation and memory spikes.

### Principle V: English Technical Documentation Standard
To ensure long-term maintainability and alignment with automated analysis tools:

- **100% Documentation Coverage**: All public and internal classes, interfaces, records, structs, methods, properties, and RelayCommands MUST be documented with standard XML doc comments (`/// <summary>`, `<param>`, `<returns>`, `<exception>`).

- **Standard Technical US English**: All code documentation, code comments, and commit messages MUST be written exclusively in standard technical American English. Non-English doc comments, colloquial language, or empty boilerplate summaries are prohibited.

### Principle VI: Spec-Driven Development & Superpowers Discipline (GSS-Bridge v1.2.0)
Every feature, architectural refactoring, and defect resolution MUST follow the Golden Triangle methodology combining **Spec-Kit** (design authority), **Graphify** (codebase context & topology), and **Superpowers** (engineering execution) via **`gss-bridge`** (`v1.2.0`):

- **Spec-Kit Central Authority**: Requirements (`spec.md`), architectural plans (`plan.md`), and task breakdowns (`tasks.md`) MUST be established before writing code. Ad-hoc development outside Spec-Kit contracts is prohibited.

- **GSS-Bridge Governance & Handoff**: Workflow transitions MUST proceed through `gss-bridge`, which automatically guards contracts (`speckit-gss-bridge-guard`), manages handoff states (`speckit-gss-bridge-handoff`), records cross-agent architectural decisions in `.specify/agent-decisions.jsonl` (`speckit-gss-bridge-decision`), and provides interactive visual telemetry via `.specify/dashboard/index.html` (`speckit-gss-bridge-dashboard`).

- **Graph-Guided Execution & TIA**: Component blast radius and parallel task coupling MUST be verified via `speckit-gss-bridge-blast-radius`. Test selection for TDD cycles MUST leverage Test Impact Analysis (`speckit-gss-bridge-test-select`) to run only topologically coupled suites.

- **Subagent Wave Isolation & Pruning**: Multi-subagent execution waves MUST be partitioned via `speckit-gss-bridge-dispatch`. Concurrently dispatched subagents MUST operate in isolated git worktrees (`speckit-gss-bridge-worktree`) with context token load reduced by 70–80% using AST structural skeleton extraction (`speckit-gss-bridge-prune`).

- **Automated Bug Triage & TDD Red-Green-Refactor**: Bug remediation MUST follow `speckit-gss-bridge-bug-assess` (mapping affected nodes and caller hierarchies) and `speckit-gss-bridge-repro-gen` (auto-synthesizing an isolated failing test harness before writing fix code). Fix execution MUST proceed through `speckit-gss-bridge-bug-fix`, which automatically synchronizes the AST graph (`graphify update .`) upon completion.

- **Architectural Drift & Cycle Gate**: Before declaring feature convergence or bug closure, the knowledge graph MUST be audited via `speckit-gss-bridge-drift` to confirm zero circular dependency loops (Tarjan SCC) and zero layer boundary violations.

### Principle VII: Zero Hardcoded User-Facing Strings & Dynamic Internationalization (i18n)
All user-facing text, titles, descriptions, dialog messages, and menu labels MUST support multi-language internationalization and MUST NOT be hardcoded in application code:

- **Zero Hardcoded Strings in C# & XAML**: Hardcoded string literals for user-facing UI text (such as tab titles, button labels, card headers, error messages, or tooltip content) are STRICTLY FORBIDDEN. All UI text MUST be defined in localization resource files (`Resources/Locales/String.resx`, `String.en.resx`, `String.vi.resx`).

- **Standardized x:Bind & Dynamic i18n Localization Model**:
  - In WinUI 3 XAML views, all localized user-facing strings MUST be bound using the standardized dynamic indexer syntax:
    `Text="{Binding [KeyName], Source={StaticResource Localization}, Mode=OneWay}"`
    or via ViewModel properties updated through `TranslationSource.Instance.PropertyChanged` and bound via `{x:Bind ViewModel.LocalizedProperty, Mode=OneWay}`.
  - For parameter-driven localized messages, ViewModels MUST construct strings via `LanguageBase.GetLangValue(key, args)` or expose typed properties refreshed upon language change events.

- **Strict UTF-8 Encoding Without BOM**: All `.resx` files MUST be authored, modified, and saved exclusively in strict UTF-8 encoding without BOM (`System.Text.UTF8Encoding(false)`). Modifying or appending `.resx` entries using shell scripts or PowerShell without explicit UTF-8 encoding is STRICTLY FORBIDDEN to eliminate double-byte Mojibake encoding corruption. Any programmatic resource generation MUST use XML DOM APIs or Unicode escape sequences (`\uXXXX`).

- **Dynamic Localization Source**: All runtime string resolution MUST use `TranslationSource` (`Zenix.Utils.Translations`) or `LanguageBase.GetLangValue(key, args)` to allow instant language switching at runtime without requiring an application restart.

- **Dynamic Binding & Notification**: ViewModels and Models presenting localized properties MUST hook into `TranslationSource.Instance.PropertyChanged` or expose resource keys (`NameKey`, `TitleKey`) so that text refreshes immediately when the user changes language.

- **Absolute Ban on Raw String Literals in XAML Text/Content Attributes**: Writing raw string literals directly into UI text properties (such as `<TextBlock Text="Tiếp tục >" />`, `<TextBlock Text="Đảo chọn" />`, `<Button Content="Lưu" />`, or placeholder/header/title attributes like `PlaceholderText="e.g. localhost"`, `Title="App"`) is STRICTLY PROHIBITED in production XAML views. All user-visible strings MUST use indexer bindings `{Binding [KeyName], Source={StaticResource Localization}, Mode=OneWay}` or `{x:Bind}` via localized resource tokens. Every agent MUST run static automated scanning before finalizing any UI change to ensure zero hardcoded string regressions.

- **Mandatory i18n Key Completeness & Zero-Orphan Invariant**: Every localization resource key referenced in XAML indexer bindings (`{Binding [KeyName], Source={StaticResource Localization}}`), C# code (`LocaleKeys.*`, `LanguageBase.GetLangValue(...)`), or ViewModel models MUST exist across **all** three localization resource bundles (`Resources/Locales/String.resx`, `String.en.resx`, `String.vi.resx`) with non-empty localized strings. Referencing undeclared, misspelled, or orphaned keys is STRICTLY FORBIDDEN. All XAML bindings and code references MUST match the canonical constant definitions in `Zenix.Utils.LocaleKeys`.

- **Automated Localization Unit Test Enforcement**: An automated test suite (`tests/Zenix.Tests/LocalizationVerificationTests.cs`) MUST run as part of `dotnet test` to extract and validate 100% of XAML binding keys and `LocaleKeys` against all resource bundles. Any missing key in any bundle MUST immediately fail the build.

- **XAML Layout Decoupling Mandate**: In WinUI 3, `TabView` controls MUST strictly host only their tab headers/items. Viewport content hosts (such as `<ContentControl>` or `<ContentPresenter>`) MUST NEVER be placed directly inside `<TabView>` tags; they MUST be decoupled into a separate layout grid row to avoid visual tree orphaning and empty viewports.

### Principle VIII: Non-Intrusive Agent Desktop Automation, Diagnostic Reproduction & Visual Verification Gate
To eliminate guesswork, prevent misdiagnosed root causes, conserve context/tokens, avoid invasive cursor hijacking, and establish an automated, non-intrusive desktop verification standard:

- **Mandatory Live Diagnostic Reproduction via `agent-desktop`**: When an issue or bug report is received (e.g. during `/speckit-gss-bridge-bug-assess` or incident intake), the agent MUST NOT hypothesize or guess the root cause based purely on static code analysis. If the report involves user interface behavior, rendering, navigation, window lifecycle, or localization, the agent MUST run the application and execute automated UI inspection via `agent-desktop` (`agent-desktop launch`, `agent-desktop snapshot -i --compact`) to reproduce the failure live, inspect the semantic accessibility element tree, and capture initial failure screenshots (`agent-desktop screenshot`) into the session artifact directory (`<appDataDir>/brain/<conversation-id>`).

- **Non-Intrusive OS Accessibility Automation Protocol (`agent-desktop`)**: All automated UI interaction and inspection MUST utilize the `agent-desktop` CLI/MCP toolchain (`npm install -g agent-desktop`, `.agents/skills/agent-desktop`). Automation interfaces natively with the Windows UI Automation (UIA) accessibility tree in a headless, background manner without stealing active window focus or moving the user's physical mouse cursor:
  1. **Observe**: Capture the semantic accessibility tree with `agent-desktop snapshot -i --compact` (or filter by application `agent-desktop snapshot --app Zenix -i --compact`) to retrieve structured JSON containing stable qualified element references (`@<snapshot_id>:e<N>`).
  2. **Act**: Target elements directly using their qualified `@ref` IDs to execute semantic actions: `agent-desktop click '<ref>'`, `agent-desktop set-value '<ref>' "text"`, `agent-desktop clear '<ref>'`, `agent-desktop select '<ref>' ...`, `agent-desktop toggle '<ref>'`, `agent-desktop scroll-to '<ref>'`, and `agent-desktop wait --element '<ref>' --predicate actionable`.
  3. **Verify**: Retake snapshots (`agent-desktop snapshot --root '<ref>' -i --compact`) to verify state transitions and read element properties (`agent-desktop get text '<ref>'`, `agent-desktop is checked '<ref>'`).
  4. **Capture Evidence**: Capture high-fidelity window screenshots using `agent-desktop screenshot --app Zenix` to produce clean PNG artifacts without pixel guesswork.
  5. **Headless by Default & Selective `--headed`**: All actions MUST run in strict headless mode by default. The `--headed` flag is permitted ONLY when physical input is strictly required by the control (such as double-clicking to expand custom items, right-clicking for native context menus, or physical hover/drag).

- **Agent Sandbox WindowStation / Desktop Isolation Protocol (`WinSta0\default` Bridge)**:
  - **Bản chất rào cản kỹ thuật**: Trong môi trường thực thi của Agent Sandbox trên Windows (như Antigravity runner), các lệnh shell được hệ thống bảo mật tự động cô lập vào một Window Desktop ảo tạm thời (ví dụ: `Desktop: exebox-...`). Do trên desktop ảo này không có cửa sổ hiển thị nào, lệnh gọi `agent-desktop` trực tiếp sẽ bị lỗi nghiêm trọng: `{"code":"INTERNAL","message":"EnumWindows failed to enumerate top-level windows"}` (do hàm Win32 `EnumWindows` trả về 0 khi desktop rỗng và `agent-desktop` hiểu nhầm là lỗi).
  - **Quy tắc cầu nối bắt buộc**: Mọi lệnh thực thi `agent-desktop` và khởi chạy ứng dụng GUI mục tiêu (`Zenix.exe`) từ Agent Sandbox BẮT BUỘC phải được liên kết sang desktop tương tác thực tế của người dùng (`WinSta0\default`):
    1. **Khởi chạy ứng dụng**: Bắt buộc khởi chạy qua script cầu nối chuẩn hóa `.agents/skills/agent-desktop/scripts/bridge.py launch-app <exe_path> [cwd]` (sử dụng Win32 `CreateProcessW` gán cờ `STARTUPINFO.lpDesktop = @"WinSta0\default"`).
    2. **Thực thi `agent-desktop`**: Bắt buộc gọi qua bridge: `python .agents/skills/agent-desktop/scripts/bridge.py <subcommand> [args]` (ví dụ: `python .agents/skills/agent-desktop/scripts/bridge.py snapshot -i --app Zenix.exe --compact`, `python .agents/skills/agent-desktop/scripts/bridge.py screenshot --app Zenix.exe <output_path>`).
    3. **Nghiêm cấm gọi trần không qua bridge**: Nghiêm cấm tuyệt đối chạy lệnh `agent-desktop` trần trong sandbox session mà không cấu hình desktop `WinSta0\default`, để ngăn ngừa triệt để lỗi sập `EnumWindows failed` do desktop isolation.

- **PowerShell Ref Quoting Rule**: In PowerShell sessions, agents MUST ALWAYS quote every ref argument (e.g. `'@s8f3k2p9:e1'`). Unquoted `@token` syntax is interpreted by PowerShell as a splatting operator, causing the parameter to disappear and resulting in `INVALID_ARGS`.

- **Continuous Live UI Testing Invariant (Giao Thức Kiểm Thử Liên Tục Thời Gian Thực)**:
  - **Cấm đóng/mở lại ứng dụng giữa các thao tác kiểm thử**: Khi tiến hành kiểm thử tương tác chuỗi (như mở phân hệ ➔ mở kết nối ➔ bung cơ sở dữ liệu ➔ kiểm tra bảng/khung nhìn/lưới dữ liệu), ứng dụng `Zenix` chỉ được phép khởi chạy đúng 1 LẦN duy nhất bằng `bridge.py launch-app` (sử dụng WMI `Win32_Process.Create` tách biệt hoàn toàn khỏi Job Object). Agent BẮT BUỘC phải thực thi toàn bộ chuỗi quan sát (`snapshot`), tương tác (`click`, `mouse-click`, `set-value`, `scroll-to`), và chụp ảnh nghiệm thu (`screenshot`) liên tục theo thời gian thực trên cùng một tiến trình đang mở. Nghiêm cấm tuyệt đối viết các script kiểm thử tạm bợ tự đóng và mở lại ứng dụng giữa mỗi bước thao tác gây mất trạng thái phiên làm việc và tốn thời gian.
  - **Tài liệu hướng dẫn kiểm thử bắt buộc**: Mọi Agent trước khi thực hiện tự động hóa UI bắt buộc phải đọc và tuân thủ hướng dẫn kỹ thuật chuẩn hóa tại `.agents/skills/agent-desktop/GUIDE.md`.

- **Zero-AutomationId Production Code Invariant**: Agents are STRICTLY PROHIBITED from modifying or polluting production XAML markup with temporary or testing-specific attributes such as `AutomationProperties.AutomationId`. Production XAML must remain 100% clean. The `agent-desktop` engine locates controls dynamically via the OS accessibility tree by matching control role (`Button`, `ComboBox`, `TreeItem`, `Edit`, `Text`), localized display names, and hierarchical snapshot topology.

- **Retirement & Elimination of Legacy Test Scripts (Zero Legacy Test Scripts Invariant)**:
  1. All legacy Win32/PowerShell interactive desktop scripts (`scripts/test-real-desktop.ps1`, `scripts/verify-*.ps1`) are permanently retired and removed from the repository.
  2. The `tests/` directory is strictly reserved for official automated unit and integration test suites (`tests/Zenix.Tests/`). Ephemeral, exploratory, or temporary test scripts MUST NEVER be committed to `tests/` or `scripts/`.
  3. All live UI verification and diagnostic reproductions MUST be executed directly via standard `agent-desktop` CLI commands or MCP tools rather than maintaining bespoke script files.

- **Zero-Speculation & Mandatory Live UI Verification Gate Before Handoff**:
  - **Absolute Ban on Speculation & Assumption**: Agents are STRICTLY FORBIDDEN from guessing, speculating, or stating phrases such as "tôi đoán là nó hoạt động" ("I guess it works"), "it should work now", "theoretically fixed", or "likely resolved". No task may be marked as complete, no PR may be approved, and no handoff may occur based on code inspection, compilation success (`dotnet build`), or headless unit tests alone whenever UI, layout, interaction, or rendering is involved.
  - **Uncompromising Delivery Gate**: Handoff and task completion are PERMITTED ONLY IF live end-to-end verification has completed successfully using `agent-desktop`, fully exercising the actual user workflow (opening connections, navigating tabs, selecting items, verifying data grids), verifying the absence of crashes, and capturing genuine window screenshots (`.png`) saved into the session artifact directory (`<appDataDir>/brain/<conversation-id>`).
  - **Mandatory Visual Inspection Prior to Completion**: Before any bug fix or new user-facing feature is signed off (e.g. during `/speckit-converge`), the agent MUST verify the visual presentation of the running application and embed the genuine captured screenshot (`agent-desktop screenshot`) into the verification report.

- **Prohibition on Purely Static / Headless Verification**: An agent is STRICTLY FORBIDDEN from diagnosing a UI defect or declaring a UI bug fix / frontend feature complete based solely on compilation success (`dotnet build`), static code inspection, or unit tests alone whenever visual presentation, layout templates, user interaction, or localization strings are involved.

### Principle IX: Deterministic Code Knowledge Graph & Architecture Radar (Graphify Integration)
To eliminate exploratory file reading, prevent blind code searching, and guarantee zero-token context conservation during Spec-Kit and Superpowers operations:

- **Source-Only Scanning Boundary & Exclusion Mandate**: Graphify knowledge base extraction MUST be restricted strictly to essential production source code (`Zenix/`), shared libraries (`Libs/`), architectural contracts, and formal automated test suites (`tests/`). Meta-governance directories (`.specify/`, `specs/`), agent skill/plugin ecosystems (`.agents/`), ad-hoc scratch scripts (`scripts/`, `scratch/`), temporary build artifacts (`bin/`, `obj/`), and IDE configurations (`.vs/`, `.vscode/`) MUST be permanently excluded via a root-level `.graphifyignore` configuration file. Agents MUST NOT pollute the architecture graph with non-product markdown artifacts or ephemeral utility scripts.

- **Automated Graph Bootstrapping & Reliability Verification Gate**: Because `graphify-out/` is untracked by version control (`.gitignore`), whenever `graphify-out/graph.json` is missing, empty, or determined to be unreliable (< 500 nodes, missing manifest, or internal schema corruption), the agent MUST immediately execute a clean bootstrap build (`graphify .` or `/graphify`) adhering to `.graphifyignore` before attempting code investigation or task planning.

- **Mandatory Knowledge Graph Consultation**: Whenever `graphify-out/graph.json` exists and is verified reliable, the agent MUST NOT perform unguided brute-force directory traversals, global greps, or repetitive full-file reads across dozens of source files. Before opening or reading files for architectural discovery, bug investigation, or feature planning, the agent MUST consult the persistent knowledge graph using:
  1. `graphify query "<question>"`: For breadth-first traversal of concept clusters and focused subgraphs (conserving 100x+ context tokens).
  2. `graphify explain "<Component>"`: To inspect the exact symbol definition, callers, callees, and in-degree/out-degree connection vectors of target classes.
  3. `graphify path "<Source>" "<Target>"`: To map execution call chains, inter-service contracts, and data-flow pathways between disparate components.

- **Delegation to GSS-Bridge Architecture Automation**: Routine graph synchronization, blast radius calculations, test impact selection, and architectural drift checking are natively delegated to GSS-Bridge v1.2.0 automated commands (`speckit-gss-bridge-drift`, `speckit-gss-bridge-test-select`, `speckit-gss-bridge-blast-radius`, and automatic `graphify update .` handoff hooks).

### Principle X: Clean Engineering, Root-Cause Resolution & Anti-Patchwork Discipline (Zero-Patchwork Invariant)
Every bug remediation, feature addition, and architectural modification MUST be engineered cleanly, comprehensively, and sustainably at the root-cause level, strictly eliminating superficial patchwork ("code chắp vá"), band-aids, or temporary hacks:

- **Root-Cause Architectural Resolution (Giải Quyết Tận Gốc Cấp Độ Kiến Trúc)**:
  - When fixing a defect or implementing a capability, changes MUST address the underlying flaw in the data model, lifecycle management, or concurrency topology rather than treating symptoms.
  - Patchwork band-aids—such as swallowing exceptions with empty `catch { }` blocks, masking `NullReferenceException` with indiscriminate null-conditional checks without investigating why state is uninitialized, inserting ad-hoc global or static state flags, hardcoding arbitrary sleep/delay intervals to mask race conditions, or adjusting magic coordinates/offsets to force alignment—are **STRICTLY FORBIDDEN**.

- **Zero-Patchwork Integration & Architectural Harmony (Chống Chắp Vá & Bảo Toàn Kiến Trúc)**:
  - All new code and refactorings MUST seamlessly integrate into the established Clean MVVM, Dependency Injection, and asynchronous concurrency patterns as if they were part of the original design.
  - Prohibited patchwork anti-patterns:
    1. **No Duct-Tape Workarounds**: Quick-and-dirty fixes that bypass established architectural boundaries (e.g., calling private fields via reflection, manipulating visual controls from ViewModels, or writing imperative visual DOM mutations in code-behind) are strictly prohibited.
    2. **Zero Logic Duplication**: Reusable business logic, validation, or query operations MUST be centralized in appropriate services or helpers. Copy-pasting duplicate routines to avoid touching existing components is forbidden.
    3. **No Circumvention of DI Container**: Bypassing constructor injection or using ad-hoc `static` singletons/locators to reach dependencies is banned.
    4. **No Silent Exception Swallowing**: Every caught exception MUST either be properly resolved with meaningful fallback state, recorded in execution telemetry/logging, or communicated transparently to the user via UI notifications (`InfoBar` / `ContentDialog`). Empty `catch { }` blocks that blind diagnostic capabilities are outlawed.
    5. **Strict Boundary Integrity**: Domain models and database entities MUST NOT leak into UI presentation layers; UI visual controls MUST NOT leak into ViewModels.

- **The Boy Scout Rule & Code Cleanliness Standard (Quy Tắc Hướng Đạo Sinh)**:
  - Whenever an agent touches a file for a bug fix or feature, it MUST leave the modified file in a cleaner, more robust, and more maintainable state than it found it:
    - Eliminate unused `using` statements, redundant dead code, and obsolete commented-out blocks in touched scopes.
    - Guarantee complete nullable reference type annotations (`string?`, non-null invariants) and defensive argument checks (`ArgumentNullException.ThrowIfNull`).
    - Provide 100% standard technical American English XML documentation comments (`/// <summary>`, `<param>`, `<returns>`).
    - Enforce Roslyn `IDE0065` using directives placed strictly outside and above `namespace`.
    - Enforce universal Windows CRLF (`
`) line endings without mixed characters.

- **Defensive Completeness & Verification**:
  - A fix or feature is only considered clean and complete when accompanied by automated unit/integration tests confirming both the primary scenario and boundary edge cases, along with live non-intrusive UI verification proving flawless real-world operation.

## Technology Stack & Security Constraints

- **Platform & Runtime**: .NET 10 (`net10.0-windows10.0.26100.0`), C# (Language Version `preview`), Windows App SDK (WinUI 3).
- **Core Presentation Libraries**: `Microsoft.WindowsAppSDK` (WinUI 3 controls, Theme management, TabView), `WinUIEx` (window management), `CommunityToolkit.Mvvm` (MVVM Toolkit v8.4+).
- **Host & Dependency Injection**: `Microsoft.Extensions.Hosting` (Generic Host, `IServiceCollection`, hosted application lifecycle, typed ViewModel registration and factories).
- **Database Engine**: `MySqlConnector` (high-performance async MySQL/MariaDB driver), `System.IO.Hashing` (fast data integrity checks).
- **Code Knowledge Graph & Architecture Radar**: Graphify (`graphifyy` AST parser, `graphify-out/graph.json`, deterministic offline semantic topology, and multi-platform agent skill).
- **Multi-Agent Orchestration & Governance Bridge**: GSS-Bridge v1.2.0 (AST context pruner, Tarjan SCC drift detector, graph-guided test impact analysis, automated repro test generator, worktree wave isolation, cross-agent decision journal, and visual dashboard).
- **Inter-Process Communication (IPC)**: Per-process Named Pipe Server with Windows Access Control Lists (ACL) supporting multi-instance process concurrency.
- **Internationalization (i18n)**: `TranslationSource` with `INotifyPropertyChanged` indexer, `.resx` resource bundles (`String.resx`, `String.en.resx`, `String.vi.resx`) encoded in UTF-8 without BOM, supporting live language switching (English, Vietnamese).
- **Internationalization & Localization Testing**: Automated static verification test scanner (`tests/Zenix.Tests/LocalizationVerificationTests.cs`) asserting complete key parity across `String.resx`, `String.en.resx`, `String.vi.resx`, `LocaleKeys.cs`, and all production `*.xaml` views.
- **Line Endings & File Formatting**: Strict Windows CRLF (`\r\n`) line endings enforced across all source, XAML, docs, data, and PowerShell scripts via `.editorconfig` (`end_of_line = crlf`) and Git attributes (`* text=auto eol=crlf`). Strict LF (`\n`) enforced on POSIX shell scripts (`*.sh`).
- **UI Automation & Verification**: `agent-desktop` CLI / MCP (`vercel-labs/agent-desktop`) over native Windows UI Automation (UIA) accessibility tree, with headless semantic actions, qualified `@ref` IDs, and high-fidelity native window screenshots (`agent-desktop screenshot`).

## Development Workflow & Quality Gates

- **Zero-Error Compilation Gate**: Every pull request and committed change MUST compile cleanly (`dotnet build src\Zenix\Zenix.csproj -c Debug`) with 0 errors and 0 warnings.

- **Clean MVVM Compliance Gate**: Every change, new feature, and bug remediation MUST pass a Clean MVVM compliance review:
  1. Code-behind files (`*.xaml.cs`) MUST NOT contain visual tree mutations, manual child container manipulation (`Children.Add`/`Children.Remove`), or state logic that bypasses declarative XAML data binding. All dynamic view switching MUST be handled declaratively.
  2. ViewModels MUST NOT reference UI types (`Microsoft.UI.Xaml.*`, `Window`, `Page`, `FrameworkElement`, `ContentDialog`).
  3. Views, pages, and dialogs MUST resolve ViewModels via Dependency Injection, NEVER via direct `new MyViewModel(...)`.
  4. Native file dialogs (`FileOpenPicker`, `FileSavePicker`) MUST NOT be instantiated directly in code-behind; they MUST be invoked via an injected `IFilePickerService`.
  5. Context menus and flyouts MUST be declared declaratively in XAML, never instantiated imperatively in C# code-behind.
  6. XAML views MUST use compiled bindings (`{x:Bind}`) for all ViewModel data and commands, reserving traditional `{Binding}` exclusively for dynamic indexer localization.

- **Roslyn Code Layout & Using Directives Gate (`IDE0065`)**: Every C# source file MUST place all `using` directives at the very top of the file, strictly outside and above the `namespace` declaration. Any C# file placing `using` directives below or inside a `namespace` declaration fails this compliance gate.

- **Deterministic Universal Line Endings Gate**: Every text file created or modified in the repository MUST be formatted strictly according to the GSS-Bridge line ending invariant: Windows CRLF (`\r\n`) for C#, XAML, Markdown, PowerShell, JSON, YAML, RESX; and LF (`\n`) for POSIX shell scripts. Any file with mixed line endings or incorrect platform endings fails this gate.

- **GSS-Bridge Architectural & Graph Integrity Gate**: Prior to declaring feature convergence or bug resolution, the agent MUST verify through GSS-Bridge v1.2.0 tools:
  1. `speckit-gss-bridge-drift`: Zero circular dependency loops and zero layer boundary violations.
  2. `speckit-gss-bridge-blast-radius`: Verified decoupled blast radius for modified components.
  3. Continuous synchronization: AST graph updated via automated `graphify update .` post-execution hooks.

- **Documentation Verification Gate**: All modified or introduced methods, properties, and types MUST meet 100% English XML documentation requirements.

- **Internationalization (i18n) Gate**: All newly introduced or modified UI elements, commands, and viewmodels MUST NOT contain hardcoded user-facing strings; all user text MUST use `TranslationSource` / `.resx` keys with verified UTF-8 encoding.

- **Comprehensive i18n Completeness & Static Verification Gate**: Every pull request, feature implementation, and bug remediation MUST pass the automated localization test suite (`LocalizationVerificationTests`). The test suite statically extracts all `{Binding [KeyName]}` occurrences across all `*.xaml` files (excluding `bin/` and `obj/`) and all `LocaleKeys.*` constants in C#, asserting that 100% of keys are defined with non-empty string values in `String.resx`, `String.en.resx`, and `String.vi.resx`. Any missing, misspelled, or untranslated key immediately breaks the compilation/test gate, preventing runtime blank text or silent translation failures.

- **Diagnostic Reproduction Gate**: Every bug assessment involving UI behavior, window interaction, or visual anomalies MUST produce live reproduction evidence (semantic element tree trace or window screenshot) captured via `agent-desktop` during the assessment phase and documented in `assess.md`.

- **Zero-Pollution XAML Gate**: Automated UI testing MUST NOT add `AutomationId` or testing markers to production XAML files. Production views must remain clean and decoupled.

- **Zero-Retry Testing & Token Conservation Gate**: All UI automation test scripts MUST execute deterministically on the first run using established standardized navigation and interaction primitives (exact database matching, double-click tree expansion, header-relative cell calculation, and automatic coordinate-derived cropping). Ad-hoc exploratory scripts and trial-and-error looping are strictly forbidden to conserve session context and token budget.

- **Script & Test Directory Hygiene Gate**: The `tests/` directory is strictly reserved for official .NET unit/integration test suites (`Zenix.Tests/`). The `scripts/` directory MUST remain clean of ephemeral, ad-hoc, or legacy UI automation scripts. UI verification is driven directly via `agent-desktop` CLI. Any extraneous or abandoned script lingering in `scripts/` or `tests/` fails this compliance gate.

- **Zero-Speculation Live UI Verification Gate**: Every frontend feature, UI modification, or defect remediation MUST execute live end-to-end automated UI verification via `agent-desktop` and capture real visual evidence (`agent-desktop screenshot`) before handoff. Handing off with guesswork, assumptions, or speculative claims ("I guess it works") is an explicit constitutional violation.

- **Visual Evidence Verification Gate**: Every frontend feature or bug fix MUST provide visual evidence (screenshot of running UI captured via `agent-desktop screenshot`) to prove layout rendering, non-emptiness, and correct typography before handoff.

- **Anti-Patchwork & Root-Cause Quality Gate**: Every bug remediation and feature addition MUST pass an explicit anti-patchwork audit before handoff:
  1. Does this change resolve the problem at its architectural root cause, or is it a superficial symptomatic band-aid?
  2. Does it introduce any silent exception swallowing, arbitrary delays, or magic numbers?
  3. Does it duplicate existing logic or bypass the Dependency Injection container?
  4. Does it leave the touched files cleaner, fully documented, and strictly compliant with Clean MVVM and Roslyn standards?
  Any solution exhibiting duct-tape or patchwork characteristics fails this quality gate and MUST be rejected for proper architectural redesign.

- **Convergence Verification Gate**: Prior to feature sign-off, `/speckit-converge` MUST confirm that the implementation exhibits zero gaps against `spec.md`, `plan.md`, and `tasks.md`.

- **Evidence-Before-Assertion Mandate**: No task may be marked as complete without verifiable execution evidence (passing build logs, test executions, or runtime traces).

## Governance

- **Constitutional Supremacy**: This Constitution represents the supreme architectural and technical law of the Zenix repository. It supersedes informal conventions, legacy habits, and ad-hoc suggestions.

- **Amendment Procedure**: Amendments to this Constitution require an explicit `/speckit-constitution` cycle detailing:
  1. The specific principle or constraint being modified.
  2. The architectural rationale and impact assessment.
  3. A corresponding semantic version bump according to SemVer rules:
     - **MAJOR**: Incompatible removal or fundamental redefinition of core architectural invariants.
     - **MINOR**: Addition of new principles, platforms, or substantially expanded quality gates / streamlining integrations.
     - **PATCH**: Non-semantic clarifications, typo fixes, or wording refinements.

- **Compliance Enforcement**: All code reviews and agent orchestrations MUST actively verify adherence to these constitutional principles.

**Version**: 1.19.0 | **Ratified**: 2026-10-19 | **Last Amended**: 2026-11-09
