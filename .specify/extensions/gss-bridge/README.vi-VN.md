# GSS-Bridge: Graphify + Spec-Kit + Superpowers

<p align="center">
  <img src="assets/social/github-social-preview.png" alt="GSS-Bridge: Graphify, Spec-Kit, và Superpowers" width="100%" />
</p>

<p align="center">
  <strong>Tam Giác Vàng trong Kỹ Nghệ Phần Mềm Điều Khiển Bởi AI: Quản trị Spec-Kit, Ngữ cảnh Graphify, và Kỷ luật Thực thi Superpowers.</strong>
</p>

<p align="center">
  <a href="https://github.com/maihcx/GSS-Bridge/actions"><img src="https://img.shields.io/badge/CI%2FCD-passing-success" alt="CI Status" /></a>
  <a href="https://github.com/maihcx/GSS-Bridge/releases/latest"><img src="https://img.shields.io/badge/release-v1.2.0-blue" alt="Release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="License" /></a>
  <a href="https://github.com/maihcx/GSS-Bridge"><img src="https://img.shields.io/badge/spec--kit-extension-purple" alt="Spec-Kit Extension" /></a>
  <a href="https://github.com/maihcx/GSS-Bridge"><img src="https://img.shields.io/badge/cross--agent-claude%20%7C%20codex%20%7C%20antigravity%20%7C%20gemini-orange" alt="Agents" /></a>
</p>

<p align="center">
  <a href="README.md"><strong>English</strong></a> •
  <a href="README.vi-VN.md"><strong>Tiếng Việt</strong></a>
</p>

---

## Tổng quan nhanh

| Trụ cột | Hệ thống | Trách nhiệm cốt lõi | Cơ chế vận hành |
|---|---|---|---|
| **Quản trị (Governance)** | **Spec-Kit** | Nguồn sự thật duy nhất về yêu cầu, kế hoạch, task và quy trình xử lý lỗi | `specs/<feature>/` (`spec.md`, `plan.md`, `tasks.md`), `.specify/bugs/<slug>/` (`assessment.md`, `fix.md`) |
| **Ngữ cảnh (Context)** | **Graphify** | Nguồn sự thật về cấu trúc mã nguồn, quan hệ AST, phạm vi ảnh hưởng (Blast Radius) | `graphify-out/graph.json` (`query`, `explain`, `path`, `blast-radius`, tự động khởi tạo biểu đồ, `update .`) |
| **Kỷ luật (Discipline)** | **Superpowers** | Kỷ luật thực thi, điều phối subagent theo đợt, TDD & xác thực | Red-Green-Refactor, `systematic-debugging`, `verification-before-completion`, `dispatch-subagents` |

---

## Vì sao cần GSS-Bridge?

### Vấn đề thực tế
Khi các coding agent (Claude Code, OpenAI Codex, Google Antigravity, Gemini CLI) triển khai tính năng hoặc sửa lỗi, chúng thường vấp phải 3 sai lầm chí mạng:
1. **Thực thi thiếu nguyên tắc (`speckit.implement`)**: Spec-Kit lập đặc tả rất tốt, nhưng lệnh implement mặc định là code một lần (one-shot), không bắt buộc TDD, không cô lập subagent và không có bước xác thực kết quả.
2. **"Mù" ngữ cảnh khi dùng Grep**: Agent cố gắng hiểu kho mã nguồn lớn bằng grep hoặc đọc toàn bộ file mù quáng, bỏ sót luồng gọi hàm AST, các component phụ thuộc và phạm vi ảnh hưởng (Blast Radius).
3. **Quy trình xử lý lỗi sơ sài (`speckit-bug-assess` & `speckit-bug-fix`)**: Lệnh đánh giá lỗi cũ chỉ tìm file thủ công không theo đồ thị cuộc gọi; còn lệnh sửa lỗi lại cho agent sửa code ngay mà không viết test tái hiện lỗi trước.

### Giải pháp: Mô hình Tam Giác Vàng

