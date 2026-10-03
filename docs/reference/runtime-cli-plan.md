# Runtime CLI / IPC（Phase A++）
最後更新：2026-08-10

## 目標
- 讓「正在執行中的 FreeformHelper UI」可被外部命令查詢與常用流程操作。
- 提供 AI / 自動化可重現的 debug 介面，不依賴手動點 UI。

## 已實作命令（Phase A++）
- `freeformhelper.exe query help`
  - 回傳：所有支援命令、參數與範例。
- `freeformhelper.exe query status`
  - 回傳：CAD/Regular 數量、選取數、workflow snapshot、Notch preview 狀態。
  - 新增：`cache`（Step3 compensation cache + runtime notch query cache + Step5 export generation cache 的 revision / hit-miss / hit-rate）。
- `freeformhelper.exe query selection`
  - 回傳：selected CAD ids、selected regular indices、selected regular ids。
  - 新增：`timings`（summary / inspector / notchPreview / total，來自最新一次 selection pipeline）。
- `freeformhelper.exe query terminal [--tail N]`
  - 回傳：console log level、總行數、最後 N 行（預設 120）。
- `freeformhelper.exe query terminal-links [--tail N] [--limit M]`
  - 回傳：terminal 解析出的可點擊 links（line number / offset / length / target / isUrl）。
  - 用途：不進 UI 也可驗證 link parser 是否命中正確範圍。
- `freeformhelper.exe query pad --cad-id <id>`
  - 回傳：指定 CAD pad 幾何、diff/ic、match regular、Notch 2.2 比例。
  - 新增：`snapshot`（與 Pad Inspector 同源）與 `ruleTrace`。
- `freeformhelper.exe query pad --regular-id <id>`
  - 回傳：指定 Regular pad 幾何、diff/ic/freeform、match CAD。
  - 新增：`snapshot`（與 Pad Inspector 同源）與 `ruleTrace`。
- `freeformhelper.exe query notch --cad-id <id> [--limit N] [--target-limit T] [--polygon-limit M]`
  - 回傳：Notch 2.2 比例、每個 regular 的 To Full debug 拆解（overlap/source/blocker/reachable）、
    seed/final polygon bounds（可限量）。
  - 新增：`stage3Allocation`（`cadArea/stage3Area` 與各 target diff 的 `area/ratio` 對照表）。
  - 新增：revision-based cache（key=`step3Revision + cadId`），同 revision 重複查詢不重算 compensation。
- `freeformhelper.exe query multi-owner --cad-id <id> [--limit N] [--overlap-percent P]`
  - 回傳：`GATE_MULTI_OWNER` 與 `owner>1` regular 清單、每格 owner CAD ids。
  - `--overlap-percent` 可覆寫嚴格 overlap 門檻（只影響本次 query，方便比較 0.1% vs 1%）。
- `freeformhelper.exe query notch-stage --cad-id <id> [--polygon-limit M]`
  - 回傳：Notch 2.2 Stage overlay（stage1 seed / stage2 candidate / stage3 final）多邊形資料，
    可直接對照 AA 畫面分階段檢查。
- `freeformhelper.exe query notch-validation --regular-id <id>`
  - 回傳：Step 5 Validation 的 regular-centric rows（direct / incoming / outgoing）。
  - 每列 row 內容走 typed-first（`NotchTableRow.V22Node`），若缺少 typed payload 會 fallback 解碼 `Values[]`。
  - 與 Step5 UI 共用同一個 trace mapper（`NotchValidationTraceService`），避免 UI/CLI 規則分叉。
- `freeformhelper.exe query load-project --path <project.json>`
  - 在已啟動 UI instance 載入專案（含 timing 回傳）。
  - timing 新增 `stepReplay`，可看到 load 後 Step1/Step2 自動重播是否發生與耗時。
- `freeformhelper.exe query run-step --step <1|2|3|4>`
  - 觸發 Step1 Match / Step2 Freeform detect / Step3 Notch preview refresh / Step4 Index diagnostics。
  - 實作改為走 VM 單一 step 執行入口（與 UI 按鈕同源），避免 CLI/UI 行為分歧。
- `freeformhelper.exe query clear-step --step <1|2|3|4|5>`
  - 清除指定步驟結果（會連動清除 downstream）。
  - 實作改為走 VM 單一 step 清除入口（與 UI clear icon 同源）。
- `freeformhelper.exe query select-cad --cad-id <id>` 或 `--cad-ids a,b,c`
  - 程式化選取 CAD pad（更新畫面 selection/inspector 上下文）。
