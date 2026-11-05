# Linux-only 測試修正報告（cloud run，2026-11-05）

分支：`cloud/linux-only-tests`（從 `origin/main` 5e48bec 切出）。來源：`origin/cloud/flaky-tests` 的 `cloud-notes/flaky-tests.md`。

## 結論

- 筆記中的 3 個 Linux-only 失敗，全部以**只改測試**的方式修好，沒有改任何 `src/` 程式碼。
- 修正前（未改動、同一環境）3 個皆失敗，訊息與來源筆記完全一致。修正後 3 個皆通過。
- 完整套件：2988 個測試，0 失敗，43 略過。略過數與來源筆記相同，都是環境因素（見最後一節）。

## 環境

- Linux x64 容器，.NET SDK 10.0.301（`dotnet-install.sh` 安裝到 `~/.dotnet`），`python3 scripts/fetch_core_packages.py` 成功。
- 容器沒有設定 `LANG`，所以 CurrentCulture 是 Invariant。ICU 74 存在。
- 建置：`dotnet build tests/FreeformHelper.Tests -c Release`，0 錯誤；修改後沒有新增與 `TestCulture` 相關的警告。
- 執行：xUnit v3 in-process runner，直接執行 `build/bin/FreeformHelper.Tests/Release/net10.0/FreeformHelper.Tests`。

## 修改內容

| 測試 | 根因 | 修正 |
|---|---|---|
| `PadInfoViewModelTests.CadPadInfo_UsesSharedNotchDisplayProjectionTexts` | culture：`{ratio:P2}` 使用 CurrentCulture。Invariant 的百分比格式是 `n %`，en-US 是 `n%` | 測試開頭加 `using var culture = TestCulture.UseEnUs();` |
| `NotchDisplayProjectorTests.Build_FromPadInspectorSnapshot_ProducesSharedDisplayProjection` | 同上，同一個來源（`NotchDisplayProjector.cs:116-117`） | 同上 |
| `FreeformHelperViewModelTests.ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads` | 換行：`DxfExportService` 用 `StringBuilder.AppendLine`，換行是 `Environment.NewLine`。Windows 為 CRLF，Linux 為 LF，測試寫死 `\r\n` | 期望字串改用 `Environment.NewLine` 組出 |

新增檔案：`tests/FreeformHelper.Tests/TestInfrastructure/TestCulture.cs`。它是一個 `IDisposable` scope，把目前執行緒的 `CurrentCulture` 和 `CurrentUICulture` 設為 en-US，Dispose 時還原。沒有修改全域或 assembly 層級的 culture。

### 沒有弱化斷言

- 3 個測試的期望字串完全沒有改（`94.10%`、`150.00% (enabled)`、`146.40 %` 等維持原樣）。這些字串原本就是以 en-US 寫的，scope 只是讓測試在任何主機上都用同一個 culture 執行。
- DXF 測試仍然要求 group code `8` 與圖層名稱各自獨占一行，且換行必須是寫入端實際使用的換行。沒有改成「去掉換行再比」或只比部分字串。
- 中立性驗證：修正後的 3 個測試在 `LANG=de_DE.UTF-8`（輸出時間顯示為 `0,597s`，確認 de-DE 生效）和 `LANG=zh_TW.UTF-8` 下都通過。

## 為什麼沒有改產品程式碼

來源筆記建議的做法是改產品（讓 projector 用固定 culture）。我評估後沒有採用，原因如下，供 Dennis 決定：

1. **產品在 Linux 上並沒有錯。** 一般 Linux 桌面的 locale 是 `en_US.UTF-8`，輸出和 Windows en-US 一樣是 `94.10%`。這次失敗只是因為容器沒有設定 locale，落到 Invariant。
2. **改產品會改變使用者看到的文字。** 要讓文字與 Windows 現況完全一致，需要自訂 `NumberFormatInfo`（`n%` 版型加千分位）；若改用 `CultureInfo.InvariantCulture`，會把 `94.10%` 變成 `94.10 %`，所有相關 UI 文字與測試期望都要跟著改。AGENTS.md 規定改預設值就是改輸出，需要明說並由 owner 決定。
3. **DXF 換行：** 匯入端（`DxfPadImporter.Tokenization.cs`）用 `StreamReader.ReadLine()`，LF 與 CRLF 都能讀，所以 Linux 輸出 LF 沒有往返問題。若要固定成 CRLF，是「匯出格式決策」，不是 Linux bug。

