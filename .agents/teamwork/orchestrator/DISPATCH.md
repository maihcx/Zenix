## 2026-11-11T07:59:02Z

Triển khai Wave 2: Storage & Cryptography Core (Tasks T-008 đến T-018, hướng tới GATE_02) để tái cấu trúc mã nguồn Zalo sang .NET 10 WinUI 3 cho giải pháp Zenix.

Working directory: d:\DEV\WinUI\Zenix
Integrity mode: development

## Reference Materials
- Đặc tả: `.specify/specs/spec.md`
- Kế hoạch: `.specify/specs/plan.md`
- Mô hình dữ liệu: `.specify/specs/data-model.md`
- Hợp đồng Interfaces: `.specify/specs/contracts/service-interfaces.md`
- Danh sách tasks: `.specify/specs/tasks.md`
- Repo tham chiếu: `d:\DEV\WinUI\zalo-swift-re`
- Hiến chương bắt buộc tuân thủ: `.specify/memory/constitution.md`

## Requirements

### R1. Storage Infrastructure & Local Repositories (Tasks T-008 - T-013)
Triển khai SQLite DAL 4.0 Connection Factory (`ZaloSqliteDalService`) với chế độ WAL, pragmas bắt buộc (`journal_mode = WAL`, `synchronous = NORMAL`, `busy_timeout = 5000`, `cache_size = -8000`, `foreign_keys = ON`), và xử lý xung đột UNIQUE an toàn. Thực thi `ZaloSqliteMigrator` quản lý schema v400 (`conversations`, `messages`, `contacts`, `groups`, `group_members`, `drafts`, `keychain_keys`, `sync_checkpoints`) cùng các indexes tối ưu truy vấn. Cài đặt các repositories: `ConversationRepository`, `MessageRepository` (hỗ trợ phân trang 50 tin nhắn với sort tie-breaking `timestamp DESC, offset DESC`), `ContactRepository`, `GroupRepository`, `FailedDraftStore`, `ZaloCredentialStorage` (mã hóa DPAPI / AES-256-GCM), và `ZaloStorageService` facade.

### R2. Core Cryptography & Signal Protocol E2EE (Tasks T-014 - T-018)
Triển khai `ZaloCryptoService` hỗ trợ mã hóa tăng tốc phần xuân AES-256-CBC (`EncryptZcid`, `EncryptApiParams`), Keychain envelope packing/unpacking với HKDF-SHA256 derivation và constant-time HMAC-SHA256 verification (`CryptographicOperations.FixedTimeEquals`), cùng bộ lọc Media De-noising (byte-wise XOR 147). Triển khai Curve25519 & X3DH Pre-Key service (`SignalX3dhService`), Double Ratchet cryptographic engine (`SignalDoubleRatchetService`) hỗ trợ skipped message keys table cho tin nhắn đến lệch thứ tự, và `SignalE2eeService` với thuật toán tạo 60-digit Safety Number.

### R3. Verification & GATE_02 Compliance
Triển khai toàn bộ unit test suites cho các phân hệ Storage và Cryptography theo đúng tiêu chí kiểm thử của `tasks.md`, khớp 100% test vectors từ `zalo-swift-re`, đảm bảo vượt qua GATE_02 (GATE 2A và GATE 2B), bảo toàn hồi quy cho toàn bộ unit tests hiện có của Wave 1 (83/83 passing), và biên dịch sạch toàn bộ giải pháp với 0 lỗi.

## Acceptance Criteria

### Storage Integrity (GATE 2A)
- [ ] Lệnh `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Storage"` chạy thành công với 0 failures.
- [ ] Chế độ `PRAGMA journal_mode = WAL` được kích hoạt và schema v400 khởi tạo đầy đủ bảng, index.
- [ ] Truy vấn phân trang 50 tin nhắn hoàn tất trong $\le 1.5\text{ ms}$ và bảo đảm tính ổn định sắp xếp `(timestamp DESC, offset DESC)`.
- [ ] Thông tin nhạy cảm và session tokens được mã hóa an toàn qua DPAPI; không ghi log hay lưu trữ plain text.

### Cryptography Integrity (GATE 2B)
- [ ] Lệnh `dotnet test tests/Zenix.Tests/Zenix.Tests.csproj --filter "Category=Crypto"` chạy thành công với 0 failures.
- [ ] 100% test vectors cho `zcid`, API params, Keychain envelope và media denoising khớp chính xác với `zalo-swift-re`.
- [ ] X3DH shared secrets giữa Alice và Bob khớp nhau 100%.
- [ ] Mô phỏng 100 bước Double Ratchet và tin nhắn out-of-order giải mã thành công không lỗi.
- [ ] Tính toán 60-digit Safety Number đúng chuẩn 12 nhóm 5 chữ số theo vector kiểm thử.

### Solution Integrity & Regression
- [ ] Giải pháp `Zenix.sln` biên dịch sạch qua `dotnet build Zenix.sln -c Debug` với 0 errors và 0 warnings.
- [ ] Toàn bộ unit tests hiện có của Wave 1 (83/83 tests) và các unit test mới của Wave 2 đều pass không hồi quy qua `dotnet test Zenix.sln`.
- [ ] Tuân thủ tuyệt đối Hiến chương `.specify/memory/constitution.md` (Clean MVVM, không code-behind UI manipulation, XML docs tiếng Anh).
