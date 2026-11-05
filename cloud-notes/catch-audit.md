# Catch 區塊稽核：吞掉例外的 catch（`src/`）

- 基準：`origin/main` @ `5e48bec`（2026-11-05 擷取）
- 範圍：`src/` 下所有 `catch`。共 74 處關鍵字命中，分布在 26 個檔案。`FreeformHelper.Domain` 與 `FreeformHelper.Infrastructure` 沒有任何 catch。
- 性質：只做分析，沒有修改 production code。
- 做法：每一處 catch 都讀過周邊程式碼與呼叫端。「使用者目前看到什麼」是從程式碼推得，沒有實際執行 UI。推論而非實測的地方會標「推論」。
- 工程規則依據：不得有靜默 fallback（`AGENTS.md` 的 Coding rules）。

## 摘要

| 分類 | 數量 |
|---|---|
| 需處理（吞例外，使用者未被告知或被靜默換成預設值） | F1–F12，共 12 個項目（高 3、中 4、低 5） |
| 已檢視、可接受（有回報使用者、重新拋出、或是取消流程） | 見「已檢視、可接受」一節 |

依資料遺失風險排序。F1、F2、F3 是會讓使用者的資料或設定在不知情下消失或被換掉的項目，建議優先處理。

---

## 高風險

### F1. 專案載入時，合併 DXF 群組被靜默丟棄，下次存檔即永久遺失