```
                           ┌──────────────────────────────┐
                           │      Spec-Kit (Quản trị)     │
                           │   specs/<feature>/ · bugs/   │
                           │   spec.md · plan.md · tasks  │
                           └──────────────┬───────────────┘
                                          │
                  ┌───────────────────────┴───────────────────────┐
                  ▼                                               ▼
   ┌──────────────────────────────┐                ┌──────────────────────────────┐
   │     Graphify (Ngữ cảnh)      │◄──────────────►│    Superpowers (Kỷ luật)     │
   │  graphify-out/graph.json     │   Khoanh vùng  │  TDD · Subagents · Debugging │
   │  query · explain · path      │   & Truy vết   │  Xác thực · Code Review      │
   │  blast-radius · update       │                │  bug-fix · systematic-debug  │
   └──────────────────────────────┘                └──────────────────────────────┘
```

GSS-Bridge kết hợp cả ba thành một vòng lặp kỹ nghệ khép kín:
* **Spec-Kit xác định CÁI GÌ (WHAT)**: Yêu cầu, hợp đồng thiết kế, danh sách task và báo cáo đánh giá lỗi.
* **Graphify chỉ ra Ở ĐÂU (WHERE)**: Ranh giới bất biến, phạm vi ảnh hưởng (Blast Radius), sơ đồ gọi hàm Mermaid và danh sách file cô lập cho từng subagent.
* **Superpowers quy định NHƯ THẾ NÀO (HOW)**: Viết test thất bại trước khi viết code, truy vết lỗi có hệ thống theo đường đi đồ thị, và bắt buộc bằng chứng xác thực trước khi kết luận hoàn thành.

---

## Bắt đầu nhanh

```bash
# 1. Khởi tạo dự án Spec-Kit (hoặc dùng dự án hiện có)
specify init my-project --integration claude   # hoặc --integration codex
cd my-project

# 2. Thêm extension GSS-Bridge
specify extension add gss-bridge \
  --from https://github.com/maihcx/GSS-Bridge/releases/latest/download/gss-bridge.zip

# 3. Lập chỉ mục toàn bộ mã nguồn với Graphify
/graphify
```

---

## Hai quy trình vận hành cốt lõi

### 1. Quy trình Phát triển Tính năng (Vòng lặp 7 bước)

1. **Specify**: Chạy `/speckit-specify <tên-tính-năng>` (truy vấn thực thể hiện có qua `graphify query`).
2. **Clarify**: Chạy `/speckit-clarify` để giải quyết các điểm mơ hồ trong yêu cầu.
3. **Plan**: Chạy `/speckit-plan` $\rightarrow$ tính toán phạm vi ảnh hưởng và vẽ sơ đồ Mermaid qua `graphify explain`, ghi chiến lược TDD trực tiếp vào `plan.md`.
4. **Tasks**: Chạy `/speckit-tasks` $\rightarrow$ kiểm tra coupling trước khi gắn cờ song song `[P]`.
5. **Dispatch Waves**: Chạy `/speckit-gss-bridge-dispatch` để phân nhóm các task `[P]` thành các đợt (waves) không va chạm tài nguyên.
6. **Implement**: Chạy `/gss-bridge` (Claude / Antigravity) hoặc `$gss-bridge` (Codex) $\rightarrow$ điều phối subagent theo phạm vi 1-hop của Graphify, áp dụng TDD Red-Green-Refactor, truy vết bug theo đồ thị và xác thực build/test.
7. **Complete & Sync**: Xác thực đạt $\rightarrow$ review mã nguồn $\rightarrow$ tự động chạy `graphify update . --force` (với quy tắc loại trừ tệp AI và Graphify qua `.graphifyignore`) $\rightarrow$ chuyển trạng thái sang `complete`.

### 2. Quy trình Xử lý Lỗi (GSS-Bug Protocol)

```mermaid
flowchart LR
    Report["Báo cáo lỗi / Stack Trace"] --> Assess["1. bug-assess\n(Truy vết Call Chain + Blast Radius)"]
    Assess --> Contract[".specify/bugs/<slug>/assessment.md\n(Sơ đồ Mermaid + Điều kiện TDD)"]
    Contract --> Fix["2. bug-fix\n(Superpowers Systematic Debug)"]
    Fix --> TDD["3. Vòng lặp TDD\n(Viết test tái hiện lỗi -> Sửa code)"]
    TDD --> Sync["4. Xác thực & Auto Sync\n(Bằng chứng test + graphify update .)"]
    Sync --> Done[".specify/bugs/<slug>/fix.md"]
```

