# TODO（Active Backlog）

> 歷史版本：`docs/archive/TODO-history-2026-03-05.md`

## 執行規則（固定）
- [ ] 每完成一個任務：`build + 對應 tests + lint`。
- [ ] 每個里程碑：`commit + push`。
- [ ] 任務完成或範圍改變時，立即同步更新本檔。

## S6 structural cleanup pass（2026-03-05）

### P1（不改行為的結構重整）
- [x] **S6.1 拆分 `FreeformHelperViewModel.DxfEditing`（550 行）**
  - 目標：按 import/edit/apply 工作流拆 partial，降低 ViewModel 熱點耦合。
  - 範圍：
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.cs`
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：主檔 < 320 行，DXF edit 路徑行為不變。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModel.DxfEditing.cs` 收斂為 snapshot/core helpers（主檔行數 `550 -> 92`）。
    - 新增 3 個 partial：
      - `FreeformHelperViewModel.DxfEditing.Edits.cs`（hide/restore/combine/clear）
      - `FreeformHelperViewModel.DxfEditing.Export.cs`（DXF/image export）
      - `FreeformHelperViewModel.DxfEditing.State.cs`（hidden/combined state sync）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S6.2 拆分 `FreeformHelperView` code-behind 熱點**
  - 目標：將 `FreeformHelperView.axaml.cs` / `FreeformHelperView.Console.cs` 依 UI 子域拆分（workspace/overlay/console binding）。
  - 範圍：
    - `src/FreeformHelper.UI/Views/FreeformHelperView.axaml.cs`
    - `src/FreeformHelper.UI/Views/FreeformHelperView.Console.cs`
    - 新增 `src/FreeformHelper.UI/Views/FreeformHelperView.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：主檔行數下降，Terminal 與快捷鍵行為不變。
  - 完成（2026-03-05）：
    - `FreeformHelperView.axaml.cs` 主檔 `539 -> 58` 行，拆分為：
      - `FreeformHelperView.Lifecycle.cs`（attach/detach/initial-fit/deferred hooks）
      - `FreeformHelperView.SettingsWindow.cs`（settings window 與 root menu）
      - `FreeformHelperView.ConsoleHost.cs`（console shell binding/theme/sync）
    - `FreeformHelperView.Console.cs` 主檔 `535 -> 27` 行，拆分為：
      - `FreeformHelperView.Console.Events.cs`（auto-scroll/事件與互動）
      - `FreeformHelperView.Console.Rendering.cs`（render/colorizer/search/filter）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S6.3 拆分 `NotchExportSelectionViewModel`（516 行）**
  - 目標：將 row list/preview/commands 分域，降低 export 視窗維護成本。
  - 範圍：
    - `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs`
    - 新增 `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：主檔 < 300 行，Export row 選取/預覽流程不變。
  - 完成（2026-03-05）：
    - `NotchExportSelectionViewModel.cs` 主檔收斂為 constructor/property/observable callbacks（`516 -> 158` 行）。
    - 新增 3 個 partial：
      - `NotchExportSelectionViewModel.Selection.cs`（visible rows/group select/counts）
      - `NotchExportSelectionViewModel.Workspace.cs`（workspace linked rows/index/preview callback）
      - `NotchExportSelectionViewModel.Models.cs`（row mode + group/row item models）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

### P2（契約測試補強）
- [x] **S6.4 契約測試：Terminal / RuntimeQuery / Ctrl+S**
  - 目標：補自動化 guard，防止已修復契約回歸。
  - 範圍：
    - `tests/FreeformHelper.Tests/UI/**`
    - `tests/FreeformHelper.Tests/Runtime/**`（若無則新增）
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - 完成定義：
    - Terminal TextEditor 路徑有測試守門。
    - Runtime query notch payload shape 有測試守門。
    - `Ctrl+S -> SaveProjectAsync -> toast` 契約有測試守門。
  - 完成（2026-03-05）：
    - 新增 Runtime query payload shape 測試：`RuntimeQueryUseCaseTests.ExecuteAsync_QueryNotchValidation_ProducesStableRowPayloadShape`。
    - 補強快捷鍵契約測試：`UiLayoutGuardTests.ShortcutContract_GlobalAndCanvasScopesRemainConsistent` 追加 `Ctrl+S -> SaveProjectFromShortcutAsync -> ShowTopToast` 斷言。
    - Terminal TextEditor 路徑延用既有守門：`TerminalStartupPathTests.ConsoleExpanded_AfterStartupLogs_UsesTextEditorOnly`。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`、`dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --nologo /p:UseAppHost=false --filter "FullyQualifiedName~RuntimeQueryUseCaseTests.ExecuteAsync_QueryNotchValidation_ProducesStableRowPayloadShape|FullyQualifiedName~UiLayoutGuardTests.ShortcutContract_GlobalAndCanvasScopesRemainConsistent|FullyQualifiedName~TerminalStartupPathTests.ConsoleExpanded_AfterStartupLogs_UsesTextEditorOnly"`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

### P2（量測基線）
- [x] **S6.5 3635 基線量測（先量測，不改演算法）**
  - 目標：量化 selection lag 與 export 時間，建立後續優化前後比較基準。
  - 範圍：
    - `docs/performance/` 新增量測紀錄
  - 驗證：
    - 文件可重現（含命令、測試資料、量測欄位）
  - 完成定義：有固定模板與第一版基準結果。
  - 完成（2026-03-05）：
    - 新增 `docs/performance/3635-selection-export-baseline-2026-03-05.md`，包含：
      - 固定 command、測試資料、量測欄位模板
      - 第一版 selection/export 基線結果（來源：`build/perf/3635-regression-verify`）
    - `docs/performance/regression-baseline-3635.md` 補上固定量測紀錄連結。
    - 驗證：文件中的命令、輸入路徑與輸出檔欄位可直接對應現有腳本與產物。

## 已完成（最近）
- [x] S4.1~S4.6（optimizer pass）
- [x] S5.1~S5.3（structural cleanup pass）
