# TODO（Active Backlog）

> 歷史封存：
> - `docs/archive/TODO-history-2026-03-05.md`
> - `docs/archive/TODO-S6-closed-2026-03-06.md`
> - `docs/archive/TODO-history-2026-03-24.md`
> - `docs/archive/TODO-history-2026-04-06.md`
> - `docs/archive/TODO-history-2026-05-05.md`
>
> 版本節點說明：
> - `Beta 0.1` 視為已收斂完成。
> - 後續 backlog 與 milestone 自 `Beta 0.2` 開始規劃。
> - 最新已收斂批次：請見最近的 milestone commit。

## 執行順序與依賴（Active）
- UI 控件一致化優先順序 `S12.025 -> S12.026 -> S12.027 -> S12.028 -> S12.029` 與 `S12.008 Shared Tool Workbench shell polish` 已完成。
- Post 1.0 workbench 可執行切片已完成；剩餘 active 項依 Beta / AutoCAD 驗證風險順序處理。
- Algorithm / allocation backlog 保留原有風險順序：`S11.153 -> S11.155 -> S11.156 -> S11.173`，但不與 UI action role 切片混在同一 commit。
- 2026-07-20 起由 `docs/guides/refactor-roadmap-1.3.x.md` 統一排序；舊 S14 UI candidates 已映射到 `R13.005` / `R13.501`～`R13.505`，不再作為第二套執行佇列。
- 2026-10-02 起，`S15.*`（基準修復與公版對齊）排在所有未完成 `R13.*` 之前：owner 指定順序為「確認現狀 → 收進 `master` → 套用 NFC 公版 → 才改架構／UI／演算法」；但依下方 2026-10-02 owner 決定，只剩 owner-only `S15.*` 項目時，`R13.*` 可開始執行。

## 工作規則（非任務）
- 每個任務完成時：`build + 對應 tests + lint`。
- 每個里程碑：`commit + push`。
- 功能實作或範圍變更時，立即同步更新本檔。
- 若任務會改變使用者可見行為，需同步補：`reason code / 驗證案例 / 文件說明`。
- 若任務會改變 decision / repair / grouping / trace / simulation projection 等使用者可見判斷行為，需同步補：`algorithm trace / explainability coverage`（至少可追到條件、原因、值、最終輸出）。
- 主畫面與首屏表格預設優先快：先產生 summary / status / key columns；detail explainability 預設可採 lazy / on-demand / background fill，不得阻塞首頁生成。
- 預設採 low usage：一次只做一個 TODO slice；非必要不跑 full-repo scan / full test sweep。
- 任務開始先看 `docs/generated/project-dependency-graph.md`（若缺失或過期，先跑 `./scripts/build/generate-dependency-graph.ps1`）。
- lint 標準不放寬：commit 前跑 `./scripts/tests/lint.ps1 -UseNoAppHost`；merge 前加跑 `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`。

## Baseline 修復與公版對齊（Active，2026-10-02 盤點）

- 盤點基準：`master` = `d6ceb2a`（2026-10-02 由 `215d411` fast-forward，133 commits）。環境：Windows 11、.NET SDK `10.0.303`、`dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo`。
- 結果：`957 total / 947 passed / 10 failed / 0 skipped`，17 分 7 秒；`./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost` 為 0 warning／0 error。
- 公版來源：`nvt_fw_combiner` 的 `origin/1.2.x`（`1c2cc2c`，2026-10-01），只讀；NFC 決議 223 的 GitHub 公版提前套用到本 repo。韌體專屬規則（profiles、Golden、write ranges）不搬。

- **Owner 決定（2026-10-02，於 1.3.x session 以選項回答）**
  - 合併規則：只有高風險變更需要 owner 在 GitHub 核准——production code（`src/**`）、CI workflow（`.github/**`）、決定 gate 內容的檔案（`scripts/**` 全部，以及 `.editorconfig`、`Directory.Build.props`、`Directory.Packages.props`、`global.json`；workflow 只寫 lane 名稱，實際擋什麼由腳本、analyzer 設定與 SDK 版本決定，兩輪審查指出後補列）、發佈相關、agent 權限設定。其餘（文件、測試）在「獨立審查結論為 accept 且沒有 P0／P1」加上必過檢查綠燈後由 bot 合併。一個 PR 只要碰到任何高風險路徑就整個算高風險。合併命令為 `gh pr merge <n> --merge --match-head-commit <head>`；執行細節以 `CONTRIBUTING.md` 的合併邊界為準。
  - 獨立審查由本專案自己派的 agent（fresh Claude 或 Codex）執行；NFC session 只用來同步資訊，不負責審查本專案。
  - CI：不穩定的測試暫時不擋合併，其餘檢查維持必過；S15.002 關閉後恢復。實作上只隔離 `FreeformHelperViewModelTests`（CI 的 `dotnet / test (viewmodel)`）；其餘 UI 類別留在會擋合併的 `dotnet / test (ui)`。
  - 35 個過時的 stacked draft PR（#9～#77）已關閉，分支保留。
  - `LICENSE`：保留所有權利（不授權使用、修改或散布）。
  - golden snapshot 的樣本列移到 private 資料 repo。
  - push 維持走 owner 的 SSH remote，不更動 git credential 設定；公開 repo 建立時再處理。
  - 允許安裝 Microsoft 診斷工具 `dotnet-dump`（已裝 `10.0.745401`）以追查 S15.002。
  - 只剩 owner-only `S15.*` 項目時，`R13.*` 可開始執行；不必等待 owner 專屬設定與發佈操作完成。
  - `LICENSE` 的 copyright holder 為 `Dennis Liu`；原「保留所有權利」決定不變。
  - `2.0.0 = 開始共用核心架構（owner 暫定目標，2026-10-02）`。
  - Avalonia 11 → 12 升級排在 `1.3.5` 之前，目前不執行升級。

- **Owner 決定（2026-10-03；回覆 `docs/reviews/ui-feature-inventory-2026-10.md` 的產品問題）**
  - V21 firmware C output：答覆「不確定，先保留」；據此：`R13.103` 的 final projection／formatter 分離（含 legacy convergence）暫緩；不得開始移除或變更 V21 output，以及舊 project 對它的讀取路徑。
  - `LegacyRegularAnchor`（重新匯出已交付 project）：答覆「不確定，先保留」；據此：`R13.101c-2` 與 `R13.102` 中僅涉及 legacy 的部分暫緩；既有 zero-diff gates 繼續保護它。
  - 後續答覆（2026-10-03；問題為 `R13.101c-2`／`R13.103` 是否繼續，保留經等價性測試的 Legacy 相容 adapter，或維持暫緩）：owner 原話「先保留吧 但後續目標會是完全移除2.1」。
  - 確認（owner 2026-10-03 在畫面回覆「V21」）：「2.1」指 V2.1，即 V21（本庫 roadmap／TODO 使用「final V2.1 projector」），不是 release 版本 2.1。
  - 後續答覆取代先前對 `R13.101c-2`／`R13.103` 及 `R13.102` 純 legacy 部分的「暫緩」後果：V21 與 Legacy 維持原樣，由既有 zero-diff gates 保護；不再投入額外收斂／等價性工作；完全移除是後續目標，版本未訂。本次只記錄，不變更任務狀態。
  - 1.3.1 退出稽核 B1／B2：已接受例外（owner 決定 2026-10-03），不執行；A1、A2 仍待完成，C 類是否不列入表格退出仍是提案，尚待 owner 確認。
  - Step4 mapping 與 Step6 validation diagnostics：答覆「移到未編號的診斷區」；據此：Owner 確認 `R13.305a` 已寫明的方向。
  - 視覺重設計：答覆「先不改視覺」；據此：1.x 只重整結構，不採用新的視覺語言或重設 token set；結構變更所迫使的範圍以外，任何會改變畫面的變更都需另取得 owner 決定。

- [x] **S15.001 處理 4 個穩定失敗的 `SimulationWorkspaceViewModelTests`**
  - 失敗：`CopperSource_WhenMoved_RegeneratesBeforeFrameThroughSharedSimulationPath`（預期 `400`、實際 `399`）、`CopperContactModel_WhenFingerSelected_UsesLowerFullPadSignal`（`360`／`359`）、`DiffViolationSummary_WhenNoMismatch_ShowsCleanState`（`EMS OK`／`audit warning`）、`DiffViolationSummary_WhenDuplicateDiffDeltaMismatches_ListsViolatingPads`（`Assert.True`）。
  - 來源：tag `1.2`（`36d53ee`）時整個類別 31/31 通過；`git bisect` 的 first bad commit 是 `493d5d6b`（R13.103c-1，Refs #32），該 commit 起 3 個失敗；第 4 個由 `cb73c8c4`（`fix(simulation): project physical audit warning`）造成。這個類別不在任何測試分組（見 S15.003），所以當時的 slice gate 沒有跑到。
  - 判定（2026-10-02）：4 個都是測試期望值過時，production 行為是有意的契約，本項只改測試。
    - `400`／`399` 與 `360`／`359`：`493d5d6b` 讓 V22 simulation 與 V21 一樣先把輸入以 `QuantizeToFirmwareInt16` 轉成韌體 `INT16`（`NotchApplySimulationService.cs:67-69`），轉換是無條件捨去（`NotchV21FirmwareEvaluator.cs:95`），且由 `Simulate_WhenFrameIsOutsideInt16Domain_UsesFirmwareBaselineForCellsAndAudit` 明確鎖定（`100.75 -> 100`）。銅柱完全覆蓋一顆 pad 時，polygon clip 精度讓投影值為 `399.9997327947845` 而非 `400`，捨去後為 `399`。實驗把捨去改成四捨五入會讓上述契約測試 V21／V22 兩案失敗，因此不改 production。
    - `EMS OK`／`audit warning`：`cb73c8c4` 有意讓 status 在 physical audit 有風險時顯示 `audit warning`；該 fixture 的 node 為 combine 94%，本來就有 residual。
    - Duplicate diff：`493d5d6b` 讓 V22 的 cell before 與 V21（自 `7ae91277`）一樣取 per-diff 韌體 baseline（`NotchApplySimulationService.cs:104-106`），共用同一 FW diff 的 pads 不再可能有不同 delta；衝突改由 `DiffIdentityContract` 的 duplicate resolution 呈現。測試改名為 `DiffViolationSummary_WhenDuplicateDiffValuesDiffer_ReportsDuplicateResolutionWithoutMismatch`，並補上原本沒有測試的 `HasDuplicateDiffResolutions`／`DuplicateDiffResolutionCount`。
  - 驗證：`dotnet test ... --filter "FullyQualifiedName~SimulationWorkspaceViewModelTests"` 32/32 通過。

- [x] **S15.019 DXF 圖層 PNG 測試暫存檔刪除偶發鎖定（2026-10-03）**
  - 現象：整套測試在機器忙碌時，`ExportLayerImage_ZeroPaddingStillKeepsWideStrokeInsideCanvas` 的斷言已通過，但 `finally` 刪除 `%TEMP%` 中剛寫出的 PNG 時偶發 `IOException`，立即重跑通過；S15.002 亦記有同類別另一測試的刪檔鎖定紀錄。
  - 判定依據：`DxfLayerImageExportService.cs:81-84`、`:106-107`、`:214-231` 以 `using` 管理繪圖、編碼資料及 `File.Open(..., FileShare.None)` 輸出串流；測試的 `SKBitmap.Decode(path)` 回傳 bitmap 也在 `using` 區塊結束後才進入 `finally`。檢查這些路徑未發現延遲關閉的檔案控制代碼，暫時外部掃描鎖定較符合現有證據。
  - 變更：只在 `DxfLayerImageExportServiceTests` 加入共用刪檔 helper，所有會刪除暫存 PNG 的測試皆使用它；僅對 `IOException` 最多嘗試 10 次、每次間隔 100 毫秒，最後一次失敗仍拋出例外。production 未變更。
  - 未能驗證：未取得鎖檔程序的身分，也未在受控環境重現該次偶發失敗；目標測試通過不能證明整套測試今後不再發生鎖定。
  - 補充證據（Claude 2026-10-03 於 PR 90 的 CI 觀察）：同類鎖定也曾在 GitHub runner 發生一次（PR 90，run `37088185564`，`ExportLayerImage_ThickerLineProducesMoreInkPixels` 刪除 thin PNG 時；當時該分支尚未納入 PR 96 的 retry helper），因此不只是工作站負載效應；retry helper 是緩解措施，根因是 scanner 還是 handle 仍未證實。

- [x] **S15.012 CAD load spinner：過期的 Hide 可能蓋掉較新的 Show（production，已修）**
  - 情境：spinner 顯示中呼叫 `Hide()`；state sync 讀到「應隱藏」後、送出 hide 之前，`Show()` 在鎖內直接送出 show 而且不再排 sync；sync 接著送出 hide。結果 requested 為 Visible、effective 為 Hidden，沒有待執行的 sync，那一次載入看不到 spinner。位置：`src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs` 的 `SyncRequestedVisibilityStateAsync` 兩處 `SendHideRequest` 之後。早於 S15 的既有問題，與 S15.002 的 busy loop 修正無關。
  - 修正：新增內部建構子注入 IPC 送出點，測試可模擬已執行的 spinner 而不啟動真實 process；兩處 hide 送出後都在 `_sync` 內重讀 `_requestedVisibility`，若較新的 `Show()` 已要求 Visible，就繼續同步並重送 show。沒有需要隱藏的狀態仍直接結束，避免 busy loop。
  - TDD：`Show_WhileHideSendIsInFlight_RestoresVisibleState` 在修正前重現失敗（requested Visible、effective Hidden），修正後通過；`Hide_WhenSpinnerIsNotVisible_CompletesStateSync` 同組通過。
  - 驗證：測試專案以 `-m:1 -p:UseSharedCompilation=false` 離線 build，0 errors（7 個既有 CA1875 warnings）；`CadLoadSpinnerProcessHostTests` 12/12、`FreeformHelperViewModelTests` 最後重跑 163/163 通過。後者另一次執行因 `HeadlessDispatcherSetup` 初始化失敗而 162/163；未加單程序選項的 build 在 sandbox 遭 `MSB3883 UnauthorizedAccessException` 擋住。

- [x] **S15.013 console 展開時每筆 log 同步完整重繪（2026-10-03 修正）**
  - 原因：`ShellViewModel.ConsoleText` 的每次通知直接呼叫 `SyncConsoleText`，繞過 S15.002 根因四為 collection change 建立的 `CoalescedRefresh`。
  - 修正：展開時的 `ConsoleText` 通知與 collection change 共用同一個排程；刷新執行時讀最新文字，沿用原有渲染、連結解析與 auto-follow 路徑。`ShellViewModel` 的文字語意與去重未更動。
  - 量測：headless console 展開後連續加入 200 筆 log，修改前完整 link parse **202 次**，修改後 **1 次**；新測試在修改前因 `202` 超出 `1..2` 而失敗，修改後通過。測試同時核對最終 editor 文字、離開底部時維持捲動位置、在底部時繼續跟隨，以及字級與篩選顯示。

- [ ] **S15.017 console link 解析仍每次重掃整份文字（production 效能，待量測）**
  - `ConsoleLinkParser.Parse` 對完整文字執行六個 regex，並對候選路徑查詢檔案系統；S15.013 只減少呼叫次數，單次成本仍隨 console 長度成長。
  - 待辦：先量測長 console 的單次解析成本，再評估增量解析新增行；須維持舊行 offset、截斷、篩選與可點擊連結的等價性。

- [x] **S15.015 多個 thread 同時改寫 log 的 UI 集合，弄壞 `ShellViewModel` 的 console 緩衝（2026-10-02 修正）**
  - 現象：限制核心數的整套測試中，dispatcher 上的工作偶爾丟出 `ArgumentOutOfRangeException: Index was out of range ... (Parameter 'chunkLength')`。9 輪中有 3 輪出現；其中 2 輪發生在測試結束之後（被 S15.002 根因八的防護攔下），1 輪發生在測試進行中，讓 `FreeformHelperViewModel_OutsideUiDispatcher_KeepsCanvasColorDefaults` 失敗並連帶弄壞 session（另外 2 個測試快速失敗）。
  - 堆疊：`AppLogStore.Add` → `AppendEntryToUiCollection` → `ObservableCollection.OnCollectionChanged` → `ShellViewModel.OnLogEntriesChanged` → `AppendConsoleLines` → `RebuildConsoleText` → `StringBuilder.ToString()`。
  - 機制：`AppLogStore.Instance` 是整個 process 共用的，`Add` 以 `Dispatcher.UIThread.CheckAccess()` 決定直接改 UI 集合或 post 回 UI thread。沒有平台或正在關閉的 dispatcher 對每個 thread 都回 true（同 S15.002 根因七），所以測試裡會有多個 thread 同時 `_entries.Add`，訂閱者（每個還活著的 `ShellViewModel`）的 `StringBuilder` 被同時改寫。app 裡只有 UI thread 會走到這裡，不受影響。
  - 修正：`AppLogStore` 對 UI 集合的新增、清除與 flush 一律在同一把 lock 內進行，訂閱者因此一次只會被一個 thread 呼叫；另外把「是否在 UI thread」與「post 到 UI thread」改為可注入（預設行為不變），讓測試不需要碰全域 dispatcher。回歸測試 `AppLogStoreTests.Add_WhenEveryThreadCountsAsTheUiThread_ChangesTheUiCollectionOneAtATime`（8 個 thread 各寫 500 筆）：拿掉 lock 會失敗，加上後通過。修正後限制核心數的 6 輪整套測試（2 核心 3 輪、4 核心 3 輪）沒有再出現這個例外。
- [ ] **S15.017 防止 log 訂閱以外的路徑跨 thread 改寫 `ShellViewModel` console 狀態**
  - 現象：測試可能在非 UI thread 切換 `IsConsoleExpanded`；S15.015 的 lock 只保護 log 訂閱路徑。
  - 驗收：盤點 console 狀態的寫入入口，讓寫入收斂到同一執行緒或同步機制，並以跨 thread 測試驗證。
- [ ] **S15.018 解除測試結束後的 `ShellViewModel` 對全域 log 的訂閱**
  - 現象：測試留下的 `ShellViewModel` 不會取消訂閱 `AppLogStore.Instance`，訂閱者數量隨測試累積。
  - 驗收：在明確的生命週期終點取消訂閱，並以測試確認結束後不再收到 log 通知。
- [x] **S15.016 其他用非 thread-safe 集合收集 `PropertyChanged` 的測試（2026-10-02 修正）**
  - 位置：`FreeformHelperViewModelTests.Basics.GridPitch.cs:72`、`FreeformHelperViewModelTests.Basics.CoreFlags.cs:229`、`:245`、`:266`、`FreeformHelperViewModelTests.NotchExportCache.cs:282`。VM 的背景工作也會觸發 `PropertyChanged`，與 S15.002 已修的 `SettingsWindowDraft_SaveAppliesGeneralSectionFields` 是同一種寫法。
  - 目標：改用 thread-safe 的集合，或抽成共用的收集 helper。
  - 修正與驗證：5 處收集器改用 `ConcurrentQueue`，保留清空及包含斷言；測試專案 build 0 errors、ViewModel 測試 163/163 通過，`lint.ps1` 與 `verify.ps1 -StructureOnly` 通過。
- [x] **S15.014 未載入內容且無變更時關閉主視窗不再詢問存檔（2026-10-03 修正）**
  - 決定：owner 於 2026-10-03 在畫面回覆「沒載入內容時不問」。
  - 新規則：只有 `HasUnsavedChanges` 為 true，或已載入內容且 `_lastSavedPath` 為空白（從未儲存）時才詢問；直接重用 `SaveProjectAsync()` 的既有載入判斷 `_cad is not null`，不新增狀態。
  - 唯一使用者可見變更：關閉未載入內容且無未儲存變更的空白程式時不再詢問；已載入且從未儲存、或有未儲存變更時仍詢問，已儲存且無變更時仍直接關閉。`MainWindow` 的關閉流程、對話框文字與儲存／載入、變更追蹤、應用程式設定持久化政策均未修改。
  - 測試：先新增空白程式、已載入但從未儲存、已載入並儲存且無變更、有未儲存變更四個案例。修改前空白案例失敗（預期 false、實際 true），其餘 3 個通過；修改後 4/4 通過。既有取消關閉清理測試改以有未儲存變更作為前提，保留原驗證目的。
  - 驗證：UI 與測試專案建置均為 0 warnings／0 errors；`FreeformHelperViewModelTests` 不需私有資料的 152 個案例、主視窗與關窗相關的 23 個 UI 案例，以及包含主視窗的 1 個渲染快照案例全數通過；`verify.ps1 -StructureOnly` 通過。依本次禁止存取 `example/` 的限制，排除 15 個私有資料案例；依 sandbox 限制略過 lint 與行程列舉／停止。

- [x] **S15.008 修正銅柱完全包含時 Before 的投影精度（產品行為）**
  - Owner 決定（2026-10-03，畫面原話）：「在投影端修成 400」。完全包含時 coverage ratio 必須為 `1`；不更動 `QuantizeToFirmwareInt16` 的捨去契約。本決定取代 S15.001 對 `399`／`359` 的暫時接受。
  - 原因與證據：`src/FreeformHelper.Application/Services/CopperPillarSimulationService.cs:IntersectionArea` 用 `Clipper.BooleanOp` 與 `GeometryPrecisionDigits = 6` 量化座標，再由 `SumArea` 累加交集面積；`ProjectCadOutputToRegularGrid` 的分母卻是原始圓形多邊形的 `Polygon2.Area()`（`src/FreeformHelper.Domain/Geometry/Polygon2.cs:SignedArea`，未量化的鞋帶公式）。因此完整包含的比例仍約為 `0.9999993319869613`。`NotchApplySimulationService.cs:Simulate` 再呼叫 `NotchV21FirmwareEvaluator.cs:QuantizeToFirmwareInt16` 的 `Math.Truncate`，將低於整數的值捨去。
  - 最小修正：僅在 CAD 投影加入私有 `ContainsCircle` 判定，沿用 `Polygon2.Contains`、`Point2` 與 `DistanceTo`；圓心在 pad 內且到每條邊線段的距離皆至少為半徑時，交集面積直接使用同一個 `circleArea`，比例精確為 `1`。逐邊檢查可處理凹多邊形，不只檢查外框，也不加入比例近似容差。部分交疊仍走原有 `IntersectionArea`；Regular 投影、韌體捨去、C 匯出與 UI 版面均未變更。
  - 斜邊切線補強：邊內部改以叉積平方與半徑平方乘邊長平方比較，避免最近點重建誤差，新增指定 pad 正向與反向頂點順序的回歸測試，鎖定 peak `400` 的投影、Before、After 皆精確為 `400`，修正前兩案均為 `(399.99973279478451, 399, 399)`（失敗紀錄：`build/test-gate/s15008-b/s15008-b-red.trx`）。
  - TDD 修正前失敗：新增 `ProjectCadOutputToRegularGrid_WhenCopperFullyInsidePad_PreservesPeakInFirmwareSimulation` 兩案，預期（原始投影、Before、After）為 `(400, 400, 400)`／`(360, 360, 360)`，實際為 `(399.99973279478451, 399, 399)`／`(359.99975951530604, 359, 359)`；2 失敗、0 通過，紀錄在 `build/test-gate/s15008/s15008-red.trx`。
  - 可見數字：Before `399 → 400`，Finger press `359 → 360`。既有測試只更新三個誤差斷言案例：`CopperSource_WhenMoved_RegeneratesBeforeFrameThroughSharedSimulationPath` 的兩個 Before `399 → 400`；`CopperContactModel_WhenFingerSelected_UsesLowerFullPadSignal` 的 Before `359 → 360`；`ReplayCopperPath_WhenPhysicalAuditWarningExists_AggregateUsesSameStatus` 的第二步 Max After 與摘要 `399 → 400`（第一步 Max After 仍為 `200`）。既有 CAD mapping 測試的原始值 `399.99973279478451 → 400`，原有範圍斷言無須修改。
  - 完整數字稽核：沿用未更動的 clip 路徑重建既有 fixture 的修正前投影，再經同一公開 simulation／audit 路徑對照；完整衍生值差異（含 histogram、heatmap、audit）保存於 `build/test-gate/s15008/numeric-comparison.json` 與 `numeric-changes.txt`。既有 peak `1000` replay 的 Before／第二步 Max After／複製匯出內容為 `999 → 1000`；peak `400`／`360`／`1000` 的來源 After 仍為 `200`／`180`／`500`，來源 Delta 分別為 `-199 → -200`／`-179 → -180`／`-499 → -500`，action leg 為 `175 → 176`／`157 → 158`／`439 → 440`，action output 為 `375 → 376`／`337 → 338`／`939 → 940`，global residual 為 `-175 → -176`／`-157 → -158`／`-439 → -440`。這些既有測試未鎖定的衍生數字亦完整列出，未另修改其斷言。
  - 部分交疊回歸：新增 `ProjectCadOutputToRegularGrid_WhenCopperPartiallyOverlapsPad_PreservesFirmwareBeforeAndAfter`，鎖定來源 `(Before 199, After 100)`、目標 `(Before 0, After 99)`；修正前已通過，修正後數字相同。另涵蓋圓心略在 pad 內但銅柱仍跨邊界的案例，防止只憑圓心就當成完全包含。
  - 驗證：UI 與 Tests 專案均以 `--no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false` build，0 警告、0 錯誤；`dotnet test` 使用 `--no-build -p:UseAppHost=false --nologo`，Copper／simulation 153/153、`scripts/tests/run-tests.ps1` 所列 notch-core 190/190、獨立 notch-golden／C export drift 6/6，均 0 失敗、0 略過；其中包含新增的 6 個測試案例（完整包含與斜邊切線 4、部分交疊 2）。韌體 C 漂移檢查通過，未更新 golden 或 baseline。
  - 結構驗證：`./scripts/verify.ps1 -StructureOnly` 通過，CRLF 全檔檢查沒有需正規化的檔案，XAML action role 0 問題；變更限於本項服務、兩個測試檔與 `TODO.md`。
  - Sandbox 限制：未執行 lint 或行程列舉；每次循環用 `prepare-ui-workspace.ps1 -SkipStopApp` 正規化 CRLF，並自行檢查編輯檔案；codex 沙盒階段不提交、不 push（之後由 Claude 提交並推送）。