- `freeformhelper.exe query select-regular --regular-id <id>` / `--regular-ids ...`
  - 程式化選取 Regular pad（亦支援 `--regular-index` / `--regular-indices`）。
- `freeformhelper.exe query clear-selection`
  - 清除目前選取。
- `freeformhelper.exe query set-tofull --enable <true|false>`
  - 切換 Notch 2.2 To Full 開關（與 UI 設定同源）。
- `freeformhelper.exe query export-notch --format <csv|c-v21|c-v22> --path <outputPath>`
  - `csv` 表示 CSV review artifact，不是 FW direct-import contract；`.c` 才是 FW 直接導入格式。
  - 非互動執行 Step 5 匯出（直接輸出檔案，不經 save dialog / row selection 視窗）。
  - 內部仍走既有 Step 5 生成流程與 cache，若前置未滿足會回錯誤。
  - 新增：`elapsedMs`（UI 內部實際匯出耗時）。
- `freeformhelper.exe query simulation [--regular-id <id>]`
  - 回傳目前 `Simulation` 工作頁的 workspace 狀態。
  - 若指定 `--regular-id`，額外回傳該 regular pad 的 `before/after/delta`、active-surface 狀態與 notch impact 摘要。
  - `workspace.safety.hasSimulationSafetyViolations`、`simulationSafetyViolationCount`，以及指定 `--regular-id` 時的 `regular.isEmsSafetyRisk`，皆由同一 current `SimulationSafetyAuditResult` 與 Application EMS after-cap predicate（`afterValue > afterCap + 1e-9`）投影；同一 snapshot 必須一致。欄位名稱、payload schema 與命令參數不變。

## 命令列參數
- 通用參數：
  - `--timeout-ms <ms>`：IPC 連線 timeout（預設 1500，範圍 1~120000）。
  - `--json-pretty` / `--json-compact`：輸出格式（預設 pretty）。
- `query terminal`：
  - `--tail <N>`：回傳最後 N 行（範圍 1~5000）。
- `query terminal-links`：
  - `--tail <N>`：掃描最後 N 行（範圍 1~5000）。
  - `--limit <M>`：最多回傳 M 個 link（範圍 1~2000，預設 200）。
- `query pad`：
  - 二選一：`--cad-id <id>` 或 `--regular-id <id>`。
- `query notch`：
  - `--cad-id <id>`：必填。
  - `--limit <N>`：回傳 regular debug 筆數（預設 200，範圍 1~5000）。
  - `--target-limit <T>`：回傳 stage3 target diff 筆數（預設 120，範圍 1~5000）。
  - `--polygon-limit <M>`：回傳 seed/final polygon bounds 筆數（預設 64，範圍 1~2000）。
- `query multi-owner`：
  - `--cad-id <id>`：必填。
  - `--limit <N>`：回傳 regular 筆數（預設 200，範圍 1~5000）。
  - `--overlap-percent <P>`：可選，範圍 0~100，覆寫 strict owner overlap 門檻（%）。
- `query notch-stage`：
  - `--cad-id <id>`：必填。
  - `--polygon-limit <M>`：回傳每階段 polygon 筆數上限（預設 64，範圍 1~2000）。
- `query notch-validation`：
  - `--regular-id <id>`：必填。
- `query load-project`：
  - `--path <project.json>`：必填。
- `query run-step` / `query clear-step`：
  - `--step <N>`：必填。
- `query select-cad`：
  - `--cad-id <id>` 或 `--cad-ids <a,b,c>`。
- `query select-regular`：
  - `--regular-id <id>` / `--regular-ids <a,b,c>` / `--regular-index <idx>` / `--regular-indices <a,b,c>`。
- `query set-tofull`：
  - `--enable true|false|1|0|on|off`。
- `query export-notch`：
  - `--format csv|c-v21|c-v22`：匯出格式（必填）；`csv` = CSV review artifact，`c-v21/c-v22` = FW C contract。
  - `--path <outputPath>`：輸出檔案路徑（必填，支援相對/絕對路徑）。
- `query simulation`：
  - `--regular-id <id>`：可選，查詢目前 Simulation workspace 的指定 regular pad。