- 位置：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Persistence.cs:226`（`catch (ArgumentException ex)`）
- 同一函式還有三個沒有 catch、但效果相同的靜默 `continue`：`:199`（群組沒有 source 或 output pad）、`:212`（source pad 不在 baseline）、`:240`（output pad id 衝突）。
- 觸發輸入：專案檔中某個 `CombinedCadGroups` 的 output pad snapshot 少於 3 個頂點。`ProjectDxfEditState.cs:57` 呼叫 `new Polygon2(Vertices)`，`Polygon2.cs:27` 會丟 `ArgumentException("A polygon must have at least 3 vertices.")`。也會在手動編輯過專案檔、舊 schema、或 DXF 重新匯出後 pad id 不同時發生（`:212`、`:240` 兩條路徑）。
- 使用者現在看到：什麼都沒有。只有一行 `_logger.Warn`。載入完成後狀態列顯示 "Project loaded"，`HasUnsavedChanges = false`（`FreeformHelperViewModel.Settings.Sync.cs:158`），所以也不會被提示要儲存或保留。該群組在畫面上就是不存在。
- 為什麼排第一：使用者接著按 Ctrl+S，專案檔會以「沒有該群組」的狀態覆寫原檔，原本的合併編輯永久消失。這是唯一一條可以由「載入」直接走到「覆寫存檔造成資料遺失」的路徑。
- 最小修法：
  1. `ReplayPersistedCombinedCadGroups` 回傳（或輸出）被跳過的群組 id 清單，涵蓋 `:199`、`:212`、`:226`、`:240` 四條路徑。
  2. `ApplyCapturedDxfEditProjectState` 的呼叫端（`FreeformHelperViewModel.Persistence.Project.cs:300`）在清單非空時用 `status.ReportWarning(...)`，訊息寫明群組 id 與「儲存會丟棄這些群組」。
  3. 不要把 `HasUnsavedChanges` 設為 false，讓使用者在關閉時至少會被提示。
- 驗證：用一個含 2 頂點 output pad 的專案檔載入，確認狀態列有警告。

### F2. 專案載入：DXF 路徑讀取失敗，靜默改用內嵌 DXF（fallback）

- 位置：`src/FreeformHelper.UI/Services/CadLoadUseCase.cs:68`（`catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))`，只把例外存進 `pathLoadException`）；`:82` 是內嵌也失敗時的後續處理。
- 觸發輸入：專案記錄的 `LastDxfPath` 檔案存在，但解析失敗或讀取失敗（DXF 被 CAD 軟體以獨占鎖開著而 `IOException`、檔案被截斷、使用者改版後含不支援的實體）。內嵌 DXF 載入成功時，`pathLoadException` 被丟掉，連 log 都沒有（這個類別沒有 logger）。
- 使用者現在看到：狀態列 `Project loaded: ... DXF (embedded), CAD pads: N/M.`（`FreeformHelperViewModel.CadLoad.cs:99`）。只有 `embedded` 這個字，沒有「為什麼不是 path」。內嵌版本可能比磁碟上的 DXF 舊。
- 資料風險：使用者以為載入的是最新 DXF，實際是舊內嵌副本。後續 Step 1–5 與匯出都基於舊幾何，存檔時舊內嵌副本再次被寫入。這是違反「no silent fallback」的明確案例。
- 最小修法：`CadLoadOutcome` 加一個 `PathFallbackReason`（string?），由 `:68` 的 catch 填入 `ex.Message`；`BuildCadLoadStatus` 在非空時用 warning 狀態輸出 "DXF path failed (reason); using embedded copy."，並寫一行 `_logger.Warn`。若要嚴格遵守規則，改為載入失敗而不 fallback，需要產品決策（見末尾「待決定」）。
- 附帶：`LastDxfPath` 檔案不存在（`File.Exists` 為 false）時也是靜默改用內嵌，不經過 catch，但同屬此問題，建議一併處理。

### F3. App 一般設定檔損毀時靜默回到預設值，且下一次存檔覆蓋損毀檔（無備份）

- 位置：`src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:69`（`TryLoad`，`catch (Exception ex) when (IsRecoverable)`，回傳 null）。連帶：`:178`、`:182`（`LooksLikeWhitelistShape`，`JsonException`／`ArgumentException` 回傳 false，之後改走 legacy 解析，再次失敗後落入 `:69`）。
- 觸發輸入：`%LOCALAPPDATA%\FreeformHelper\app-general-settings.json` 被截斷或寫壞的 JSON、被其他程序鎖住（`IOException`）、權限不足。`Save`（`:127`–`:130`）用 `File.WriteAllText` 直接覆寫、非原子，所以寫入途中當機或斷電正好會造出截斷檔。
- 使用者現在看到：什麼都沒有。啟動時 `Program.cs:124` 與 `FreeformHelperViewModel.AppSettings.cs:129` 都把 null 當作「沒有設定」。介面以預設值呈現。之後任何設定變更觸發 `PersistAppGeneralSettingsNow` → `Save`，損毀檔被新檔覆蓋，原內容（可能只壞了最後幾個位元組）無法救回。
- 資料風險：只有 App 層偏好（視圖、匯入、行為設定），不是專案資料，因此排在 F1／F2 之後。但屬於「靜默吞掉 + 下次覆寫」。
- 最小修法：
  1. `TryLoad` 在 `JsonException`／`IOException` 時，先把損毀檔改名為 `app-general-settings.json.corrupt-<UTC時間>`，再回傳 null，並把原因透過 status 顯示一次（啟動時至少 `_logger.Error` 而非 `Warn`）。
  2. `Save` 改為寫暫存檔再 `File.Replace`（或 `File.Move` overwrite）。
- 備註：`AGENTS.md` 規定「App settings tests must not write to the user's real settings」，修改時沿用 `TestAppSettingsIsolation`。

---

## 中風險

### F4. 專案存檔成功後，App 設定的延遲寫入失敗只寫 log

- 位置：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.AppSettings.cs:100`（`PersistAppGeneralSettingsNow`）
- 觸發輸入：App 設定檔所在目錄唯讀、磁碟已滿、檔案被鎖。`Save Project` 之後會呼叫 `FlushDeferredAppGeneralSettingsIfNeeded`（`FreeformHelperViewModel.Persistence.Project.cs:77`）。
- 使用者現在看到：專案存檔的成功訊息 `Project saved: <path>`。App 設定沒寫成功，只有一行 `Warn`。`HasDeferredChanges` 維持 true（失敗時不會被清掉），所以之後會再試，但使用者全程不知道。
- 最小修法：`PersistAppGeneralSettingsNow` 回傳 bool；`FlushDeferred...` 在失敗時透過 `status.ReportWarning("Project saved, but app settings could not be written: ...")`。計時器觸發的路徑維持只寫 log，避免連續跳訊息。

