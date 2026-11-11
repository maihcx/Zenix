# Changes: Core Service Contracts (T-006) & Architecture Test Suite (GATE_01)

**Worker**: `worker_wave1_contracts`  
**Milestone**: Wave 1: Foundation & Contracts Setup  
**Date**: 2026-11-11  

---

## 1. Files Added / Modified

### 1.1 Core Contracts (`Zenix/Core/Zalo/Contracts/`)
All files authored in UTF-8 without BOM, Windows CRLF line endings, `namespace Zenix.Core.Zalo.Contracts;`, with using directives outside the namespace (IDE0065), zero UI visual dependencies, and 100% XML doc comments in technical English.

1. **`IZaloClient.cs`**:
   - Master client coordinator aggregating `Auth`, `Gateway`, `Messages`, `Media`, `Contacts`, `Crypto`, and `Storage`.
   - Exposes `CurrentUser` (`UserProfile?`) and `IsAuthenticated`.
   - Asynchronous lifecycle methods: `InitializeAsync(CancellationToken)`, `DisconnectAsync(CancellationToken)`, and `IAsyncDisposable`.
2. **`IZaloAuthService.cs`**:
   - Manages desktop QR login state machine (`QrLoginStep`, `QrCodeState`, `QrConfirmedUser`, `QrLoginStepResult`).
   - Defines `SessionKickedOut` event, `GetOrCreateClientImeiAsync`, `RequestQrCodeAsync`, `PollQrScanStateAsync` (`IAsyncEnumerable`), `TryRestoreSavedSessionAsync`, and `LogoutAsync`.
3. **`IZaloGatewayService.cs`**:
   - Duplex WebSocket transport abstraction for Zalo binary framing `[version: 1B][cmd: 2B LE][subCmd: 1B]`.
   - Events: `FrameReceived` (`ZaloIncomingFrameEventArgs`), `ConnectionStateChanged` (`ZaloConnectionStateChangedEventArgs`).
   - Methods: `ConnectAsync`, `DisconnectAsync`, `SendBinaryFrameAsync`, and `IAsyncDisposable`.
4. **`IZaloCryptoService.cs`**:
   - Low-level cryptographic methods for telemetry encryption (`EncryptZcid`), API parameter encryption (`EncryptApiParams`), trusted device keychain envelope encryption and decryption (`EncryptKeychainData`, `DecryptKeychainData`), CDN media de-noising (`DenoiseMedia`), and 60-digit Curve25519 Safety Number computation (`ComputeSafetyNumber`).
5. **`IZaloMessageService.cs`**:
   - High-level messaging operations: `MessageReceived`, `MessageStatusChanged`, `MessageRecalled`.
   - Methods: `SendTextMessageAsync` (with mentions and `QuotedMessageSummary`), `SendMediaMessageAsync` (with `ChatMessagePayload`), `AddReactionAsync`, `RecallMessageAsync`, `DeleteMessageWithUndoAsync` (with countdown and `CancellationTokenSource`), `GetMessageHistoryAsync`, and `SaveDraftAsync`.
6. **`IZaloMediaService.cs`**:
   - Upload operations: `UploadPhotoAsync` returning `PhotoPayload`, `UploadFileAsync` returning `FileAttachmentPayload`.
   - Download operations: `DownloadMediaAsync` with `MediaTransferProgress`.
   - Video poster extraction: `ExtractVideoPosterAsync` using ISO-BMFF box parsing.
   - LIFO thumbnail streaming: `GetThumbnailStreamAsync`.
7. **`IZaloContactService.cs`**:
   - Address book synchronization: `SyncContactsAsync`, `GetContactsAsync`, `GetContactByIdAsync`, `GetGroupsAsync`, `GetGroupByIdAsync`, `SearchContactsAsync`.
   - Events: `ContactUpdated`, `GroupUpdated`.
8. **`IZaloStorageService.cs`**:
   - Master storage DAL 4.0 interface and repository sub-interfaces:
     - `IConversationRepository`: `GetAllConversationsAsync`, `GetByIdAsync`, `UpsertAsync`, `UpdateUnreadCountAsync`.
     - `IMessageRepository`: `InsertAsync`, `GetByConversationAsync`, `UpdateStatusAsync`, `DeleteAsync`.
     - `IContactRepository`: `GetAllContactsAsync`, `UpsertBatchAsync`.
     - `IZaloStorageService`: Exposes repositories and manages `InitializeAccountDatabaseAsync`, `CloseDatabaseAsync`, and `IAsyncDisposable`.
