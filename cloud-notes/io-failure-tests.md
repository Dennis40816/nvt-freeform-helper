# io-failure-tests 筆記

分支:`cloud/io-failure-tests`(從 `origin/main` 3622ed7 切出)。只新增測試與本筆記,未改任何 production 程式。

## 新增內容

`tests/FreeformHelper.Tests/UI/ViewModels/ProjectIoFailureBehaviorTests.cs`,共 33 個測試案例(含 Theory 展開)。
全部經由真實入口:`FreeformHelperViewModel.SaveProjectAsync` / `LoadProjectCommand` / `LoadProjectFromPathAsync` / `ExportDxfAllCommand` / `ExportNotchCommand`,以及 `JsonProjectStore`、`ProjectPersistenceUseCase`。

| 類別 | 情境 | 斷言 |
|---|---|---|
| 存檔 | 目標資料夾不存在 | 成功並建立資料夾,只有完整專案檔,無暫存檔(見下方「行為備註」) |
| 存檔 | 父路徑是一般檔案 | 回傳 false、狀態文字 `Save project failed`、仍為未儲存、目錄內容與前後相同 |
| 存檔 | 目標是既有資料夾 | 同上,資料夾內容不變 |
| 存檔 | 目標唯讀 | 同上,原檔位元組不變 |
| 存檔 | 目標被另一個 handle 以 FileShare.None 持有 | 兩種結果之一且內部一致:(錯誤訊息+原檔不變)或(完整新專案+dirty 旗標清除);無暫存檔 |
| 存檔 | 失敗後改存有效路徑 | 重試成功、SessionMode 回到 Ready、指令可用 |
| Store | 設定值無效 | `SaveAsync` 在碰既有檔案之前就丟例外,原檔不變 |
| Store | 父路徑是一般檔案 / 資料夾不存在 | 前者丟 IOException 且無殘留;後者建立資料夾 |
| 載入 | 截斷、空檔、`null`、`[]`、二進位垃圾、設定值無效(6 種) | 經 `LoadProjectFromPathAsync` 與 `LoadProjectCommand` 各一次:結果 `LOAD_FAILED`、狀態文字 `Load project failed`、VM 狀態不變、來源檔位元組不變;之後再存檔寫出的是原專案(未被污染) |
| 載入 | 檔案被獨占鎖定 | `LOAD_FAILED`+錯誤訊息,VM 狀態不變 |
| 載入 | 檔案在選取後被刪除 | `LoadProjectFromPathAsync` 回 `PATH_NOT_FOUND`;UseCase 丟 `FileNotFoundException`;指令路徑見下方 bug |
| 匯出(DXF、Notch 各一) | 資料夾不存在 / 目標是資料夾 / 唯讀 / 被鎖定 | 狀態文字含 `failed`(Notch 另驗 `NotchExportProgressText`、`IsNotchExporting` 回 false);不建立資料夾、不產生半成品,既有檔案位元組不變 |

## 發現的問題

### Bug 1(已 skip):載入指令對「路徑不存在」靜默失敗

- 測試:`LoadProjectCommand_FileDeletedBetweenPickAndLoad_ShowsAnErrorToTheUser`(`[AvaloniaFact(Skip = ...)]`)。
- 位置:`FreeformHelperViewModel.Persistence.Project.cs` 的 `LoadProjectAsync` 以 `_ = await RunProjectLoadAsync(...)` 丟棄結果;`LoadProjectFromPathCoreAsync` 對 `INVALID_ARGUMENTS` 與 `PATH_NOT_FOUND` 只回傳 `ProjectLoadCommandResult.Failed(...)`,沒有呼叫 `status.Report*`。
- 重現:picker 回傳路徑後檔案被刪除(或使用者手動輸入不存在的路徑),按 Load → 狀態文字不變,使用者看不到任何訊息。同一個 `LoadProjectFromPathAsync` 供 Runtime CLI 使用時,回傳碼足夠,所以只有 UI 指令路徑受影響。
- 預期:`StatusText` 應包含 `Project file not found: <path>`。
- 修正方向(未實作):在 `LoadProjectFromPathCoreAsync` 的兩個提早 return 分支呼叫 `status.ReportError`/`ReportBlocked`,與其他分支一致。
- 現有的 `LoadProjectCommand_UnreadableFile_*` 測試對 `missing.json` 也沒有檢查狀態文字,所以此缺陷沒被抓到。

## 行為備註(非 bug,供審查者判斷)

1. **專案存檔會自動建立不存在的資料夾。** `Nvt.Core.IO.AtomicOutput` 文件寫明「Its parent directory is created if needed」,因此測試記錄的是這個行為。匯出(`File.WriteAllText`)則不會建立資料夾而是報錯,兩者不一致;若希望一致需另行決定。
2. **匯出不是原子寫入。** DXF 與 Notch 匯出直接 `File.WriteAllText(path, ...)`,專案存檔才走 `AtomicOutput`。覆寫既有檔案時若中途失敗(磁碟滿等),舊檔會被截斷。目前沒有可從公開入口注入「寫到一半失敗」的接縫,所以沒有對應測試;建議視為後續改善項目。
3. **被鎖定的專案存檔結果與平台相關。** Windows 無法取代開啟中的檔案;Unix 的 rename 不受 advisory lock 影響。因此該測試接受兩種結果,但要求各自內部一致。
4. **唯讀測試在 root 下會 `Assert.Skip`。** 雲端容器是 root,這 3 個案例(存檔 1、匯出 DXF/Notch 各 1)在此被跳過;我另以 uid 65534 執行,全部通過。Windows 上的唯讀屬性(以 `File.Move` 覆寫唯讀檔)沒有實測,請在本機 Windows 確認。
5. **新增檔案的限制。** 測試專案禁用 `File.ReadAllBytes`/`WriteAllText`/`Path.GetTempPath` 等 API(BannedSymbols),新檔以 `FileStream` 輔助方法達成,沒有新增 RS0030 警告。

## 執行結果

- 環境:雲端容器安裝 .NET SDK 10.0.301;`scripts/fetch_core_packages.py` 取得 Nvt.Core 套件後 `dotnet build tests/FreeformHelper.Tests` 為 0 error,新檔案無新增警告。
- root:`33 total, 29 passed, 4 skipped`(3 個唯讀案例因 root 無視檔案權限而跳過,另 1 個為上述 bug skip)。
- uid 65534:`33 total, 0 failed, 1 skipped`(僅 bug skip)。
- 同次另跑 `DebtBaseline*`、`GoldenCiContract*`、`CoreHealthBundle*`、`LayeringBaseline*`:76 passed。
- 未執行:`scripts/tests/lint.ps1`、`repo-health.ps1 -Mode Verify`(容器沒有 pwsh)、完整測試套件。合併前請在本機 Windows 補跑。

## 未涵蓋(後續可做)

- DXF 圖層影像匯出(`ExportDxfLayerImageAsync`)、Coordinate Planner 與 Simulation 的匯出路徑。
- 存檔時嵌入 DXF/遮罩檔被鎖定或無法讀取(`TryEmbedDxf` 的 `File.ReadAllBytes`)。
- 載入時內嵌 DXF 損毀、`LastDxfPath` 指向不存在檔案的訊息內容。
- 專案檔 `schemaVersion` 過新或無法遷移的訊息。