### F5. DXF 圖層目錄解析失敗：目錄被清空，只寫 log

- 位置：`src/FreeformHelper.UI/Services/LayerCatalogStateService.cs:72`（path）、`:137`（embedded）
- 觸發輸入：DXF 主解析成功但圖層目錄的第二次讀取丟例外（例如解析器對某種實體處理不同）。`TryLoadFromPath`／`TryLoadFromEmbedded` 由 `FreeformHelperViewModel.Operations.cs:55`、`:60` 以 `Task.Run` 呼叫。
- 使用者現在看到：圖層目錄為空，沒有錯誤。受影響的是依賴目錄的功能（例如「Regular 來源 = DXF 圖層」要選的圖層清單）。之後 grid rebuild 會顯示 "Select a DXF layer for regular source."（`GridRebuildOrchestrator.cs:83`），訊息與真實原因無關，容易誤導。
- 沒有驗證的部分：目錄為空時，專案中已儲存的 `RegularSourceLayerName` 是否被重設，這一點沒有追到。實作前需要確認。
- 最小修法：服務暴露 `LastLoadError`（string?），呼叫端 `status.ReportWarning("DXF layer catalog failed: ...")`。

### F6. 衍生工作區預熱失敗：版本標記為已處理，使用者不知情

- 位置：`src/FreeformHelper.UI/ViewModels/ShellViewModel.Workspaces.cs:201`
- 觸發輸入：`Simulation.PrewarmWorkspaceAsync` 或 `CoordinatePlanner.PrewarmWorkspaceAsync` 丟例外（例如幾何資料在特定專案上讓某個衍生計算失敗）。
- 使用者現在看到：沒有。catch 內把 `_processedWorkspaceSourceChangeVersion` 推到最新（避免無限重試）並寫 `Error` log。使用者之後開啟該工作區時才會碰到（或碰不到）問題。
- 沒有驗證的部分：隨選建置時是否會重新顯示錯誤，沒有追到。如果隨選建置會顯示錯誤，這項可降為低風險。
- 最小修法：失敗時把原因寫入 `Simulation` / `CoordinatePlanner` 各自既有的狀態訊息（`FreeformHelperViewModel.Simulation.cs:157` 已有 `LastSimulationWorkspaceAttemptMessage` 模式，可沿用）。

### F7. 所有 `BackgroundWork.Run` 的故障最終只被 `UnobservedTaskException` 記 log 後標為已觀察

- 位置：`src/FreeformHelper.UI/Program.cs:114`–`:117`；`src/FreeformHelper.UI/Services/BackgroundWorkRunner.cs`（`_ = work();`）。這裡不是 catch，但效果等同「全域吞例外」。
- 觸發輸入：任何沒有自己 catch 的 fire-and-forget 工作，例如 `IndexMappingReportViewModel.AlgorithmTrace.cs` 的 `PrefetchBackgroundTraceAsync`（沒有 catch）。例外要等 GC 回收該 Task 時才被 `UnobservedTaskException` 看到，時間不確定，log 也沒有關聯到哪個工作。
- 使用者現在看到：沒有。
- 最小修法：`FireAndForgetBackgroundWorkRunner.Run` 以 `name` 包一層 `try/await/catch` 並記 `Error`（名稱已經是參數但目前沒用到）。這是一個通則修法，不需要逐一補 catch。

---

## 低風險（不影響資料；使用者看不到失敗原因）

### F8. 檢視器與預覽類 UI 的失敗只寫 log

| 位置 | 觸發輸入 | 使用者現在看到 | 最小修法 |
|---|---|---|---|
| `FreeformHelperViewModel.PadInspectorDeferred.cs:261`、`:327` | 延遲重新計算 pad 檢視器快照時丟例外 | 檢視器停在先前的簡易快照，缺少 notch／規則追蹤細節；pending 狀態被清掉，看起來像已完成 | 快照加錯誤旗標，顯示 "Details unavailable" |
| `FreeformHelperViewModel.NotchDetails.AutoPlay.cs:124` | 自動播放迴圈中丟例外 | 播放按鈕仍是開啟，但畫面不再前進 | catch 內把 `NotchPreviewAutoPlayEnabled` 設為 false，並顯示狀態 |
| `IndexMappingReportViewModel.AlgorithmTrace.cs:78` | `TraceBuilder.Build` 丟例外 | `Trace: failed (ExceptionType)`；訊息被丟掉，也沒有 log | 補一行 `_logger.Error(ex, ...)`；UI 文字保留 |
| `FreeformHelperViewModel.MatchWorkflow.cs:232` | Step 2 自動偵測丟例外 | 狀態列有錯誤（有告知） | 只缺 log；補 `_logger.Error`，與同檔其他 catch 一致 |