1. **Đánh giá lỗi (`/speckit-gss-bridge-bug-assess`)**:
   - Trích xuất ký hiệu và hàm crash trong stack trace đối chiếu với `graphify-out/graph.json`.
   - Lập bản đồ cuộc gọi, tính toán 1-hop callers và 2-hop blast radius.
   - Xuất sơ đồ Mermaid và điều kiện TDD bắt buộc vào `.specify/bugs/<slug>/assessment.md`.
2. **Khắc phục lỗi (`/speckit-gss-bridge-bug-fix`)**:
   - Guard chặn đứng việc sửa code nếu chưa có `assessment.md`.
   - Áp dụng `systematic-debugging` và `test-driven-development`: BẮT BUỘC viết regression test đỏ (thất bại) trước khi chạm vào mã nguồn sản phẩm.
   - Sửa mã nguồn tối thiểu trong ranh giới blast radius để test xanh (thành công).
   - Xác thực toàn bộ test suite, tự động cập nhật đồ thị (`graphify update .`) và ghi báo cáo `.specify/bugs/<slug>/fix.md`.

---

## Bảng so sánh năng lực

| Tiêu chí | Chỉ dùng `speckit.implement` | Chỉ dùng Superpowers | speckit-superpowers-bridge | **GSS-Bridge** |
|---|---|---|---|---|
| **Nguồn sự thật tài liệu** | `specs/` | Rời rạc ở `docs/plans/` | `specs/` | **`specs/` & `.specify/bugs/` chuẩn tuyệt đối** |
| **Đồ thị kiến trúc AST** | Không có (grep mù) | Không có (grep mù) | Không có (grep mù) | **Có: `graphify-out/graph.json`** |
| **Tính toán Blast Radius** | Không có | Không có | Không có | **Có: `graphify explain` + Sơ đồ Mermaid** |
| **Kiểm tra task song song `[P]`** | Đoán mò | Đoán mò | Đoán mò | **Có: Kiểm tra coupling bằng `graphify path`** |
| **Phân đợt Subagent song song** | Không có | Thủ công | Thủ công | **Có: Bộ điều phối sóng tự động (`dispatch-subagents`)** |
| **Chẩn đoán & Đánh giá Bug** | Quét file mù quáng | Debug ngẫu hứng | Không có | **Có: Định vị call chain bằng Graph (`bug-assess`)** |
| **Khắc phục Bug theo TDD** | Vá trực tiếp không test | TDD Red-Green | TDD Red-Green | **Có: Buộc TDD theo hợp đồng (`bug-fix`)** |
| **Khoanh vùng Subagent** | Nạp cả repository | Cả thư mục lớn | Cả thư mục lớn | **1–2 hop AST (Tiết kiệm token 50-100 lần)** |
| **Cắt gọt ngữ cảnh AST Skeleton** | Không có (file thô) | Không có | Không có | **Rút gọn AST (Tiết kiệm 70–80% token)** |
| **Phân tích tác động kiểm thử (TIA)** | Chạy cả bộ test | Chạy tất cả | Chạy tất cả | **Chỉ chạy test liên quan trong 1–2 bước đồ thị** |
| **Kiểm toán độ lệch kiến trúc & Chu trình** | Không có | Không có | Không có | **Thuật toán Tarjan SCC + Ranh giới phân tầng** |
| **Tự động tạo Repro Test Bug** | Thủ công | Thủ công | Thủ công | **Tự tổng hợp ca kiểm thử lỗi ban đầu (RED)** |
| **Bộ nhớ quyết định đa Agent** | Rời rạc | Không có | Không có | **Nhật ký chung (`.specify/agent-decisions.jsonl`)** |
| **Bảng điều khiển trực quan (Dashboard)** | Không có | Không có | Không có | **Dashboard HTML độc lập tại `.specify/dashboard/`** |
| **Cô lập đợt sóng bằng Git Worktree** | Không có (kẹt lock) | Không có | Không có | **Môi trường worktree độc lập (`.specify/worktrees/`)** |
| **Cảnh báo xung đột đa Agent** | Không có | Không có | Không có | **Có: Phát hiện xung đột tài nguyên song song** |
| **Đồng bộ đồ thị tự động** | Không có | Không có | Không có | **Có: Tự động chạy `graphify update .`** |
| **Hỗ trợ đa nền tảng Agent** | Một phần | Một phần | Claude + Codex | **Claude + Codex + Antigravity + Gemini** |

