# Repo Refactor Scan（2026-02-26）

## 掃描範圍
- 分支：`refactor-code-reduction-pass1`
- 掃描檔案：`src/`、`tests/`、`docs/`（排除 `bin/obj/build`）
- 檔案數（tracked）：`408`

## 量化結果（本輪）
1. 行數熱點（前 10）
   - `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.cs`：1493
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs`：1483
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.cs`：1373
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`：1352
   - `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs`：1083
   - `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`：1040
   - `src/FreeformHelper.UI/Views/FreeformHelperView.axaml.cs`：944
   - `src/FreeformHelper.UI/Views/FreeformHelperView.Console.cs`：914
   - `src/FreeformHelper.UI/Services/ManualSizingService.cs`：899
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`：868
2. Lint/analyzer（`./scripts/tests/lint.ps1 -AllFiles`）
   - 總 warning：`74`
   - `CA1822`：72（大量 stateless service / helper 可轉 static）
   - `CA1865`：2（`StartsWith(string)` 單字元用法）
3. 同步/關閉路徑風險
   - `src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs` 仍有 `_runLoopTask?.Wait(...)`，存在關閉時阻塞風險。
4. 文件同步問題
   - `docs/reference/behavior-inventory.md` 尚有已失效敘述（例如 `ShowHomeCommand`）。

## 重構機會（按優先順序）

### P0（先做）
1. **Selection/Inspector 低延遲化第二輪**
   - 目標：降低框選起手延遲（先更新 selection summary，inspector 走背景批次，並在 drag 期間禁重工作業）。
   - 入口：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.cs`
2. **IPC 關閉流程去阻塞**
   - 目標：把 `_runLoopTask?.Wait(...)` 改為 async stop（含 timeout/cancel），避免 UI close 卡住。
   - 入口：`src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`
3. **Analyzer 清債（先清 CA1822/CA1865）**
   - 目標：把 stateless 成員標示為 `static`，修正單字元 `StartsWith`，降低噪音 warning。
   - 入口：`src/FreeformHelper.UI/Services/*.cs`、`src/FreeformHelper.Application/**/*.cs`

### P1（流程/品質）
1. **Lint gate 擴大到 solution 層**
   - 目前 lint 僅 build `FreeformHelper.UI.csproj`；建議新增「全解決方案 analyzer gate」模式，避免跨專案品質債累積。
   - 入口：`scripts/tests/lint.ps1`、`scripts/tests/run-refactor-gate.ps1`
2. **測試命名規則策略化**
   - `CA1707`（底線命名）在 tests 比例高；決策需二選一：
     - A. 逐步改名
     - B. 僅在 tests 專案調整規則（保留可讀性）

### P2（架構可維護性）
1. **超大檔案再拆分**
   - `RuntimeQueryUseCase`、`FreeformHelperViewModel.Settings/State/Operations`、`PadCanvas.Rendering` 仍超大。
   - 建議按 command domain / setting domain / render layer 再分模組，持續保持 single-entry。
2. **Notch/Inspector 重算策略文件化**
   - 補齊「何時快取命中、何時強制重算、何時只更新 overlay」決策表（供 UI/CLI 共用）。

### UI 顯示（持續優化）
1. **文字溢出守門**
   - 新增 layout guard：關鍵資訊欄（Settings、Inspector、Notch export rows）必須換行且不超出容器。
2. **重點層級 token 化**
   - 把「結果/原因/次要資訊」顏色與字重收斂為 token，避免區塊視覺規則漂移。

## 驗證與門檻
- Build：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
- Lint：`./scripts/tests/lint.ps1`（必要時 `-AllFiles`）
- 回歸：`scripts/tests/run-tests.ps1 -Group application`
- 回歸：`scripts/tests/run-tests.ps1 -Group ui-core`
- 回歸：`scripts/tests/run-tests.ps1 -Group smoke`