### 建議的後續（需 owner 決定，本分支未做）

- **百分比文字的 culture 不一致（真正的產品議題，但與 Linux 無關）：** 同一個 `NotchDisplayProjector` 內，`ToRegularRatioText`/`ToFullRatioText`（第 116-117 行）用 CurrentCulture，而 `CombinedRatioText`、`ToRegularValueText`、`ToFullValueText`、`CombinedValueText`（第 118-121 行與 `BuildCombinedRatioValueText` 第 254 行）用 InvariantCulture。所以在 en-US 的 Windows 上，同一個 projection 會同時產生 `94.10%`（`ToRegularRatioText`）和 `94.1 %`（`ToRegularValueText`）；在 de-DE 的 Windows 上前者會變成 `94,10 %`。這兩個文字是否出現在同一個畫面，我沒有檢查。其他同樣使用 CurrentCulture 的位置：`NotchDetailViewModel.cs:80`、`RegularPadInfoViewModel.Core.cs:400-401`、`IndexMappingMaskAuditRowViewModel.cs:90,96`、`FreeformHelperViewModel.PadInspector.Trace.cs:51,121`、`FreeformAutoDetectStatRow.cs:22`、`IndexMappingDecisionRowViewModel.cs:336,343`。建議另開 issue：選定單一格式（含是否保留 `n%` 與 `n %` 的差異），一次收斂。這個決策完成後，`TestCulture` 的使用處可以拿掉。
- **DXF 換行：** 若希望匯出檔在各平台位元組一致，可讓 `DxfExportService` 明確寫 `\r\n`（Windows 輸出不變）。完成後測試可改回寫死 `\r\n`。

## 測試結果

| 項目 | 結果 |
|---|---|
| 3 個目標測試，修正前（Linux，Invariant） | 3 失敗 |
| 3 個目標測試，修正後（Linux，Invariant） | 3 通過 |
| 3 個目標測試，修正後，`LANG=de_DE.UTF-8` | 3 通過 |
| 3 個目標測試，修正後，`LANG=zh_TW.UTF-8` | 3 通過 |
| 完整套件（含 Architecture ratchet 測試） | 2988 總數 / 0 失敗 / 43 略過 / 339 秒 |

## 沒有執行的項目

- `scripts/dev/prepare-ui-workspace.ps1`、`scripts/tests/lint.ps1`、`scripts/verify.ps1`：容器沒有 PowerShell，無法執行。CRLF 以 `sed` 手動正規化，4 個觸及的檔案均已確認是 CRLF。**本機 review 時請先跑 `./scripts/tests/lint.ps1 -UseNoAppHost`。**
- `eng/core-health/repo-health.ps1 -Mode Verify`（約 25 分鐘）：未執行。本分支沒有改動 `src/`，也沒有觸碰 `eng/code-health/baseline.json`、`eng/code-health/test-debt.json`。新增的 `TestCulture.cs` 是否影響 test-debt ledger，需要在本機 Verify 確認。
- `docs/generated/project-dependency-graph.md`：已讀取，但無法用 `generate-dependency-graph.ps1` 重建（沒有 pwsh）。本次只動測試檔，沒有改專案相依。
- Windows 上的結果未驗證。預期不變，因為 Windows 的 CurrentCulture 若是 en-US，行為與修正前相同；Windows 的 `Environment.NewLine` 是 `\r\n`，與原本寫死的值相同。

## 略過的 43 個測試（與來源筆記相同，環境因素）

- 26：私有 `example/` submodule 未檢出。
- 16：RuntimeQuery 固定 pipe 需要 Windows 的 pipe 存在性檢查。
- 1：Windows pipe 存取規則需要 Windows。