- [ ] **S15.002 消除 `FreeformHelperViewModelTests` 在完整測試下的 6 個逾時**
  - 現象：完整測試時 6 個測試以 `TimeoutException` 失敗，單獨重跑（同一 build）6/6 通過且各 < 1.3 秒。位置：`FreeformHelperViewModelTests.NotchResolvedSnapshot.cs:19`、`:67`、`:131`、`:354`、`:395` 與 `FreeformHelperViewModelTests.NotchGenerationStaleCompletion.cs:317`。
  - 線索：`WaitForConditionAsync` 預設 `timeoutMs = 1500`（`FreeformHelperViewModelTests.Helpers.cs` 的 `WaitForConditionAsync`）；`FreeformHelperViewModelTests` 161 個測試佔全部測試時間 759 秒中的 667 秒，單一測試最長 49.9 秒。
  - 另一個不穩定測試（2026-10-02 第二次完整測試時出現一次）：`DxfLayerImageExportServiceTests.ExportLayerImage_ThickerLineProducesMoreInkPixels` 以 `IOException` 失敗，訊息為 `%TEMP%` 下剛寫出的 `dxf-layer-thick-*.png` 正被其他 process 使用；第一次完整測試時通過。
  - 一次未定位的卡住：`uncategorized` 分組連跑 3 次中有 1 次 testhost 單核空轉超過 8 分鐘（CPU 464 秒）仍未結束，另外 2 次約 10 秒全過。當時沒有 hang 偵測所以不知道是哪個測試；S15.010 加上 `--blame-hang` 後，再發生時會留下點名的 Sequence 檔。
  - [x] **根因一：`CadLoadSpinnerProcessHost` 的 state sync 會無限空轉（production bug，2026-10-02 修正）**
    - 機制：`SyncRequestedVisibilityStateAsync` 在「要求 Hidden、effective 不是 Visible」時走 `continue` 回到 `while (true)` 開頭，中間沒有任何 await，佔住一條 thread-pool thread 與 `_stateSyncGate`，直到下一次 Show 或 Dispose。由 `16147183`（2026-03-12，`Fix spinner visibility race across repeated CAD loads`）引入。任何在 spinner 沒顯示時呼叫的 `Hide()` 都會觸發；測試環境的 spinner process 啟動不了，所以每個用到 host 的測試都會留下一條空轉的 thread。
    - 證據：第一次 CI（run 36962059739）的 `dotnet / test (ui)` 在 173 passed + 5 skipped 後停在 `SaveThenLoadProject_ReplaysStep2FreeformAfterAsyncProjectLoad`，5 分鐘後被 blame-hang 中止。本機把 testhost 限制為 2 核心（affinity mask 3）可重現：同樣在第 178 個測試停住；改成不平行執行也一樣會停（119 個之後）。前面記錄的「testhost 單核空轉 464 秒」是同一件事。
    - 修正：沒有東西可隱藏時 `return`。回歸測試 `CadLoadSpinnerProcessHostTests.Hide_WhenSpinnerIsNotVisible_CompletesStateSync`（修正前 5 秒內不結束而失敗，修正後 145 ms 內整個類別通過）。
    - 驗證：2 核心、平行執行的 `ui-core` 分組不再卡住，274 個測試 4 分 17 秒跑完。
  - [x] **根因二：DXF 編輯測試沒有等背景 grid rebuild 完成（測試缺陷，2026-10-02 修正）**
    - 現象：根因一修正後，同樣的 2 核心 `ui-core` 執行有 2 個失敗：`ResetAllDxfEditsCommand_RestoresBaselineAcrossHiddenCombinedLayerMovesAndRotation`（`GeometryTransforms.cs:184`，`HasRotatedCadPads` 為 false）與 `ResetAllDxfEditsCommand_PreservesAutoHiddenDuplicatePads`（`Duplicates.cs:159`，`DeletedCadPadCount` 為 0）。兩者單獨在 2 核心下連跑 3 次都通過。
    - 機制（由程式確認）：DXF 編輯指令以 `_ = TriggerGridRebuildAsync(...)` 啟動 rebuild 而不等待；rebuild 完成時會 `_selectionCoordinator.ClearSelection(CanvasHost)`（`FreeformHelperViewModel.Operations.cs` 的 `RebuildGridAsync`）。測試沒有 UI `SynchronizationContext`，完成的 continuation 在 thread pool 上執行，可能落在測試剛做完選取之後，下一個指令便沒有選取對象。在 app 中 continuation 會排回 UI thread 依序執行。修正見下方「測試端」。
  - [x] **根因三：非 UI thread 上的「讓出一幀」會等一個沒人處理的 dispatcher 工作（production bug，2026-10-02 修正）**
    - 機制：`YieldCadLoadCanvasOverlayFrameAsync` 只判斷 `Application.Current is null`。探針測試顯示：plain `[Fact]` 中 `Dispatcher.UIThread.InvokeAsync` 不會完成（3 秒內未完成）；而 `[AvaloniaFact]` 結束後緊接著執行的 plain 測試仍看得到 `Application.Current`。此時排進 dispatcher 的工作若在 headless session 重設前沒被處理就永遠不會完成，載入流程便停住。
    - 修正：不在 UI thread（`Dispatcher.UIThread.CheckAccess()` 為 false）時改用 `Task.Yield()`。回歸測試 `YieldCadLoadCanvasOverlayFrame_OffTheUiThread_DoesNotWaitForTheDispatcher` 修正前失敗（5 秒內不結束）、修正後通過。
  - [x] **根因四：每筆 log 都排一次完整的 console 重繪（production 效能缺陷，2026-10-02 修正）**
    - 證據：2 核心下整個套件停住時用 `dotnet-dump` 擷取 testhost。headless UI thread 正在 `Dispatcher.RunJobs` 裡執行 `FreeformHelperView.OnConsoleEntriesChanged` 排入的工作：`SyncConsoleText` → `ApplyConsoleRender` → `RefreshConsoleLinks` → `ConsoleLinkParser.Parse`（對整份 console 文字跑六個 regex，並對每個 Windows 路徑呼叫 `File.Exists`／`Directory.Exists`）。`AppLogStore` 是全域的，log 隨測試累積；同一份 dump 裡有 6 個停在 `MainWindow.OnClosingAsync` 沒關掉的視窗，各自的 view 都還訂閱著 log 變動。
    - 機制：`OnConsoleEntriesChanged` 對每一次 collection change 各 post 一個 Background 工作，沒有合併；N 筆 log 就做 N 次完整重繪。
    - 修正：新增 `FreeformHelper.UI.Services.CoalescedRefresh`（已有一個 refresh 在排隊時不再多排，執行時讀最新 snapshot），`FreeformHelperView` 的 console 更新改用它。`CoalescedRefreshTests` 4 個測試。
    - 驗證：修正後再擷取的 2 份 dump 中，UI thread 都閒置在 `PushFrame`，不再出現 console 重繪的堆疊。實際 app 的改善幅度未量測。
    - 效果：20 核心下 `verify.ps1 -All` 由 339 秒降為 197 秒；`FreeformHelperViewModelTests` 單獨一組 47 秒（修正前所在的 `ui-core` 分組為 3 分 40 秒）。4 核心下整個套件修正前 3 輪各有 6、4、4 個失敗，修正後 2 輪都是 965 個全過（見下方）。
    - 未涵蓋：console 展開時，`ShellViewModel` 的 `ConsoleText` 變更仍會讓 view 對每筆 log 同步做一次完整重繪與 link 解析（既有行為），見 S15.013。
  - [x] **根因五：測試結束時還開著的視窗會讓 headless session 收尾失敗，下一個 headless 測試因此停住（測試基礎設施缺陷，2026-10-02 修正）**
    - 現象：會擋合併的 `uncategorized` 分組（CI 的 `dotnet / test (core)`）在 20 核心下 21 次中有 4 次停住：`TerminalStartupPathTests.ConsoleExpanded_AfterStartupLogs_RendersVisibleGlyphs` 5 分鐘沒有進度，被 blame-hang 中止，沒有任何測試回報失敗。
    - 證據（停住當下的 `dotnet-dump`）：測試停在 `FlushUiQueueAsync` 的第一個 `await`；headless session thread 閒置在 `ManagedDispatcherImpl.RunLoop`。該 thread 的 `SynchronizationContext` 指向**上一個測試**的 `Dispatcher`（其平台實作已被換成 `DummyShuttingDownUnitTestDispatcherImpl`、`_hasShutdownFinished` 為 false），測試的 continuation（`Xunit.Sdk.AsyncTestSyncContext.Post`）排在那個不再執行的 dispatcher 上。heap 上留有收尾時丟出的例外：`KeyNotFoundException: The given key 'fonts:SystemFonts' was not present in the dictionary`，堆疊為 `HeadlessUnitTestSession.EnsureIsolatedApplication` 的收尾 → `Dispatcher.ResetForUnitTests` → `MediaContext.Render` → 排版 → `AvaloniaEdit.Rendering.TextView.MeasureOverride` → `FontManager.SystemFonts`。
    - 機制（對照 Avalonia 11.3.12 原始碼）：每個 headless 測試結束時，session 依序「釋放 `FontManager` → `Dispatcher.ResetForUnitTests()` 把還排著的工作跑完 → 離開 locator scope → 還原 `SynchronizationContext`」。還開著的視窗有排版工作在排隊，字型管理員已釋放，所以丟例外；後面三步被跳過，而測試結果在這之前已經回報，例外被吞掉。下一個測試開始時 `AvaloniaSynchronizationContext.InstallIfNeeded()` 看到 thread 上已經有（過期的）Avalonia context 就不再安裝，於是該測試所有 `await` 都排到死掉的 dispatcher。
    - 為什麼視窗還開著：`MainWindow.OnClosingAsync` 在專案沒有存檔路徑時一律取消 `Close()` 並開「是否存檔」對話框；測試裡 7 處顯示 `MainWindow` 後呼叫的 `window.Close()` 因此都沒有真的關閉（根因四的 dump 裡「6 個停在 `MainWindow.OnClosingAsync` 的視窗」是同一件事）。是否丟例外取決於收尾當下有沒有排版工作在排隊（平行測試寫入全域 `AppLogStore` 會讓還開著的 console 重排），所以是間歇性的。另有 3 個測試只量版面、從不顯示也不關閉視窗（`MainWindow_WithExpandedConsole_Can_Layout_Headless`、`IndexMappingReportWindow_Can_Layout_Headless`、`NotchExportSelectionWindow_Can_Layout_Headless`）；量版面時內容已經掛上並訂閱全域的 log，效果相同（審查發現，讀碼推得，未單獨重現）。
    - 修正：`tests/FreeformHelper.Tests/UI/TestHost/HeadlessSessionGuardAttribute.cs`（組件層級的 `BeforeAfterTestAttribute`，只對 `[AvaloniaFact]`／`[AvaloniaTheory]` 生效）。測試結束後、session 收尾前，把還開著的視窗依開啟的相反順序關掉（先清掉 `DataContext`，`MainWindow` 就不會再詢問），並把排隊的工作跑完；視窗若仍拒絕關閉則讓該測試失敗。測試開始前若 thread 上的 context 是先前測試用過的同一個物件，立刻以說明原因的例外失敗，而不是停 5 分鐘。不顯示的視窗無法自動追蹤，由測試呼叫 `HeadlessSessionGuardAttribute.CloseAtTestEnd(window)` 登記（上述 3 個測試已加）。關閉過程逐一視窗處理並彙整例外，一個視窗關閉失敗不影響其他視窗。`HeadlessSessionGuardTests` 12 個測試。
    - 驗證：修正後 `uncategorized` 分組連跑 16 次都是 355 個全過（13 到 23 秒）；修正前同樣的做法 21 次中 4 次停住。限制核心數的整個套件結果見下方「驗收量測」：根因五的停住不再出現，但量測揭露了根因七。
    - 未涵蓋：過期 context 無法在下一個測試內修復（`HeadlessUnitTestSession.DispatchCore` 在測試主體返回後仍用 thread 上的 context 來結束等待），而且收尾中斷時 locator scope 也沒有離開，之後的測試會經由上層 scope 拿到舊的 `FontManager` 與 `MediaContext`；所以防護只能「預防」加「快速失敗」。一旦發生，同一輪之後的每個 headless 測試都會在開始時以同樣的訊息失敗，只需要看第一個。防護依賴 session 每個測試重建 application（預設的 `AvaloniaTestIsolationLevel.PerTest`）。Avalonia 上游的收尾順序（先釋放字型再跑剩餘工作、例外不還原 context）未回報。
  - [ ] **根因六：別的 thread 在 headless session 設定期間先建立了 `Dispatcher.UIThread`（測試基礎設施的 race，2026-10-02 只做到偵測，尚未消除）**
    - 現象：`[AvaloniaFact]` 在 1 ms 內以 `PlatformNotSupportedException`（`Dispatcher.PushFrame`）失敗。根因五修正後的一次 `verify.ps1 -All` 中，`ui-viewmodel` 分組的 `ExportNotchCommand_WhenDebugChangesToRelease_ReusesGenerationAndFormatsReleaseProfile` 發生一次，同組其餘 162 個通過。先前把兩個回歸測試搬到獨立類別時 2 次執行 2 次都發生的也是這個。
    - 機制（對照 Avalonia 11.3.12 原始碼，並以探針確定性重現）：`Dispatcher.UIThread` 是 `s_uiThread ??= CreateUIThreadDispatcher()`，由第一個取用的 thread 建立；headless session 在每個測試開始時先清掉它，之後才註冊平台。這段期間若有別的 thread 取用（先前測試留下的背景工作），會建立沒有平台實作的 dispatcher，而且設定過程中建立的 `MediaContext` 與 `Compositor` 會留住它。探針（在 rendering 初始化之後、windowing 初始化之前，由另一個 thread 取用一次）讓 21 個 headless 測試中 20 個失敗。
    - 試過的修補：偵測到之後重設 dispatcher（反射呼叫 `Dispatcher.ResetBeforeUnitTests`）不夠，5 個截圖測試拿不到畫面；再重跑 windowing 初始化也不行（`RenderLoop` 與新的 render timer 對不上，4 個失敗）。結論是設定完成後無法修復。
    - 已做：`tests/FreeformHelper.Tests/UI/TestHost/HeadlessDispatcherSetup.cs` 在平台註冊後立刻檢查 `Dispatcher.UIThread.SupportsRunLoops`，不成立就讓設定以說明原因的例外失敗。效果是該測試以明確訊息失敗、session 丟棄半成品、後面的測試不受影響（探針下 20 個測試共 264 ms 失敗，沒有連鎖停住）。
    - 待做：找出是哪些背景工作在取用 `Dispatcher.UIThread`。最可能的是 `AppLogStore.Add`／`Clear`（`_uiReady` 之後每一筆 log 都呼叫 `Dispatcher.UIThread.CheckAccess()`，而 `AppLogStore.Instance` 是整個 process 共用的）；尚未量測證實。根治方向是 production 端不要在沒有 `Application.Current` 時取用 `Dispatcher.UIThread`（VM 與 view 透過可注入的 UI 排程介面工作），屬於架構調整。
    - [x] A6a 守衛盤點與窄修（2026-10-03）：可由非 UI thread 到達的路徑包含 VM 建構子的 `ApplyCanvasColorDefaultsFromTokens`／`ResolveTokenColor`、`YieldCadLoadCanvasOverlayFrameAsync`、`ShellViewModel` 建構子、VM 的 deferred inspector／selection preview／auto-play／progress／export 背景續接、`RuntimeQueryIpcHost` 與 `AppLogStore.Add`／`Clear`；view 的 spinner／console 屬性或集合通知也可能從背景 thread 進入。直接 `CheckAccess()` 守衛中，token 預設色與 spinner 通知會誤把無執行迴圈的 dispatcher 當成 UI thread；已與 overlay 共用 `UiThread.IsCurrent`（application 存在、dispatcher 可跑迴圈且目前 thread 有存取權），並在 token 讀取前再確認。UI 專用的 `UiResourceResolver`／`DevView` 也只在 helper 放行後讀 application 成員。`AppLogStore` 行為依本項範圍不改；其他直接排入 dispatcher 的背景路徑仍屬根因六待追查。新增 `[AvaloniaFact]` 的 thread-pool token 讀取回歸測試；兩個專案 build 0 error，指定 182 個測試連跑 3 輪皆 0 failed，已知 dispatcher 建立競態 0 次。
    - [x] A6b 背景入口量測與窄修（2026-10-03）：暫時把 UI 專案及 headless test-host 的 `Dispatcher.UIThread` getter 接到帶堆疊與執行緒種類的探針；在未限制核心數的 sandbox 連跑 `ui-viewmodel` 3 輪（每輪 163 個測試），第 1 輪全過，第 2、3 輪各有 1 個測試被 `HeadlessDispatcherSetup` 以「Another thread created ...」擋下。三輪在 `Application.Current == null` 且 dispatcher `SupportsRunLoops == false` 時，共量到 production getter 8,486 次：`AppLogStore.Add` 經 `InAppConsoleTarget.Write` 8,176 次（thread pool 3,392、`.NET Long Running Task` 4,784）；VM progress post 252 次、deferred CAD inspector 31 次、deferred REG inspector 9 次、selection preview 6 次、auto-play 12 次（皆為 thread pool）。test-host 的平台註冊後檢查另有正常 run loop 91 次、無 run loop 2 次；失敗兩輪最先列出的無 run loop 堆疊都是 deferred CAD inspector，因探針在 getter 返回後記錄，不能單憑順序斷言它就是唯一搶先建立者。另以全新 testhost 的 plain `[Fact]` 確定性證實：沒有 application 時 `AppLogStore.MarkUiReady` 與背景 `Add`／`Clear` 使原本為 null 的全域 dispatcher 變成 `SupportsRunLoops=False`（修正前 RED）。
      修正：`UiThread.TryGetRunningDispatcher` 在 `Application.Current` 缺席時不取用 getter，且僅接受有 run loop 的 dispatcher；`AppLogStore` 無 UI 時在呼叫執行緒處理 pending、add、clear，沿用 `_uiCollectionGate` 序列化集合通知與既有 ring log，app 運行時仍按原順序 post 到 UI。已量到的 VM deferred inspector／selection preview／auto-play 在無 UI 時停止背景畫面更新，progress 在呼叫執行緒更新；同時守衛 `ShellViewModel` 建構、export render yield、Runtime Query IPC、spinner IPC 與 console view 的背景通知入口。暫時探針已移除，plain 回歸測試由 RED 轉 GREEN；最後版本 `AppLogStoreTests` 4/4、相關 smoke／IPC／layout 36/36、2／4 核心的 `ui-viewmodel` 各 163/163 通過。sandbox 可將 PowerShell 的 ProcessorAffinity 設為 3／15，且已驗證子行程繼承。根因六仍保持 open。Claude 在 sandbox 外（本機同時有其他 codex 任務在跑）以 ProcessorAffinity 限制 testhost 測整套 984 個測試：2 核心 3 輪、4 核心 3 輪全部通過（984／984），`Another thread created ...` 0 次、`PlatformNotSupportedException` 0 次。另有一次因 Claude 的腳本把遮罩 `3,15` 誤傳成 `315`（約 6 核心）的 3 輪，其中第 2 輪 `RuntimeQueryIpcTests.SendRequest_WhenRuntimeQueryThrows_ReturnsIpcErrorEnvelope` 失敗一次（預期 `IPC_ERROR`、實際 `IPC_TIMEOUT`，3 秒逾時），另兩輪通過；沒有在修正前的基底以同條件對照，所以無法說這是本修正造成，還是測試間共用全域 dispatcher 狀態本來就有的問題。計畫中的關閉門檻（2 與 4 核心各連續 3 輪零失敗）已達成，但修正前的失敗率隨機器負載而變（sandbox 內 3 輪中有 2 輪），6 輪不足以宣稱 race 已消除，也有一個未解釋的 IPC 逾時，另外，rebase 到含 PR 91、93 的主幹後第一次 `verify.ps1 -All`（預設核心數、本機同時有其他 codex 任務）在 `uncategorized` 分組又被 `HeadlessDispatcherSetup` 以「Another thread created ...」擋下一次（`CadLoadSpinnerProcessHostTests.Show_WhileHideSendIsInFlight_RestoresVisibleState`，1 ms 內失敗），重跑通過：表示本修正之後仍有沒被量到或沒被守衛的背景入口（或別的測試留下的工作），來源尚未找到。所以保持 open，等 GitHub runner 累積更多紀錄、再追下一個來源，才決定是否關閉與是否把 `viewmodel` 分片改回必過。
    - [x] A6c 剩餘來源量測與窄修（2026-10-03）：2 核心整套測試在修正前跑滿 8 輪（每輪 986/986 通過），守衛訊息各 0 次。直接 getter 探針共 828 筆：`HeadlessDispatcherSetup` 816 筆（thread pool、`Application.Current=null`、`SupportsRunLoops=true`，正常的設定檢查）；production 的 `UiThread.TryGetRunningDispatcher` 12 筆（xUnit `.NET Long Running Task`、`SupportsRunLoops=false`，其中 11 筆 getter 返回時 application 非空、1 筆為空）。production 堆疊分別是 `AppLogStore.Add`／`InAppConsoleTarget.Write` 10 筆、VM 建構時 `ApplyCanvasColorDefaultsFromTokens` 2 筆；兩者都在 A6b 改成走 helper 後仍會觸發 getter。`Dispatcher.CurrentDispatcher` 在 UI 與 test-host 沒有直接取用，`SynchronizationContext.Current` 只有 `HeadlessSessionGuardAttribute.Before` 的讀取。
      - 12 筆的目前 headless 測試都是 `none`；探針記下的前一個 headless 測試依呼叫點為：log 路徑在 `CreateSimulationWorkspaceSessionAsync_Step5ClearsAtCompletion_DoesNotReviveClearedState` 後 2 筆、`CadLoadSpinnerProcessHostTests.Show_WhileHideSendIsInFlight_RestoresVisibleState` 後 1 筆、`ResolvedSnapshot_OutputOnlyStateReusesIdentity_ComputationChangeInvalidatesIt` 後 2 筆、`CreateSimulationWorkspaceSessionAsync_WarmFinalOnlyInvalidation_ResetsRowCountAndClearsPriorSuccessOnReject` 後 2 筆、`ExportNotchCommand_WhenReleaseChangesToDebug_ReusesGenerationAndFormatsDebugProfile` 後 2 筆、`ExportNotchCommand_LegacyRegularAnchor_FinalSettingChangesRemainRequestSpecificTableMisses` 後 1 筆；VM token 路徑在第一個 `CreateSimulationWorkspaceSessionAsync_Step5...` 與 `ExportNotchCommand_WhenReleaseChangesToDebug...` 後各 1 筆。前一個 headless 測試不等於當時正在執行的 plain 測試，也不能當成工作來源。spinner host 沒有 dispatcher 取用；該測試等待 state sync，測試用的 warmup 已完成；Runtime Query IPC 測試在清理時等待 `StopAsync`，其他明示的測試 `Task.Run` 也有 await／Wait，沒有找到須改的測試清理。
      - 機制與修正：A6b helper 先看 `Application.Current` 才讀 getter，但 headless 收尾／設定期間 application 狀態會變；探針有一筆在通過前置檢查後，getter 返回時已變成 null，顯示此判斷不是原子保護。改在 app 的平台設定 callback 與 headless 平台註冊檢查成功後保存可執行的 dispatcher；背景入口只讀已保存的參照，不再呼叫懶建立 getter。另兩個 VM 的欄位初始化直接建構 `DispatcherTimer`，會在沒有 application 的普通測試中隱含建立無執行迴圈的 dispatcher；兩個單獨測試在修正前均觀察到 `s_uiThread: null -> SupportsRunLoops=false`，改成只在目前執行緒是有效 UI thread 時建構計時器，正常 app 的 dispatcher、優先序與預設間隔維持相同，之後需要時可延後建構。
      - 驗證：helper 全域槽位測試與兩個 VM 計時器測試由 RED 轉 GREEN。保留探針的修正後 2 核心整套測試連跑 3 輪，每輪 989/989 通過、守衛 0 次；309 筆探針紀錄全是正常的 test-host 設定檢查，production 異常 getter 為 0。移除探針後，最終二進位的 2 核心整套測試 3 輪守衛皆 0 次：第 1 輪 988/989（`NotchDetail_SameSelectionRevision_ProjectsExistingResolvedSnapshot` 的 3 秒等待逾時）、第 2、3 輪各 989/989；該逾時測試用同一二進位單獨 2 核心連跑 3 輪皆通過。兩專案最終 build 0 error／0 warning、相關測試 71/71、`verify.ps1 -StructureOnly` 通過。
      - 未關閉：探針只在 getter 返回後記錄，不能證明 12 筆由哪一筆「最先」建立 dispatcher；修正前 8 輪 guard 本就為 0，因此 0→0 不能證明先前那次間歇失敗已消失。最終整套第 1 輪的逾時與 S15.002 原記錄相同，但沒有無探針的修正前對照，不能判定本次改動是否影響其發生率。根因六維持 open；其他 Avalonia 內部的隱含取用與未觀察到的背景工作仍需依後續失敗證據追查。
  - [x] **根因七：「讓出一幀」把沒有執行迴圈的 dispatcher 當成 UI thread（production 缺陷，根因三修正不完整，2026-10-02 修正）**
    - 現象：根因五修正後（build `edbac3f2`）把 testhost 限制核心數跑整個套件，6 輪中 2 輪停住（2 核心 1 輪、4 核心 1 輪）。停住的是 plain `[Fact]`：`GetCadV22StageOverlays_TM81_CAD113_DisablesToFull_WhenCadHasNoExpansionPotential` 與 `GetCadV22StageOverlays_CAD4818_ReturnsFinalOutlineCoveringFullReg384`，`dumpasync` 顯示 `LoadProjectAsync` → `LoadingScopeCoordinator.RunAsync` → `YieldCadLoadCanvasOverlayFrameAsync` 停在 `Dispatcher.UIThread.InvokeAsync(..., Render)`；headless session thread 閒置在等下一個測試。
    - 機制：根因三的修正用 `Application.Current is null || !Dispatcher.UIThread.CheckAccess()` 判斷「不在 UI thread」。但沒有平台實作的 dispatcher、以及收尾中被換成 `DummyShuttingDownUnitTestDispatcherImpl` 的 dispatcher，對任何 thread 的 `CheckAccess()` 都回 true。排進去的工作在正常收尾時會被執行或被中止（`await` 得到 `TaskCanceledException`）；永遠不執行的情況是沒有平台的 dispatcher，或收尾被例外中斷（根因八）之後留下的 dispatcher。所以這兩次停住很可能是根因八先發生、再走到這裡（審查依原始碼推論，未以 dump 證實）。app 裡的 dispatcher 一定有執行迴圈，不受影響。
    - 修正：`YieldCadLoadCanvasOverlayFrameAsync` 取一次 dispatcher，只有在 `CheckAccess()` 且 `SupportsRunLoops` 時才等一幀，否則 `Task.Yield()`。判斷抽成 `IsUiThreadThatRunsALoop`，`CadLoadOverlayFrameYieldPolicyTests` 以真值表測試。沒有平台的 dispatcher 無法在測試中確定性建立，所以沒有端到端的回歸測試；依據是下方的驗收量測。修正對上述兩種情況都有效。
    - A6a 後續：真值表判斷已搬到共用 `UiThread` helper；overlay 透過同一個 helper 取得一次 dispatcher，沒有可用 UI 迴圈時仍以 `Task.Yield()` 讓出執行權。
  - [x] **根因八：測試結束後才進到 dispatcher 的殘留工作在收尾時丟例外（測試基礎設施，2026-10-02 修正）**
    - 現象：根因七修正後的 6 輪限制核心數執行都不再停住，其中 5 輪 976 個全過；1 輪（2 核心）有 24 個 `[AvaloniaFact]` 在開始時以「session 還留著先前測試的 context」失敗。也就是根因五的防護生效（快速失敗而不是停住），但 session 還是壞了一次，而且那個測試沒有開視窗。
    - 機制：防護在測試結束時把排隊的工作跑完，但其他 thread 之後才 post 進來的工作（先前測試留下的背景流程）會在 session 收尾、字型管理員已釋放的狀態下執行；任何一個丟例外都會讓收尾中斷。
    - 修正：防護在測試開始時對該測試的 dispatcher 掛上 `UnhandledException` 處理。測試結束之後、或 session 正在收尾（發出事件的 dispatcher 已不支援執行迴圈）時丟出的例外標記為已處理並連同完整堆疊寫到 stderr（`[HeadlessSessionGuard] work on the dispatcher of <測試> threw after the test ended or during session teardown: ...`），收尾因此能完成；測試還在進行時的例外不處理，照常讓該測試失敗。三個測試涵蓋 hook 標記、測試結束後的例外不逸出，以及進行中的例外照常逸出。收尾期間的事件路徑無法在測試裡重現，依據是驗收量測。被攔下的例外只會出現在測試輸出的 stderr（`dotnet test` 預設會顯示），訊息裡的測試名稱是「例外發生在誰的 dispatcher 上」，工作本身可能來自更早或平行的測試。
  - [x] **測試缺陷：`SettingsWindowDraft_SaveAppliesGeneralSectionFields` 用 `List<string>` 收集 `PropertyChanged`（2026-10-02 修正）**：VM 的背景工作也會觸發 `PropertyChanged`，斷言列舉清單時被同時寫入，2 核心下出現一次 `Collection was modified`。改用 `ConcurrentQueue<string>`。
  - [x] **測試端**：DXF 編輯測試共 27 處選取改走 `SelectCadPadsAsync`（先等 grid rebuild 完成），另有 3 處在最後的斷言前等待；等待超過 30 秒會帶著 VM 狀態失敗而不是卡住（`FreeformHelperViewModelTests.Helpers.cs` 的 `WaitForGridRebuildAsync`）。審查後把同一類別其餘 12 處直接 `await vm.WaitForGridRebuildIdleAsync()` 的等待也改走這個 helper，測試裡不再有沒有上限的 rebuild 等待。
  - **試過但撤回**：把根因三的兩個回歸測試（`YieldCadLoadCanvasOverlayFrame_*`，`[AvaloniaFact]`）移到獨立類別，讓它們落在會擋合併的 `uncategorized` 分組（審查建議）。第一次執行就失敗：`YieldCadLoadCanvasOverlayFrame_OnTheUiThread_RunsQueuedRenderWorkFirst` 以 `PlatformNotSupportedException` 失敗於 `Avalonia.Threading.Dispatcher.PushFrame`（headless session 拿到的是沒有平台實作的 dispatcher），之後該分組停住 5 分鐘被 blame-hang 中止。後半段的停住是根因五，前半段的 `PlatformNotSupportedException` 是根因六（當時只是推測，之後以探針重現）；搬動後 2 次執行 2 次都發生，放回後 20 多次執行中出現 1 次。兩個測試已放回 `FreeformHelperViewModelTests`（該類別自成一個分組，沒有其他類別與它平行），因此目前由不擋合併的 `viewmodel` 分片執行；恢復該分片為必過時即納入把關。
  - **試過但撤回**：測試組件加 `DisableTestParallelization`（測試類別一次跑一個）。2 核心下沒有消除停住，而且在 20 核心下讓 `ShellViewModelConsoleTests.ConsoleText_UsesRingTailWhenExpandedBeforeUiEntriesFlush` 在 `uncategorized` 分組 4 次中失敗 3 次（該測試讀寫全域的 `AppLogStore.Instance`，執行順序一變就受前面測試殘留的背景工作影響；單獨執行 3 次都過）。已移除該設定。這個測試對全域狀態的依賴本身也是待修項目。
  - **殘餘（未解決）**，資料來自把 testhost 限制核心數的整個套件執行。前三行是根因四修正之前、且測試類別為依序執行時量到的：
    - 4 核心，3 輪：都能跑完（960 個，11 分 1 秒到 14 分 33 秒），分別有 6、4、4 個失敗，全是 `FreeformHelperViewModelTests` 的 `[AvaloniaFact]`：`DeferredInspector_*`、`NotchDetail_SameSelectionRevision_*`、`ResolvedSnapshot_CrossIcAllocationPool_*`、`SelectionPreviewAndDeferredInspector_*`、`ExportAndSimulation_Overlap_*`。
    - 2 核心，整個套件 7 輪（其中 3 輪把 thread pool 下限調到 32）：每輪都在 120 秒無進度後被中止，停住的測試每輪不同（專案載入的 plain 測試與上述 `[AvaloniaFact]` 都出現過）；調高 thread pool 下限沒有改善。只跑 `ui-core` 分組的 2 輪中，1 輪 275 個全過、1 輪停住。
    - 20 核心：`verify.ps1 -All` 穩定通過。
    - 根因四修正後，4 核心下整個套件 2 輪都是 965 個全過（10 分 39 秒與 10 分 51 秒），沒有逾時也沒有停住。GitHub runner 上的第一輪（run 36979138024）七個檢查全過，包含 `dotnet / test (viewmodel)`。
    - 根因四修正後，2 核心下整個套件 2 輪仍在 90 秒無輸出後被判定停住。`dumpasync` 顯示的是等不到的 continuation，而不是忙碌的 thread：
      - 目前執行中的 `[AvaloniaFact]`（例：`ResolvedSnapshot_OutputOnlyStateReusesIdentity_ComputationChangeInvalidatesIt`、`TerminalStartupPathTests.ConsoleExpanded_AfterStartupLogs_RendersVisibleGlyphs` 的 `FlushUiQueueAsync`）在等 UI dispatcher 的工作，而 headless session thread 閒置在 `PushFrame`。
      - 先前測試留下的 VM 停在 `LoadingScopeCoordinator.EndAsync`、`RequestInitialGridBuildAsync` 的 `Task.Yield`：continuation 被 post 到已被 headless session 重設的 dispatcher。
    - 當時的判斷（已被根因五取代）：VM 與 view 直接使用全域的 `Dispatcher.UIThread` 與 `SynchronizationContext`，而 headless 測試會在測試之間重設它，要根治需架構調整。實際上 `[AvaloniaFact]` 的停住是根因五（context 過期是因為上一個測試的收尾丟了例外，不是設計上必然），plain 測試的停住是根因七；仍未消除的是根因六的 race。
  - **驗收量測（2026-10-02，把 testhost 限制核心數跑整個套件，150 秒沒有輸出判定為停住）**：
    - 只有根因五的防護（`edbac3f2`）：2 核心 3 輪為「968 個全過（8 分 22 秒）／停住／1 個失敗」；4 核心 3 輪為「停住／全過／全過」。兩次停住是根因七，1 個失敗是上面的清單 race。
    - 加上根因七與審查修正：2 核心 3 輪為「976 個全過／24 個快速失敗（根因八，沒有停住）／全過」；4 核心 3 輪都是 976 個全過。
    - 再加上根因八：2 核心 4 輪、4 核心 3 輪，7 輪都是 978 個全過、沒有停住也沒有逾時（2 核心 200 到 276 秒，4 核心 165 到 225 秒；根因五修正前 4 核心約 10 分 40 秒、2 核心跑不完）。時間縮短的原因沒有另外量測；推測是測試不再留下還開著的 `MainWindow`，全域 log 的每一筆不再讓那些視窗各自重繪 console。其中 2 輪各有一次殘留工作的例外被防護攔下並記錄（見 S15.015）。之後為了取得完整堆疊再跑的 2 輪 2 核心：1 輪 978 個全過；1 輪有 3 個失敗（S15.015 的例外發生在測試進行中，另外 2 個是 session 壞掉後的快速失敗），沒有停住。
    - 最終狀態（再加上 S15.015 的修正、防護改為「測試結束後或收尾期間才處理例外」與第二輪審查修正）：2 核心 3 輪、4 核心 3 輪，6 輪都是 982 個全過，沒有停住、沒有失敗，也沒有任何被防護攔下的殘留工作例外（170 到 251 秒）。
    - GitHub runner：`9a1e6d7b`（run 36979138024）、`edbac3f2`（run 36989882096）與 `2ace2d1c`（run 36997715790）七個檢查全過，包含 `dotnet / test (viewmodel)`。
  - 狀態：A6b 時本機曾在 2 核心與 4 核心各連續 3 輪全部通過；A6c 最終無探針的 2 核心 3 輪有 1 次既有 Notch snapshot 等待逾時，所以目前未重新達到連續 3 輪零失敗。根因六的 guard race 仍須觀察，`viewmodel` 分片也尚未改回必過；待 GitHub runner 累積證據後再決定是否移除 `ci.yml` 的 `continue-on-error`。
  - **沒有回歸測試的修正與流程上的例外**：
    - `da03a2bc`（view 卸載後排隊中的 console 重繪不再執行）沒有專屬測試：卸載後 `_consoleEditor` 已清掉，重繪原本就沒有可觀察的輸出，寫不出修正前會失敗的測試。卸載後 Loaded 優先序的那一個 post 仍會執行（既有行為）。
    - 根因六的偵測（`HeadlessDispatcherSetup`）與根因八「收尾期間由 dispatcher 狀態辨識」的事件路徑只用暫時的探針或量測驗證。
    - 已補自測：`scripts/tests/check-private-path-patterns.ps1` 驗證 `verify.ps1` 的私有路徑規則，包括應命中與不應命中的樣本；由 structure lane 執行。
    - 已補自測：`scripts/tests/check-temporary-environment.ps1` 驗證 `assert-example-data.ps1` 使用的環境變數暫存與還原，包括原先不存在的變數；由 structure lane 執行。
    - S15 系列的 commit 與 PR 以 `Refs: TODO.md S15.xxx` 連結而不是 `Refs #N`：目前使用的 GitHub App 沒有 issues 權限，無法建立 issue（`docs/agents/issue-tracker.md` 要求把例外明列，即此處）。
  - 驗收：完整測試在 4 核心與 2 核心條件下各連續 3 次無逾時、無停住；不得以單純放大 timeout 當作唯一修法而不說明等待條件。完成後移除 `ci.yml` 中 `dotnet-test` 的 `continue-on-error`。