9. **`IFilePickerService.cs`**:
   - Abstracted, UI-agnostic file and folder picker: `PickSingleFileAsync`, `PickMultipleFilesAsync`, `PickSaveFileAsync`, `PickFolderAsync`.
10. **`IDialogService.cs`**:
    - Abstracted UI notification and dialog service: `ShowAlertAsync`, `ShowConfirmationAsync` (returning `ConfirmationResult`), and non-blocking `ShowNotification` (`NotificationSeverity`).
11. **`IChatViewModel.cs`**:
    - UI-agnostic ViewModel contract for active chat viewport: `INotifyPropertyChanged`, `ActiveConversation`, `Messages` (`ObservableCollection<ChatMessage>`), `InputText`, `IsLoadingHistory`, `IsTransferringMedia`, `TransferProgress`, `ReplyingToMessage`, and commands (`SendMessageCommand`, `AttachFileCommand`, `ReplyToMessageCommand`, `CancelReplyCommand`, `RecallMessageCommand`, `DeleteMessageCommand`, `ToggleReactionCommand`).
12. **`IConversationListViewModel.cs`**:
    - UI-agnostic ViewModel contract for sidebar conversation list: `INotifyPropertyChanged`, `Conversations` (`ObservableCollection<ConversationSummary>`), `SelectedConversation`, `SearchQuery`, `IsRefreshing`, `TotalUnreadCount`, and commands (`RefreshCommand`, `TogglePinCommand`, `ToggleMuteCommand`, `MarkAsReadCommand`, `ToggleArchiveCommand`, `DeleteConversationCommand`).

### 1.2 Architecture Test Suite (`tests/Zenix.Tests/Zalo/Contracts/`)
- **`ContractsArchitectureTests.cs`**:
  - Decorated with `[Trait("Category", "Contracts")]`.
  - Tests included:
    1. `AllRequiredContracts_ExistAndArePublicInterfaces`: Reflection check ensuring all 15 contract interfaces exist, are public, and reside in `Zenix.Core.Zalo.Contracts`.
    2. `AllInterfaces_FollowStandardNamingConvention`: Asserts all interfaces start with 'I'.
    3. `ContractsNamespace_ContainsZeroReferencesToUiVisualTypes`: Deep reflection scan verifying zero types, properties, methods, return types, parameter types, or events in `Zenix.Core.Zalo.Contracts` reference prohibited UI namespaces (`Microsoft.UI.Xaml.*`, `Windows.UI.Xaml.*`) or UI visual element types (`UIElement`, `FrameworkElement`, `Window`, `ContentDialog`).
    4. `AllAsynchronousMethodsInContracts_MustAcceptCancellationToken_WithDefaultValue`: Asserts 100% of methods returning `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, or `IAsyncEnumerable<T>` accept a `CancellationToken` with a default value.
    5. `ViewModelContracts_AdhereToCleanMvvmConventions`: Asserts ViewModels implement `INotifyPropertyChanged`, command properties implement `ICommand`, and collections use `ObservableCollection<T>`.
    6. `AsyncDisposableServices_ImplementIAsyncDisposable`: Asserts `IZaloClient`, `IZaloGatewayService`, and `IZaloStorageService` implement `IAsyncDisposable`.
    7. `AuxiliaryEnumsAndRecords_AreProperlyDeclared`: Verifies supporting enums and records (`QrLoginStep`, `ConfirmationResult`, `NotificationSeverity`, `QrCodeState`, `QrConfirmedUser`, `QrLoginStepResult`, `ZaloIncomingFrameEventArgs`, `ZaloConnectionStateChangedEventArgs`, `MessageReceivedEventArgs`, `MessageStatusChangedEventArgs`, `MessageRecalledEventArgs`, `MediaTransferProgress`).

---

## 2. Verification Summary
- **Build**: `dotnet build Zenix.sln -c Debug` -> 0 errors, 0 warnings.
- **Tests**:
  - `dotnet test Zenix.sln --filter 'Category=Contracts'` -> 7 passed, 0 failed.
  - `dotnet test Zenix.sln --filter 'Category=Models | Category=Contracts'` -> 76 passed, 0 failed.
  - `dotnet test Zenix.sln` -> 83 passed, 0 failed.
- **Compliance**: `python scripts/audit_compliance.py` -> AUDIT PASSED (100% compliant with CRLF, UTF-8 no BOM, IDE0065, IDE0073, Zero UI types).
- **Knowledge Topology**: `graphify update .` -> 684 nodes, 1214 edges, 41 communities updated in `graphify-out/`.