### F9. 啟動 log level 覆寫失敗

- 位置：`src/FreeformHelper.UI/Program.cs:141`
- 觸發輸入：App 設定檔內 `Import.LogLevel` 是不認得的字串。
- 使用者現在看到：沒有。以預設 log level 執行，只有一行 `Warn`。
- 最小修法：維持現狀即可（只影響診斷細節，且 log 已記錄）。若要嚴格，可在 UI 的設定頁顯示該值無效。

### F10. 載入動畫（spinner）子程序與 IPC：全部回傳 false／忽略

這一群是純外觀元件，失敗不影響資料。列出來是因為它們符合「吞例外」的字面定義，且有一處吞得過寬：

- `Services/CadLoadSpinnerProcessHost.cs:243`、`:329`、`:693`（`Warn` log）。
- `:511`–`:531`（`SendRequest(... swallowIoErrors)`，只有 `Debug` log）。
- `:569`、`:573`（`TrySendRequestAsync`，回傳 false）。
- `:639`–`:659`（`TrySendRequest`）、`:737`–`:757`（另一個 `TrySend` 變體）：同樣 6 種例外型別各回傳 false，**沒有任何 log**。
- 過寬之處：`ArgumentException`／`NotSupportedException`／`InvalidOperationException` 在這裡代表程式錯誤（管道名稱無效、序列化不支援），被當作「spinner 暫時連不上」吞掉。程式 bug 會變成「spinner 永遠不顯示」，沒有痕跡。
- 最小修法：`:639`–`:659` 與 `:737`–`:757` 對 `ArgumentException`／`NotSupportedException` 補一行 `_logger.Warn`，其餘保持。

### F11. Spinner 視窗的 IPC 迴圈

- 位置：`src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml.cs:201`
- 觸發輸入：格式錯誤的 JSON 請求。使用者現在看到：沒有，也沒有 log（只有註解 "Best-effort helper process"）。
- 最小修法：補 `Debug` log。

### F12. 主控台連結解析：回傳 default，點擊沒有反應

- 位置：`src/FreeformHelper.UI/Services/ConsoleTargetResolver.cs:47`、`:51`、`:55`、`:59`、`:63`（`Resolve`）；`:187`、`:191`（`EnumerateFileNameMatches`）
- 觸發輸入：主控台輸出中的路徑含非法字元、無權限、路徑過長、或在不可存取的目錄。
- 使用者現在看到（推論，沒有追到 UI 呼叫端）：點擊連結沒有任何反應，沒有「無法開啟」訊息。
- 最小修法：`ConsoleTargetResolution` 加一個 `FailureReason`，呼叫端顯示狀態。

---

## 已檢視、可接受（不需處理）

這些 catch 有回報使用者、重新拋出、或只是取消流程。記錄在此，避免後續稽核重複檢查。