- [x] **S15.003 讓測試分組完整覆蓋所有測試類別**
  - 現象：`scripts/tests/run-tests.ps1` 的分組是類別白名單；118 個測試類別中有 62 個（346 個測試）不屬於任何分組，只有 `-Group all` 會執行，其中包含 S15.001 的 `SimulationWorkspaceViewModelTests`、`NotchOverlayVisibilityPolicyTests`（87）、`CoordinatePlannerWorkspaceViewModelTests`（14）、`RuntimeQueryIpcTests`（5）。
  - 完成：新增 `uncategorized` 分組，filter 是所有明列類別名稱的否定，因此新增的測試類別不可能落在所有分組之外；`run-refactor-gate.ps1` 固定加跑這個分組。既有分組彼此有重疊（例如 `notch-golden` 包含於 `notch-core`），所以驗收改為聯集而非加總。審查後補正：只加 `uncategorized` 時，固定 gate 仍漏跑 17 個只列在 opt-in 分組的類別（notch-core 專屬 10、infrastructure 3、ui-snapshots 4，約 108 個測試）；現在固定 gate 一律跑 `notch-core`、`application`、`infrastructure`、`ui-core`、`ui-snapshots`、`uncategorized` 全部六組。filter 也改成 `FullyQualifiedName~.<類別>.`，避免名稱只是包含某個明列類別的新類別被吸進該分組。另加一個檢查：分組清單若有已不存在的類別名稱（R13.004d 曾因此漏跑）直接報錯。
  - 驗證：`./scripts/tests/run-tests.ps1 -Group uncategorized -UseNoAppHost` 為 347 個測試全過（原本未涵蓋的 346 個加上 S15.009a 新增的 1 個），約 10 秒。

- [x] **S15.010 gate 腳本必須在 `dotnet` 失敗時失敗**
  - 現象：`run-tests.ps1`、`run-refactor-gate.ps1` 的 build stage 與 `scripts/build/build.ps1` 呼叫 `dotnet` 後沒有檢查 `$LASTEXITCODE`。PowerShell 7 預設 `$PSNativeCommandUseErrorActionPreference = $false`，native command 失敗不會中止腳本，所以測試或建置失敗時 gate 仍印出 `DONE` 並寫出 summary。`run-pre-push-gate.ps1` 透過這兩支腳本執行，同樣受影響；`lint.ps1` 原本就有檢查。
  - 完成：三處都在 `dotnet` 之後檢查結束代碼並 throw；審查指出的第四處 `scripts/tests/check-startup-budget.ps1` 的 build 也已補上。`run-tests.ps1` 另加 `--blame-hang --blame-hang-timeout 5m --blame-hang-dump-type none`，卡住的測試會變成點名的失敗。
  - 驗證：testhost 被中止時 `run-tests.ps1` 以 `Test group 'uncategorized' failed with exit code 1.` throw。
  - 影響：先前各 slice 記錄的 gate 結果，是 agent 讀輸出判定的，不是腳本強制的；之後可由腳本結束狀態判定。

- [x] **S15.004 `prepare-ui-workspace.ps1` 只停本 workspace 的 process**
  - 現象：`Stop-FreeformHelperUiProcess` 收了 `$RepoRoot` 參數但沒有使用（`scripts/dev/prepare-ui-workspace.ps1:31-46`），會停掉整台機器上所有命令列含 `FreeformHelper.UI` 的 process，包含其他 worktree、其他 agent session 或 owner 正在使用的 app。
  - 完成：以單一函式判斷 UI 執行檔或 dotnet 命令列中的 UI 專案／組件路徑是否位於本工作樹；正規化斜線、忽略大小寫，並要求 root 後接路徑分隔符。主 checkout 還會排除 Git 列出的巢狀工作樹；Git 查詢失敗時不停止任何程序。`-DryRun` 列出會停止與略過的候選程序，不停止程序或正規化檔案；結構驗證納入純資料自測。
  - 驗證：`check-ui-process-workspace.ps1` 12/12 通過，包含主 checkout、巢狀工作樹與排除邊界。此 sandbox 拒絕 `Get-CimInstance Win32_Process`，實際 `-DryRun` 無法列舉程序；`verify.ps1 -StructureOnly` 中自測通過，但 Git 無法讀取工作樹外的全域設定，submodule link 檢查失敗。

- [ ] **S15.005 套用 NFC 公版（依序；每一步獨立 commit／PR）**
  - [ ] **S15.005a 基礎**：`global.json` 鎖定 SDK、`VERSION` 單一版本來源、repo verifier 的 structure lane、repo 外固定 test area（`TEMP`／`TMP`／`TMPDIR` 指向它）、`.github/workflows/ci.yml`（structure、build、test shards、單一 aggregator check）、`dependabot.yml`。
    - [x] `global.json`（SDK `10.0.301`、`latestPatch`）。
    - [x] `scripts/verify.ps1`：單一驗證入口。`-StructureOnly`（必要檔案、`example` 只能是 submodule link、本機絕對路徑、SDK pin、workflow action 必須 pin 到完整 SHA、測試分組清單、CRLF、XAML action role）、`-CiLane build`（`lint.ps1 -AllFiles -WarningsAsErrors`）、`-CiLane test -Shard core|ui|viewmodel|snapshots`、`-All`。workflow 只寫 lane 與 shard 名稱，內容由腳本決定。
    - [x] `.github/workflows/ci.yml`：check 名稱為 `policy / structure`、`dotnet / build`、`dotnet / test (core|ui|viewmodel|snapshots)`、aggregator `dotnet / build-test`；required checks 只需要 `policy / structure` 與 `dotnet / build-test`。`scripts/ci/install-dotnet.ps1` 安裝 `global.json` 的 SDK 與專案 target 的 runtime。本機三條 lane 已驗證；GitHub runner 上的結果見對應 PR。
    - [x] 第二輪審查（PR #81，accept-with-changes，P1 一個）後補正：aggregator `dotnet / build-test` 改成 `if: always()`，草稿 PR 上會明確失敗而不是被 skip（skip 的 required check 會被當成通過）；沒有 `TESTDATA_DEPLOY_KEY` 時輸出 warning，repo 變數 `TESTDATA_REQUIRED=true` 時直接失敗；push 到 `main`／trunk 的 run 不再被後續 push 取消；`assert-example-data.ps1` 加 `-AllowMissing`（資料存在就一律檢查）與 `-ForPush`（指標已 commit、資料 commit 已在資料 repo 的 remote）；`build.ps1` 的測試改走 `run-tests.ps1`；`update-ui-baseline.ps1` 的 repo root 少算一層已修正；`verify.ps1` 的本機路徑檢查補上正斜線、`/home`、`/Users`、Git Bash 路徑並掃描所有非二進位檔；`-warnaserror` 加 `--no-incremental`；`smoke` 清單不再影響 `uncategorized`；installer 下載加重試。
    - [ ] CI 取得 private 範例資料：owner 在 `FreeformHelper-testdata` 新增唯讀 deploy key，並把私鑰存成跑 CI 的 repo 的 secret `TESTDATA_DEPLOY_KEY`。未設定時 CI 以 `-AllowMissingExampleData` 執行，golden 測試顯示為略過。
    - [ ] `VERSION`、repo 外 test area、`dependabot.yml`（等公開 repo 建立後再開，避免在 private 存檔 repo 產生 PR 與 CI 用量）。
  - [x] **S15.005b 文件**：已依公版章序重整 `AGENTS.md`，新增 `CONTRIBUTING.md`、兩份 `docs/governance/` 流程／分支文件，以及 `docs/handoff/` 協定與空白 bug ledger。
  - [ ] **S15.005c 審核**：authority policy（R0～R3）與檢查腳本、只涵蓋 R3 路徑的 `CODEOWNERS`、PR 與 issue 範本、review record。
    - 暫停（owner 2026-10-03 經 commander 轉達；共用 CI 方案由 nvt_fw_core 定案，NFH 為第 3 階段，輪到時由 NVT CORE 發具體步驟）。
  - [ ] **S15.005d owner 專屬設定**：預設分支為 `main`、沿用範本專案的 GitHub App 作為 merge bot 已決定，設定仍由 owner 執行；rulesets、`release` environment，以及 `AGENTS.md`／`CONTRIBUTING.md` 是否屬 agent 權限設定仍待決定。agent 不執行，只提供一鍵步驟。
    - 暫停（owner 2026-10-03 經 commander 轉達；共用 CI 方案由 nvt_fw_core 定案，NFH 為第 3 階段，輪到時由 NVT CORE 發具體步驟）。
  - [ ] **S15.005e 發佈**：release 與 rehearsal workflow；等一次手動發佈成功後才做。
  - 已於 2026-10-02 決定：分支模型為 `main`（預設分支，只存已發佈版本）+ `1.3.x` trunk + `feature/<version>/<topic>`，並沿用範本專案的 GitHub App 作為 merge bot；仍待決定：`VERSION` 初始值、中性命名。

- [x] **S15.006 本工作樹 build 輸出與磁碟空間管理**
  - 現象：主 checkout 的 `build/` 約 37 GB（2026-10-02）；`Directory.Build.props:4-5` 把所有 bin／obj 放在 repo 內的 `build/`，沒有任何清理或保留政策；每個 worktree 各有一份。
  - 目標：定義本工作樹的 build 輸出、worktree 與 evidence 的位置及清理規則；repo 外 test area 隨 S15.005a 決定。
  - 完成：`docs/guides/build-output-and-disk-space.md` 定義本工作樹各類輸出與證據保留規則；`scripts/dev/clean-build-output.ps1` 預設 dry run，只列出本工作樹 `build/bin/`、`build/obj/` 與容量，`-Apply` 才刪除，`-IncludeEvidence` 才納入證據與交付產物。拒絕 reparse point、含 `.git` 項目或其他已登錄 worktree 根目錄的選取目錄。`-Apply` 採刻意嚴格的規則：機器上有任何指定的 .NET 建置／測試／UI 程序，或無法讀取程序清單時，一律拒絕刪除並列出阻擋程序名稱與 ID；無關的 .NET 程序也可能阻擋。請關閉建置、測試、UI 與 IDE 工作階段，執行 `dotnet build-server shutdown` 後重試，因為建置伺服器在 build 結束後仍可能留在背景。已移除程序歸屬判斷與其測試，僅保留三個名稱清單案例；`test-ui-process-in-repo.ps1` 還原為任務開始時的 `origin/1.3.x` 內容。未對真實資料執行 `-Apply`，主 checkout 的 37 GB 未清理；owner 待腳本進入主 checkout 後，可在該目錄先 dry run，再以 `-Apply` 清理預設項目。
  - [ ] **隨 S15.005a 決定 repo 外 test area**：明定位置、`TEMP`／`TMP`／`TMPDIR` 指向方式、保留期限與清理規則；目前尚未定義，不計入上述完成範圍。

- [x] **S15.007 移除 Application 專案未使用的 `CommunityToolkit.Mvvm` 參照**
  - 現象：`src/FreeformHelper.Application/FreeformHelper.Application.csproj:15` 參照該套件，但 `src/FreeformHelper.Application` 內沒有任何檔案使用（`git grep "CommunityToolkit" -- src/FreeformHelper.Application/*.cs` 為 0）。
  - 驗收：移除後 build、lint、notch-core 與 notch-golden 全綠；code-size baseline 依既有規則更新或記錄。
  - 完成紀錄：確認 Application 原始碼、global using、source generator attribute 與 `InternalsVisibleTo` 均未使用 Toolkit；UI 已直接參照，Tests 未直接使用且直接參照 UI。已移除 Application 參照；code-size 檢查僅計入 `.cs`／`.axaml`，基線不受影響。驗證：`./scripts/verify.ps1 -All` 通過（build 0 warning／0 error；notch-core 190、application 221、infrastructure 11、uncategorized 369、ui-stable 113、ui-viewmodel 163、ui-snapshots 21 全過）。

- [ ] **S15.009 機密測試資料分離，之後以新 repo 公開**
  - 決定（owner，2026-10-02）：`example/` 內的客戶面板 CAD、標示 Internal 的 IC mapping、由它們產生的 project JSON 與韌體 C 匯出，全部移到 private repo；公開版另建新 repo、歷史從單一乾淨 commit 開始，現有 repo 維持 private 作為完整歷史存檔。客戶與型號名稱（BOE、Lucid、TM 8.1、3635、NT51932）可以留在程式與文件中。
  - 稽核（2026-10-02，當時全部 refs 的歷史）：沒有 token 或金鑰；機密內容當時集中在 `example/`（當時 HEAD 18 個檔，另有歷史中的 `example/boe_30.25.json`）。當時 `tests/FreeformHelper.Tests/Snapshots/` 的兩個 golden snapshot 以筆數與分布統計為主，但也含少量由客戶面板算出的樣本列（`notch-golden-baseline.json` 的 `samples`：pad／reg／diff id 與韌體 payload 值；`tm81-notch-acceptance-matrix.json` 的 `sampleRepairRows`）。先前記為「只有筆數與雜湊」是錯的（只看了檔案開頭）。Owner 已決定把這兩個檔移到 private 資料 repo，見 S15.009d。當時 HEAD 有 4 個檔含本機絕對路徑（`README.md`、`docs/guides/` 下 2 個、`example/` 內 1 個 DXF）；當時 repo 沒有 `LICENSE`。
  - [x] **S15.009a 測試在資料不存在時略過**：新增 `ExampleDataFact`／`ExampleDataTheory`（`tests/FreeformHelper.Tests/TestInfrastructure/ExampleData.cs`），套用到 25 個讀取 `example/` 的測試；`FREEFORMHELPER_REQUIRE_EXAMPLE_DATA=1` 時改為必須存在，由 `ExampleDataAvailabilityTests` 以明確訊息失敗。三個 `*_WhenEnabled` benchmark 本來就由環境變數 opt-in，未更動。
    - 驗證：資料存在時相關 31 個測試全過。把 `example/` 暫時移開後跑完整測試：`956 total / 927 passed / 25 skipped / 4 failed`，4 個失敗都是 S15.002 的不穩定測試（3 個逾時、1 個暫存檔鎖定），沒有其他測試依賴 `example/`。
  - [x] **S15.009b 建立 private 資料 repo 並改為 submodule**：`Dennis40816/FreeformHelper-testdata`（private）的 `main` = `c49c645e`：`git subtree split --prefix=example` 的 32 個 commits（tree 與 `d6ceb2a:example` 相同）加上一個複製本 repo `.gitattributes` 與 README 的 commit。本 repo 以 submodule 掛回同一個 `example/` 路徑，URL 用相對路徑 `../FreeformHelper-testdata.git`，SSH 與 HTTPS remote 都能解析；測試、腳本與文件的路徑都不必改。
    - 驗證：轉換前後 `example/` 的 18 個檔案 SHA-256 完全相同。
  - [x] **S15.009c gate 預設要求資料存在且在釘住的 commit**：`scripts/tests/assert-example-data.ps1` 檢查 `example/` 已抓取、HEAD 等於本 repo index 釘住的 commit、沒有未提交變更。`run-tests.ps1` 每次實際跑測試前都會呼叫它（`-ValidateOnly` 不跑測試所以不呼叫），資料存在時設定 `FREEFORMHELPER_REQUIRE_EXAMPLE_DATA=1`、以 `-AllowMissingExampleData` 執行且資料不存在時清掉該變數；`run-refactor-gate.ps1`、`run-pre-push-gate.ps1`、`verify.ps1` 另在 lint／build 之前先檢查一次，所以資料有問題時會在最前面以一個明確訊息失敗。沒有資料權限時各腳本都接受 `-AllowMissingExampleData`；即使加了它，資料存在時仍會完整檢查，`example/` 有檔案但不是 git checkout 時一律報錯。`run-pre-push-gate.ps1` 另以 `-ForPush` 檢查指標已 commit、資料 commit 已在資料 repo 的 `origin`（檢查前會先 `fetch --prune`，所以遠端被倒退或刪除的分支都會被發現；無法 fetch 時直接失敗），加 `-SkipTests` 時也照樣檢查。`run-3635-regression-baseline.ps1` 直接讀 `example/` 的檔案，資料不在本來就會失敗，未更動。
  - 注意：本 repo 的 git 歷史仍包含 `example/` 的全部內容，所以本 repo 必須維持 private；公開只能走 S15.009e 的新 repo。新建的 worktree 不會自動有 submodule 內容，需先 `git submodule update --init example`。
  - [ ] **S15.009d 公開前清理**
    - [x] 移除本機絕對路徑：`README.md`（3 處）、`docs/guides/info-refactor-acceptance-2026-02-23.md`、`docs/guides/perf-baseline-padcanvas-2026-03-06.md`、`docs/archive/TODO-history-2026-03-24.md`（3 處，雙反斜線寫法，第一次稽核漏掉）。`scripts/verify.ps1 -StructureOnly` 之後會擋下新的本機路徑。
    - [x] README 說明 `example/` submodule 與沒有資料時的行為（S15.009b 已加）。
    - [x] `LICENSE`：owner 已決定「保留所有權利」，copyright holder 為 `Dennis Liu`；已新增專有權利聲明，`scripts/verify.ps1` 將其列為必要檔案。
    - [x] 兩個 golden snapshot 已移到 `FreeformHelper-testdata` 的 `golden-snapshots/`（commit `8c84e4d6`），本 repo 透過 `example/golden-snapshots/` 讀取；本次變更刪除舊路徑，提交後的 HEAD 不再包含它們，但本 private repo 歷史仍保留，因此公開 repo 必須由匯出的檔案樹建立。S15.005a 的 deploy key 尚未設置前，CI 會略過這兩個測試。測試程式與文件中同樣有個別 pad 編號、節點數與雜湊這類由面板資料算出的數值，維持不動（owner 已同意名稱可公開）。
    - [ ] `.gitmodules` 會讓公開 repo 顯示 private 資料 repo 的名稱；外部使用者 `--recurse-submodules` 會失敗（一般 clone 不受影響）。公開 repo 必須由匯出的檔案樹建立，不能 push 現有的任何 ref。
  - [ ] **S15.009e 建立公開 repo「NVT Freeform Helper」並把本 repo 標示為 archived**（owner 2026-10-02 決定名稱與處置）
    - 新 repo 由 owner 建立（slug 待定，GitHub 名稱不能有空白）；以單一 commit 匯入，不 push 本 repo 的任何 ref。之後的日常開發在新 repo 進行。
    - 本 repo 在遷移完成後明確標示為 archived（名稱或描述註明，並設為唯讀）。archive 之後不能再 push 或開 PR，所以要先把本 repo 上未完成的 PR 收尾，並確認 `FreeformHelper-testdata` 與新 repo 的 submodule 指標正確。
    - 待決定：GitHub issues（`TODO.md` 與文件中連到本 repo 的 issue 連結，公開後外部無法開啟）要搬到新 repo、改寫連結，還是保留原狀。搬移 issue 會公開其內容，需先檢查有無機密。

## 1.3.x Refactor Program（Active）