## AI 快速操作建議（實務）
1. 先確認 UI instance 在執行中（否則會回 `INSTANCE_NOT_RUNNING`）。
2. 常見完整流程（建議腳本）：
   - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/runtime/runtime-quick.ps1 -Project <project.json> -CadId <id>`
   - 腳本會依序做：`load-project` → wait-ready → `run-step 1/2/3(/4)`（step3 前自動選取 CAD）→ `query notch-stage` → `query notch` → `query status`。
   - 內建 transient retry（`STEP_NOT_READY` / `NOT_READY` / `IPC_*`），可降低 load/step 時序衝突。
3. 手動命令最小組合（不走腳本）：
   - `freeformhelper.exe query load-project --path <project.json>`
   - `freeformhelper.exe query run-step --step 1`
   - `freeformhelper.exe query run-step --step 2`
   - `freeformhelper.exe query terminal-links --tail 300 --limit 300`
   - `freeformhelper.exe query select-cad --cad-id <id>`
   - `freeformhelper.exe query run-step --step 3`
   - `freeformhelper.exe query run-step --step 4`
   - `freeformhelper.exe query notch-stage --cad-id <id>`
   - `freeformhelper.exe query notch --cad-id <id>`
   - `freeformhelper.exe query notch-validation --regular-id <id>`
   - `freeformhelper.exe query simulation --regular-id <id>`
   - `freeformhelper.exe query export-notch --format csv --path build/perf/notch_review.csv`
   - `freeformhelper.exe query export-notch --format c-v21 --path build/perf/notch_v2.1.c`
   - `freeformhelper.exe query export-notch --format c-v22 --path build/perf/notch_v2.2.c`
   - `freeformhelper.exe query multi-owner --cad-id <id> --overlap-percent 1.0`
4. 常見陷阱：
   - `run-step --step 3` 需要先有 selected CAD，否則回 `STEP_NOT_READY`。
   - `run-step --step 4` 需要 Step1 結果；尚未 Match 會回 `STEP_NOT_READY`。
   - 請優先比對 `query notch-stage` 的 stage1/2/3 polygon，再看 `query notch` 的 per-regular 規則診斷。

## 3635 V21/V22 exact export gate

- 前置：先關閉既有 `FreeformHelper.UI` instance；exact gate 自行啟動 hidden UI，並以暫時 app-general-settings path 隔離個人 DXF import/view preference。
- 入口：`./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi`；只有 apphost executable 已存在時才加 `-SkipBuild`。
- lifecycle：`load-project` 後依序呼叫 `run-step 1~4`，驗證正式 workflow entry points 仍可重建完整輸出 state。
- production single entry：UI Step5 直接進 `ExportNotchCommand`；Runtime Query 經 `RuntimeQueryUseCase -> ExportNotchCommand`，之後兩者共用 generation/export/write path。CLI 不直接呼叫 Application exporter，也不建立第二套 production workflow entry。
- `c-v21`／`c-v22` 只 pin 最後的 selection/export target，不建立第二套 generation algorithm。CLI adapter 不會為指定 format 覆寫 VM 的 enabled versions 或 export profile；但共同 Step5 command 仍依既有契約把 live UI state 同步至 in-memory project settings，可能標記 `HasUnsavedChanges`。因此下述四欄 invariant 不能延伸解讀成整份 `ProjectSettings`、status 或 cache metrics 均不變。
- 結果：產生 `notch_v2.1.c` / `notch_v2.2.c`，比較分成兩層：
  1. diagnostic compare：把 CRLF、LF、lone CR 統一為 LF，每側最多移除一個 terminal LF，再做 ordinal compare；不 trim space/tab/comment，也不忽略 row ordering，失敗時回報 first-diff line/column/code point。
  2. hard byte gate：actual raw bytes、SHA-256 與 node macro 必須命中 signed manifest；所以最終 gate 不容許 encoding、newline 或任何 byte 漂移。
- executable provenance 的唯一 machine-readable owner 是 `example/BOE36.35/notch_export_golden_manifest.json`；它鎖住 project、mask、兩份 checked-in C、node/bytes/SHA 與 actual output。人工簽核、完整數值與環境見 `docs/performance/regression-baseline-3635.md`。
- 失敗：actual C 保留在 `OutDir`；錯誤訊息列出第一個差異的 line/column、expected/actual code point 與 actual/golden path。Gate 不會覆蓋 checked-in golden。
- lifecycle：啟動前拒絕任何既有 `FreeformHelper.UI`／`dotnet ... FreeformHelper.UI` process，啟動後以 `status.processId` 驗證 IPC server 正是 managed PID；結束時只停止 gate 自己啟動且 executable path 已驗證的 UI process。
- state invariant：`status.notchExportState` 鎖住 `EnableV21`、`EnableV22`、file type、profile；連續 export 前後四者必須完全相同。
- side effects：query 會建立 output directory、寫入或覆寫指定 output file、更新 export status/progress/summary，並 lookup/store generation cache；file type 與 selection delegates 是 transient adapter，必須在 `finally` 還原。
- `-SkipGoldenCheck` 只供非 gate 的自訂效能實驗；VM、IPC 或 exporter slice 不得使用此參數作驗收。
- version state/invalidation slice 必須再以 `-ReverseCExportOrder` 驗證 V22→V21；預設 V21→V22 與反向順序都要 exact。

## IPC 協定（v1）
- Transport：Windows Named Pipe（`freeformhelper.runtime.v1`）。
- Request（單行 JSON）：
  - `{ "version":"1", "command":"status", "args":{...} }`
- Response（單行 JSON）：
  - 成功：`{ "ok": true, "data": { ... } }`
  - 失敗：`{ "ok": false, "error": { "code":"...", "message":"..." } }`

## 錯誤碼（目前）
- `INSTANCE_NOT_RUNNING`：沒有執行中的 UI instance 可回應。
- `INVALID_ARGUMENTS`：命令列參數格式錯誤。
- `UNKNOWN_COMMAND`：不支援的 query command。
- `PAD_NOT_FOUND`：指定 pad 在目前可視資料中不存在。
- `NOT_READY`：請先完成必要前置（例如 grid 尚未建立）。
- `STEP_NOT_READY`：指定步驟執行後仍不滿足可用狀態（通常是前置條件不足）。
- `STEP_EXECUTION_FAILED`：步驟執行期間例外。
- `IPC_IO_ERROR` / `IPC_ERROR`：IPC 傳輸或執行錯誤。
- UI-side command執行若拋出未處理例外，Named Pipe server必須回單行`IPC_ERROR` failure envelope並保留原message；不能以斷線／`EMPTY_RESPONSE`取代協定回應。Lifecycle cancellation仍直接結束server，不轉成錯誤回應。
- `IPC_TIMEOUT`：已連上執行中的 UI instance，但 query 在 `--timeout-ms` 上限內未完成。
- `IPC_REQUEST_TIMEOUT`：client 連上 IPC server 後沒有送出完整 request，server 已主動中止該連線。

## 架構
- `Program`：判斷是否 `query` 模式，query 走 IPC client，否則啟動 UI。
- `RuntimeQueryIpcServer`：UI process 內 Named Pipe server。
- `RuntimeQueryUseCase`：將 query 映射為 ViewModel snapshot / 常用 workflow action。
- `FreeformHelperViewModel.GetWorkflowStateSnapshot()`：提供 workflow gate 狀態給 CLI。
- `R13.102b-2` 後，`query multi-owner` 先確認CAD仍在visible collection，再只取得一份由current setting或`--overlap-percent` override決定的current-revision `NotchV22ResolvedResult`；輕量Inspector snapshot只供既有CAD response metadata，所有multi-owner evidence均投影`resolved.Compensation.RegularDebugInfos`。CLI arguments、payload schema、ordering、limit/truncation與error codes不變。
- `R13.102a-2b-2` 後，normal `CadAllocation` generation cache保存compact output-request-neutral candidate batch；同一 `ExportNotchCommand` 的matching request以當下enabled versions、threshold、`NullValue`與target coverage guard/cap做final projection，再由當下profile／file type serialization。UI與Runtime export仍共用同一command／write path；batch request可攜帶目前單一選取CAD的resolved result，warm時reuse exact instance，cold時至多回傳一份並在currentness驗證後promotion回既有per-CAD owner。batch不保存其餘candidate的完整polygons/debug evidence；`LegacyRegularAnchor`仍維持request-specific table cache。R13.101e另讓normal generator／UI compensation都只執行完整`NotchV22CompensationContext`，舊multi-parameter API僅為compatibility adapter；Runtime command、payload schema與final-output identity不變。R13.101c-2／R13.102／R13.102a-2／R13.103依各自exit criteria保持open。
- IPC schema未變：`status.cache.exportGenerationCache.entryRowCount`在batch-backed `CadAllocation` entry上表示最新一次projected table row count，而非candidate count或batch大小；因此零列projection可同時呈現`hasEntry=true`與`entryRowCount=0`。Warm cache hit只執行／回報phase 4，不捏造phase 1～3工作。