---

## Hướng dẫn CLI & Scripts

GSS-Bridge cung cấp đầy đủ script PowerShell 5.1+ và POSIX Bash tại `.specify/extensions/gss-bridge/scripts/`:

```powershell
# 1. Kiểm tra trạng thái và độ sẵn sàng hệ thống
.\.specify\extensions\gss-bridge\scripts\powershell\bridge-status.ps1 -Readiness

# 2. Tính toán Blast Radius và vẽ sơ đồ Mermaid
.\.specify\extensions\gss-bridge\scripts\powershell\graphify-blast-radius.ps1 -Node "AuthService" -Mermaid

# 3. Kiểm tra tính độc lập giữa 2 component trước khi đánh dấu [P]
.\.specify\extensions\gss-bridge\scripts\powershell\graphify-blast-radius.ps1 -CheckCoupling -NodeA "OrderService" -NodeB "PaymentService"

# 4. Phân nhóm các task [P] thành các đợt subagent song song kèm cắt gọt ngữ cảnh AST
.\.specify\extensions\gss-bridge\scripts\powershell\dispatch-subagents.ps1 -PruneContext -Actor claude

# 5. Trích xuất khung sườn AST để tiết kiệm token
.\.specify\extensions\gss-bridge\scripts\powershell\ast-skeleton.ps1 -Path "src/auth.py"

# 6. Lọc các bộ test bị ảnh hưởng theo cấu trúc đồ thị (TIA)
.\.specify\extensions\gss-bridge\scripts\powershell\test-impact.ps1 -Node "AuthService"

# 7. Kiểm toán độ lệch kiến trúc và phát hiện chu trình phụ thuộc vòng
.\.specify\extensions\gss-bridge\scripts\powershell\drift-detector.ps1 -FailOnCycles

# 8. Chẩn đoán lỗi bằng Graphify và tự động tạo test tái hiện (RED)
.\.specify\extensions\gss-bridge\scripts\powershell\bug-assess.ps1 -Slug "login-crash" -Symptom "NullReferenceException" -GenerateRepro -Actor antigravity

# 9. Khắc phục lỗi và hoàn tất quy trình TDD
.\.specify\extensions\gss-bridge\scripts\powershell\bug-fix.ps1 -Slug "login-crash" -Complete -RegressionTest "tests/repro/test_login-crash__repro.ps1" -VerificationEvidence "PASS" -Actor antigravity

# 10. Ghi và truy vấn nhật ký quyết định kiến trúc đa Agent
.\.specify\extensions\gss-bridge\scripts\powershell\log-decision.ps1 -Decision "Dùng AST skeleton" -Rationale "Tiết kiệm 70-80% tokens" -Topic "arch"
.\.specify\extensions\gss-bridge\scripts\powershell\log-decision.ps1 -Query "skeleton"

# 11. Tạo bảng điều khiển HTML tương tác trực quan
.\.specify\extensions\gss-bridge\scripts\powershell\generate-dashboard.ps1 -Open

# 12. Quản lý git worktree cô lập cho các đợt sóng subagent
.\.specify\extensions\gss-bridge\scripts\powershell\wave-worktree.ps1 -Action create -Name "wave-1"
.\.specify\extensions\gss-bridge\scripts\powershell\wave-worktree.ps1 -Action cleanup -Name "wave-1"
```

---

## Bộ kiểm thử tự động

Chạy toàn bộ test suites trên bất kỳ hệ điều hành nào:

```powershell
# Windows PowerShell (Toàn bộ 13 test suites)
pwsh -File tests/run-all.ps1

# Linux / macOS Bash (Toàn bộ 12 test suites)
bash tests/run-all.sh
```

Mọi bản phát hành đều được kiểm định tự động qua GitHub Actions trên cả 3 nền tảng Linux, Windows và macOS.

---

## Giấy phép (License)

Phát hành dưới [Giấy phép MIT](LICENSE).  
Bản quyền © 2026 GSS-Bridge Contributors.