| 位置 | 為什麼可接受 |
|---|---|
| `FreeformHelperViewModel.Persistence.Project.cs:126`、`:130`、`:134`、`:138` | `Path.GetFullPath` 的四種文件化例外，轉成 `INVALID_ARGUMENTS` 失敗結果 |
| `FreeformHelperViewModel.Progress.cs:67`、`:108` | 邊界；交給 `onError`。我逐一看過現有 `onError`（Save/Load Project、Match、IndexMapping、Rebuild grid、DXF check、Export），都會顯示狀態並寫 log。`FreeformHelperViewModel.Simulation.cs:157` 在 `silent` 為 true 時是否通知使用者，沒有追到 |
| `FreeformHelperViewModel.DxfEditing.Export.cs:116`、`:163` | `SetStatusError` + `Error` log。附帶觀察：`File.WriteAllText(path, text)`（`:159`）是非原子覆寫，寫到一半失敗會留下被截斷的 DXF；使用者有被告知失敗，但原檔已損 |
| `FreeformHelperViewModel.Operations.LayerFiltering.cs:54` | 狀態列錯誤 + log |
| `Services/RegularVisibilityMaskService.cs:116` | 回傳 `Failed` 結果，UI 摘要（`FreeformHelperViewModel.RegularVisibilityMask.cs:263`）與 log（`:179`）都會顯示 |
| `Services/GridRebuildOrchestrator.cs:119` | 回傳帶訊息的 `Failed` 結果並寫 log |
| `Services/RuntimeQueryUseCase.Commands.cs:30` | 回傳 CLI 契約的失敗 envelope，含訊息 |
| `Services/CadLoadUseCase.cs:82` | 內嵌載入失敗時：若 path 也失敗則丟 `AggregateException`，否則重新拋出。不吞。（吞的是 `:68`，見 F2） |
| `Services/DxfLayerImageExportService.cs:71` | 轉成 `ArgumentOutOfRangeException` 重新拋出 |
| `Services/SpinnerParentProcess.cs:27`、`:32` | 文件化例外，回傳 false 為函式契約 |
| `Application/Services/NotchTableGenerator.Generation.cs:632` | 解包單一 `AggregateException` 後重新拋出 |
| `Program.cs:54` | 記 log 後 `throw;` |
| `ViewModels/ShellViewModel.Workspaces.cs:145` | 只在 `_isDisposed` 時吞 `ObjectDisposedException`，過濾條件窄 |
| `OperationCanceledException`：`PadInspectorDeferred.cs:257`、`:323`；`Selection.NotchPreview.cs:58`；`NotchDetails.AutoPlay.cs:120`；`CadLoadSpinnerWindow.axaml.cs:162`、`:197` | 取消屬於正常流程 |
| `Logging/EmergencyLogSink.cs` | 只有註解中含 "catch"，沒有 catch 區塊 |

## 全域觀察

- `ExceptionPolicy.IsRecoverable` 只排除 `OutOfMemoryException`，其餘全部視為可吞。這使 `catch (Exception ex) when (IsRecoverable(ex))` 在語意上等同 `catch (Exception)`。本稽核中所有「只寫 log」的項目都靠這個過濾條件通過。
- `catch (Exception ex) when (IsRecoverable(ex))` 共 24 處。其中只寫 log、沒有通知使用者的有：F4、F5、F6、F8（PadInspector 兩處、AutoPlay）、F9、F10（spinner host 的 `:243`、`:329`、`:693`）、F11。
- 非 catch 但相同風險：F1 的 `continue`、F2 的 `File.Exists` 為 false 時直接 fallback、F3 的非原子 `Save`、DXF 匯出的非原子 `WriteAllText`。

## 建議處理順序（每項一個 commit）

1. F1（載入警告 + 不清除 dirty 狀態）
2. F2（fallback 原因可見）
3. F3（備份損毀檔 + 原子寫入）
4. F7（通則修法，補 `BackgroundWork.Run` 的 catch）
5. F4、F5、F6
6. F8–F12

## 待決定（需要 Dennis 回覆才能實作）

1. F2：DXF 路徑讀取失敗時，要「用內嵌副本並明確警告」，還是「載入失敗，不 fallback」？我的建議是前者，因為使用者還能繼續工作，但警告必須可見。
2. F1：被跳過的合併群組，要只警告，還是要在警告的同時阻止覆寫存檔（例如存檔前再確認）？我的建議是先警告並保持 dirty，覆寫確認另議。
3. F3：損毀的設定檔備份檔名與保留數量（建議只保留最近 1 份）。

## 方法限制

- 沒有跑任何程式或測試；所有「使用者現在看到」皆由程式碼推得。
- F5、F6 與 F12 各有一處沒有追到的下游行為，已在各項中標明。
- 依賴圖 `docs/generated/project-dependency-graph.md` 的產生日期為 2026-10-29，比 `origin/main` 舊，沒有重新產生（本任務不改程式、不跑 build）。