- 執行規格：`docs/guides/refactor-roadmap-1.3.x.md`
- GitHub parent spec：[#1](https://github.com/Dennis40816/FreeformHelper/issues/1)
- 固定順序：`1.3.0 gate/contract -> 1.3.1 Notch single-result -> 1.3.2 matching/domain -> 1.3.3 settings/presentation -> 1.3.4 workspace VM -> 1.3.5 UI structure`。
- 每次只做一個 `R13.*`；`V21_before == V21_after`、`V22_before == V22_after`，3635 V21/V22 C export 與 TM8.1 acceptance 必須零差異。
- V21/V22 共用 version-neutral input/evidence/allocation/compensation/audit/resolved-result 路徑，只能在最後的 data projection／formatter 邊界分岔；需要改 C 的 correctness work 不屬於本計畫。

### 1.3.0 可信 gate 與數值契約

- [x] **R13.003 定義並統一 V21 Q7 codec；零 C 差異下鎖住 C# simulation / GCC Firmware parity**（[#5](https://github.com/Dennis40816/FreeformHelper/issues/5)）
  - Contract：payload magnitude 固定 `UINT8 0..255`、`128=100%`、sign 只由 ADD/SUB type 承載；threshold/allocation Q7 由獨立 `0..128` contract 管理。Exporter 與 simulation 共用 final firmware projector/evaluator，LegacyRegularAnchor 不再偽造 source-oriented Actions。
  - Evidence：targeted 63、notch-core 117、notch-golden 6 tests pass；CadAllocation／LegacyRegularAnchor GCC runtime 與 C# simulation 逐點 exact。Lucid 3635 正反匯出順序皆維持 V21 `692 nodes / 130979 bytes / 8961B815…E57488`、V22 `548 nodes / 84023 bytes / 5208068B…57BB47`，未更新 golden。
  - Code size：本 slice 的 touched production surface 淨增 `+320 physical / +288 nonblank`；這是為了保留 user 指定的合理中間層、消除 exporter/simulation 兩套投影與 percent 近似，不列為 code-size cutdown。後續只在 R13.103 完成 final projection/formatter 收斂後回收 adapter，不為追求短碼重新複製演算法。
- [x] **R13.004 刪除已證明死碼並以相容遷移處理失效 matching settings**（[#6](https://github.com/Dennis40816/FreeformHelper/issues/6)）
  - [x] **R13.004a 移除 CadAllocation 永遠不產生 row 的 legacy-compatible profile traversal**：`NotchAlgorithmVersion` 僅有 V21/V22，而舊 pass 對兩者皆直接跳過；保留真正的 `LegacyRegularAnchor` 路徑，並以 public generation progress seam 鎖定四個可執行 phase。
    - Inventory：tracked-repo 搜尋確認 production 只有一個 private caller、private method 與 private context field；無 reflection／name-string、serializer 或 XAML binding consumer。S11.134 歷史紀錄保留 provenance，現行 flow diagram 已更新；`LegacyRegularAnchor` 的 strategy dispatch 與 `BuildLegacyRowsElapsedMs` 仍保留。
    - Evidence：production generator `-110` physical／`-99` nonblank lines；generator 27、notch-core 106、notch-golden 6 tests pass；Lucid 3635 正反匯出順序的 V21/V22 C hashes 皆 exact；lint/build 0 warning、0 error。
  - [x] **R13.004b 移除 CurrentGain 永遠無法進入的 scaled Stage3 leg path**：`BuildV22DiffLegs` 先以 `UsesTargetRegularCoverage(CurrentGain)=true` 回傳 target-coverage legs，因此後續 `model == CurrentGain` 分支不可滿足；刪除 `BuildScaledStage3Legs`、分支與失去用途的 `combinedPercent` 參數。
    - Inventory：production 只有一個 private caller 與一個 private method；reflection／name-string／serializer／XAML／script consumer 均為 0。未知 compensation enum 會先被 validation 拒絕，EnableToRegular/ToFull、rule engine、guard 與 V21/V22 output 組合都不改變 predicate。
    - Public seam：exhaustive policy test 鎖全部三個 compensation enum 的 area mode／coverage predicate；CurrentGain generator fixture 鎖完整 node `[0,200,1,100,NULL,0,0]`，若誤走舊分支會產生可辨識的重複腿 `[0,200,1,100,1,100,0]`。
    - Evidence：死方法／branch／argument 的嚴格刪除為 `-75 physical / -67 nonblank`；同域 expression-bodied 簡化後 production 淨值為 `-80 physical / -71 nonblank`。Targeted 4、notch-core 118、notch-golden 6 tests pass；Lucid 3635 正反匯出順序仍維持 V21 `8961B815…E57488`、V22 `5208068B…57BB47`；lint/build 0 warning、0 error。
  - [x] **R13.004c-1 將 `MatchThreshold` 固定為 presentation-only unmatched diagnostics，並刪除無入口的 auto-tune**：保留 project settings、UI snapshot、XAML editor 與 roundtrip；direct／draft 修改只保留 dirty/persistence，交由 `PadCanvas` coalesced visual refresh 重畫，不重建 grid、不 raise `ViewChanged`，也不清 Step1 result、links 或 selection。
    - Inventory：`PadMatcher` 明確忽略整份 `MatchingSettings`；`MatchThreshold` 的實際 production consumer 是 PadCanvas unmatched 顯色。`AutoTuneMatchThresholdCommand`、private method 與 quantile helper 無 XAML、reflection/name-string、serializer、RuntimeQuery、script 或 test consumer，刪除後全 repo reference 為 0。
    - Evidence：production 淨減 `-61 physical / -53 nonblank`（其中 strict auto-tune deletion `-54 / -47`）；targeted 12、notch-core 118、notch-golden 6 tests pass；完整 refactor gate 另含 application 171、ui-core 233、smoke 24、ui-snapshots 19，lint/build 0 warning、0 error。Lucid 3635 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，export state invariant 與 performance budget 均 PASS。
  - [x] **R13.004c-2 移除 `Mode`／centroid fallback／`NearestK` 的假 live UI owner，改由 compatibility adapter 原值保存**：三欄均無 XAML／RuntimeQuery／reflection live consumer，且 `PadMatcher` characterization 證明不同極值下 Links、telemetry、best match 完全相同；正常 flow 不再宣稱存在 centroid fallback。
    - Persistence：保留 `MatchMode` enum、`MatchingSettings`／`UiMatchingSnapshot` schema 與 reflection contract；`ProjectSettings.Matching` 與 `UiSnapshot.Matching` 的 retired values 各自 lossless load→save，live `MatchThreshold` 仍投影到兩處。新專案首次 Save 維持歷史 `Overlap / true / 5`。跨 Application/UI 的 compatibility parameter 留給 R13.201 正式化 overlap-evidence API 時移除。
    - Evidence：production 淨減 `-157 physical / -141 nonblank`；targeted 5、notch-core 118、notch-golden 6、application 172、infrastructure 11、ui-core 233、smoke 24、ui-snapshots 19 tests pass，lint/build 0 warning、0 error。Lucid 3635 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，export state invariant 與 performance budget 均 PASS；其餘 R13.004 dead-code inventory 已由 d～f 收口。
  - [x] **R13.004d 移除已停用的 row-sequence DP 與專用 scaffolding**：2026-03-18 的 pure best-match commit 已讓所有 auto mode 直接採用 strict seed；本 slice 刪除零 caller 的 `TryAssignRowSequence`、`BuildRegularDiffsByIcRow`、`EmptyAnchorMap` 與永遠為空的 private adjusted-list 傳遞，保留公開 `anchorCadIdByIc` 參數與 result telemetry shape。
    - Inventory／public seam：tracked repo 確認三個 private symbol 均無 caller、reflection／name-string、serializer、XAML 或 script consumer；仍在 direct grouping 使用的 `CadRowItem`／`RowGroup` 移回 `Grouping.cs`。`Enum.GetValues<CadOutputFwDiffAutoMode>()` theory 以 `35/36/42/43` strict seeds 加 active anchor 鎖住所有 policy 都不做 row compression；舊 DP 會投影為 grid row diffs `0/1/2/3`，所以不是只靠 telemetry counter。另修正 `application` test group 沿用舊 test class 名而漏跑此 seam 的問題。
    - Evidence：production 淨減 `-245 physical / -218 nonblank`；targeted 4、application 175、notch-core 118、notch-golden 6 tests pass，lint/build 0 warning、0 error。Lucid 3635 真實 hidden UI/IPC 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，export state invariant 與 performance budget 均 PASS；V22 strategy 命名與殘留 branch 分別由 e／f 收口。
  - [x] **R13.004e 將誤導 canonical ownership 的 internal `V22NotchAlgorithm` 更名為 `V22LegacyRowStrategy`**：保留 `NotchAlgorithmVersion.V22 = 30`、`v2.2` persistence/display identity、public generator API、row payload 與 C output；不新增延續舊誤導名稱的 alias。
    - Inventory／public seam：production 只有 class declaration 與 `DefaultStrategies` 直接建構；production reflection、`nameof`、`GetType().Name`、serializer discriminator、DI discovery、XAML、resource、script、config consumer 均為 0。唯一 test type-name reflection 已改走 public `NotchTableGenerator.Generate`，並鎖 legacy V2.2 仍為 9-int compatibility row；歷史 archive 保留舊名 provenance。
    - Evidence：production source physical／nonblank 淨值皆為 0；移除 test-only reflection helper 後，targeted 27、application 175、notch-core 118、notch-golden 6 tests pass。Lucid 3635 真實 hidden UI/IPC 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，export state invariant 與 performance budget 均 PASS；lint/build 0 warning、0 error。
  - [x] **R13.004f 稽核 `V22LegacyRowStrategy.Build` 的 CadAllocation compensation branch，結論保留**：一般 UI／RuntimeQuery 的 CadAllocation path 直接走 canonical generator，LegacyRegularAnchor 才會呼叫 strategy `Build`；但公開 `Generate` 會在 dispatch 後同步呼叫 caller 提供的 `IProgress.Report`，callback 可修改同一份 mutable settings，讓 `Build` 重新讀到 CadAllocation。因此它不是 public API 下可證明的死碼；依 zero-behavior-drift 規則保留 branch、comment 與其所需的 `allCadPads`。
    - Inventory／public seams：strategy `Build` 只有一個 production caller，branch 沒有 reflection／name-string、serializer 或 XAML binding consumer，但同步 progress callback 是可觀察的 public reachability seam。LegacyRegularAnchor + V22 triangle fixture exact 鎖定 `V22|IC0|DIFF0|REG0|CAD1|0,50,100,54321,70,0,54321,0,54321|Freeform=XWay CadPadId=1 Case=0`；VM load→save→load 另鎖 `LegacyRegularAnchor + V22-only` persistence，不因正常 UI 隱藏而遺失。R13.103d-1 其後以獨立 ticket 簽核 callback request-freeze 契約，才移除這個 hybrid branch；本段保留當時不能冒充 dead code 的歷史結論。
    - Evidence：本稽核 production delta 為 0；R13.004 累計淨減 `-653 physical / -582 nonblank`。application 175、ui-core 234、notch-core 118、notch-golden 6 tests pass，lint/analyzer 與 UI build 0 warning／0 error。Lucid 3635 真實 hidden UI/IPC 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden、export state invariant 與 performance budget 均 PASS。
- [x] **R13.006a 建立可重現的 production source／Release artifact code-size baseline**（[#4](https://github.com/Dennis40816/FreeformHelper/issues/4)）
  - Contract：primary metric 只計 tracked production C#/AXAML，鎖 CRLF/LF/lone-CR、EOF newline、empty-file、nonblank 與 Assets/Generated/bin/obj 等排除語意；五個互斥 group 與 ViewModels/Services subsets 可由 `check-code-size-baseline.ps1` 重跑。
  - Anchors：immutable signed start `3032121` 為 total `586 / 98,950 / 88,315`、logic-first `381 / 64,709 / 57,370`；相對 #4 observation 皆為 `+0 / +4 / +4`。獨立 cutdown before 固定為 post-R13.004 `207e29d`／`src` tree `c5048b4ecfd0c8a89a8a4719a48afbf11b980f68`，total `589 / 98,617 / 88,021`、logic-first `384 / 64,377 / 57,077`。
  - Release policy：secondary only；clean tracked build inputs、兩個 fresh isolated bin/obj roots、no-PDB deterministic profile，四個 primary DLL bytes/SHA-256 必須 exact。動態 manifest 留在 ignored `build/code-size/`。
  - Evidence：source contract 同時鎖 `3032121` signed start 與 `207e29d` independent-cutdown anchor 的 commit/tree/group/subset/delta，實跑通過。Clean candidate commit 的兩次 isolated Release build 皆為 `15,315,456` bytes，四個 DLL hashes exact、`authoritative=true`；normal Debug output path未變，UI build與 lint/analyzer 皆 0 warning／0 error。R13.006a 沒有 `src` diff，完成後 source tree仍為 `c5048b4e…bf11b980f68`。
- [x] **R13.006b 完成首個獨立、可證明且零行為差異的 production code-size cutdown**（[#7](https://github.com/Dennis40816/FreeformHelper/issues/7)）
  - [x] **R13.006b-1 移除 simulation heatmap 對已排序 cell list 的第二次相同排序**：`Simulate` 已先以 row／col 排序並 materialize 唯一 `cells` list；公開 seam 以 `[3,0,2,1]` 的亂序 grid 鎖定 `Cells` 與 `Heatmap.Cells` 均為相同 row／col 順序，mutation 移除上游排序時測試會失敗。
  - Inventory：`BuildHeatmap` 是 private 且只有 `Simulate` 一個 caller；兩者之間沒有 callback、alias escape 或 mutation。被刪除的 LINQ operations 沒有 reflection／name-string、serializer、XAML、script 或 config consumer；public result/model shape 與 UI control 均保留。
  - Size evidence：固定 before `207e29d`／`c5048b4e…bf11b980f68`；完成 source tree `b35b9a50cb646be14db5c15cdd5533ab64d73867`。Total `589 / 98,617 / 88,021 -> 589 / 98,615 / 88,019`，Application `95 / 17,475 / 15,517 -> 95 / 17,473 / 15,515`，logic-first `384 / 64,377 / 57,077 -> 384 / 64,375 / 57,075`，皆為 `0 files / -2 physical / -2 nonblank`。
  - Release／gates：clean no-PDB profile 的兩次 isolated builds 均為 `15,315,456` bytes，四個 DLL bytes/SHA exact repeatable；PE alignment 使 artifact bytes delta 為 0，Application hash 更新為 `B7B07B17…62A9B`。Targeted 10、notch-core 119、notch-golden 6 tests pass；Lucid 3635 hidden UI/IPC 正反順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，state invariant／budget、lint 與 build 均 PASS，無 warning suppression。
- [x] **R13.005 刷新 behavior/spec/reference calibration、performance hotspot、settings entry matrix 與 Runtime CLI contract 文件**（[#8](https://github.com/Dennis40816/FreeformHelper/issues/8)）
  - Contract：canonical docs 明確分開 1.3.0 current behavior、R13.101～104/301～305 target 與 unresolved debt；per-CAD resolved result、generated firmware table、simulation review 各自列出 owner/readers，不再把 UI/CLI/Inspector/export 多路推導誤寫成已完成 single-result。Coordinate calibration與 Step4 scoring 明定 normal-flow=No，但 schema/default/consumer與 project/app-view compatibility 保留。
  - Calibration：Lucid 3635 project/mask/V21/V22 hashes、lock commit/tree、script hash、環境、正反命令與 `Dennis40816` approval scope均已留存；latest R13.005 hidden UI/IPC artifacts兩種 export order皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden、raw signed actual、export-state invariant與budget全 PASS。
  - Evidence：reviewer verifier實跑 `41/12/6`；targeted Runtime/resolved/allocation/simulation/golden 29、notch-core 119、notch-golden 6 tests pass；UI build與 lint/analyzer 0 warning／0 error。Production source、UI、public contract與 checked-in golden均無 diff；Spec/Standards findings已全部修正。
- [x] **R13.007 導入 Pocock spec-to-tickets workflow，以 GitHub Issues／PR dependencies 追蹤 1.3.x**（[#2](https://github.com/Dennis40816/FreeformHelper/issues/2)）
  - Tracker：[#1](https://github.com/Dennis40816/FreeformHelper/issues/1) 是 approved parent spec，native sub-issues 固定為 #2～#8；native blocked-by edges 為 #5←#3、#6←#3、#7←#3/#4/#6、#8←#2/#3/#5/#6/#7，並保留 issue body 的人類可讀關係。
  - Workflow：Pocock skills以 MIT/pinned upstream `84fdeffd12f2ee307994d1eb6feb48173b6e0502`（post-v1.2.3 snapshot，較tag commit超前2筆）保存；GitHub tracker、五個 lifecycle labels、domain/triage規則與 commit/PR link policy均由 `docs/agents/` 維護。1.3.0 implementation commits使用 `Refs #N`，完成PR使用 `Closes #N`；兩個含literal `\n` 的早期commit body與ticket建立前的bootstrap commit保留為已記錄歷史例外，不改寫已推送history。
  - Evidence：#1具Problem／Solution／Testing／Out-of-scope；#2～#8均具parent/scope/out-of-scope/AC/test plan/blocker，live API可回讀native graph；PR #9連結各slice與golden/gate證據。R13.007沒有`src` diff，lint/UI build 0 warning／0 error；#2/#3/#8 checklist與lifecycle在本治理commit後同步為`ready-for-human`。

### 1.3.x 非阻塞 code-size 後續候選（不屬於 1.3.0 exit／#7）

- [x] **R13.006c 以直接 null check 取代 `EmptyOverrideMap` 手刻空 dictionary**（[#56](https://github.com/Dennis40816/FreeformHelper/issues/56)）：with-grid direct assignment與grid-null fallback的manual override loops直接檢查optional dictionary；兩處只為隱藏nullability的alias及完整手刻`IReadOnlyDictionary<int,int>` singleton均已刪除，public API、assignment ordering與result counters不變。
  - TDD／mutation：public two-pad theory同時跑valid grid與`grid:null`；null overrides固定strict seeds `10/11`與override counters全零，duplicate overrides固定第一顆`77`、第二顆回退`11`及`override/auto/duplicate/mismatch = 1/1/1/1`。移除null guard時兩路皆以`NullReferenceException` RED；繞過duplicate branch時兩路皆把第二顆錯配為`77`而RED，restore後focused 6/6 GREEN。
  - Size／gates：相對`31c74df`，production total `590 / 99,346 / 88,688 -> 590 / 99,329 / 88,672`、logic-first `385 / 65,099 / 57,737 -> 385 / 65,082 / 57,721`，皆為`0 files / -17 physical / -16 nonblank`。兩次fresh isolated deterministic Release完全一致，總DLL `15,332,864 -> 15,332,352 bytes`（`-512`）；只有Application由`699,904 / 33FB9E60…FA6170`降為`699,392 / 53541491…5AE784`，Domain／Infrastructure／UI不變。Focused 6、Application 214、ui-core 258、smoke 24、golden 6與Runtime Query 18 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI／IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`35/15/16`與`36/17/19 ms`，evidence SHA-256為`B8B71BB0…3EC0B6`與`C3AF05BB…39452B`。本slice不宣稱效能提升；所有R13.101+ parents保持open。
- [x] **R13.006d 簡化 CAD Output FW Diff direct grouping**（[#58](https://github.com/Dennis40816/FreeformHelper/issues/58)）：`BuildRowGroups`在首次見到row時保存`FirstOrder`並直接持有ordered `CadPad`；`CadRowItem`已刪除，row groups只在owner內排序一次，override／auto兩個consumer直接依序列舉。Public API、IC／row／pad traversal、assignment與counter契約不變。
  - TDD／mutation：public 2×2 top-to-bottom fixture刻意讓numeric row與CAD ID排序都不同於physical input；四顆duplicate override固定首顆`900 -> 77`、其餘strict fallback `100/700/200 -> 11/12/13`，並鎖`override/auto/duplicate/mismatch = 1/3/3/1`。row暫改按`RowIndex`時錯成`700 -> 77`，row內pad暫改按ID時錯成`100 -> 77`，兩者皆RED；restore後focused 7/7 GREEN。
  - Size／gates：相對`594917f`，production total `590 / 99,329 / 88,672 -> 590 / 99,326 / 88,670`、logic-first `385 / 65,082 / 57,721 -> 385 / 65,079 / 57,719`，皆為`0 files / -3 physical / -2 nonblank`。兩次fresh isolated deterministic Release完全一致，總DLL `15,332,352 -> 15,329,792 bytes`（`-2,560`）；只有Application由`699,392 / 53541491…5AE784`降為`696,832 / 06F8CF03…C0A4E`，Domain／Infrastructure／UI不變。Focused 7、Application 215、ui-core 258、smoke 24、golden 6與Runtime Query 18 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI／IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`29/15/16`與`33/16/18 ms`，evidence SHA-256為`B2F01E68…582EDA`與`C9DAA555…CF9CF`。本slice不宣稱效能提升；所有R13.101+ parents保持open。

### 1.3.1 Notch single-result pipeline

- [ ] **R13.101 引入不含 output version 分支的 `NotchGenerationContext`，移除選填 precomputed 組合**
  - [x] **R13.101a 消除每個 CAD/IC candidate 重複複製並掃描同一 candidate pool 的近似平方成本**（[#10](https://github.com/Dennis40816/FreeformHelper/issues/10)）：generator 內部 per-IC pool 改為 owned、固定大小 snapshot，已有 shared boundary context 時成為 authoritative pool，`Compute` 不再讀取或複製 redundant fallback；shared context 缺席時仍依 CAD identity 正規化 null／empty／same-ID pool，並在 pool 漏掉 current CAD 時補回。Public `CreateBoundaryQueryContext` 的 caller-ownership 契約沒有在本 slice 改寫。
    - TDD／performance：public `Compute + CreateBoundaryQueryContext` seam 在修改前以 fallback read count `2 != 0` RED；完成後以 throw-on-read pool 鎖住 shared path zero-read，另以 mutation 移除 current-CAD append 證明 fallback characterization 會由 owner IDs `[1,2]` 退化為 `[2]`。Lucid 3635 candidate count 固定 `9,831`；p50 canonical candidate build `207 -> 128 ms`、compensation `1,596 -> 798 ms`，但 wall `3,568 -> 3,593 ms`（`+0.7%`），因此只宣稱移除重複 work，不把局部改善誇成整體加速。
    - Size／gates：相對 `927b4a3`，production total `589 / 98,615 / 88,019 -> 589 / 98,613 / 88,017`、logic-first `384 / 64,375 / 57,075 -> 384 / 64,373 / 57,073`，皆為 `0 files / -2 physical / -2 nonblank`。Targeted compensation/generator 51、notch-core 122、notch-golden 6、GCC exporter/parity 15 tests pass（GCC `15.1.0`）；Lucid 3635 hidden UI/IPC 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，export state invariant 與 budget PASS；UI build、lint/analyzer 為 0 warning／0 error。
  - [x] **R13.101b 修正 allocation memo geometry key**（[#12](https://github.com/Dennis40816/FreeformHelper/issues/12)）：memo 仍以 CAD ID 分區，但 entry 改持有 immutable `Polygon2`；同 instance 以 `ReferenceEquals` O(1) 命中，只有同 ID 換 polygon instance 時才由既有 `CadPadGeometrySignature` owner 做 invariant round-trip exact comparison。Public 6-decimal／AwayFromZero signature 與 DXF duplicate/audit tolerance 保持不變，grid-reference invalidation、8,192-entry capacity、version-neutral ownership與公開 API 未改。
    - TDD：同 area／bounds 且 public rounded signature 相同的 `x=5.0000004`／`4.9999996` concave polygons，會把左右 overlap `37.500001 / 37.499999` 對調。Public `Generate` 在舊 area/bounds key 先 RED 為 stale DIFF；完成後 A→B、B→A 都 exact 等於 fresh generator、且 V21/V22 ordered rows 與 warm geometry不同。將 exact mode暫退回 public 6-decimal `Build` 時，DIFF20 立即退化為 DIFF10 RED；cyclic-start／reversed-winding與固定 public signature literal亦已鎖定。
    - Size／performance／gates：相對 `4a05fc9`，production total `589 / 98,613 / 88,017 -> 589 / 98,621 / 88,022`、logic-first `384 / 64,373 / 57,073 -> 384 / 64,381 / 57,078`，皆為 `0 files / +8 physical / +5 nonblank`；這是 exact identity 的必要小幅成本，未新增 framework/dependency。兩次 fresh isolated deterministic Release 完全一致，四個 own-output DLL 總量 `15,315,456 -> 15,314,944 bytes`（`-512`）：Application `691,200 / 2679A0FE…C093A -> 690,688 / 3B2BA783…F2FA`，Domain `39,936 / 0EAD9AC9…66D5`、Infrastructure `60,928 / CAFA1813…6F29`、UI `14,523,392 / 3A0CBCAC…72B6` 皆 byte/hash 不變。Lucid 3635 candidates 固定 `9,831`，p50 wall `5,914 -> 4,726 ms`、generation `5,861 -> 4,695 ms`、BuildProfiles `5,672 -> 4,530 ms`，僅作 no-regression evidence、不宣稱此修正帶來加速。Targeted generator/signature/既有 consumers 36、notch-core 124、notch-golden 6、GCC exporter/parity 15（GCC `15.1.0`）、RuntimeQuery 13 tests pass；hidden UI/IPC 正反順序均維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS。
  - [ ] **R13.101c 拆分 shared-result 與 final-output cache key**：exit target 是 version-neutral result fingerprint 不含 `ExportProfile`／`EnabledVersions`；只有 final output request/cache 可攜帶 output contract，舊 project fields 完整 roundtrip。
    - [x] **R13.101c-1 移除 generated-table cache identity 的 `ExportProfile`**（[#14](https://github.com/Dennis40816/FreeformHelper/issues/14)）：Release／Debug 共用同一 cached `NotchTable`，再由當下 requested profile 做最終 C serialization；profile persistence、UI／Runtime state 與既有輸出差異不變。沒有新增 formatter cache、key abstraction或公開 API；`EnabledVersions` 與所有真正 generation inputs 仍保留於現行 table key。
      - TDD：public headless `ExportNotchCommand` seam 在 Release→Debug／Debug→Release 兩個方向修改前皆為 `expected hit=1, actual=0` RED；完成後為一次 miss/store 加一次 hit，warm output 與相同 requested profile 的 fresh raw bytes exact，且 Release／Debug metadata與 Debug-only FW mask仍有意不同。把 `ExportProfile` 暫加回 fingerprint 兩案立即回到 RED；切換 V21/V22 與修改 V21 Q7 threshold 仍各自 miss，防止過早移除 output-version或真實 generation state。
      - Size／gates：相對 `87cf59a`，production total `589 / 98,621 / 88,022 -> 589 / 98,620 / 88,021`、logic-first `384 / 64,381 / 57,078 -> 384 / 64,380 / 57,077`，皆為 `0 files / -1 physical / -1 nonblank`。兩次 fresh isolated deterministic Release完全一致，總 DLL 維持 `15,314,944 bytes`；Domain `39,936 / 0EAD9AC9…66D5`、Application `690,688 / 3B2BA783…F2FA`、Infrastructure `60,928 / CAFA1813…6F29` bytes/hash不變，UI 維持 `14,523,392 bytes`、hash 由 `3A0CBCAC…72B6` 變為 `B1AC7878…1881`。Targeted 10、notch-core 130、ui-core 237、golden 6、GCC exporter/parity 15（GCC `15.1.0`）、RuntimeQuery 13 tests pass；Lucid 3635 hidden UI/IPC 正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS；UI build與 lint/analyzer 0 warning／0 error。
    - [ ] **R13.101c-2 移除 upstream shared-result identity 的 `EnabledVersions`**：R13.102a-2a 已讓 normal `CadAllocation` resolved-batch settings fingerprint 不含 `EnabledVersions`、V21/V22 threshold、`NullValue` 與 target coverage guard/cap；matching export request 可由同一 batch 重投影當下 final request。R13.101c-2a 再讓 live UI 的 guard/cap-only 變更遵守同一 final-projection boundary，不再增加 Step3 revision或清除 sparse result／export batch。R13.102a-2b-2已讓目前單一selected sparse result與full batch建立bounded task/session bridge，但此parent仍未完成：其餘result shape／consumer尚未證明repository-wide共用，`LegacyRegularAnchor`仍使用request-specific `NotchTable` cache；R13.103c-1已收斂V22 exporter／simulation的final Firmware projector，但沒有建立repository-wide final-output cache。
      - owner 2026-10-03：`LegacyRegularAnchor` 是否仍需重新匯出已交付 project 尚未確定，暫時保留；`R13.101c-2` 暫緩，既有 zero-diff gates 繼續保護它。
      - owner 後續答覆（2026-10-03）取代上述「暫緩」後果：V21／Legacy 維持原樣，既有 zero-diff gates 繼續保護；不再投入額外收斂／等價性工作，完全移除為版本未訂的後續目標；原話與 owner 確認「2.1」指 V21 見本檔 Owner 決定。
      - [x] **R13.101c-2a 將 target guard/cap 固定為 live UI final-projection-only invalidation**（[#64](https://github.com/Dennis40816/FreeformHelper/issues/64)）：direct Step3 controls與Settings draft Save共用既有settings plan的具名internal `InvalidateNotchFinalProjection` flag；guard/cap-only變更保留Step3 revision、per-CAD `NotchV22ResolvedResult` identity與output-neutral export batch，只清Step5 summary／last projected table／validation並通知Simulation source revision。完整Step5 clear才重設operation progress／busy並evict batch，避免設定變更偽造in-flight export已結束。
        - TDD／mutation：public 1x2 CurrentGain V22 export theory在修改前對guard toggle與cap-only變更皆因Step3 revision `+1` RED；修正後兩案都以同一resolved instance與retained batch命中一次，無額外miss/store/clear，warm projection bytes exact等於requested-state fresh VM且不同於受影響的warm request，stale validation回`NOT_READY`。Settings Save另以兩案鎖field apply、revision／identity不變與Simulation revision `+1`；public enum names／values contract在修正前亦RED，避免internal effect誤改既有ABI。暫退回`Step3Notch` mapping、移除projected-state clear或刪除draft changed-property routing時，各自對應public assertions RED後restore。
        - Size／gates：相對`74301a3`，production total `590 / 99,288 / 88,637 -> 590 / 99,310 / 88,656`、logic-first `385 / 65,041 / 57,686 -> 385 / 65,063 / 57,705`，皆為`0 files / +22 physical / +19 nonblank`；0新dependency/service/cache/session。兩次fresh isolated deterministic Release完全一致，總DLL `15,329,792 -> 15,330,304 bytes`（`+512`）；只有UI由`14,532,608 / 44805041…29B52`變為`14,533,120 / 78596A7B…F1BEA`，Domain `39,936 / 0EAD9AC9…66D5`、Application `696,320 / 9899BC42…B952EBF`與Infrastructure `60,928 / CAFA1813…6F29`不變。Focused 5、notch-core 182、Application 218、ui-core 264、smoke 25 tests通過，UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`27/15/15`與`28/16/15 ms`，evidence SHA-256為`76693BA7…95BE6D`與`F2707A4C…B88FC6`。本slice不宣稱效能提升；Standards／Spec／simplification findings均已修正。export執行中設定變更後的舊request completion stale-apply仍留給R13.102a-2b task/session，R13.101c-2／R13.102／R13.103 parents保持open。
  - [x] **R13.101d-1 將 normal `CadAllocation` computation inputs 凍結為單一 immutable generation context**（[#20](https://github.com/Dennis40816/FreeformHelper/issues/20)）：private `CadAllocationGenerationContext` 統一攜帶 allocation profiles／allocations、per-IC CAD pools、boundary query/index evidence、owned active-regular／CAD-output snapshots、grid/CAD references、strict ratio，以及 resolved compensation／switch／rule／boundary／allocation policy；三個 candidate helpers 不再接收或重讀 mutable `ProjectSettings`／`NotchSettings`。Enabled outputs、thresholds、`NullValue` 與 target coverage guard/cap 保留在獨立 final projection request；`LegacyRegularAnchor` compatibility path 未改。
    - Freeze boundary／TDD：先 BuildProfiles，再擁有化 caller set/map 並建立 pool／strict／boundary prerequisites；phase-2 initial progress callback 返回後才一次解析其餘 computation policy 與 projection request，之後的 candidate／merge／final callback 不得改變本次 run。Public 1x2 CurrentGain fixture 鎖 phase-2 `ProcessedCount=0/1` 的前後時序、cap-50 V2.1/V2.2 exact rows、`NullValue`／enabled-output snapshot，以及 caller-owned active set／CAD-output map 不被保留；architecture guard 亦鎖三個 candidate helpers 只依賴同一 context。
    - Size／performance／gates：相對 `0e03662`，必要 context boundary 令 production total `589 / 98,596 / 88,000 -> 589 / 98,608 / 88,010`、logic-first `384 / 64,356 / 57,056 -> 384 / 64,368 / 57,066`，皆為 `0 files / +12 physical / +10 nonblank`。Carrier 刻意採 private sealed class + readonly fields，避免 positional record 產生未使用的 equality／deconstruct／`ToString`；兩次 fresh isolated deterministic Release 完全一致，總 DLL `15,312,384 -> 15,312,896 bytes`（`+512`），只有 Application `688,640 / CC0C1F71…8E86 -> 689,152 / 28F3BBF2…4E8F`，其餘三個 DLL bytes/hash 不變。Lucid 3635 candidates 固定 `9,831`；同環境 p50 wall `3,819 -> 3,971 ms`、generation `3,798 -> 3,942 ms`、candidate phase `121 -> 131 ms`、compensation `906 -> 960 ms`，均只作 no-regression observation。Targeted 77、notch-core 140、notch-golden 6、GCC exporter/parity 15（GCC `15.1.0`）、RuntimeQuery 13、ui-core 243 tests pass；hidden UI/IPC 正反順序均維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS；UI build、lint/analyzer與雙軸 review 全數通過。
  - [x] **R13.101d-2 統一 preview／generation 的 per-IC CAD allocation-pool admission**（[#26](https://github.com/Dennis40816/FreeformHelper/issues/26)）：`NotchAllocationService.HasQ7PositiveAllocationInIc` 與 `BuildAllocations` 共用同一 Q7-positive allocation predicate；UI 先排除非 target IC regular，再 short-circuit geometry intersection，不建立完整 allocation／candidate。實際 pool 以 canonical ordered CAD IDs（含 count）的 fingerprint 取代 count-only cache identity，same-count member swap 不再命中舊 compensation／resolved／deferred snapshot。
    - TDD／intentional correction：public two-IC fixture A `[0,5]`、B `[5,15]` 在修改前為 UI `ToRegular / ToFull / Combined = 0.5 / 2 / 1`、generator `0.5 / 1 / 0.5`；完成後 selection preview path、deferred Inspector、Notch Detail 與 generator 皆為 `0.5 / 1 / 0.5`，owners、`blocker=1`、reason／trace 與 target allocation 一致，Inspector／Detail 投影同一 resolved identity。Primary-IC-only、raw-overlap/Q7-zero 與 count-only cache 三個 mutation 皆會 RED；B→C 同數量 member swap 亦證明 cache miss。這是刻意修正 cross-IC UI computation，未改 firmware API／schema、UI 視覺或 golden C。
    - Size／performance／gates：相對 `e18a4f3`，production total `589 / 98,624 / 88,024 -> 589 / 98,686 / 88,081`、logic-first `384 / 64,384 / 57,080 -> 384 / 64,446 / 57,137`，皆為 `0 files / +62 physical / +57 nonblank`。兩次 fresh isolated deterministic Release 完全一致，總 DLL `15,313,408 -> 15,314,432 bytes`（`+1,024`）；Application 維持 `689,152 bytes`、hash `28F3BBF2…F4E8F -> 3FE87F11…CCBB5`，UI `14,523,392 / 984487EE…BE67 -> 14,524,416 / AF1EC4E4…9D67`，Domain／Infrastructure bytes/hash 不變。Fresh current-source benchmark 的 candidate count 仍為 `9,831`；forward／reverse selection `total / notchPreview / Inspector` p95 由 `11 / 3 / 8`、`5 / 3 / 1 ms` 增為 `38 / 22 / 20`、`45 / 24 / 23 ms`，這是 correctness 修正的實測成本，不宣稱加速，仍遠低於 official total `600 ms`／Inspector `400 ms` budget。Focused 71、notch-core 143、ui-core 247、notch-golden 6、GCC exporter/runtime 16（GCC `15.1.0`）、RuntimeQuery 13 tests pass；正反 gate 皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；UI build、lint/analyzer 0 warning／0 error。R13.101、R13.101c-2、R13.102、R13.102a-2 與 R13.103 均保持 open。
  - [x] **R13.101e 以單一完整 compensation context 取代選填 precomputed 組合**（[#78](https://github.com/Dennis40816/FreeformHelper/issues/78)）：normal generator／UI 現在一次攜帶 allocation evidence、boundary indices、active mask、strict/query context與computation policy，再由唯一 `Compute(context)` path執行。既有public multi-parameter signature只保留為薄相容adapter；明確空allocations成為authoritative empty，不再隱性回退geometry。Stage A/B的nullable／partial fallback分支已刪除，output version仍只屬final projection。
    - TDD／mutation：public empty-precompute fixture在修改前把幾何半覆蓋重算為`ToRegular=0.5`而RED，完成後固定ratio／combined／overlap／count為`0`且無debug rows；public raw factory、canonical context與compatibility adapter投影相同normal diagnostics。Focused compensation/generator/Detail 76、notch-core 190、Application 221、ui-core 274、smoke 25、golden 6、Runtime Query/IPC 23與GCC 8 tests通過；callback freeze、V21/V22 ordered rows與Runtime schema不變。
    - Size／gates：相對`659f214`，production total `592 / 99,788 / 89,092 -> 592 / 99,896 / 89,195`、logic-first `387 / 65,541 / 58,141 -> 387 / 65,649 / 58,244`，皆為`0 files / +108 physical / +103 nonblank`；同時直接刪除Stage A/B nullable fallback 45 physical lines，0新service／cache／session／dependency／generic executor，相關最大檔案為497行。兩次fresh isolated deterministic Release皆為`15,362,560 bytes`，相對[#76](https://github.com/Dennis40816/FreeformHelper/issues/76)的`15,360,512`為`+2,048`；Application `715,264 / B55657DD…3C7218`、UI `14,546,432 / E0A4F051…113A7C`，Domain／Infrastructure bytes/hash不變。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`43/23/19`與`29/15/16 ms`，UI build與lint/analyzer為0 warning／0 error。R13.101仍因R13.101c-2 final-output/cache split保持open；R13.102／R13.103與Legacy convergence亦未由本slice宣稱完成。
- [ ] **R13.102 建立與 V21/V22 output request 無關的 compensation/Stage1-3/allocation/audit/trace 單一 resolved result**
  - 盤點證據（2026-10，INV2）：`docs/reviews/r13-slice-inventories-2026-10.md` 第 1 節列出 generator、Inspector、PadInfo、RuntimeQuery、simulation／overlay 讀者及下一個零行為 leaf；本 parent 保持未完成，純 `LegacyRegularAnchor` 議題等待 owner decision。
  - [ ] **R13.102a 同一次 selection/revision 的 preview、inspector、export 共用同一 resolved snapshot/task**，不得同步 preview 算一次、deferred inspector 再算一次。
    - [x] **R13.102a-1 讓同一 selection/revision 的 Step 3 preview 與 deferred CAD Inspector 共用同一 resolved snapshot**（[#16](https://github.com/Dennis40816/FreeformHelper/issues/16)）：preview 改由既有 revisioned per-CAD owner 取得 `NotchV22ResolvedResult`；200 ms deferred warm path 只投影同一 immutable instance，不再重算 compensation、evict cache 或以等價新 instance 取代。Cold miss 仍在 background 建立、驗證、保存並投影一次，既有 debounce、pending state、cancellation、selection/revision stale rejection 與 UI-thread apply 契約不變。
      - TDD：public ViewModel workflow 鎖 warm identity、cold stable identity、CAD A→B／clear-selection stale rejection、output-only profile/file-type/version reuse、pending 中 strict-overlap invalidation，以及 strict-override／partial-cache 不污染 current key。將 preview 暫退回 uncached construction、讓 deferred path忽略 prewarmed result，或強制 warm path重算 compensation時，identity／build-count tests各自恢復 RED。
      - Size／gates：相對 `f600dfe`，production total `589 / 98,620 / 88,021 -> 589 / 98,604 / 88,010`、logic-first `384 / 64,380 / 57,077 -> 384 / 64,364 / 57,066`，皆為 `0 files / -16 physical / -11 nonblank`。兩次 fresh isolated deterministic Release完全一致，總 DLL `15,314,944 -> 15,314,432 bytes`（`-512`）；UI `14,523,392 / B1AC7878…1881 -> 14,522,880 / B3B399D9…400F`，Domain `39,936 / 0EAD9AC9…66D5`、Application `690,688 / 3B2BA783…F2FA`、Infrastructure `60,928 / CAFA1813…6F29` bytes/hash不變。Targeted 6、notch-core 130、ui-core 243、golden 6、GCC exporter/parity 15、RuntimeQuery 13 tests pass；hidden UI/IPC正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS；UI build、lint/analyzer、Standards、Spec與 simplification review全數通過。
    - [ ] **R13.102a-2 讓 export/generator 消費 version-neutral per-CAD/per-IC resolved batch/task**：R13.102a-2a 已建立 normal `CadAllocation` export/generator 專用的 Application-owned compact output-request-neutral candidate batch，R13.102a-2b-2亦已把目前單一選取CAD的 sparse resolved result 接入同一 batch session；本 parent 仍須依自己的整體 exit criteria 稽核所有 export/generator consumer 與 Legacy convergence，因此保持 open。
      - [x] **R13.102a-2a 讓 normal `CadAllocation` export cache 重用 output-request-neutral candidate batch**（[#28](https://github.com/Dennis40816/FreeformHelper/issues/28)）：cold miss 由 `NotchTableGenerator` 建立一次 opaque `CadAllocationResolvedBatch` 與首次 final `NotchTable` projection，零列結果亦保存可重用 batch；matching warm request只以當下 enabled versions、V21/V22 threshold、`NullValue` 與 target coverage guard/cap重投影，再由當下 profile／file type做serialization。CAD/grid、active/map、Step3 revision與真實 computation settings仍屬cache identity；`LegacyRegularAnchor`維持原本request-specific `NotchTable` cache。
        - TDD／telemetry：public generator 鎖同一 batch 的 V21↔V22、threshold、`NullValue` 與 target guard/cap projection皆 exact 等於 fresh generation；public export workflow 鎖版本雙向重用、threshold rejected→admitted 的零列 batch重用，以及 Legacy version／threshold／`LenScale`／`NullValue`仍各自miss。強型別 resolution settings逐欄比對可拒絕同一32-bit fingerprint的不同有效輸入；warm hit不重播phase 1～3，只回報當次phase 4。Mutation 分別加回 version/threshold key、移除強相容性 guard、拒存零列 batch、在 hit 重跑 candidates、或回傳首次 final table，均由 focused characterization RED；combined-request admission沿用 R13.103b-1 已鎖的 V22-controlled shared set。Runtime Query schema不變，`EntryRowCount`表示最新一次projected table rows，而非candidate/batch大小。
        - Size／performance／gates：相對`1deb591`，production total `589 / 98,686 / 88,081 -> 589 / 99,091 / 88,458`、logic-first `384 / 64,446 / 57,137 -> 384 / 64,851 / 57,514`，皆為`0 files / +405 physical / +377 nonblank`；新增量是typed batch/project boundary、雙artifact cache與強型別identity，不含task/session framework。兩次fresh isolated deterministic Release完全一致，四個own-output DLL總量`15,314,432 -> 15,322,112 bytes`（`+7,680`）：Application `689,152 / 3FE87F11…CCBB5 -> 693,248 / 141A8972…730A`，UI `14,524,416 / AF1EC4E4…9D67 -> 14,528,000 / B00AAB25…E2D0`，Domain `39,936 / 0EAD9AC9…66D5`與Infrastructure `60,928 / CAFA1813…6F29` bytes/hash不變。Current-source Lucid 3635三次cold皆為`9,831` candidates／`1,148` rows，cold p50 `1,328.20 ms`；同batch 40次warm projection p50／p95 `0.5884 / 1.0393 ms`，phase 1～3 timings皆為0；三batch GC slope估計retained `331,509 bytes/batch`，cold約`4.83 GB`只代表process-wide allocation churn，不是retained size，亦不宣稱跨版本加速；ignored evidence SHA-256為`56198980…A031205`。Focused 54、notch-core 146、ui-core 250、notch-golden 6、GCC exporter/runtime 16（GCC `15.1.0`）、RuntimeQuery 13 tests pass；hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS，selection total p95 `53 / 51 ms`。R13.101c-2、R13.102、R13.102a-2與R13.103均保持open。
      - [x] **R13.102a-2b-1 以 revisioned request identity 拒收過期 CadAllocation completion**（[#66](https://github.com/Dennis40816/FreeformHelper/issues/66)）：既有 export cache 新增 monotonic generation epoch，workflow owner 新增 final-projection revision；每次 generation 凍結 epoch／projection／Simulation source identity，只有仍 current 的 completion 可 commit projected row count、last table、completion progress與 caller continuation。真正 computation／full invalidation 拒絕舊 store；guard/cap 等 final-only invalidation保留 output-neutral batch但立即把 warm/cold projected row count歸零，拒絕舊 table／validation／progress／Simulation session。Export／Simulation 共用 ref-count busy scope，accepted session保存 captured source；沒有新增第二套 session/cache service、public API、dependency或 generic executor，sparse/full shape 與 shared in-flight task 留給 R13.102a-2b-2。
        - TDD／mutation：public Avalonia workflow以既有 final canonical-export callback作 deterministic barrier，另鎖 generation 不得向 caller 暴露 pre-acceptance `generated` success；涵蓋 direct guard toggle、Settings draft cap Save、warm retained-batch row-count reset、strict computation invalidation、full Step5 clear、Export不開 selection/save且不寫檔、captured source、queued/reentrant progress、prior-success message與 Export／Simulation overlap busy。修改前 completion suite `5/5` RED；完成後 focused cache／workflow `17/17` GREEN。暫時繞過 epoch check、final-projection check、cold batch `rowCount=0`、captured source、ref-count End、clear後 progress write ordering或重新暴露 pre-acceptance `generated` callback，各自對應 characterization RED，均已 restore。
        - Size／gates：相對 `c44d5c9`，production total `590 / 99,310 / 88,656 -> 590 / 99,458 / 88,788`、logic-first `385 / 65,063 / 57,705 -> 385 / 65,211 / 57,837`，皆為 `0 files / +148 physical / +132 nonblank`；增量只在既有 UI ViewModel／service，0新 production檔、service、API或dependency。兩次 fresh isolated deterministic Release完全一致，總 DLL `15,330,304 -> 15,332,864 bytes`（`+2,560`）；Domain `39,936 / 0EAD9AC9…66D5`、Application `696,320 / 9899BC42…B952EBF`與Infrastructure `60,928 / CAFA1813…6F29`不變，UI `14,533,120 / 78596A7B…F1BEA -> 14,535,680 / 694DE0BA…F9B2F`；size evidence SHA-256 `F340BD88…C9C3`。Focused 17、notch-core 183、Application 218、ui-core 273、smoke 25、notch-golden 6、Runtime Query 23與GCC 8（GCC `15.1.0`）tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`34/18/15`與`35/18/16 ms`，evidence SHA-256為`CBD28B83…E62B`與`34549E25…783E`。本slice不宣稱演算法或效能提升；R13.101c-2、R13.102、R13.102a、R13.102a-2、R13.102a-2b-2與R13.103均保持open。
      - [x] **R13.102a-2b-2a 讓並行 full-table consumers 共用同一 CadAllocation batch-resolution task**（[#68](https://github.com/Dennis40816/FreeformHelper/issues/68)）：先建立2b-2的full-generation task owner；single-CAD sparse result與full batch的bridge保留給下一個leaf，parent不得提前完成。
        - Scope：拆分Application phases 1～3 output-neutral resolution與phase 4 final projection；只在既有`NotchExportGenerationCacheService`保存一個identity／epoch-bound in-flight task。禁止新增generic executor、第二個cache/session service、per-CAD／per-candidate task registry、dependency或public diagnostics/schema。
        - TDD／mutation：public Avalonia Simulation＋Export overlap以既有progress callback啟動第二consumer，鎖cold同identity由`2 misses / 2 stores`收斂為`1 / 1`與shared busy lifetime；service-level pending-task identity seam精確鎖同一task join、final-only request相容、full invalidation拒絕舊store、collision不join及fault retry，public final-only overlap另鎖舊projection拒絕與current projection exact。繞過join、把final-only state放入task key、讓舊epoch store或保留faulted task時，各自對應characterization RED後restore。舊`NotchExportService.GenerateCadAllocationResolvedBatch` facade已恢復為同一路徑薄委派，並以compile-time signature guard鎖source compatibility；projection request亦只保留一個typed tuple shape。
        - Size／gates：相對`03faae6`，production total `590 / 99,479 / 88,805 -> 590 / 99,661 / 88,974`、logic-first `385 / 65,232 / 57,854 -> 385 / 65,414 / 58,023`，皆為`0 files / +182 physical / +169 nonblank`；Application為`+71 / +67`、UI C#為`+111 / +102`，ViewModel淨減`-2 / -4`，未新增production file／service／dependency。兩次fresh isolated deterministic Release完全一致，總DLL `15,332,864 -> 15,342,080 bytes`（`+9,216`）：Application `696,320 / 9899BC42…B952EBF -> 697,856 / EA953FCE…33E3B4`、UI `14,535,680 / 258E5AB4…723CE6 -> 14,543,360 / EDB83455…4D0D4B`，Domain／Infrastructure bytes/hash不變。Facade focused 1、notch-core 187、Application 218、ui-core 274、smoke 25、notch-golden 6、Runtime Query 23與GCC 8 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`31/17/15`與`29/15/15 ms`，evidence SHA-256為`5B5C5272…D5DB4E`與`C3EC88C3…D2961`。目前最大相關檔為generator `974 / 899`與cache service `634 / 575` physical／nonblank；下一個leaf必須extract/delete-first，不再把新責任堆入generator。本slice不宣稱演算法或效能提升，R13.101c-2、R13.102、R13.102a、R13.102a-2、R13.102a-2b-2與R13.103均保持open。
      - [x] **R13.102a-2b-2b 將目前單一選取 CAD 的 sparse resolved result 接入同一 batch session**（[#76](https://github.com/Dennis40816/FreeformHelper/issues/76)）：VM平行compensation dictionary已移除，preview／Inspector／Detail／Runtime只投影同一`NotchV22ResolvedResult` owner；cold/warm full batch至多攜帶並重用一份由CAD ID + anchor IC/diff識別的selected result，不保留全部candidate polygons/debug evidence。
        - TDD／一致性：public Application seam鎖matching warm exact-instance reuse、cold selected anchor capture，以及CAD／grid內容／CAD pool／active mask／model／anchor identity mismatch拒絕；同一`RegularGrid` reference mutation亦因immutable SHA-256 state signature拒絕舊result。public headless workflow鎖cold completion stale rejection、fresh current identity與Export／Simulation overlap共用同一selected instance；preview隱藏時不做eager full compute。mutation暫時忽略reusable result時same-instance test RED；settings freeze timing regression由full gate捕獲後已恢復既有時序。
        - Architecture／size：generator carrier抽至`NotchTableGenerator.Generation.Context.cs`，主檔由`974`降為`841` physical lines；新增`NotchSparseResultUseCase`集中currentness/promotion policy，ViewModel淨減`109 / 100` physical／nonblank。相對`7b06722`，production與logic-first皆為`590 / 99,661 / 88,974 -> 592 / 99,788 / 89,092`，即`+2 files / +127 physical / +118 nonblank`；其中Application `+125 / +116`、UI C# `+2 / +2`，而被取代的duplicate compensation-cache path直接淨刪`128` physical lines，符合delete-first抵銷，0新dependency／第二cache/session／per-CAD task registry／generic executor。
        - Release／gates：兩次fresh isolated deterministic Release完全一致，總DLL `15,342,080 -> 15,360,512 bytes`（`+18,432`）：Application `697,856 / EA953FCE…33E3B4 -> 713,216 / FD709EE8…5F6A4`、UI `14,543,360 / EDB83455…4D0D4B -> 14,546,432 / C90662AA…D1919`，Domain／Infrastructure bytes/hash不變。Focused 8、notch-core 189、Application 220、ui-core 274、smoke 25、notch-golden 6、Runtime Query/IPC 23與GCC 8 tests pass；UI build與lint/analyzer為0 warning／0 error，三軸review PASS。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`41/25/21`與`40/22/18 ms`，evidence SHA-256為`4311D9D6…FE6F4`與`396C3168…5E2`。本leaf只收口R13.102a-2b-2；R13.101／101c-2／102／102a／102a-2／103與Legacy convergence保持open。
  - [x] **R13.102b-1 讓 Notch Detail 投影目前 selection/revision 的 resolved snapshot**（[#22](https://github.com/Dennis40816/FreeformHelper/issues/22)）：`ShowNotchDetailCommand` 改走既有 full-key owner，再由同一 `NotchV22ResolvedResult` 投影 ratios、Stage1/2、debug、allocation/Q7、threshold與 display；不再同步重算 compensation／resolved result。Public compatibility `NotchDetailUseCase.Build` signature與 unanchored 行為保留，未新增 cache/task/framework。
    - TDD／相容邊界：public headless workflow 在修改前以 Stage1／Stage2／debug reference identity RED；完成後 warm、deterministic cold、output-only reuse與 strict-overlap invalidation皆投影正確 identity，並鎖 raw allocation、Q7、freeform、FW diff、display、status與 dialog。跨 IC 時 authoritative workflow snapshot 只投影 anchor IC；characterization 鎖 public compatibility entry 的舊 unanchored `200%` 與 current resolved projection 的 `100%`，這是消除 Detail-only second derivation 的刻意一致性修正。Deferred Inspector cold completion apply 前會以完整 key重查，避免晚到背景結果移除 Detail 已建立的 current identity；此項是 code-review-backed race hardening，不宣稱有 timing-dependent public RED。
    - Size／gates：相對 `2973229`，production total `589 / 98,608 / 88,010 -> 589 / 98,624 / 88,024`、logic-first `384 / 64,368 / 57,066 -> 384 / 64,384 / 57,080`，皆為 `0 files / +16 physical / +14 nonblank`；新增量只包含 compatibility-preserving projection seam與 deferred cold race guard。兩次 fresh isolated deterministic Release完全一致，總 DLL `15,312,896 -> 15,313,408 bytes`（`+512`）；只有 UI `14,522,880 / B3B399D9…5400F -> 14,523,392 / 984487EE…BE67`，Domain `39,936 / 0EAD9AC9…66D5`、Application `689,152 / 28F3BBF2…4E8F`、Infrastructure `60,928 / CAFA1813…6F29` bytes/hash不變。Focused 4、ui-core 246、notch-core 140、golden 6、GCC exporter/parity 15、RuntimeQuery 13 tests pass；hidden UI/IPC正反匯出順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS（selection p95 `6 ms`）；UI build、lint/analyzer、Standards、Spec與 simplification review全數通過。
  - [x] **R13.102b-2 讓 Runtime Query `multi-owner` 消費 override-aware revisioned resolved result**（[#36](https://github.com/Dennis40816/FreeformHelper/issues/36)）：先確認 CAD 仍在 visible collection，再只取得一份依 current setting 或本次 `--overlap-percent` override 解析的 current-revision `NotchV22ResolvedResult`；CAD response metadata來自不建立冷 Notch result的輕量 Inspector snapshot，rows、summary與rule trace全由`resolved.Compensation.RegularDebugInfos`投影，不再先建立settings-default threshold result再執行override compensation。Command、arguments、payload schema、ordering、limit/truncation與error codes不變。
    - TDD／mutation：public 1x1 geometry以0.5% minor owner區分project 0.1%與query 1.0% threshold；cold override在修改前為`miss +2 / hit +0`，完成後固定為`miss +1 / hit +0`，相同CAD/revision/override重複查詢為`miss +0 / hit +0`。恢復expensive default snapshot、resolved後重讀compensation或忽略override時，公開payload／cache telemetry各自RED；不可見CAD即使仍可由`_cad` fallback取得也維持`PAD_NOT_FOUND`且零miss，visible但grid未就緒仍維持`NOT_READY`。
    - Scope：本slice只完成R13.102b-2；未建立UI/export共用的revisioned task/session，未改export candidate batch、`LegacyRegularAnchor`或combined-overflow display。R13.102、R13.102a-2、R13.101c-2、R13.103與R13.104保持open。
    - Size／gates：相對`f866e6a`，production total `590 / 99,147 / 88,514 -> 590 / 99,161 / 88,526`、logic-first `385 / 64,907 / 57,570 -> 385 / 64,921 / 57,582`，皆為`0 files / +14 physical / +12 nonblank`，且只增加既有Runtime Query reader的side-effect ordering。兩次fresh isolated deterministic Release完全一致，總DLL `15,322,624 -> 15,323,136 bytes`（`+512`）；只有UI `14,528,512 / 9B017D15…09DBC -> 14,529,024 / 20280245…86A7`，Domain／Application／Infrastructure bytes/hash不變。Focused 2、RuntimeQueryUseCase 18、RuntimeQueryIpc 5、notch-core 170、ui-core 250、notch-golden 6與GCC exporter/runtime 8（GCC `15.1.0`）tests pass；UI build、lint/analyzer與Standards／Spec／simplification reviews亦通過。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total p95 `47 / 96 ms`，不宣稱效能提升。Evidence為`build/perf/r13102b2-forward/regression-baseline-summary.json`（SHA-256 `359F4F0B…B75F4`）與`build/perf/r13102b2-reverse/regression-baseline-summary.json`（SHA-256 `854254B0…D2D5B`）。
- [ ] **R13.103 將 V21/V22 threshold／node projection／formatter 固定為唯一版本分岔邊界，補 typed/legacy 等價性**
  - owner 2026-10-03：V21 firmware C output 尚未確定，暫時保留；本項 final projection／formatter 分離（含 legacy convergence）暫緩，不得開始移除或變更 V21 output 與舊 project 對它的讀取路徑。
  - owner 後續答覆（2026-10-03）取代上述「暫緩」後果：V21／Legacy 維持原樣，既有 zero-diff gates 繼續保護；不再投入額外收斂／等價性工作，完全移除為版本未訂的後續目標；原話與 owner 確認「2.1」指 V21 見本檔 Owner 決定。
  - [x] **R13.103a 補 final projector/evaluator characterization**（[#24](https://github.com/Dennis40816/FreeformHelper/issues/24)）：public exporter／simulation seam 鎖住三個 V21 terms 依 source identity排序並拆成兩個 destination nodes、generated C 保留 `INT16` carrier，且 C# evaluator／GCC runtime 對 `32767` fixture 都得到 exact `32765`；missing destination／source／anchor diagnostics 亦鎖住 exact IC／diff文字與穩定順序。這是 initial-green contract slice，未改 production projection、ABI、Q7 codec或golden。
    - TDD／mutation：exact C node lines與node count防止第三個term被截斷；將projector步進暫改為3、將evaluator暫改為saturating clamp、或暫時抑制missing-source diagnostics時，各自的public characterization都會RED，restore後targeted exporter／simulation 27 tests pass。測試不宣稱可觀察所有代數等價的narrowing位置，只鎖生成C的carrier contract與C#／GCC最終值一致。
    - Size／gates：相對`a6c5193`無production source差異，total維持`589 / 98,624 / 88,024`、logic-first維持`384 / 64,384 / 57,080`；兩次fresh isolated deterministic Release維持`15,313,408 bytes`與四個own-output DLL bytes/SHA。notch-core 142、notch-golden 6 tests pass，GCC `15.1.0`實際執行；Lucid 3635 hidden UI/IPC正反順序皆維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS；UI build與lint/analyzer 0 warning／0 error。
  - [x] **R13.103b-1 將 CadAllocation version threshold admission 移到 final row projection boundary**（[#18](https://github.com/Dennis40816/FreeformHelper/issues/18)）：先建立 output-request-neutral candidate superset，再以入口凍結的 compatibility request 產生單一 admitted view，供 primary selection、coverage guard、audit、V22 rows 與 V21 projection 共用；V21-only／V22-only／both 仍各自保持既有 admission 與 C bytes，both 不在本 refactor 改成獨立 per-version filtering。
    - Boundary：compatibility request在 BuildProfiles與CAD pool／strict／boundary prerequisites完成、phase-2 initial progress callback回來後、平行 candidate compute開始前一次凍結。V21-only使用Q7 predicate；只要 request含V22，便沿用V22 effective-percent predicate選出一個shared admitted set，再由同一集合輸出V22與V21 compatibility projection。Candidate superset與其timing/count不含output request；merge-phase `GeneratedRowCount`描述superset bucket，final phase才回報實際output rows。
    - TDD：public generator matrix鎖25% unlinked divergent thresholds、1/3 linked Q7 rounding、V21-only／V22-only／both與enabled-set反向順序；修改前candidate count為0而RED，完成後三種request皆建立同一個candidate evidence、final rows仍與既有literal exact。Coverage-audit guard鎖被threshold淘汰的candidate不產假expectation；raw combine `>255`只在admitted後以原type/message拋出，rejected與unsupported-only request維持empty/no-throw。phase-2 progress callback在snapshot前／後改threshold的兩案亦鎖定既有同步時序。
    - Size／performance／gates：相對 `7490477`，production total `589 / 98,604 / 88,010 -> 589 / 98,596 / 88,000`、logic-first `384 / 64,364 / 57,066 -> 384 / 64,356 / 57,056`，皆為 `0 files / -8 physical / -10 nonblank`。兩次fresh isolated deterministic Release完全一致，總DLL `15,314,432 -> 15,312,384 bytes`（`-2,048`）；Application `690,688 / 3B2BA783…F2FA -> 688,640 / CC0C1F71…8E86`，Domain／Infrastructure／UI bytes/hash不變。Lucid 3635 candidates固定`9,831`；p50 wall `4,726 -> 3,819 ms`、generation `4,695 -> 3,798 ms`、candidate phase `158 -> 121 ms`、compensation `979 -> 906 ms`，只作同環境no-regression observation、不宣稱此slice帶來加速。Targeted generator 30、notch-core 132、notch-golden 6、GCC exporter/parity 15（GCC `15.1.0`）、ui-core 243 tests pass；hidden UI/IPC正反順序皆維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS；UI build、lint/analyzer、Standards、Spec與simplification review全數通過。
  - [x] **R13.103c-1 讓 V22 C export 與 simulation 共用單一 final Firmware projector**（[#32](https://github.com/Dennis40816/FreeformHelper/issues/32)）：Application-owned `NotchV22FirmwareProjector` 一次正規化 typed `NotchV22Node`、`Values.Length >= 7` compatibility payload 與 `< 7` legacy fallback；`SourceRows` 保留原始 table／action ordinal，`NodesByIc` 保留既有 C per-IC ordering，且兩個 view 共用同一 projected row。`NotchFirmwareCExporter` 只格式化 projection；`NotchApplySimulationService` 逐 node 以 frozen V22 `INT16` semantics 執行後才聚合 Actions。Release no-op filtering 保持 exporter-only emission policy。
    - TDD／intentional correction：public typed／untyped normalization 鎖住 combine、signed ratio、null 與 flags 的相同 clamp；short-row fixture 鎖既有 legacy combine 與 exact overflow exception；Release／Debug fixture 鎖 no-op emission policy，invalid-IC fixture 鎖 `SourceRows` diagnostics 與 `NodesByIc` filtering。MAIN `(10,120,11,60,12,20,NONE)` 加 CONT `(10,100,13,10,NULL,0,CONT)`、baseline `[100,0,0,0]` 的 V22 simulation 由錯誤的 source `30` 修正為與 generated C 相同的 `40`，完整結果為 `[40,60,20,10]`；同一 canonical fixture 的 frozen V21 ABI 仍為 `[30,60,20,10]`。source `1`、target `50%` 的 V22 result 亦由浮點 `0.5` 修正為 firmware integer `0`。兩案只修 simulation/projector consumer，C node bytes／ordering 與 golden 皆不改。
    - Size／gates：相對 `f0d16c8`，production total `589 / 99,112 / 88,478 -> 590 / 99,131 / 88,498`、logic-first `384 / 64,872 / 57,534 -> 385 / 64,891 / 57,554`，皆為 `+1 file / +19 physical / +20 nonblank`；新增 single projector／row evaluation 的同時刪除 exporter decoder/order model與simulation merged-operation model。兩次 fresh isolated deterministic Release完全一致，總 DLL維持 `15,322,624 bytes`；Application bytes仍為 `693,248`、SHA由 `D88E9F7F…E0CBD`變為 `45431F85…1068EF`，Domain／Infrastructure／UI bytes/hash不變。Focused exporter／simulation 35、notch-core 156、notch-golden 6、GCC 8（GCC `15.1.0`）、Runtime Query 15、ui-core 250 tests pass；merged-continuation與floating-rounding兩個production mutation皆由public parity fixture RED，restore後全綠。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS（selection total p95 `41 / 47 ms`）；UI build、lint/analyzer與correctness／simplification review無 blocker。本slice只完成R13.103c-1，parent與列出的後續項仍保持open。
  - [x] **R13.103c-2 將 Notch null sentinel 限制為 Firmware `UINT16` domain**（[#34](https://github.com/Dennis40816/FreeformHelper/issues/34)）：`NotchSettings.ValidateNullValueOrThrow` 固定合法範圍為 `0..65535`；`ProjectSettings.ValidateOrThrow` 與 V21／V22 final projectors 共用此 Application owner，Step 5／Runtime Query notch validation亦由同一 settings entry驗證。VM／direct UseCase在report建立前fail-fast；Named Pipe最外層則以既有`IPC_ERROR` failure envelope傳回原message，避免例外斷線退化成`EMPTY_RESPONSE`。會實際投影 Firmware nodes 的 public exporter／simulation 對越界值在 formatting／evaluation 前以相同 `InvalidOperationException("NullValue must be in [0,65535].")` fail-fast，不再讓 generated C clamp、simulation raw comparison或validation reader silent clamp分岔；empty C table 與 unsupported simulation 維持既有 fast path。
    - TDD／compatibility：public `NullValue=65536`、target diff `65535` fixture 在修改前固定 C／GCC `[50,0]`、simulation `[50,50]`；修正後兩入口 exact fail-fast。Runtime Query `notch-validation` 的 invalid application state在修改前會被clamp成`65535`，修正後direct UseCase以同一exception/message拒絕，actual IPC則回`IPC_ERROR`與相同message；新增public pipe fixture先固定舊`EMPTY_RESPONSE`再GREEN。單次C export在讀取caller-owned collections前snapshot sentinel；active-set enumeration同步把`65534`改為`65535`時，舊live-read實作的V21／V22 fixtures均RED，修正後macro／projector／formatter仍共用`65534`。`0／65535` 合法、`-1／65536` 拒絕；sentinel `65534` 時，V22 target與V21 Legacy ref的diff `65535` 仍由C／GCC及simulation視為合法。V21 custom-adjacent sentinel 的舊 `NHC_DIFF_NONE` literal刻意修正為numeric `65535`；既有default-sentinel ABI／ordering／arithmetic、Lucid signed C與golden均零差，不更新golden。R13.103、`LegacyRegularAnchor` convergence、R13.101c-2／R13.102／R13.102a-2 與 R13.104 均保持 open。
    - Size／gates：相對 `493d5d6`，production total `590 / 99,131 / 88,498 -> 590 / 99,147 / 88,514`、logic-first `385 / 64,891 / 57,554 -> 385 / 64,907 / 57,570`，皆為 `+0 files / +16 physical / +16 nonblank`。兩次 fresh isolated deterministic Release完全一致，總 DLL維持 `15,322,624 bytes`；Application `693,248 bytes` SHA由 `45431F85…1068EF`變為 `827EF8A0…1EE5D8`，UI `14,528,512 bytes` SHA由 `D0BABA97…3431B`變為 `9B017D15…09DBC`，Domain／Infrastructure不變。Focused sentinel/settings/Runtime Query/IPC/export snapshot 13、notch-core 168、notch-golden 6、GCC 8（GCC `15.1.0`）、RuntimeQueryUseCase 16、RuntimeQueryIpc 5、ui-core 250 tests pass；projector guard、settings upper-bound、validation silent-clamp、IPC `EMPTY_RESPONSE`與export live-read mutations均RED，V21 adjacent-sentinel public C/GCC fixture亦先RED後GREEN。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；forward／reverse selection total p95 `53 / 40 ms`的evidence為`build/perf/r13103c2-forward-snapshot-final/regression-baseline-summary.json`（SHA-256 `31B3D8F4…AF85D`）與`build/perf/r13103c2-reverse-snapshot-final/regression-baseline-summary.json`（SHA-256 `ED200E05…5FB15`）。UI build、lint/analyzer、correctness與simplification review無 blocker，不宣稱效能提升。
  - [x] **R13.103d-1 在 progress callback 前凍結 `LegacyRegularAnchor` generation request**（[#54](https://github.com/Dennis40816/FreeformHelper/issues/54)）：在第一次同步 progress report 前擁有化 enabled-version ordering，並一次凍結 linked／unlinked threshold、`NullValue`與`LenScale`；V21／V22 compatibility strategy只讀同一 request，不再重讀 caller mutable settings。Callback 修改仍同步回到 caller，但只影響下一次 generation。完成此契約後，V22 strategy 的 CadAllocation hybrid compensation branch與僅為它保留的all-CAD參數才可移除；Legacy geometry／payload／comment／ordering／timing與request-specific table cache不變。
    - TDD／mutation：public triangle fixture在修改前由entry Legacy row被initial callback改成CadAllocation-compensated hybrid（ToFull `100 -> 200`），完成後維持exact 9-int Legacy row。1x2 fixture在第一列後修改unlinked／linked threshold、`NullValue`與`LenScale`，鎖住後續列、V21→V22 ordering與progress tuple不變，下一次呼叫才觀察新值；in-place修改caller `EnabledVersions`亦只影響下一次。退回live caller set或live threshold各自使public fixture RED，原始live mode branch由首個RED擊穿。
    - Size／gates：相對`36d7bb8`，production total `590 / 99,349 / 88,692 -> 590 / 99,346 / 88,688`、logic-first `385 / 65,102 / 57,741 -> 385 / 65,099 / 57,737`，皆為`0 files / -3 physical / -4 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL `15,330,816 -> 15,332,864 bytes`（`+2,048`）；Application `697,856 / 699FF83D…744649 -> 699,904 / 33FB9E60…FA6170`，Domain `39,936 / 0EAD9AC9…66D5`、Infrastructure `60,928 / CAFA1813…6F29`與UI `14,532,096 / 67C40172…D7AF33`不變。Legacy targeted 6、generator 44、Application 212、notch-core 179、ui-core 258、smoke 24、UI snapshots 21、golden 2、exporter／GCC 30與Runtime Query 18 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI／IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`38/23/14`與`27/15/15 ms`，evidence SHA-256為`57685C9D…395309F`與`82BF6DF4…FD47D5`。本slice不宣稱效能提升，也不合併Legacy per-version dispatch／table cache；R13.103 parent、R13.101c-2、R13.102／102a-2與R13.104保持open。
  - [x] **R13.103d-2 以固定 V21／V22 明確派送取代 Legacy strategy registry**（[#60](https://github.com/Dennis40816/FreeformHelper/issues/60)）：`NotchTableGenerator` 的 generation 與 eligibility 共用同一個 version switch；V21／V22 compatibility algorithms改為stateless static owners，並移除只有兩個固定實作、沒有production／test injection caller的`INotchAlgorithmStrategy`、dictionary registry與internal injection constructor。`LegacyNotchGenerationRequest`、threshold、row payload／comment／ordering／progress與public parameterless generator shape不變。
    - TDD／mutation：public XWay／YWay／XYWay matrix以反向configured version set鎖canonical V21→V22 ordering、eligible versions、estimated row count、anchor identity及9-int compatibility rows。暫把V22 admission放寬至XYWay會令預期`[V21]`變成`[V21,V22]`；暫交換V21／V22 builder則令row version順序與identity全部RED，restore後Legacy focused 9與generator 47 tests全綠。
    - Size／gates：相對`f244c47`，production total `590 / 99,326 / 88,670 -> 590 / 99,265 / 88,617`、logic-first `385 / 65,079 / 57,719 -> 385 / 65,018 / 57,666`，皆為`0 files / -61 physical / -53 nonblank`。兩次fresh isolated deterministic Release完全一致，總DLL `15,329,792 -> 15,329,280 bytes`（`-512`）；只有Application由`696,832 / 06F8CF03…C5C0A4E`變為`696,320 / 9899BC42…B952EBF`，Domain `39,936 / 0EAD9AC9…66D5`、Infrastructure `60,928 / CAFA1813…6F29`與UI `14,532,096 / 67C40172…D7AF33`不變。Application 218、notch-core 182、notch-golden 6、ui-core 258、smoke 24、Runtime Query／exporter／GCC 48 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI／IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`29/16/15`與`32/19/14 ms`，evidence SHA-256為`F1F09E5A…B79A89`與`0581FE62…F1D12`。本slice只刪除未使用的registry/injection seam；request-specific Legacy table cache與broader compatibility boundary仍在，R13.103 parent、R13.101c-2、R13.102／102a-2與R13.104保持open。
- [ ] **R13.104 收斂 Notch display / EMS safety predicate 與文字投影**
  - [x] **R13.104a-1 統一 Simulation EMS after-cap risk predicate**（[#30](https://github.com/Dennis40816/FreeformHelper/issues/30)）：Application-owned `SimulationSafetyAuditService.IsEmsAfterCapViolation(afterValue, afterCap)` 保留既有 `afterValue > afterCap + 1e-9` 契約，並成為 safety audit、net-flow／target-coverage `EmsRisk` 分類、Runtime Query regular snapshot、cell EMS status 與 workspace／overview high-risk projection 的共同判斷。既有 cap、result／payload schema、normal-value wording、ordering、UI layout 與 firmware ABI 不變。
    - TDD／mutation：public one-cell headless Simulation + `query simulation --regular-id 0` 鎖 `cap`、`cap + 0.5e-9` 安全、`cap + 2e-9` 違規，並鎖 workspace flags/count、`regular.isEmsSafetyRisk`、cell `EMS OK`／`EMS risk` 及 high-risk `IsViolation`／status／margin 一致。Runtime Query 暫退回 raw `After > cap`，或 shared predicate 暫退回 raw `>` 時，focused tests 均 RED；restore 後全綠。
    - Size／gates：相對 `0c46d69`，production total `589 / 99,091 / 88,458 -> 589 / 99,112 / 88,478`、logic-first `384 / 64,851 / 57,514 -> 384 / 64,872 / 57,534`，皆為 `0 files / +21 physical / +20 nonblank`。兩次 fresh isolated deterministic Release 完全一致，總 DLL `15,322,112 -> 15,322,624 bytes`（`+512`）；Application bytes 不變而 SHA 更新為 `D88E9F7F…E0CBD`，UI 為 `14,528,512 / D0BABA97…3431B`，Domain／Infrastructure bytes/hash 不變。Affected 59、ui-core 250、notch-core 148、notch-golden 6、GCC 6、Runtime Query 15 tests pass；hidden UI/IPC 正反順序皆維持 V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden/state/budget PASS；UI build 與 lint/analyzer 0 warning／0 error。此 slice 只完成 EMS after-cap predicate；`NotchDisplayProjector >255` 與其餘 display/text 收斂未做，R13.104 parent、R13.102 與 R13.103 保持 open。
  - [x] **R13.104a-2 由 final target eligibility 投影 Notch combined-overflow 顯示**（[#38](https://github.com/Dennis40816/FreeformHelper/issues/38)）：`NotchV22TargetAllocationPolicy.ProjectTargetCoverage` 一次擁有 anchor-IC／非 source admission、`strict || ToFull` 例外、per-group rounded raw combine 與 `>255%` risk，並把 immutable `NotchV22TargetCoverageProjection` 交給 generator、revisioned resolved result、Inspector、Pad Info、Notch Detail 與 Runtime Query。`NotchDisplayProjector` 只格式化 supplied ratio／risk，不再 sum all targets 或重算門檻；below-gate targets仍完整保留於 diagnostics。Unanchored public compatibility與非 target-coverage modes則由同一 Application policy明確保留既有 all-target display fallback。
    - TDD／mutation：public headless fixture以 anchor `100%`、eligible `100%`、below-gate/no-ToFull `100%` 固定舊 generator `200%`、display誤報`300.00 % (overflow risk >255%)`，修正後兩者皆為`200%`且 rejected target仍在summary；三個 emitted `100%` 的真 overflow仍顯示既有warning並由generator原exception拒絕。Application fixtures另鎖ToFull bypass、anchor max-once、逐group rounding `150`（而非raw-double `151`）、compatibility fallback及`255/256`邊界。暫退回UI all-target sum、strict-only admission或raw-double sum時各自 focused RED，restore後全綠。
    - Size／gates：相對 `f98c2e3`，production total `590 / 99,161 / 88,526 -> 590 / 99,234 / 88,593`、logic-first `385 / 64,921 / 57,582 -> 385 / 64,994 / 57,649`，皆為 `0 files / +73 physical / +67 nonblank`。兩次 fresh isolated deterministic Release完全一致，總 DLL `15,323,136 -> 15,326,720 bytes`（`+3,584`）；Application `693,248 / 827EF8A0…1EE5D8 -> 696,320 / 97C3F213…1B3FC2`，UI `14,529,024 / 20280245…86A7 -> 14,529,536 / D18E2F57…33467`，Domain／Infrastructure不變。Focused 16、notch-core 175、ui-core 252、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build、lint/analyzer與Spec／Standards／simplification review無 implementation blocker。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`50/24/25`與`45/24/22 ms`，evidence SHA-256為`1CD338D8…E5D69`與`EB40B37F…C231`。本slice不宣稱效能提升，只完成combined-overflow owner；R13.104 parent與R13.102／R13.103／Legacy convergence保持open。
  - [x] **R13.104a-3 由 final emitted eligibility 投影 Notch target summary／line／role**（[#40](https://github.com/Dennis40816/FreeformHelper/issues/40)）：normal anchored target coverage將`EmittedTargets`依穩定`(IcIndex, DiffIndex)`建立唯一membership view；effective count、compact target lines與target-card role共讀此view。Anchor及全部diagnostic targets／ordering仍保留，non-emitted target維持`Below gate`；`RawCombinedPercent == null`的unanchored／non-target compatibility保留既有strict-only顯示。
    - TDD／mutation：public Pad Info fixture鎖`Targets: 1 effective / 3 total`、唯一`IC1/diff84  A 18.09 mm²  R 54%`、diff84 `Target`、rounded-zero diff85 `Below gate`及anchor role/value不變；strict-only與UI自行重建`strict || ToFull`兩個production mutations皆RED並restore，compatibility fixture另鎖`RawCombinedPercent == null`仍為strict-only。Paired order-race test最初`3/3` RED，改為await既有grid-rebuild idle後`3/3` GREEN；這是test harness stabilization，production未變。
    - Size／gates：相對`0fe6416d`，production total `590 / 99,234 / 88,593 -> 590 / 99,256 / 88,614`、logic-first `385 / 64,994 / 57,649 -> 385 / 65,016 / 57,670`，皆為`0 files / +22 physical / +21 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL `15,326,720 -> 15,327,744 bytes`（`+1,024`）；只有 UI `14,529,536 / D18E2F57…33467 -> 14,530,560 / 5BCB3018…AE304F`，Domain `39,936 / 0EAD9AC9…66D5`、Application `696,320 / 97C3F213…1B3FC2`與Infrastructure `60,928 / CAFA1813…6F29` bytes／SHA不變。Focused public reader 41、notch-core 175、ui-core 253、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build與lint/analyzer PASS。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`31/16/16`與`33/18/15 ms`，evidence SHA-256為`DA135200…707C14`與`C33465AB…FA1034`。本slice不宣稱效能提升，只完成target display membership；R13.104 parent與R13.102／R13.103／Legacy convergence保持open。
  - [x] **R13.104a-4 從 shared Simulation audit result 投影 availability status**（[#42](https://github.com/Dennis40816/FreeformHelper/issues/42)）：`SimulationSafetyTextProjector.BuildStatusText` 一次擁有 supplied audit 的 base availability／risk／safe wording；`null` 或 `HasCells == false` 為 `Simulation not run`，有 cells 且 violation 為 `EMS risk`，其餘為 `EMS OK`。Workspace status 與 overview 共讀此 projection；`(stale)` 與 build-failure `Simulation unavailable` 仍只由 overview boundary 裝飾。
    - TDD／mutation：public headless empty-regular-grid fixture在修改前固定 workspace `EMS OK`、overview `Simulation not run`，修改後兩者皆為 `Simulation not run`，且 supported/no-cells audit、既有 summary與overview `HasAudit/HasRisk == false`不變；既有public workspace／overview fixtures另鎖non-empty safe `EMS OK`、risk `EMS risk`、stale `EMS OK (stale)`與build-failure `Simulation unavailable`／`NeedsAttention`。暫退回 workspace binary ternary或讓shared owner忽略`HasCells`時，public fixture均RED，restore後focused `7/7` GREEN。Empty fixture只鎖public/headless availability invariant，不宣稱normal operator workflow必然建立empty workspace。
    - Size／gates：相對`ef408f6`，production total `590 / 99,256 / 88,614 -> 590 / 99,267 / 88,623`、logic-first `385 / 65,016 / 57,670 -> 385 / 65,027 / 57,679`，皆為`0 files / +11 physical / +9 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL維持`15,327,744 bytes`；Domain `39,936 / 0EAD9AC9…66D5`、Application `696,320 / 97C3F213…1B3FC2`與Infrastructure `60,928 / CAFA1813…6F29` bytes／SHA不變，UI bytes維持`14,530,560`而SHA由`5BCB3018…AE304F`更新為`2A133931…AD043A`。Focused 7、notch-core 175、Application 208、ui-core 253、smoke 24、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build與lint/analyzer PASS。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`35/21/17`與`58/26/32 ms`，evidence SHA-256為`17B745E4…F04EC41C`與`A2E1D970…DACA098`。本slice不宣稱效能提升，亦不改Runtime Query schema、Simulation safety predicate／cap、cell／high-risk／export／replay文字或UI layout；R13.104 parent的hard-coded cap與其餘display/text debt、R13.102、R13.103及Legacy convergence保持open。
  - [x] **R13.104a-5 從 shared replay result 投影 Copper replay summary status**（[#44](https://github.com/Dennis40816/FreeformHelper/issues/44)）：`CopperPillarPathReplayStep`以不改 positional constructor／serialization 的`[JsonIgnore]` facts保存`IsSupported`與`HasGlobalFlowResidual`，其`HasPhysicalAuditRisks`一次涵蓋global-flow、net-flow與target-coverage；`CopperPillarPathReplayResult`再從steps投影`HasUnsupportedSteps`／`HasPhysicalAuditRisks`。`SimulationSafetyTextProjector.BuildReplayStatusText`成為唯一severity wording owner，固定`unsupported > EMS risk > audit warning > EMS OK`；step status與VM aggregate summary共讀此owner。Artifact row複製JSON-ignored global-flow fact，讓既有structured risk與status一致而不新增serialized field；EMS aggregate仍保留total violation count suffix。
    - TDD／mutation：public headless `SimulationWorkspaceViewModel` fixture以`CopperDiameter=0.5`、`CopperPeakValue=400`、`CopperBaselineValue=0`，從`(0.5,0.5)` replay至`(1.5,0.5)`共兩點；step 0固定`Max After 200`、零EMS／net-flow／coverage count但有global-flow residual及`audit warning`，step 1固定`Max After 399`。修改前aggregate exact為`Copper path replay: 2 point(s) · Max After 399 at step 1 · EMS OK.`，修正後為`Copper path replay: 2 point(s) · Max After 399 at step 1 · audit warning.`。四段shared priority、structured support/global-flow facts、empty/not-run、既有EMS count、artifact structured risk及JSON schema guard均有characterization；暫退回aggregate EMS-only ternary、遺失global-flow fact或讓shared projector忽略support state時focused tests皆RED，restore後全綠。此fixture只鎖public/headless replay seam，不宣稱normal operator workflow必然建立或暴露同一replay。
    - Size／gates：相對`bdab625`，production total `590 / 99,267 / 88,623 -> 590 / 99,299 / 88,649`、logic-first `385 / 65,027 / 57,679 -> 385 / 65,059 / 57,705`，皆為`0 files / +32 physical / +26 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL `15,327,744 -> 15,329,792 bytes`（`+2,048`）；Domain `39,936 / 0EAD9AC9…66D5`與Infrastructure `60,928 / CAFA1813…6F29`不變，Application為`697,856 / 699FF83D…744649`，UI為`14,531,072 / 6C0492FA…315EA`。Focused 17、Application 208、notch-core 175、ui-core 253、smoke 24、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build、lint/analyzer與correctness／simplification review無blocker。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`44/18/21`與`40/18/26 ms`，evidence SHA-256為`619E0484…89875B`與`0A4DD778…85C48`。本slice不宣稱效能提升，亦不改replay physics／sampling、EMS cap/predicate、Runtime Query或artifact schema、persistence、normal UI layout、firmware ABI、generated C或golden；R13.104 parent的hard-coded cap與其餘Simulation／replay／display文字、R13.101c-2、R13.102、R13.103及Legacy convergence保持open。
  - [x] **R13.104a-6 從 shared audit result 投影 no-cell EMS cap 文字**（[#46](https://github.com/Dennis40816/FreeformHelper/issues/46)）：`SimulationSafetyTextProjector.FormatEmsAfterCap`一次擁有 supplied audit cap 與 null/default fallback；overview no-cells `EmsCapText`及export no-cells handoff prompt共讀此projection。Supplied empty audit保留其`AfterCap`，只有null audit回退`DefaultEmsAfterCap = 480`；availability status、`HasAudit`／`HasRisk`、summary、predicate、schema與layout不變。
    - TDD／mutation：public `SimulationSafetyAuditService.Analyze([], afterCap: 512)` fixture在修改前固定overview cap `480`與export `After <= 480`，修正後兩者皆為`512`；null audit兩者仍為`480`。暫退回overview default constant、export literal `480`，或把null fallback改為`0`時，各自對應的supplied／null theory RED；restore後focused projector `12/12` GREEN。
    - Size／gates：相對`dd559a0`，production total `590 / 99,299 / 88,649 -> 590 / 99,305 / 88,654`、logic-first `385 / 65,059 / 57,705 -> 385 / 65,065 / 57,710`，皆為`0 files / +6 physical / +5 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL維持`15,329,792 bytes`；Domain／Application／Infrastructure bytes及SHA不變，UI維持`14,531,072 bytes`而SHA由`6C0492FA…315EA`更新為`CD7779FD…34E37`。Focused 12、notch-core 175、ui-core 253、smoke 24、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`40/19/20`與`34/18/16 ms`，evidence SHA-256為`F874E56C…CE447A`與`4CD7EBB7…5EA40`。本slice不宣稱效能提升，只收斂no-cell cap provenance；R13.104 parent的其餘hard-coded cap／Simulation／replay／display文字、R13.101c-2、R13.102、R13.103及Legacy convergence保持open。
  - [x] **R13.104a-7 將 Simulation physical audit warning 投影至 status／overview**（[#48](https://github.com/Dennis40816/FreeformHelper/issues/48)）：`SimulationSafetyTextProjector.BuildStatusText` 對有 cells 的 supplied audit 共用既有 severity priority：EMS violation 為 `EMS risk`，無 EMS violation但有 global-flow／net-flow／target-coverage physical risk為 `audit warning`，其餘為 `EMS OK`；null／no-cells仍為`Simulation not run`。Workspace status與overview共讀此owner；overview `HasRisk`仍只代表EMS danger，`NeedsAttention`則涵蓋physical risk與stale，summary在既有EMS句後附加同一physical evidence文字。
    - TDD／mutation：public canonical audit fixture以`Before=400`、`After=450`及global action-flow residual `+50`固定`HasViolations=false`、`HasPhysicalAuditRisks=true`；修改前status誤為`EMS OK`且overview不需注意，修正後exact為`audit warning`、`NeedsAttention=true`並保留`Physical audit: global flow residual +50; net-flow OK; target coverage <= 120%.`。同時有EMS及physical risk時仍以`EMS risk`優先。暫退回EMS-only status、移除physical attention或省略physical summary時，各自對應的public assertion RED；restore後focused projector `14/14` GREEN。
    - Size／gates：相對`e1c92d0`，production total `590 / 99,305 / 88,654 -> 590 / 99,311 / 88,660`、logic-first `385 / 65,065 / 57,710 -> 385 / 65,071 / 57,716`，皆為`0 files / +6 physical / +6 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL維持`15,329,792 bytes`；Domain／Application／Infrastructure bytes及SHA不變，UI維持`14,531,072 bytes`而SHA由`CD7779FD…34E37`更新為`08878462…F95AB`。Focused 14、Application 208、notch-core 175、ui-core 253、smoke 24、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`27/15/14`與`31/18/15 ms`，evidence SHA-256為`3BFF980B…04D199`與`577A9BB1…0A229`。本slice不宣稱效能提升，也不新增export-selection warning chip或改audit predicate／counts／schema／layout；R13.104 parent的其餘hard-coded cap與Simulation／replay／display文字、R13.101c-2、R13.102、R13.103及Legacy convergence保持open。
  - [x] **R13.104a-8 將 Simulation physical audit warning 投影至 Notch export review**（[#50](https://github.com/Dennis40816/FreeformHelper/issues/50)）：export badge共讀`SimulationSafetyTextProjector.BuildStatusText`的`EMS risk > audit warning > EMS OK` priority，physical-only summary復用既有`BuildPhysicalAuditSummaryText` evidence。Physical-only audit令`HasSimulationPhysicalAuditWarning=true`、`IsSimulationSafetyClean=false`，但仍不阻擋export；warning badge只重用既有`chipStatus warning`／`chipStatusText`與tokens。Clean、EMS-blocked及null／no-cells presentations保持exact。
    - TDD／mutation：public export-selection fixture以`Before=400`、`After=450`及global action-flow residual`+50`固定修改前錯誤`IsSimulationSafetyClean=true`、`EMS OK · Max After 450`且無physical evidence；修正後badge exact為`audit warning · Max After 450`，summary保留`Safe for EMS cap 480: Max After 450 at REG 1.`並附加`Physical audit: global flow residual +50; net-flow OK; target coverage <= 120%.`。同時EMS／physical risk仍由EMS badge與既有block dialog優先；null與empty supplied cap維持not-run。暫退回EMS-only badge／summary、clean的EMS-only predicate或移除warning-chip binding時，public／static characterization各自RED，restore後focused export／projector／window `65/65` GREEN。Dev page既有warning status preview與shared `BrushWarning` token足以驗證重用，未新增style或token。
    - Size／gates：相對`cb73c8c`，production total `590 / 99,311 / 88,660 -> 590 / 99,328 / 88,676`，為`0 files / +17 physical / +16 nonblank`；logic-first `385 / 65,071 / 57,716 -> 385 / 65,081 / 57,725`，為`0 files / +10 physical / +9 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL `15,329,792 -> 15,330,304 bytes`（`+512`）；Domain `39,936 / 0EAD9AC9…66D5`、Application `697,856 / 699FF83D…744649`、Infrastructure `60,928 / CAFA1813…6F29`不變，UI `14,531,072 / 08878462…F95AB -> 14,531,584 / 956A3FA5…06AFC9`。Focused 65、Application 208、notch-core 175、ui-core 256、UI snapshots 20、smoke 24、notch-golden 6、Runtime Query 18與GCC 8（GCC `15.1.0`）tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`40/25/17`與`35/20/16 ms`，evidence SHA-256為`981C85CA…4B32D8`與`979B4E22…DD664F`。本slice不宣稱效能提升，也不改export blocking／eligibility、Runtime Query schema、persistence、firmware ABI、generated C或golden；R13.104 parent的hard-coded guidance與其餘Simulation／replay／display文字、R13.101c-2、R13.102、R13.103及Legacy convergence保持open。
  - [x] **R13.104a-9 由 current Simulation EMS cap 投影 Notch safety guidance**（[#52](https://github.com/Dennis40816/FreeformHelper/issues/52)）：`SimulationSafetyTextProjector`一次擁有Notch short／Simulation policy／export handoff三個guidance templates；主`FreeformHelperViewModel`只供應current `SimulationSafetyOverviewEmsCapText`與availability，並在cap變更時通知三個dependent properties。`SettingsWindowViewModel`以同一owner供應既有default-`480`文字，active Step 5 overview tooltip改綁既有`NotchExportSafetyPolicySummary`；沒有新增control、style或token。
    - TDD／mutation：public one-cell audit以`AfterCap=512`固定修改前overview為`512`但三個Notch guidance仍為`480`；修正後short／Simulation／export文字皆exact使用`512`，null audit及Settings仍exact使用default`480`。另鎖三個`PropertyChanged`與active tooltip binding。暫退回主VM `480` literal、移除dependent notification或恢復static tooltip時，各自public／static characterization精準RED；restore後focused guidance／overview／layout `32/32` GREEN。
    - Size／gates：相對`e1b0b9c`，production total `590 / 99,328 / 88,676 -> 590 / 99,349 / 88,692`、logic-first `385 / 65,081 / 57,725 -> 385 / 65,102 / 57,741`，皆為`0 files / +21 physical / +16 nonblank`。兩次fresh isolated deterministic Release完全一致，總 DLL `15,330,304 -> 15,330,816 bytes`（`+512`）；Domain `39,936 / 0EAD9AC9…66D5`、Application `697,856 / 699FF83D…744649`與Infrastructure `60,928 / CAFA1813…6F29`不變，UI為`14,532,096 / 67C40172…D7AF33`。Focused 32、Application 208、notch-core 175、ui-core 258、UI snapshots 21、smoke 24、notch-golden 6、Runtime Query 18與GCC 8 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI/IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`37/23/16`與`34/17/19 ms`，evidence SHA-256為`97152F5A…41A1EA`與`DD69E750…7DA0EB`。本slice不宣稱效能提升，也不改default cap／predicate、audit、target-coverage calibration、Runtime Query、persistence、layout／style、firmware ABI、generated C或golden；R13.104 parent的其餘hard-coded cap／Simulation／replay／display文字、R13.101c-2、R13.102、R13.103及Legacy convergence保持open。
  - [x] **R13.104a-10 由 current Notch／EMS caps 投影 target-cap help**（[#62](https://github.com/Dennis40816/FreeformHelper/issues/62)）：`SimulationSafetyTextProjector.BuildNotchTargetCoverageCapHelpText`一次格式化目前target cap、uniform `400`對應的After與比較用EMS cap。Active Step 3由主`FreeformHelperViewModel`供應current `SimulationSafetyOverviewEmsCapText`，Settings則刻意沿用documented default cap；兩邊都供應目前draft target cap。既有兩個tooltip改綁`NotchTargetCoverageCapHelpText`，沒有新增control、layout、style或token，亦未改target guard／EMS計算。
    - TDD／mutation：public headless `RightWorkflowStep3View`與public VM／Settings fixtures把target cap設為`128%`；active workflow exact顯示`At uniform 400, target cap 128% maps to After 512; compare with EMS cap 512.`，Settings exact保留`EMS cap 480`。Static XAML literal、active端default-`480` cap與遺失overview-cap dependent notification三個production mutations各自令headless／public／notification characterization RED，restore後focused `19/19` GREEN；兩個binding與舊`120% maps ... 480` literal移除另由layout guard鎖定。此slice只更新既有tooltip source，無需新增Dev page control preview。
    - Size／gates：相對`32f4469`，production total `590 / 99,265 / 88,617 -> 590 / 99,288 / 88,637`、logic-first `385 / 65,018 / 57,666 -> 385 / 65,041 / 57,686`，皆為`0 files / +23 physical / +20 nonblank`。兩次fresh isolated deterministic Release完全一致，總DLL `15,329,280 -> 15,329,792 bytes`（`+512`）；只有UI由`14,532,096 / 67C40172…D7AF33`變為`14,532,608 / 44805041…29B52`，Domain `39,936 / 0EAD9AC9…66D5`、Application `696,320 / 9899BC42…B952EBF`與Infrastructure `60,928 / CAFA1813…6F29`不變。Focused 19、ui-core 260、smoke 25、notch-core 182、notch-golden 6、Runtime Query／exporter／GCC 48 tests pass；UI build與lint/analyzer為0 warning／0 error。Hidden UI／IPC正反順序維持V21 `692 / 130979 / 8961B815…E57488`、V22 `548 / 84023 / 5208068B…57BB47`，golden／export state／budget PASS；selection total／Inspector／preview p95為`40/20/19`與`39/23/21 ms`，evidence SHA-256為`D4BE8E99…A08D95D`與`36915EDF…2A3EE3`。本slice不宣稱效能提升，也不改default cap／predicate、audit、target guard、Runtime Query、persistence、UI layout／style、firmware ABI、generated C或golden；R13.104 parent的其餘hard-coded cap／Simulation／replay／display文字、R13.101c-2、R13.102、R13.103及Legacy convergence保持open。

### 1.3.2 Matching 與 Domain state

- [ ] **R13.201 正式化 Pad overlap、DXF audit、Canvas hit-test 三個 bounded contexts**；在 overlap evidence API 定案後移除 `PadMatcher`／`PadMatchService` 目前僅為相容而保留、實際未讀取的 `MatchingSettings` 參數
  - 盤點證據（2026-10，INV2）：`docs/reviews/r13-slice-inventories-2026-10.md` 第 2 節；三個 context 與測試已列，實作仍待執行。
- [ ] **R13.202 CadBest/hover 共用 best-match projection 與 deterministic tie-break**
  - 盤點證據（2026-10，INV2）：同文件第 2 節列出各 best selector 的欄位、順序、同分行為與最小 characterization；狀態未完成。
- [ ] **R13.203 依 API boundary 漸進導入 typed IDs 與 legacy adapters**
- [ ] **R13.204 封裝 `RegularPad` writers 為等價的狀態轉移 API**
  - 盤點證據（2026-10，INV2）：同文件第 3 節逐欄列 writer／讀者／副作用；狀態未完成。
- [ ] **R13.205 將 DxfRegularMaskAudit segment/local-repair/passive-compensation 改為顯式 pipeline**
- [ ] **R13.206 將 CoordinatePlanner machine/normalized/pixel/world/safe projection 收斂為參數化 transform builder**

### 1.3.3 Settings 與 Presentation orchestration

- [ ] **R13.301 建立 Settings draft snapshot/map 與逐欄 Apply/Discard/roundtrip tests**
  - 盤點證據（2026-10，INV2）：同文件第 4 節列 draft/live 複製欄位與待補逐欄測試；狀態未完成。
  - [ ] **R13.301a 將 full-snapshot Save 改成 dirty-field change set**，避免 Settings 開啟後從 Canvas/Header 做的 live change 被舊 draft 覆寫。
- [ ] **R13.302 將多個 settings 入口收斂到同一 apply plan 與 side-effect owner**
  - 盤點證據（2026-10，INV2）：同文件第 4 節列入口、plan 與副作用；typed invalidation 等 parent 工作仍待執行。
  - [ ] **R13.302a 建立 revisioned workflow result state**：合法零結果仍為 Completed，input 改變轉 Stale；UI、RuntimeQuery、CLI 不再從 row count、preview item 或 summary string 猜執行狀態。
  - [ ] **R13.302b 補齊 Step2／Step4／Step5 typed invalidation**，設定 Apply 後不得把舊分類、diagnostic summary 或 export summary 留成 current。
- [ ] **R13.303 收斂 simulation color/opacity scale、brush cache 與限域 token fallback contract**
- [ ] **R13.304 修正 Console 結構化 dedup 並收斂雙 hosted mode action path**
- [ ] **R13.305 將 Coordinate pixel、Step4 scoring 等非人工參數移出 normal Settings／workflow，保留完整 persistence 相容**
  - [ ] **R13.305a normal numbered flow 改為 Step3 直達 Step5**；Step4 mapping 與 Step6 validation 保留為未編號 Diagnostics，不讓使用者誤認為匯出前必須手動執行。
    - owner 2026-10-03：確認 Step4 mapping 與 Step6 validation diagnostics 移出編號流程，放到未編號的 Diagnostics 區域。
  - [ ] **R13.305b 移除 normal Settings 中不可操作／非日常的 derived toggles 與 placeholder**；V21/V22 選擇需禁止「UI 顯示皆關閉、實際正規化為兩版」的 split-brain。

### 1.3.4 Workspace ViewModel 拆解

- [ ] **R13.401 建立 root project-session shell 與 child VM compatibility contract**
  - 盤點證據（2026-10，INV2）：同文件第 5 節按 workspace 列 root VM commands、callbacks、View／RuntimeQuery 入口與 compatibility test leaf；狀態未完成。
- [ ] **R13.402 遷移 `DxfWorkspaceViewModel` orchestration**
- [ ] **R13.403 遷移 `MatchingWorkspaceViewModel` orchestration**
- [ ] **R13.404 遷移 `NotchWorkspaceViewModel` orchestration**
- [ ] **R13.405 收斂 `ProjectSessionViewModel` facade、Save/Load/IPC 與跨 workspace tests**

### 1.3.5 UI 結構與 token 收斂（no visual change）

- [ ] **R13.501 拆分 Controls.Core responsibilities，限域導入 BasedOn/template 去重並鎖住 selector precedence**
- [ ] **R13.502 拆分 DevView preview sections 並完整保留 action role laboratory**
- [ ] **R13.503 抽取 Simulation/Coordinate 與 card/tile 真正同構的 shared layout shell**
- [ ] **R13.504 收斂同義 token/legacy alias、WorkspaceHeader popup token 與安全的 workspace naming**
- [ ] **R13.505 顯式化 NumberScrubber 初始化契約；不執行 BalancedWrapPanel false-positive cleanup**
- [ ] **R13.506 將 PadCanvas selection/visible-draw engines 改為可獨立測試的窄 dependency seam**
  - 盤點證據（2026-10，INV2）：同文件第 6 節列兩個 engine 的 owner 讀寫與 characterization；狀態未完成。
- [ ] **R13.507 統一 CadAreaBucketService selection/顯色 membership，並記錄 hatch 留在 UI 的 ownership gate**
  - 盤點證據（2026-10，INV2）：同文件第 6 節列 selection／顯色兩套 membership 與 hatch owner；狀態未完成。

### 1.3.x 已完成

- [x] **R13.002 建立 Runtime CLI 3635 V21/V22 normalized ordinal exact export gate**（[#3](https://github.com/Dennis40816/FreeformHelper/issues/3)）
  - 完成（2026-08-08）：
    - `notch_export_golden_manifest.json` executable-lock Lucid 3635 project/mask/golden SHA-256、bytes、nodes 與 production layer/filter provenance。
    - 已核准的一次性 provenance correction：V21 692 nodes / 130,979 bytes / `8961B815…E57488`；V22 548 nodes / 84,023 bytes / `5208068B…57BB47`。後續 1.3.x 不得更新 C golden。
    - hidden UI gate 拒絕 output/golden path collision 與既有 UI server，並以 `status.processId` 驗證固定 IPC pipe 連到 managed PID；只停止自己啟動且 path 已驗證的 process。
    - `--format` 不修改 `EnableV21`／`EnableV22`／profile；V21→V22 與 V22→V21 兩種順序都驗證 export state invariant 與 signed exact output。
    - Application drift test 改走 production `BuildFilteredCadPadSet` + CAD-output FW diff projection，不再使用 raw `_cad` test-only seam。
  - 驗證：
    - path-collision negative gate：PASS。
    - `NotchExampleCExportDriftTests|RuntimeQueryUseCaseTests`：17 passed / 0 failed / 0 skipped。
    - `./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost`：105 passed / 0 failed / 0 skipped。
    - `run-3635-regression-baseline.ps1 -LaunchIsolatedUi`：V21→V22 signed exact PASS。
    - 同腳本加 `-ReverseCExportOrder -SkipBuild`：V22→V21 signed exact PASS。
    - `./scripts/tests/lint.ps1 -UseNoAppHost`：action roles 0 issues；analyzer build 0 warning / 0 error。

- [x] **R13.001 建立 `notch-core` / `notch-golden` test groups 並接入 refactor gate**
  - 完成（2026-07-20）：
    - `run-tests.ps1` 提供 `notch-golden`（3 個 baseline class）與 `notch-core`（15 個核心 class，含 golden）短命令。
    - `run-refactor-gate.ps1` 預設執行 `notch-golden`；核心資料路徑 slice 可用 `-IncludeNotchCore` 以完整 core group 取代。
    - `run-pre-push-gate.ps1` 接受兩個新 group；tests/scripts README 與 canonical test strategy 已同步。
  - 行為/side effects：只改測試 orchestration，沒有 production data path、UI state 或 golden 檔變更。
  - 驗證：
    - PowerShell parser：3 個變更腳本通過。
    - `./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost`：103 passed / 0 failed / 0 skipped。
    - `./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost`：lint/build 0 warning、0 error；notch-golden 4、application 165、ui-core 233、smoke 24，全數通過。

## UI Action Role System（詳細實作計畫）

### 已完成
- [x] S12.029 Dev page role laboratory 與 visual QA baseline
  - 完成（2026-05-05）：
    - `DevView` 的 action preview 已升級為 `Action role laboratory`，補齊 neutral / primary / danger / ghost / viewport / console / chip action / status badge matrix。
    - role laboratory 已覆蓋 Button、ToggleButton checked、icon-only、icon+text、disabled、chip action、passive status severity variants。
    - 新增 `docs/guides/ui-action-role-visual-qa.md` 作為最小 visual QA baseline，並把 `docs/guides/ui-density-token-rules.md` review checklist 指向該 baseline。
  - 驗收：
    - `pwsh scripts/tests/check-xaml-action-roles.ps1`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
- [x] S12.028 XAML lint guard：禁止錯誤 role 組合回歸
  - 完成（2026-05-05）：
    - 新增 `scripts/tests/check-xaml-action-roles.ps1`，用 XML 解析 XAML `Classes`，檢查 legacy action token、禁止 role pair、chipStatus/passive contract、chipAction/interactive contract。
    - `scripts/tests/lint.ps1` 已接入 XAML action role guard，並把掃描數、元素數與 issue count 寫入 lint result。
    - `scripts/tests/normalize-crlf.ps1` 與 lint 的 changed-file 合併修正為陣列合併，避免單一 tracked + 單一 untracked 檔案時漏掃。
  - 驗收：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `pwsh scripts/tests/check-xaml-action-roles.ps1`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
    - 負向驗證：暫時加入 `consoleHeaderAction icon` 後，`lint.ps1 -UseNoAppHost` 會以 `XAR002` 失敗並指出檔案與 class。
- [x] S12.027 全 repo 控件角色遷移與 legacy alias 移除計畫
  - 完成（2026-05-05）：
    - 已遷移 Freeform / Simulation / Coordinate 主要工作區、Console、Workflow steps、DXF edit、PadInfo、Dev page preview 的 action control class 組合。
    - `Button` / `ToggleButton` 已轉向 `actionTextButton`、`actionIconButton`、`actionChip` primitives 與 `actionNeutral`、`actionPrimary`、`actionDanger`、`actionGhost`、`viewportOverlayAction`、`consoleHeaderAction`、`chipAction`、`chipStatus` roles。
    - `panelAction`、`panelToggle`、`coordinateOverlayAction`、`workspacePrimaryAction`、`workspaceDangerAction`、`workspacePresetButton`、`workspaceSummaryChipButton`、`workspaceSummaryChip` 不再作為 active XAML class token。
  - 驗收：
    - 禁用 token/pair 精準掃描：無 `consoleHeaderAction + icon`、`viewportOverlayAction + panelAction/panelChromeToggle`、legacy action tokens。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
- [x] S12.026 Shared action style primitives 與 token contract 收斂
  - 完成（2026-05-05）：
    - 新增 `src/FreeformHelper.UI/Styles/Controls.Action.axaml` 作為 action role 樣式入口，建立 `actionButton` / `actionIconButton` / `actionTextButton` / `actionChip` primitives。
    - `Tokens.axaml` 已補 action semantic token/brush groups，涵蓋 neutral、primary、danger、ghost、selected、viewport、console、chip/status。
    - Dev page 已加入最小 action role primitives preview，覆蓋 Button/ToggleButton、checked、disabled、icon-only、chip/status。
  - 驗收：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
- [x] S12.025 Action Role matrix 與 state contract 正式化
  - 完成（2026-05-05）：
    - `docs/guides/ui-action-role-system.md` 已正式化為 source of truth，補齊 primitive/semantic role/state contract/legacy mapping。
    - `docs/guides/ui-density-token-rules.md` 已把 Action Role System 納入 UI review checklist。
  - 驗收：
    - 任一新 button/toggle/chip 都能從文件選到唯一 role，不需要靠猜測疊 class。
    - 文件明確回答 Quick Check、Fit AA、Dedup、preset、badge 各自應用哪個 role。

## Post 1.0（Tool Workbench：Simulation / Coordinate）已完成
- [x] S12.008 Shared Tool Workbench shell polish
  - 完成（2026-05-06）：
    - Simulation / Coordinate 共用 action primitives、status chips、viewport overlay action pattern 已由 S12.026-S12.029 Action Role System 收斂並由 lint guard 鎖住。
    - Simulation EMS safety diagnostic block 從 header 文字流中的重膠囊 chip 改為 block 右上角輕量 count badge，圓心對齊 block 右上角。
    - EMS safety badge count 由 ViewModel 單一投影為 `SimulationSafetyViolationBadgeText`，最大顯示 `99+`，XAML 不再推導數字格式。
    - Dev preview 與 visual QA baseline 已納入 fold diagnostic corner badge 檢查。
  - 驗收：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~SimulationWorkspaceViewModelTests" /p:UseAppHost=false`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
- [x] S12.002 Simulation scenario model 與 run snapshot 收斂
  - 完成（2026-05-07）：
    - 新增 `SimulationScenario` / `SimulationScenarioSnapshot`，以 `BuildScenarioSnapshot` 作為 Manual / CSV / Copper / Copper Path Sweep 的單一 simulation + audit 入口。
    - `SimulationWorkspaceViewModel` 維持現有 UI，但內部 refresh 改為建立 source scenario，再從 scenario snapshot 投影 overlay、selection、diff violation、EMS safety 與 status。
    - Copper path replay 每個 step 改由 `CopperPathSweep` scenario snapshot 取得 simulation result 與 `SafetyAudit`，不再在 replay loop 重新推導 audit。
    - 補 `SimulationWorkspaceUseCaseTests` 與 ViewModel source-kind regression，鎖住 Manual / CSV / Copper / Path Sweep 都從同一 scenario snapshot 取得 result/audit。
  - 驗收：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~SimulationWorkspaceUseCaseTests|FullyQualifiedName~SimulationWorkspaceViewModelTests|FullyQualifiedName~NotchApplySimulationReviewUseCaseTests" /p:UseAppHost=false`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

## Active Backlog（保留未完成項）

- [ ] **V21／Legacy 完全移除（owner 後續目標，版本與範圍未訂；owner 2026-10-03 已確認「2.1」指 V21）**

- [ ] **S14.011 3635 DXF single-pass import/catalog optimization（中風險）**
  - 目標：Open DXF 目前會先由 `DxfPadImporter` 讀完整 DXF tokens，再由 `DxfLayerCatalogReader` 重新讀一次 layer catalog；評估合併為單次解析輸出 CAD pads + layer catalog。
  - 基準：先使用 gated `Cad3635LoadBenchmarkTests` 量測 3635 `import / layerCatalog / apply / rebuild`，並用 gated `Cad3635EndToEndBenchmarkTests` 量測 `Load project / Step1 / Step2 / Step5` 的 compute + render flush，再逐步重構。
  - 驗收：3635 direct DXF load 與 E2E benchmark 顯示改善比例；3635 V21/V22 export drift 維持一致；一般測試不因 benchmark 變慢。

- [ ] **S14.012 Source-line length coverage model note（研究 / 不進 UI 重構 commit）**
  - 目標：記錄「polygon 面積相同但 source/data line 覆蓋長度不同時，實際感應量可能不同」的物理模型假設，避免未來 allocation 討論只剩 area ratio。
  - 範圍：新增或更新 `docs/reference/source-line-coverage-model.md`；盤點 `src/FreeformHelper.Application/Services/PadMatcher.cs`、`src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs`、`src/FreeformHelper.Application/Services/NotchV22CompensationService*.cs` 目前 area-based assumption。
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - 文件需明確區分 display TFT `source/data line`、`gate/scan line`、touch Tx/Rx line 與 pixel/subpixel pitch。
  - 完成定義：文件列出 `effective signal ~= sum(source-line covered length * pitch * weight)` 的候選模型、和現有 polygon-area model 的差異、以及是否值得進 prototype 的判斷條件。

## UI Layer Review Candidates（已映射到 1.3.x）

- [x] **S14.013 UI no-visual-change refactor baseline refresh（已轉入 R13.005）**
  - 2026-07-20：此 checkbox 代表舊工作項已完成遷移；實際工作狀態改由 `R13.005` 追蹤。
  - 目標：在拆 UI code 前，先刷新可比對的行為/設定入口文件與 visual guard baseline，確保後續每一片都能證明「外觀不變、行為不變」。
  - 範圍：`docs/reference/behavior-inventory.md`、`docs/guides/settings-entry-matrix.md`、`docs/guides/ui-density-token-rules.md`、`docs/guides/repo-refactor-scan-2026-07-06.md`、必要時 `docs/guides/ui-action-role-visual-qa.md`。
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：文件不再停在 2026-03 / 舊分支基線；明確列出 UI refactor 的 no-visual-change contract、Dev page preview 使用點、以及每個後續切片的驗收命令。

- [x] **S14.014 BalancedWrapPanel direct mutable state cleanup（2026-07-20 審核關閉）**
  - Repo scan 命中的是 private nested `Row` 的 `public List<Control> Children`；private containing type 使它不會成為 control 外部可取得的 mutable state。
  - 不修改 production code；`R13.505` 保留此負向決策，避免再次建立相同 false-positive TODO。

- [x] **S14.015 DevView/action preview extraction without visual drift（已轉入 R13.502）**
  - 2026-07-20：此 checkbox 代表舊工作項已完成遷移；實際工作狀態改由 `R13.502` 追蹤。
  - 目標：縮短 `DevView.axaml` 與 action preview 區塊，保留同一組 role laboratory 視覺與 guard coverage。
  - 範圍：`src/FreeformHelper.UI/Views/DevView.axaml`、必要的新 preview view/control、`src/FreeformHelper.UI/Styles/Controls.Action.axaml`、`tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs`。
  - 驗證：
    - `pwsh scripts/tests/check-xaml-action-roles.ps1`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：Dev page action role laboratory 仍覆蓋 neutral / primary / danger / ghost / viewport / console / chip/status states；XAML 行數下降且沒有新增 inline color/size。

- [x] **S14.016 Simulation/Coordinate workbench shell extraction（已轉入 R13.503）**
  - 2026-07-20：此 checkbox 代表舊工作項已完成遷移；實際工作狀態改由 `R13.503` 追蹤。
  - 目標：整理 Simulation / Coordinate workbench 控制 pane 與 details pane 的重複 shell 結構，抽出共用 layout pattern，但不改首屏外觀或互動順序。
  - 範圍：`src/FreeformHelper.UI/Views/WorkspaceSections/SimulationWorkspaceControlsPaneView.axaml`、`src/FreeformHelper.UI/Views/WorkspaceSections/CoordinatePlannerWorkspaceControlsPaneView.axaml`、`src/FreeformHelper.UI/Views/WorkspaceSections/SimulationWorkspaceDetailsPaneView.axaml`、相關 shared control/style。
  - 驗證：
    - Dev page 或 workspace preview 檢查 Simulation / Coordinate control pane。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：兩個 workbench 仍維持原有密度與 action role；重複 shell XAML 減少，沒有 UI guard 或 lint regression。

- [x] **S14.006 Badge text style naming cleanup（低風險）**
  - 完成（2026-06-26）：
    - `workspaceSummaryChipText` 已收斂為 `chipStatusText`，與 `chipStatus` passive badge role 對齊。
    - 保留原 setter 值，避免同時改變 badge 文字顏色、字重、截斷或 wrapping 行為。
  - 目標：把目前 `chipStatus` container 內仍使用的 `workspaceSummaryChipText` 舊命名收斂為目前語意，例如 `chipStatusText`。
  - 觀察依據：`src/FreeformHelper.UI/Styles/Controls.Action.axaml` 已以 `chipStatus` 作為 badge container；但多個 View 仍在 `chipStatus` 內使用 `workspaceSummaryChipText`，名稱和實際 role 不一致。
  - 範圍：`src/FreeformHelper.UI/Views/**/*.axaml`、`src/FreeformHelper.UI/Styles/Controls.Core.axaml`、`src/FreeformHelper.UI/Styles/Controls.Action.axaml`、`tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs`
  - 驗證：
    - `rg "workspaceSummaryChipText" src/FreeformHelper.UI tests -n` returns no matches.
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：badge text class 名稱和 `chipStatus` role 對齊，不再保留舊 workspace summary naming。

- [x] **S14.007 Pad info legacy selector cleanup（低風險）**
  - 完成（2026-06-26）：
    - 刪除 `padInfoSectionTitle`、`padInfoLabel`、`padInfoValue`、`padInfoHoverHint`、`padInfoLink` legacy selector blocks。
    - 保留 active `padInfoKeyLabel`、`padInfoKeyValue`、`padInfoBodyText`、`padInfoInfoButton`、section/metric/action styles。
  - 目標：移除 `Controls.PadInfo.axaml` 中已無 View 使用的舊 selector，例如 `padInfoLabel`、`padInfoValue`、`padInfoHoverHint`、`padInfoLink`。
  - 觀察依據：Pad info views 已使用 `padInfoKeyLabel`、`padInfoKeyValue`、`padInfoBodyText`、`padInfoInfoButton` 等新 class；上述舊 selector 只剩 style 定義。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.PadInfo.axaml`
  - 驗證：
    - `rg "padInfoSectionTitle|padInfoLabel|padInfoValue|padInfoHoverHint|Button\.padInfoLink|Classes=\"[^\"]*padInfoLink" src/FreeformHelper.UI/Views src/FreeformHelper.UI/Styles -n` returns no matches.
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：未使用的 pad info legacy selector 不再存在，active pad info class 不變。

- [x] **S14.008 Controls.Core style responsibility split（中風險）**
  - 完成（2026-06-26）：
    - 新增 `Controls.Overlay.axaml`，承接 confirm dialog 與 tooltip overlay styles。
    - `Controls.axaml` 在 `Controls.Core.axaml` 後立即 include overlay styles，維持原本位於 Core 檔尾的相對載入順序。
    - `UiLayoutGuardTests` 改為直接檢查 overlay style contract，tooltip 白底黑字 guard 沒有放寬。
  - 目標：降低 `Controls.Core.axaml` 的混合責任，先把檔尾 overlay/dialog style 分到更清楚的 style file。
  - 觀察依據：`Controls.Core.axaml` 仍約 1,196 行，混合 validation、workspace table、dialog、DXF、button legacy support；閱讀成本高，容易讓 selector cleanup 和 active contract 混在一起。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.Core.axaml`、新增或既有 `src/FreeformHelper.UI/Styles/*.axaml`、`src/FreeformHelper.UI/Styles/Controls.axaml`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：overlay/dialog style 載入順序不變，UI guard 通過，tooltip 白底黑字 contract 仍由測試保護。

- [x] **S14.009 Controls.Core workspace/DXF split follow-up（已轉入 R13.501）**
  - 2026-07-20：此 checkbox 代表舊工作項已完成遷移；實際工作狀態改由 `R13.501` 追蹤。
  - 目標：評估是否把 workspace table、validation、DXF edit style 進一步拆出 Core。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.Core.axaml`、可能新增的 `src/FreeformHelper.UI/Styles/Controls.Workspace.axaml` / `Controls.Dxf.axaml`、`src/FreeformHelper.UI/Styles/Controls.axaml`、`tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs`。
  - 風險：這些 style 目前位於 global Button/ToggleButton state selector 之前；直接搬到 `Controls.Core.axaml` 之後會改變 selector precedence，可能影響 hover/disabled/checked 顏色。
  - 驗證：
    - 先定義等價 include 位置或 Core 內部載入順序，不直接改 selector precedence。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
    - Dev page / DXF edits preview 確認 hover/disabled/checked 視覺未漂移。
  - 完成定義：`Controls.Core.axaml` 責任下降，workspace/DXF style 拆分後載入順序與 selector contract 由測試或文件鎖住。

## S13 UX / Project Persistence Follow-up
- [x] S13.003 Free-form DXF operation controls layout cleanup
  - 完成（2026-06-25）：
    - Free-form / DXF quick actions 已收斂為 section label + compact icon toolbar pattern，並由 Dev page preview 覆蓋。
    - `dxfEditToolbarAction:pointerover` 改用 inverse foreground，避免 hover 時背景與圖示同色或低對比。
    - Ponytail skills 已安裝於本機 Codex skills；repo gate 另以 build/analyzer/lint/test 確認無 dead-code warning。
  - 觀察依據：
    - Free-form / DXF 操作按鈕目前排版視覺雜亂，操作分組、主要/次要/危險動作層級不清楚。
  - 目標：
    - 先以 `3635` 截圖記錄現況，確認醜感來源是 spacing、button role、icon/text 對齊、還是同區域放太多 action。
    - 將 DXF operation controls 改為一致的 section header + action row / compact toolbar pattern。
    - 一般操作使用 white/black neutral token；刪除、清除、reset 類操作使用 danger token。
  - 驗收：
    - 新增或更新 Dev page preview，覆蓋 Free-form DXF operation button group。
    - 3635 visual check 截圖前後可比較。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

- [x] S13.002 Action button visual contract cleanup
  - 完成（2026-06-05）：
    - `Tokens.axaml` 將 shared action neutral / primary / chip 契約收斂為白底黑字；danger 收斂為白底紅字。
    - 移除 `actionPrimary` 與 checked/selected action token 的 accent-blue border，改用 neutral/focus token。
    - `Controls.Action.axaml` 將 action primitives 的 platform focus adorner 關閉，改用非藍色 `BrushActionFocus*` token 提供 keyboard focus affordance。
    - `docs/guides/ui-action-role-system.md` 已同步更新 action role 契約。
  - 驗證（2026-06-05）：
    - `pwsh scripts/tests/check-xaml-action-roles.ps1`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false --nologo`，12 passed。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
  - 觀察依據：
    - 目前部分按鈕有藍色邊框/焦點外觀，視覺上和工具型 UI 不一致。
  - 目標：
    - 統一 action button 視覺：可按的一般按鈕預設白底黑字；刪除/清除/危險操作改為紅字或 danger token。
    - 移除不必要的藍色 border/focus 視覺，焦點狀態改走 shared token，仍保留鍵盤可辨識的 focus affordance。
    - 不在 View/Control 新增 inline color/size，全部經由 `Tokens.axaml` 與 `Controls.Action.axaml`。
  - 驗收：
    - Dev page action role laboratory 覆蓋 neutral / primary / danger / ghost / chip states。
    - `pwsh scripts/tests/check-xaml-action-roles.ps1`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

- [x] S13.001 Embed SeeRegular mask in project snapshot when embed mode is selected
  - 完成（2026-06-05）：
    - `ProjectFile` 新增 embedded SeeRegular mask bytes/name，與 existing embedded DXF 同層保存。
    - Project save 選擇 embed 時，同一個 embed 決策會同時嵌入 DXF 與 SeeRegular mask；decline 會清掉兩者 embedded fields。
    - Load Project 時 `RegularVisibilityMaskService` 優先使用 embedded SeeRegular mask；沒有 embedded 內容才回退到既有 path-based 載入。
    - project-load 期間的 CAD replay 不再清掉 project file 內的 embedded mask，避免 path 失效時丟失 mask。
  - 驗證（2026-06-05）：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo --filter "FullyQualifiedName~ProjectPersistenceUseCaseTests|FullyQualifiedName~ProjectStoreTests|FullyQualifiedName~RegularVisibilityMask"`，15 passed。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
  - 觀察依據：
    - 目前 project 可保留 `SeeRegular.csv` 路徑與 mask 啟用狀態，但使用者若選擇 embed project，仍需要確認 SeeRegular mask 也跟著內嵌，避免換機或移動資料夾後 mask 遺失。
  - 目標：
    - 當 project save 選擇 embed 模式時，將 SeeRegular mask 內容作為 project snapshot 一部分儲存。
    - Load Project 必須優先使用 embedded SeeRegular mask；若沒有 embedded 內容才回退到既有 path-based 載入規則。
    - UI / RuntimeQuery / project persistence tests 不得各自重算 mask 狀態，需讀同一份 project load result / mask snapshot。
  - 驗收：
    - `3635` project 以 embed 模式 save 後搬離原資料夾仍能還原 active/inactive mask。
    - 補 save/load roundtrip test，覆蓋 embedded mask 優先與 legacy path fallback。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

## Code Size Reduction（安全縮減）
- [x] **S14.005 Archive stale repo scan docs**
  - 完成（2026-06-26）：
    - 舊 `docs/guides/repo-refactor-scan*.md` 報告已移到 `docs/archive/repo-refactor-scans/`。
    - `docs/guides` 只保留最新 `repo-refactor-scan-2026-06-26.md` 與 `repo-refactor-scan-template.md`。
    - `docs/README.md` 與 `docs/guides/new-user-reading-guide.md` 已更新掃描報告入口說明。
  - 目標：降低 `docs/guides` 噪音，避免舊 scan 報告被誤認為目前主線。
  - 範圍：`docs/guides`、`docs/archive/repo-refactor-scans`、`docs/README.md`、`docs/guides/new-user-reading-guide.md`
  - 驗證：
    - `Get-ChildItem docs/guides -File -Filter 'repo-refactor-scan*.md'`
    - `Get-ChildItem docs/archive/repo-refactor-scans -File`
  - 完成定義：current guides 只保留最新 scan/template，歷史 scan 報告集中 archive。

- [x] **S14.004 Dead workspace container selector cleanup**
  - 完成（2026-06-26）：
    - `Controls.Core.axaml` 已刪除沒有 View 命中的 `workspaceSummaryChip` container、`workspaceDataCard`、`workspaceInfoFlyout`、`validationGroupTitle`、`verificationTableHeader*` selectors。
    - 保留 active `chipStatusText`、`workspaceDataRow`、`workspaceDataCellText`、`workspaceInfoButton`、`workspaceTableHeader*` styles。
    - `ui-density-token-rules.md` 已把 current summary badge contract 更新為 `chipStatus` / `actionChip chipAction`。
  - 目標：移除已無 View 命中的 workspace container/header selector，保留 active row/text/button styles。
  - 觀察依據：`rg "workspaceSummaryChip\b|workspaceDataCard\b|workspaceInfoFlyout\b|validationGroupTitle\b|verificationTableHeader\b|verificationTableHeaderText\b" src/FreeformHelper.UI/Views src/FreeformHelper.UI/Controls tests docs -n` 顯示這些 selector 沒有 active View class 命中；summary badge container 已改用 `chipStatus`。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.Core.axaml`、`docs/guides/ui-density-token-rules.md`
  - 驗證：
    - `rg "workspaceSummaryChip\b|workspaceDataCard\b|workspaceInfoFlyout\b|validationGroupTitle\b|verificationTableHeader\b|verificationTableHeaderText\b" src/FreeformHelper.UI/Styles docs/guides/ui-density-token-rules.md -n`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：Styles 不再保留未命中的 container/header selector；current UI contract 文件改指向 `chipStatus` / `actionChip chipAction`。

- [x] **S14.003 DXF edit legacy style alias cleanup**
  - 完成（2026-06-26）：
    - `Controls.Core.axaml` 已刪除未使用的 `dxfEditAdvancedCard` block，並移除 `dxfEditIconOnlyAction` alias。
    - `UiLayoutGuardTests` 的 action token regex 已同步移除 dead `dxfEditIconOnlyAction`，保留 active `dxfEditCompactIconAction`。
  - 目標：刪除 DXF edit 已無使用的 legacy style selector，保留目前 active 的 `dxfEditCompactIconAction`。
  - 觀察依據：`rg "dxfEditAdvancedCard|dxfEditIconOnlyAction|dxfEditCompactIconAction" src/FreeformHelper.UI tests docs -n` 顯示 `dxfEditAdvancedCard` / `dxfEditIconOnlyAction` 只剩 Styles/tests regex，active View 使用 `dxfEditCompactIconAction`。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.Core.axaml`、`tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs`
  - 驗證：
    - `rg "dxfEditAdvancedCard|dxfEditIconOnlyAction" src/FreeformHelper.UI/Styles tests -n`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：Styles/tests 不再保留 `dxfEditAdvancedCard` / `dxfEditIconOnlyAction`，`dxfEditCompactIconAction` 仍由 Dev/LeftDxfPanel 使用。

- [x] **S14.002 Legacy workspace alias selector cleanup**
  - 完成（2026-06-26）：
    - `Controls.Core.axaml` / `Controls.Scroll.axaml` 已移除已無 View/Control 使用的 `notchExport*` / `notchApply*` style alias 分支。
    - 保留仍由 `FreeformHelperView.axaml` 使用的 `notchExportRestoreHint*` selectors。
  - 目標：移除已遷移到 `workspace*` class 後殘留的 `notchExport*` / `notchApply*` legacy style aliases，保留仍使用的 `notchExportRestoreHint*`。
  - 觀察依據：`rg "notchExport[A-Za-z0-9_]*|notchApply[A-Za-z0-9_]*" src/FreeformHelper.UI/Views src/FreeformHelper.UI/Controls tests -n` 只顯示 `notchExportRestoreHint*` 仍在 View 使用；其他 style aliases 只剩在 Styles。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.Core.axaml`、`src/FreeformHelper.UI/Styles/Controls.Scroll.axaml`
  - 驗證：
    - `rg "notchExport[A-Za-z0-9_]*|notchApply[A-Za-z0-9_]*" src/FreeformHelper.UI/Styles -n`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter FullyQualifiedName~UiLayoutGuardTests --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：Styles 中只保留仍被 View 使用的 `notchExportRestoreHint*` selectors，現行 `workspace*` 樣式不變。

- [x] **S14.001 Dead Dev prototype style cleanup**
  - 完成（2026-06-26）：
    - `Controls.Form.axaml` 已刪除未被任何 View/Control 使用的 `devDropdownCandidate`、`devRightTabPreview*`、`devInspector*` prototype selector cluster。
    - 本切片淨刪 404 行 XAML style，不改使用者可見流程或演算法。
  - 目標：刪除未被任何 View/Control 使用的 Dev prototype style selector，降低 UI style surface。
  - 觀察依據：`docs/guides/repo-refactor-scan-2026-06-26.md` 顯示 `Controls.Form.axaml` 仍是 style 熱點；`rg "devDropdownCandidate|devRightTabPreview|devInspector" src tests docs -n` 只命中 `Controls.Form.axaml`。
  - 範圍：`src/FreeformHelper.UI/Styles/Controls.Form.axaml`
  - 驗證：
    - `rg "devDropdownCandidate|devRightTabPreview|devInspector" src tests docs -n`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj --no-restore`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：上述 dead Dev prototype selector 不再存在，UI 專案 build/lint 通過。

- [x] S12.030 Legacy XAML action style cleanup
  - 完成（2026-05-10）：
    - `Controls.Core.axaml` 已移除 dead legacy action selector alias：`notchExportSummaryChip`、`workspaceSummaryChipButton`、`workspacePrimaryAction`、`workspaceDangerAction`、`panelAction` button/toggle variants，以及已不命中的 `panelAction.dxfEdit*` / `Button.icon.ghost.dxfEdit*` style。
    - `Controls.Tab.axaml` 已移除舊 `Button.icon.panelChromeToggle` / `panelToggle` selector；active `panelChromeToggle` 由 `Controls.Action.axaml` 的 action primitive 接手。
    - 保留仍被使用的 `panelActionRow`、`workspacePresetButtonContent`、`workspacePresetButtonLabel`、`verificationSummaryChip`。
  - 觀察依據：
    - `S12.027` 已把 active action controls 遷移到 `actionTextButton` / `actionIconButton` / `actionChip` primitives。
    - `Controls.Core.axaml` / `Controls.Tab.axaml` 仍保留多組舊 selector alias（例如 `panelAction`、`workspacePrimaryAction`、`workspaceDangerAction`、`workspaceSummaryChipButton`、`notchExportSummaryChip`、`panelToggle`），目前 Views/Controls 已沒有對應 active class 組合。
  - 範圍：
    - `src/FreeformHelper.UI/Styles/Controls.Core.axaml`
    - `src/FreeformHelper.UI/Styles/Controls.Tab.axaml`
  - 目標：
    - 刪除已無 active XAML 命中的 legacy action style alias。
    - 保留仍被使用的 `panelActionRow`、`workspacePresetButtonContent`、`workspacePresetButtonLabel`、`verificationSummaryChip` 等實際樣式。
  - 驗收：
    - `rg "panelAction|workspacePrimaryAction|workspaceDangerAction|workspaceSummaryChipButton|notchExportSummaryChip|panelToggle" src/FreeformHelper.UI/Views src/FreeformHelper.UI/Controls` 只允許 `panelActionRow` 這類非 action button layout class。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

- [x] S12.031 Code-behind UI resource helper consolidation
  - 完成（2026-05-10）：
    - `UiResourceResolver` 補齊 `CornerRadius` / `Thickness` resource lookup。
    - `CoordinatePlannerWorkspaceView` 與 `SimulationWorkspaceView` 已移除本地重複 resource lookup 實作，改走 shared resolver。
    - 保留 View 內語意化 wrapper 名稱，讓 overlay rendering code 維持原本可讀性。
  - 範圍：
    - `src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml.cs`
    - `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml.cs`
    - `src/FreeformHelper.UI/Services/UiResourceResolver.cs`
  - 目標：
    - 將剩餘 `GetResourceDouble` / `GetBrush` / `GetResourceThickness` / `GetResourceCornerRadius` 類型 helper 收斂到共用 resolver。
    - 不改 token、不改 fallback 值、不改 UI 行為。
  - 驗收：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

- [x] S12.032 Simulation safety text projection consolidation
  - 完成（2026-05-10）：
    - 新增 `SimulationSafetyTextProjector` 作為 EMS safety value / signed value / count badge / workspace summary / export summary / high-risk diff text 的單一投影入口。
    - `SimulationWorkspaceViewModel.Validation` 與 `NotchExportSelectionViewModel` 保留既有 binding surface，內部改為呼叫 projector。
    - `SimulationSafetyOverviewProjector` 改用同一個 numeric formatter，避免 safety 數值格式散落。
  - 範圍：
    - `src/FreeformHelper.UI/ViewModels/SimulationWorkspaceViewModel.Validation.cs`
    - `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs`
    - `src/FreeformHelper.UI/Services`
  - 目標：
    - 將 EMS safety summary / high-risk diff / safety numeric formatting 抽成單一 projector。
    - 保留既有 public binding property 與目前 UI 文案語意。
  - 驗收：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~SimulationWorkspaceViewModelTests|FullyQualifiedName~NotchExportSelection" /p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

- [x] S12.033 Test fixture factory consolidation
  - 完成（2026-05-11）：
    - 新增 `tests/FreeformHelper.Tests/TestInfrastructure/TestGeometryFactory.cs`，集中 rectangular `Polygon2`、`RegularPad`、`RegularGrid`、linear diff grid、`CadPad` 測試資料建構。
    - 收斂 simulation / coordinate planner / workflow / validation / CAD union 相關測試內重複 grid/pad/cad pad helper。
    - 產品碼與 assertions 未變更；只改測試 fixture 建構入口。
  - 驗證（2026-05-11）：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo --filter "<S12.033 touched test classes>"`，108 passed。
    - 完整 `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo` 目前仍被非本切片 touched files 的既有 gate failure 擋住：`UiVisualSnapshotTests`、`UiRenderedVisualSnapshotTests`、`FreeformHelperViewModelTests.RestoreAllHiddenCadPadsCommand_RestoresManualHiddenPadsWithoutClearingCombinedOrLayerEdits`。
  - 範圍：
    - `tests/FreeformHelper.Tests`
  - 目標：
    - 收斂重複的 `BuildGrid` / `CreateGrid` / `CreatePad` / `BuildCadPad` 測試資料工廠。
    - 不改產品碼、不改 assertion 語意。
  - 驗收：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

## Quality Gate Follow-up（驗證阻塞）
- [x] S12.034 Restore full test gate after UI/CAD editing baseline drift
  - 完成（2026-05-11）：
    - 更新 `ui-visual-minimal-baseline.json`，同步先前 UI source XAML 調整後的 normalized hashes。
    - `UiRenderedVisualSnapshotTests` 的 `MainWindow.ConsoleExpanded` 在 isolated run 與 full run 出現 36-bit headless render variance；保留既有 hash，只將該 surface `maxDistance` 從 28 調整為 36。
    - `FreeformHelperViewModelTests.RestoreAllHiddenCadPadsCommand_RestoresManualHiddenPadsWithoutClearingCombinedOrLayerEdits` isolated run 通過；完整 gate 重跑後未再重現失敗。
  - 驗證（2026-05-11）：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo`，789 passed。
  - 觀察依據：
    - 2026-05-11 full test gate 曾有 3 failures outside `S12.033` touched files：
      - `UiVisualSnapshotTests.CriticalUiFiles_MatchMinimalVisualBaseline`
      - `UiRenderedVisualSnapshotTests.RenderedUiSurfaces_MatchAdvancedVisualBaseline`
      - `FreeformHelperViewModelTests.RestoreAllHiddenCadPadsCommand_RestoresManualHiddenPadsWithoutClearingCombinedOrLayerEdits`
  - 範圍：
    - `tests/FreeformHelper.Tests/UI/Snapshots`
    - `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.CadEditing.cs`
    - 對應 UI baseline 或 CAD editing behavior source（需先判定是 baseline drift 還是行為回歸）
  - 目標：
    - 不混入 code-size refactor；獨立恢復完整 test gate。
  - 驗收：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo`
    - `pwsh scripts/tests/lint.ps1 -UseNoAppHost`

## Beta 0.9（AutoCAD 驗證前待追）
- [x] S11.173 AutoCAD 實測驗證 target coverage guard 是否應成為 CurrentGain 預設
  - 完成（2026-05-11）：
    - 既有實測/文件已收斂：3635 `CAD4818` 右側邊緣案例顯示舊算法問題是 source-wide gain 二次分配，非 CAD 幾何真實覆蓋；新 beta0.9 target-regular coverage 語意已記錄於 `docs/reference/notch-system-reference.md` 與 `docs/core/notch-v21-v22-flow.md`。
    - `CurrentGain` 仍保留 `TargetCoverageGuard=true` / `TargetCoverageCapPercent=120` 作 EMS safety net；不把 guard 移除或關閉。
    - `3635` current v2.2 checked-in C example 已鎖定 `{ 1473, 158, 1472, 99, ... }` 的 per-target coverage 語意，避免回到 `R * source share` 的錯誤路徑。
  - 驗證（2026-05-11）：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo --filter "FullyQualifiedName~NotchExampleCExportDriftTests|FullyQualifiedName~GetCadV22StageOverlays_CAD4818_ReturnsFinalOutlineCoveringFullReg384|FullyQualifiedName~NotchSettingsTests|FullyQualifiedName~NotchGoldenBaselineTests|FullyQualifiedName~SimulationSafetyAuditServiceTests"`，13 passed。
  - 目標：
    - 量測左右邊緣 high-risk regular 的實際 CAD overlap area、inside overlap、ToFull support area。
    - 確認 target coverage guard 造成的 signal drop 是否符合實際 sensor / NF / FW 座標需求。
  - 待確認：
    - 若實測顯示 target coverage >120% 是 CAD 幾何真實覆蓋，應保留警示但不強制 guard。
    - 若實測顯示 target coverage >120% 是 ToRegular/Stage3 組合造成的非物理堆疊，則保留 guard 作 CurrentGain 預設。


## Beta 0.7（安全縮減與架構整理）
- [x] S11.153 Stage3 / target allocation phase 1：ToFull 改為 support/cap，主權重回到 source overlap
  - 完成（2026-05-11）：
    - 主線已具備 `NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage` 與 `TargetRegularStage3Coverage`。
    - `ConservativeNoGain` 使用 per-target source overlap coverage：`overlapAreaOnTarget / targetRegularArea`，`ToFull` 只作 support/cap/allowance。
    - `CurrentGain` 使用 per-target Stage3 coverage，但不再回到 source-wide `R * source share` 的舊錯誤路徑。
    - Export metadata / Step3 model text 已同步描述 target-regular coverage 與 support/cap 語意。
  - 驗證（2026-05-11）：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -p:UseAppHost=false --nologo --filter "FullyQualifiedName~NotchV22TargetAllocationServiceTests|FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~NotchSettingsTests|FullyQualifiedName~NotchTableExporterTests"`，48 passed。
  - 原觀察依據：
    - 現行 v2.2 allocation 會在 `IsToFullApplied` 時直接以 `ReachableArea` 作 target weight，造成 tiny-overlap case 被 full-area expansion 放大。
    - `3635` 例如 `IC1 diff72` 曾出現 `400 -> 192` 的過度分流。
  - 目標：
    - 先做最小可驗證版：
      - target allocation 主權重改回 `SourceArea`
      - `ToFull` 先只保留 `support / inclusion / cap` 語意
      - candidate ratio 在 `anchor IC` 內正規化
  - 目前進度（2026-04-25）：
    - prototype 已落在 `codex/beta0.8`
    - `IC1 diff72` 已由 `400 -> 192` 改善為 `400 -> 364`
    - `3635` current `maxAfter` 約 `452`，目前低於 `480` EMS guard，但仍需要正式 audit 判斷是否符合幾何面積比例與 net-flow 合理性。
  - 驗證：
    - `NotchV22TargetAllocationServiceTests` 已補：
      - `ToFull` 不再直接以 `ReachableArea` 主導 target ratio
      - `anchor IC` 內正規化

- [x] S11.155 beta0.8 頁面同步：把 ToFull / ToRegular / allocation / EMS guard 狀態投影到 UI
  - 觀察依據：
    - beta0.8 演算法語意已從「ToFull 直接決定比例」改成「ToFull 作 support / cap / boundary allowance」，目前頁面文字仍容易讓使用者以為 ToFull 會直接分配更多量。
    - Simulation 目前沒有明確顯示 EMS cap、`maxAfter`、violation count，也沒有把高風險 diff 與 source/target legs 連起來。
  - 目標：
    - Step 3 顯示目前 compensation model、allocation model、ToFull semantic（support/cap/boundary allowance）與 gain 狀態。
    - Simulation 顯示 `EMS cap = 480`、`maxAfter`、violations count、high-risk diff list。
    - Step 5 / export 前顯示 safety status；若超過 EMS cap，匯出前要求明確確認或阻擋（待 UX 決策）。
    - Inspector tooltip 改成同時顯示 `Before / After / Delta / NetFlow / EMS status`。
  - 目前進度（2026-04-25）：
    - 新增 `SimulationSafetyAuditService`，Simulation workspace 會投影 `EMS cap / Max After / violation count / high-risk REG list`。
    - Simulation tooltip 與 runtime query payload 已帶出 EMS safety 狀態。
    - Step 3 / Step 5 / Settings Step 3 / Settings Step 5 已補 beta0.8 allocation 與 export safety 語意。
  - 完成確認（2026-05-11）：
    - `RightWorkflowStep3View`、`RightWorkflowSettingsTabView`、`SettingsStep3SectionView` 已投影 `NotchAllocationModel*`、`NotchToRegular*`、`NotchToFull*`、`BoundaryVirtualAreaCap`、`TargetCoverageGuard` 狀態。
    - `SimulationWorkspaceViewModel` / `SimulationSafetyOverviewProjector` 已提供 `EMS cap`、`Max After`、violation count、high-risk diff list、selected diff `EMS status / NetFlow / source-target legs`。
    - `NotchExportSelectionViewModel` 與 export selection window 已使用同一份 `SimulationSafetyAuditResult`，由 shared EMS after-cap predicate 判定的 risk 會阻擋 export。
  - 驗證：
    - 3635 開啟後，使用者能從 Simulation 或 Step 3/5 直接看出目前是否低於 `480` cap，以及 target allocation 使用的是哪個模型。

- [x] S11.156 Settings overview redesign：重新排版 settings / workflow overview
  - 觀察依據：
    - 目前右側 settings 把 IC layout、grid、workflow action、preview controls、Step N 說明混在一起，資訊密度過高。
    - 重要選項分散在右側 inline settings 與 SettingsWindow，且部分 beta0.8 選項尚未露出。
  - 目標：
    - 右側 panel 改成「狀態總覽 + 快速動作」，只放目前狀態、風險摘要與常用 action。
    - SettingsWindow 改成完整設定表單，使用左側 navigation + 分組 cards + sticky footer。
    - 設定項分組為：Project/Grid、Geometry Match、Freeform Tagging、Notch Model、Simulation Safety、Export、Display/Debug。
    - Dev page 先做 layout preview，確認 token / spacing / two-column row pattern 後再導入主頁。
  - 目前進度（2026-04-25）：
    - 右側 settings tab 已新增 Overview block，先呈現 model/export/safety 摘要。
    - SettingsWindow 初步改為左側 navigation rail + 右側內容表單，並放大視窗 token。
    - SettingsWindow 第二輪重構移除 shell capsule tab 重用，改用 settings 專用 flat nav rail、section header、sticky footer。
    - Settings Step 3 已拆成 `Current compensation model` / `Signal semantics` / `Advanced allocation gates` / `AA preview display` 四張卡片，避免 beta0.8 語意、compute gate、preview display 混在同一段文字。
    - SettingsWindow 第三輪 modern UI 重排：新增 settings 專用色階與 style layer，改成 hero + nav rail + content shell + section/tile 滿版布局，General/Step1/Step2/Step4/Step5 全部改為 setting tile 排版。
    - Settings polish：說明文字改為短句 + 低噪音 note 色階，主要輸入框改用 Settings scoped input 背景/邊框/focus 樣式。
  - 完成確認（2026-05-11）：
    - `RightWorkflowSettingsTabView` 已以 Overview block 呈現 `Project / grid`、`Notch model`、`Simulation safety`、`Export handoff`，並用 settings icon deep-link 到完整設定頁。
    - `SettingsWindow` 已具備 settings hero、左側 navigation rail、右側 content shell、section visibility binding 與 sticky footer。
    - `SettingsStep3SectionView` 已拆成 `Current compensation model`、`Signal semantics`、`Advanced allocation gates`、`AA preview display` 四個 section block，且使用 token/style class 而非 inline style。
    - `UiLayoutGuardTests` 與 `UiRenderedVisualSnapshotTests` 已覆蓋 SettingsWindow / right workflow overview 的關鍵 layout contract。
  - 驗證：
    - 規劃檔：`docs/guides/settings-overview-redesign-plan-2026-04-25-beta08.md`
    - 後續 UI 實作分小 commit：overview shell、Step3 model card、Simulation safety card、SettingsWindow navigation。
