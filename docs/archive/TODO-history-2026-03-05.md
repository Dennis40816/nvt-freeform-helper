# TODO（未完成清單）

## 新一輪未完成（2026-03-05，S5 structural cleanup pass）
### P1（大型檔案拆分，行為不變）
- [x] **S5.1 拆分 `NotchV22CompensationService.Geometry`（512 行）**
  - 目標：按 polygon clipping / reachability / rectangle merge 拆 partial，降低 notch 幾何修改衝突。
  - 範圍：`src/FreeformHelper.Application/Services/NotchV22CompensationService.Geometry*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：geometry 主檔 < 260 行，`NotchV22CompensationServiceTests` 全綠。
  - 完成（2026-03-05）：
    - 將 reachability/expand/grid-cell 區塊拆到新檔 `NotchV22CompensationService.Geometry.Reachability.cs`。
    - `NotchV22CompensationService.Geometry.cs` 收斂為 polygon clip/normalize/rectangle merge（主檔行數 `512 -> 198`）。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S5.2 拆分 `DxfOverlapAnalyzer`（576 行）**
  - 目標：按 overlap gather / report format / guard policy 拆分 helper，降低演算法修改風險。
  - 範圍：`src/FreeformHelper.Application/Services/DxfOverlapAnalyzer*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：主檔 < 320 行，`DxfOverlapAnalyzerTests`（現有應用測試群組）維持全綠。
  - 完成（2026-03-05）：
    - `DxfOverlapAnalyzer` 改為 partial 並拆成 3 個檔案：
      - `DxfOverlapAnalyzer.Candidates.cs`（spatial bucket candidate pairs）
      - `DxfOverlapAnalyzer.Geometry.cs`（polygon overlap 判定）
      - `DxfOverlapAnalyzer.Signatures.cs`（duplicate signature canonicalization）
    - 主檔 `DxfOverlapAnalyzer.cs` 行數 `576 -> 279`，保留 `Analyze` 主流程與報表收斂。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S5.3 拆分 `RuntimeQueryUseCase.NotchDetails`（510 行）**
  - 目標：按 payload builder / polygon projection / diagnostics map 拆成 partial，保留 query response contract。
  - 範圍：`src/FreeformHelper.UI/Services/RuntimeQueryUseCase.NotchDetails*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
  - 完成定義：主檔 < 280 行，runtime query `notch*` 指令輸出契約不變。
  - 完成（2026-03-05）：
    - `RuntimeQueryUseCase.NotchDetails.cs` 收斂為 `QueryCadNotch` 主 payload（主檔行數 `510 -> 178`）。
    - 新增 2 個 partial：
      - `RuntimeQueryUseCase.NotchDetails.MultiOwner.cs`（`QueryCadMultiOwner` + `QueryCadNotchStage`）
      - `RuntimeQueryUseCase.NotchDetails.PadQueries.cs`（`QueryCadPad` + `QueryRegularPad`）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

## 新一輪未完成（2026-03-05，S4 optimizer pass）
### P0（品質基線）
- [x] **S4.1 恢復 `lint -AllFiles` 全綠（ENDOFLINE 大量回歸）**
  - 目標：修正目前 `dotnet format` 報出的 CRLF/LF 行尾不一致，回到全專案可驗證狀態。
  - 範圍：`src/**/*.cs`、`tests/**/*.cs`（以 lint 實際報錯檔案為主）
  - 驗證：
    - `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 完成定義：`lint -AllFiles` 0 error，且不新增行為變更。
  - 完成（2026-03-05）：
    - 針對 lint 報錯檔案批次正規化 CRLF 行尾（含 `PadCanvas*`、`FreeformHelperViewModel.*`、`ManualSizingService`、`RuntimeQueryUseCase.Commands.WorkflowSelection`、`NotchTableGenerator*`、`SettingsPersistence*` 等）。
    - 驗證：`./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`、`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`。

- [x] **S4.2 新增 git EOL 防漂移護欄（`.gitattributes`）**
  - 目標：避免後續 refactor 再次引入跨檔案 EOL 漂移，固定 repo 文字檔換行策略。
  - 範圍：`.gitattributes`、必要文件註記
  - 驗證：
    - `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
    - `git diff --stat`（僅預期 EOL 正規化/護欄檔案）
  - 完成定義：新提交後在 Windows 環境持續寫入 CRLF，lint 不再因 ENDOFLINE 回歸。
  - 完成（2026-03-05）：
    - 新增 `.gitattributes`，固定主要文字檔（`*.cs`/`*.axaml`/`*.ps1`/`*.json`/`*.md`/`*.txt`）為 `eol=crlf`，並標記常見二進位資產 `binary`。
    - 搭配 S4.1 驗證完成後，`lint -AllFiles` 已回復全綠。

### P1（結構熱點，行為不變）
- [x] **S4.3 拆分 `DxfVisibleIndexAssignmentService`（607 行）**
  - 目標：按 assignment policy / sequence / diagnostics 拆 partial，降低演算法修改衝突。
  - 範圍：`src/FreeformHelper.Application/Services/DxfVisibleIndexAssignmentService*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：主檔 < 320 行，diff idx 指派行為與測試結果不變。
  - 完成（2026-03-05）：
    - `DxfVisibleIndexAssignmentService.cs` 收斂為 service shell + `Assign` 主流程（主檔行數 `607 -> 259`）。
    - 新增 3 個 partial：
      - `DxfVisibleIndexAssignmentService.Fallback.cs`（no-grid fallback assignment）
      - `DxfVisibleIndexAssignmentService.Grouping.cs`（regular diff grouping / per-IC resolve helpers）
      - `DxfVisibleIndexAssignmentService.RowSequence.cs`（row-sequence DP 指派與 nested row item/group types）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S4.4 拆分 `FreeformHelperViewModel.Selection`（589 行）**
  - 目標：按 CAD/Regular selection、batch apply、inspector sync 拆分，降低熱路徑複雜度。
  - 範圍：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：主檔 < 320 行，selection side-effects 契約不變。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModel.Selection.cs` 收斂為 selection core（主檔行數 `589 -> 171`）。
    - 新增 3 個 partial：
      - `FreeformHelperViewModel.Selection.NotchPreview.cs`（deferred notch preview queue/cancel/run）
      - `FreeformHelperViewModel.Selection.Summary.cs`（selection summary + manual size/range sync）
      - `FreeformHelperViewModel.Selection.Locate.cs`（quick locate / match locate / focus-highlight helpers）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

- [x] **S4.5 拆分 `NotchV22CompensationService` 主檔（584 行）**
  - 目標：按 owner decision / to-full gate / trace builder 拆分，維持現行輸出一致。
  - 範圍：`src/FreeformHelper.Application/Services/NotchV22CompensationService*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：主檔 < 320 行，`NotchV22CompensationServiceTests` 全綠。
  - 完成（2026-03-05）：
    - `NotchV22CompensationService.cs` 收斂為 service shell + `Compute` + public helpers（主檔行數 `584 -> 108`）。
    - 新增 3 個 partial：
      - `NotchV22CompensationService.Stages.cs`（Stage A/B/C/D 與 rule-decision adapter）
      - `NotchV22CompensationService.Boundary.cs`（boundary seed/active set helper + stage records）
      - `NotchV22CompensationService.SpatialIndex.cs`（CAD bounds spatial index + StageB context owner cache）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

### P2（文件一致性）
- [x] **S4.6 文件基線校正：behavior/settings matrix stale-check**
  - 目標：更新 `behavior-inventory`、`settings-entry-matrix` 的最後更新日期與基線描述，確認與現行契約一致。
  - 範圍：`docs/reference/behavior-inventory.md`、`docs/guides/settings-entry-matrix.md`
  - 驗證：
    - 文件中的「最後更新」「分支」「發布基準」與當前實際一致
    - 關鍵契約（deferred app settings、Ctrl+S、runtime query single-entry）仍可對應到現行程式
  - 完成定義：文件可直接作為下一輪驗收基線，無明顯過時描述。
  - 完成（2026-03-05）：
    - 更新文件最後更新日期為 `2026-03-05`，並加入 `S4.6 stale-check` 區塊。
    - 明確對照 3 個關鍵契約的程式入口：
      - deferred app settings（`MarkProjectLoadedForAppGeneralPersistence` / `FlushDeferredAppGeneralSettingsIfNeeded`）
      - `Ctrl+S -> SaveProjectAsync -> top toast`
      - runtime query IPC -> `RuntimeQueryUseCase.ExecuteAsync` single-entry
    - 完成檔案：
      - `docs/reference/behavior-inventory.md`
      - `docs/guides/settings-entry-matrix.md`

## 新一輪未完成（2026-03-05，S3 refactor pass）
### P1（不改行為的結構重整）
- [x] **S3.1 拆分 NotchTableGenerator 熱點（864 行）**
  - 目標：將 cad-allocation/v2.2 candidate/eligibility/memo 區塊拆為 partial，保留單一對外入口。
  - 範圍：`src/FreeformHelper.Application/Services/NotchTableGenerator.cs`、`src/FreeformHelper.Application/Services/NotchTableGenerator.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：主檔降到 < 350 行，`NotchTableGeneratorTests` / `NotchV22CompensationServiceTests` 行為不變。
  - 完成（2026-03-05）：
    - `NotchTableGenerator` 已拆為 4 個 partial：
      - `NotchTableGenerator.cs`（對外入口 + constructor）
      - `NotchTableGenerator.Generation.cs`（legacy/cad-allocation/v2.2 candidate 與 threshold/progress helpers）
      - `NotchTableGenerator.Eligibility.cs`（single-CAD eligibility 評估）
      - `NotchTableGenerator.Memo.cs`（cad allocation memo）
    - 主檔行數 `864 -> 119`。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S3.2 拆分 FreeformHelperView.Canvas 視圖流程（804 行）**
  - 目標：拆出 canvas viewport/overlay/selection event 子域，避免主 view code-behind 巨型化。
  - 範圍：`src/FreeformHelper.UI/Views/FreeformHelperView.Canvas.cs`、`src/FreeformHelper.UI/Views/FreeformHelperView.Canvas.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：主檔 < 350 行，canvas 顯示與互動行為不變。
  - 完成（2026-03-05）：
    - `FreeformHelperView.Canvas.cs` 已拆成：
      - `FreeformHelperView.Canvas.cs`（canvas host/event wiring + pad info interaction entry）
      - `FreeformHelperView.Canvas.Layout.cs`（popover 位置計算與幾何工具）
      - `FreeformHelperView.Canvas.DirtyState.cs`（pending changes / hit-test block policy）
    - 主檔行數 `804 -> 343`。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S3.3 拆分 PadCanvas 核心殼層（798 行）**
  - 目標：將 `PadCanvas` 核心狀態/資源/初始化拆分為 partial（state/resources/lifecycle）。
  - 範圍：`src/FreeformHelper.UI/Controls/PadCanvas.cs`、`src/FreeformHelper.UI/Controls/PadCanvas.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：`PadCanvas.cs` 主檔 < 320 行，selection/render pipeline 行為不變。
  - 完成（2026-03-05）：
    - `PadCanvas` 新增 3 個 partial：
      - `PadCanvas.Properties.cs`（StyledProperty 宣告與 wrapper）
      - `PadCanvas.State.cs`（核心 state/caches/selection fields）
      - `PadCanvas.Lifecycle.cs`（constructor、theme/resource lifecycle、cache invalidation、match-link index rebuild）
    - `PadCanvas.cs` 收斂為 control shell + events（主檔行數 `798 -> 27`）。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

- [x] **S3.4 拆分 PadCanvas.Rendering（797 行）**
  - 目標：render layers（grid/cad/regular/labels/overlay）拆分，降低渲染回歸風險。
  - 範圍：`src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs`、`src/FreeformHelper.UI/Controls/PadCanvas.Rendering.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
  - 完成定義：主檔 < 320 行，draw order/overlay 顯示契約維持一致。
  - 完成（2026-03-05）：
    - `PadCanvas.Rendering.cs` 收斂為 render 入口與 draw-list orchestration（主檔行數 `797 -> 241`）。
    - 新增 3 個 partial：
      - `PadCanvas.Rendering.Cad.cs`（CAD draw + diff-idx overlay）
      - `PadCanvas.Rendering.Regular.cs`（Regular draw + hatch + selection overlay hook）
      - `PadCanvas.Rendering.Labels.cs`（match allocation labels / ratio label placement）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`。

- [x] **S3.5 拆分 FreeformHelperViewModel.State（794 行）**
  - 目標：將 state transition / selection snapshot / derived summary 拆成專責 partial。
  - 範圍：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.cs`、`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：主檔 < 350 行，狀態切換與 property changed 行為不變。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModel.State.cs` 收斂為 state shell（selection/inspector/status entry），主檔行數 `794 -> 238`。
    - 新增 2 個 partial：
      - `FreeformHelperViewModel.State.Configuration.cs`（grid/match/index-mapping/UI options 與對應 partial callbacks）
      - `FreeformHelperViewModel.State.SizingAndPitch.cs`（manual sizing fields、pitch summary、cascade settings）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

- [x] **S3.6 拆分 FreeformHelperViewModel.Settings（726 行）**
  - 目標：設定欄位/套用/side-effects 維持單一政策入口，拆掉主檔密度。
  - 範圍：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`、`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：主檔 < 320 行，settings persistence/deferred policy 不變。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModel.Settings.cs` 收斂為 settings shell（cascade rebuild + utility + dirty-tracking override），主檔行數 `726 -> 236`。
    - 新增 `FreeformHelperViewModel.Settings.PropertyCallbacks.cs` 承接所有 `partial void On*Changed(...)` callback 與 setting side-effects。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`。

- [x] **S3.7 拆分 ManualSizingService（713 行）**
  - 目標：進一步拆分 parsing/apply/undo path，降低單檔變更衝突。
  - 範圍：`src/FreeformHelper.UI/Services/ManualSizingService.cs`、`src/FreeformHelper.UI/Services/ManualSizingService.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - 完成定義：主檔 < 320 行，`ManualSizingServiceTests` 行為一致。
  - 完成（2026-03-05）：
    - `ManualSizingService.cs` 收斂為 snapshot/ensure + result records（主檔行數 `713 -> 114`）。
    - 新增 2 個 partial：
      - `ManualSizingService.Operations.cs`（Apply/Reset/SetPadDimensions + global/local apply paths）
      - `ManualSizingService.Distribution.cs`（compose/distribute helpers、target size、resize/jagged clone）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`。

- [x] **S3.8 拆分 PadCanvas.View（712 行）**
  - 目標：抽離 viewport transform/fit/zoom helpers，降低 view path 複雜度。
  - 範圍：`src/FreeformHelper.UI/Controls/PadCanvas.View.cs`、`src/FreeformHelper.UI/Controls/PadCanvas.View.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：主檔 < 320 行，fit/zoom/pan 行為不變。
  - 完成（2026-03-05）：
    - `PadCanvas.View.cs` 收斂為 fit/zoom/pan 入口（主檔行數 `712 -> 223`）。
    - 新增 2 個 partial：
      - `PadCanvas.View.AxisLabels.cs`（axis labels / IC block rendering 與 label layout helpers）
      - `PadCanvas.View.Bounds.cs`（world/screen transform、world/selection bounds 計算）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

- [x] **S3.9 拆分 RuntimeQueryUseCase.Commands（693 行）**
  - 目標：按 command domain 再拆（selection/export/notch/terminal），避免 command handler 再膨脹。
  - 範圍：`src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.cs`、`src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：主檔 < 320 行，runtime query JSON schema 不變。
  - 完成（2026-03-05）：
    - `RuntimeQueryUseCase.Commands.cs` 收斂為 shell（主檔行數 `693 -> 4`）。
    - 新增 4 個 domain partial：
      - `RuntimeQueryUseCase.Commands.WorkflowSelection.cs`
      - `RuntimeQueryUseCase.Commands.Export.cs`
      - `RuntimeQueryUseCase.Commands.TerminalStatus.cs`
      - `RuntimeQueryUseCase.Commands.NotchQueries.cs`
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

- [x] **S3.10 拆分 SettingsPersistence 測試熱點（678 行）**
  - 目標：按 app-settings/project-settings/deferred-save 主題拆測試檔。
  - 範圍：`tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.cs`、`tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 完成定義：測試名稱/行為不變，檔案拆分後仍全綠。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModelTests.SettingsPersistence.cs` 收斂為 settings-draft 主題（主檔行數 `678 -> 186`）。
    - 新增 2 個測試 partial 檔：
      - `FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs`（save/load project snapshot 邏輯）
      - `FreeformHelperViewModelTests.SettingsPersistence.AppGeneral.cs`（app-general precedence 與 deferred flush）
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

## 新一輪未完成（2026-03-05，repo-optimizer-loop）
### P0（先恢復品質基線）
- [x] **S2.1 修復 lint -AllFiles 回歸（FINALNEWLINE / IMPORTS）**
  - 目標：恢復 `lint -AllFiles` 全綠，避免後續優化在失真基線上進行。
  - 範圍：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Snapshots.cs`、`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Formatting.cs`、`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Trace.cs`
  - 驗證：
    - `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 完成定義：上述命令皆成功且不新增格式化修正建議。
  - 完成（2026-03-05）：
    - 透過 `dotnet format --include` 修復 `PadInspector.Snapshots/Formatting/Trace` 的 `FINALNEWLINE` 與 `IMPORTS`。
    - 驗證：`./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`、`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`。

### P1（維護性/可擴充）
- [x] **S2.2 拆分 DevView 巨型 XAML（1245 行）**
  - 目標：將 Dev 頁拆為 section controls，降低調整 prototype UI 時的衝突與回歸風險。
  - 範圍：`src/FreeformHelper.UI/Views/DevView.axaml`、`src/FreeformHelper.UI/Views/DevSections/*`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
  - 完成定義：`DevView.axaml` 主檔降至 < 400 行且 Dev 頁現有 prototype 區塊功能不變。
  - 完成（2026-03-05）：
    - `DevView.axaml` 改為 shell + probe 區塊，並拆出 3 個 section controls：
      - `Views/DevSections/DevUiLabsSectionView`
      - `Views/DevSections/DevIconAndStickySectionView`
      - `Views/DevSections/DevInspectorPrototypeSectionView`
    - `DevView.axaml` 行數由 `1245 -> 37`。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`。

- [x] **S2.3 拆分 NotchDetails 主流程（866 行）**
  - 目標：把 snapshot/trace/export row 組裝拆到專責 partial，保留單一 entry。
  - 範圍：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`、`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application`
  - 完成定義：主檔 < 350 行，notch details/runtime query 輸出行為維持一致。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModel.NotchDetails.cs` 收斂為 preview/detail entry（`181` 行）。
    - 新增 `FreeformHelperViewModel.NotchDetails.Compensation.cs`（cache + compensation/query helper）。
    - 新增 `FreeformHelperViewModel.NotchDetails.AutoPlay.cs`（Step3 auto-play loop 與 stage 切換）。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S2.4 拆分 PadCanvas.Input（854 行）為 pointer/keyboard/selection 子域**
  - 目標：降低輸入事件路徑耦合，讓框選延遲與快捷鍵修正可獨立迭代。
  - 範圍：`src/FreeformHelper.UI/Controls/PadCanvas.Input.cs`、`src/FreeformHelper.UI/Controls/PadCanvas.Input.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`
  - 完成定義：`PadCanvas.Input.cs` 主檔 < 300 行，既有 selection/hit-test/shortcut 行為不變。
  - 完成（2026-03-05）：
    - `PadCanvas.Input` 已拆成：
      - `PadCanvas.Input.cs`（pointer press/release + shared selection fields）
      - `PadCanvas.Input.PointerMove.cs`（pointer move / box-select dragging）
      - `PadCanvas.Input.Navigation.cs`（wheel + keyboard shortcuts）
      - `PadCanvas.Input.Selection.cs`（hit-test/selection helpers + selection API）
    - 主檔行數 `854 -> 247`。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

- [x] **S2.5 拆分 UI 測試熱點 Basics（722 行）**
  - 目標：把 `FreeformHelperViewModelTests.Basics.cs` 依主題拆檔，降低測試維護成本。
  - 範圍：`tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.cs`、`tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.*.cs`
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
  - 完成定義：原 Basics 巨型檔被拆分，測試名稱/覆蓋主題保持可追蹤且全綠。
  - 完成（2026-03-05）：
    - `FreeformHelperViewModelTests.Basics.cs` 已拆為 4 個主題檔：
      - `FreeformHelperViewModelTests.Basics.CoreFlags.cs`
      - `FreeformHelperViewModelTests.Basics.LocateAndMatchActions.cs`
      - `FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs`
      - `FreeformHelperViewModelTests.Basics.GridPitch.cs`
    - 測試名稱與行為維持不變，僅做檔案分拆與格式整理。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`、`./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`、`./scripts/tests/lint.ps1 -UseNoAppHost`。

## 掃描基準（2026-02-26）
- 目前工作分支：`master`
- 目前發布基準：`beta-0.1`（tag，2026-03-04）
- 掃描範圍（tracked files）：`353` 檔
  - `src`: 247
  - `tests`: 54
  - `docs`: 22
  - `scripts`: 15
  - `example`: 6
- 已做靜態熱點掃描（`.cs/.axaml`）：`286` 檔（含測試）

## 新增未完成（2026-02-27，優先清單）
### P0（行為優先，先收斂可用性/效能痛點）
- [x] **U0.1 Export DXF(visible) 要納入「作為 regular source 的原始 DXF layer」**
  - 背景：若目前 regular source 使用的是 DXF layer（非 generated），`Export DXF (visible)` 也要把該 layer 一併輸出，避免匯出內容與畫面/運算來源不一致。
  - 驗收：
    - 載入含 `regular source = DXF layer` 的專案，勾選 visible export。
    - 匯出結果需包含被指定為 regular source 的圖層幾何。
    - 若 regular source 為 generated，行為維持現狀（不誤加原始 layer）。
  - 完成（2026-02-27）：
    - `Export DXF (visible)` 會在 `RegularSourceMode=FromDxfLayer` 時附加 regular source layer pads（仍排除 hidden CAD）。
    - 補測：`FreeformHelperViewModelTests.CommandsAndUndo.ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads`。

- [x] **U0.2 Step3 重算入口整理：`Recompute selected` 的定位與 `Recompute all` 決策**
  - 背景：目前按鈕語義不清；需釐清是否保留 selected 路徑或改為 all（或兩者並存）。
  - 方向：
    - 定義 Step3 自動重算與手動重算觸發矩陣（selected/all）。
    - UI 文案與行為一致（避免使用者誤解）。
  - 驗收：
    - `docs/reference/behavior-inventory.md` 補齊 Step3 觸發矩陣。
    - UI 上可明確分辨「只重算選取」或「全域重算」。
  - 完成（2026-02-27）：
    - 決策：維持「focused selection」路徑，不引入 `Recompute all`（避免重算全量 CAD 造成不必要阻塞）。
    - UI 文案改為 `Recompute focused CAD (Step 3)`，tooltip 明確標示「以目前選取中的焦點 CAD 重算，非全域重算」。
    - `docs/reference/behavior-inventory.md` 已補 Step3 自動/手動觸發矩陣與 recompute 實際語義。

- [x] **U0.3 Notch row 存在意義與 no-op row 策略文件化 + UI 顯示策略**
  - 背景：目前使用者難理解像 `8,100,65535,0,65535,0,0` 這類 no-op row 為何存在。
  - 方向：
    - 文件化 notch row 的資料契約（輸出相容、索引對齊、下游解析需求）。
    - UI 預設以可讀摘要顯示 no-op，並提供「隱藏 no-op」檢視切換（不改輸出預設）。
  - 驗收：
    - `docs/reference/behavior-inventory.md` 與 `docs/guides/*` 補充 no-op row 說明與範例。
    - `Select notch rows` 視窗可快速辨識 no-op row，閱讀負擔下降。
  - 完成（2026-02-27）：
    - `Select notch rows` 新增 `Rows shown`（預設 `Transfer-only`，可切 `All rows`）。
    - `docs/reference/behavior-inventory.md` 補充 no-op row 契約、存在意義與 UI 顯示策略。

- [x] **U0.4 Inspector 熱路徑增量快取（selection revision key）**
  - 現況：`FreeformHelperViewModel.PadInspector.cs` 單選仍有重複 `OrderBy/GroupBy/ToList`。
  - 方向：對 matched link/details 與 rule trace 建立 revision-based 快取，僅在 selection 或上游資料版本變更時重算。
  - 驗收：
    - `project_3635` 單選反覆切換同一 pad，inspector 耗時顯著下降。
    - 既有 `PadInfoViewModelTests` / `FreeformHelperViewModelTests` 全綠。
  - 完成（2026-02-27）：
    - 新增 revision-based inspector match cache（CAD/Regular 分流），重複單選不再重跑 `OrderBy/GroupBy/ToList` 熱路徑。
    - `ApplySelection` 新增 `selectionRevision`，deferred inspector refresh 只會套用當前選取版本，避免過期背景結果覆蓋。
    - `BumpNotchExportCad/Grid/IndexFingerprint` 會同步失效 inspector cache，確保上游資料變更後不讀到舊快取。
    - 補測：`FreeformHelperViewModelTests.PadInspectorCache`（3 cases）覆蓋 cache reuse / invalidation 行為。

- [x] **U0.5 AppLogStore 改 ring buffer（限制記憶體成長）**
  - 現況：`AppLogStore` 目前無上限，長時間執行會累積記憶體。
  - 方向：加入可設定上限（預設 5k~20k），超量自動淘汰舊行，保留 thread-safe 與 UI 綁定語義。
  - 驗收：
    - 壓力寫入後 `Entries.Count` 不超過上限。
    - console/runtime query 行為一致，無跨執行緒例外。
  - 完成（2026-02-27）：
    - `AppLogStore` 新增 ring buffer 上限（預設 `10000`），UI entries/pending queue 會自動裁切。
    - 新增 `GetTail(int)` 與 `GetTotalCount()`。
    - 補測：`AppLogStoreTests.Add_WhenExceedMaxEntries_KeepsNewestTail`、`AppLogStoreTests.Clear_RemovesAllRingEntries`。

- [x] **U0.6 runtime query terminal 改 tail API，避免全量複製**
  - 現況：`RuntimeQueryUseCase.Commands.cs` 對 terminal 讀取使用 `Entries.ToList()`。
  - 方向：新增 `AppLogStore.GetTail()`（或等價 API），直接回傳尾段 snapshot，避免全量拷貝。
  - 驗收：
    - `query terminal --tail N` 與 `query terminal-links --tail N` 不再建立全量中介集合。
    - 既有 runtime query 回傳格式不變。
  - 完成（2026-02-27）：
    - `RuntimeQueryUseCase.Commands` 的 `terminal` / `terminal-links` 改用 `AppLogStore.GetTail()`，不再 `Entries.ToList()` 全量複製。
    - 回傳 schema 維持不變（`totalLines`, `returnedLines/scannedLines`, `links`）。

- [x] **U0.7 Notch export 視窗大型資料虛擬化**
  - 現況：`NotchExportSelectionWindow.axaml` 使用巢狀 `ItemsControl + ScrollViewer`，大量 row 成本高。
  - 方向：改用可虛擬化清單（`ItemsRepeater` / `DataGrid` 或等價方案），保留分組與 preview 關聯。
  - 驗收：
    - 3635 大量 row（500+）捲動與選取流暢度改善。
    - row 選取、group 全選/全不選、preview 同步行為不退化。
  - 完成（2026-02-27）：
    - `NotchExportSelectionWindow` 左側由巢狀 `ItemsControl + ScrollViewer` 改為「IC 分組摘要列 + 單一 `ListBox`（`VirtualizingStackPanel`）」。
    - 保留 `Rows` 主清單、group 全選/全不選、row preview 同步與現有資料契約。
    - 驗證：`NotchExportSelectionViewModelTests`、`NotchExportSelectionWindowSmokeTests`、`UiLayoutGuardTests` 全綠。

- [x] **U0.8 啟動首幀再精簡：首幀只保 workspace/grid，重物件延後 idle/background**
  - 現況：雖已達 <1s budget，仍有同步初始化重物件可再後移。
  - 方向：盤點 `App/ShellViewModel` 首幀同步工作，延後非關鍵初始化。
  - 驗收：
    - `workspace.initial-grid-built` 穩定維持在 budget 內。
    - 冷啟動首幀無可感卡頓；`check-startup-budget.ps1` 持續通過。
  - 完成（2026-02-27）：
    - `App` 的 runtime IPC 啟動改為 `Opened/ApplicationIdle -> DispatcherPriority.Background`，避免與首幀競爭。
    - `FreeformHelperViewModel.EnsureInitialGrid()` 新增 in-flight guard，避免啟動路徑重複觸發 initial grid rebuild。
    - `ShellViewModel` 啟動時只更新 console summary，console 全文字串建構延後到展開時（或背景 ready），降低首幀同步負擔。
    - `FreeformHelperView` 的 AvaloniaEdit theme 載入補上 re-entrant guard，避免 console 首次展開時重複遞迴載入 style 造成閃退風險。
    - `ShellViewModel` 的 console rebuild / summary count 已改讀 `AppLogStore` ring buffer，不再依賴 UI observable collection flush；terminal 展開時即使 UI entries 尚未補齊，也會先顯示既有 log tail。
    - `FreeformHelperView` 的 console restore 會保留上一份非空 snapshot，避免 editor 剛解析完成時被暫時空字串覆蓋成空白。
    - `AvaloniaEdit` theme 已回到 `App.axaml` app-level static include；移除 workspace lazy-load，避免 terminal `TextEditor` 首輪 template/style 競態造成黑屏。
    - terminal 顯示路徑固定為 `AvaloniaEdit TextEditor` 單一路徑；保留非空 snapshot restore 與 startup smoke，避免再回到 `TextBox fallback` 或重新引入 workspace lazy-load 分叉。
    - terminal 預設展開；初始尺寸沿用 `ConsolePanelRowHeight=180`、`ConsolePanelMinHeight=120`，不再以收合狀態啟動。
    - `HeadlessUiSmokeTests` 改為檢查真實 `MainWindow + FreeformHelperView + expanded terminal` 的 render 結果；terminal 若再被改成全黑或無字，gate 會直接失敗。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~RuntimeQueryIpcTests|FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~WorkspaceInteractionStateTests|FullyQualifiedName~FreeformHelperViewModelTests.Basics"`、`./scripts/tests/check-startup-budget.ps1 -SkipBuild`（`workspace.initial-grid-built=658ms`, PASS）。

- [x] **U0.9 CAD / Regular 右鍵資訊架構正式套版（沿用 Dev prototype）**
  - 背景：Dev 頁已確認新版兩欄節奏、段落白線與短說明+tooltip 的資訊層級，需要正式落到產品右鍵，同時保留既有功能入口。
  - 驗收：
    - CAD / Regular 右鍵改為固定兩欄節奏，長文字換行且不超出容器。
    - 保留既有高頻動作與編輯入口（match / highlight / diff idx override / anchor / size / freeform）。
    - `PadInfoPopover` build 與 `ui-core/smoke` 測試不退化。
  - 完成（2026-03-01）：
    - `PadInfoPopover.axaml` 已把新版 CAD / Regular 右鍵資訊架構正式套到產品頁。
    - 新增 `padInfo*` section-divider / key-value / info-button / action-button 樣式，統一沿用 token。
    - `CadPadInfoViewModel` / `RegularPadInfoViewModel` 已補 identity、summary 與 short-reason 顯示欄位。
    - 低頻 `Geometry / Debug` 區塊已改為點擊白線段落標題直接展開/收合，不再額外放 `Show detail` 按鈕。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core`、`./scripts/tests/run-tests.ps1 -Group smoke`。

- [x] **U0.10 Notch export 視窗加入 Workspace / AA 反查與 row enable-disable 連動**
  - 背景：原本 `Select notch rows` 只有 `row -> canvas preview` 單向流程，無法從工作區選取 CAD / Regular 反查 notch row。
  - 驗收：
    - 匯出視窗保持開啟時，點主工作區 CAD / Regular 能在視窗中看到 linked row。
    - 可直接在 linked row 區塊進行 enable / disable，不必手動在左側清單搜尋。
    - `Transfer-only / All rows` 模式切換後，linked row 顯示與目前 row mode 一致。
  - 完成（2026-03-01）：
    - `NotchExportSelectionWindow` 改為 modeless 開啟；工作區選取事件會同步到 `NotchExportSelectionViewModel.ApplyWorkspaceSelection(...)`。
    - 視窗新增 `Workspace selection / AA lookup` 區塊，顯示 linked rows，並提供 `Enable linked` / `Disable linked`。
    - `Preview` 對同一列的重複點擊現在也會重新同步到 workspace selection，不再因為 `SelectedRow` 未變而無反應。
    - 右側 preview 首屏已縮成 `Key value / Effect / Pad relation`，長 payload 細節改到 `Payload / Debug` 收合段落，降低資訊噪音。
    - 新增 `NotchExportSelectionViewModelTests` 覆蓋 workspace-linked rows 與 row-mode 過濾行為。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`./scripts/tests/run-tests.ps1 -Group ui-core`、`./scripts/tests/run-tests.ps1 -Group smoke`。

### P1（高價值架構優化）
- [x] **U1.1 Notch export cache 改 revision/fingerprint，移除每次全量 signature**
  - 現況：`NotchExportGenerationCacheService` 每次 lookup 會重算 `ComputeCadSignature/ComputeGridSignature`。
  - 方向：使用可遞增 revision 或 idempotent fingerprint（由上游變更事件驅動）替代全量 hash。
  - 驗收：
    - 在資料未變更時，cache lookup 成本顯著下降。
    - cache hit/miss 語義與輸出正確性不變。
  - 完成（2026-02-27）：
    - `NotchExportGenerationCacheService` 新增 fingerprint overload：
      - `TryGet(ulong cadFingerprint, ulong gridFingerprint, ...)`
      - `Store(ulong cadFingerprint, ulong gridFingerprint, ...)`
    - `BuildNotchTableForExportAsync` 改走 revision fingerprint 路徑，不再每次 lookup 全量掃 `cad/grid` 計算 signature。
    - `FreeformHelperViewModel` 新增 `cad/grid/index` fingerprint revision，於 layer filter、grid rebuild、Step1/Step2 變更時遞增，確保 cache key 正確失效。
    - 補測：`NotchExportGenerationCacheServiceTests.TryGet_WithFingerprintOverload_HitsWithoutCadGridSignatureScanPath`。

- [x] **U1.2 NotchTableGenerator 重複昂貴流程清理 + DI 一致化**
  - 現況：存在重複 `BuildAllocations` 路徑與未使用/臨時 `new`（service 注入不一致）。
  - 方向：
    - 統一 service 注入與生命週期。
    - 抽出 v2.2 candidate memoization，避免重複計算。
  - 驗收：
    - `NotchTableGeneratorTests`、`NotchV22CompensationServiceTests` 全綠。
    - 3635 export 時間下降且輸出內容一致。
  - 完成（2026-02-27）：
    - `NotchTableGenerator` 補齊 v2.2 compensation service 注入一致化（constructor path 全部收斂）。
    - 新增 CAD allocation memoization：同一 `RegularGrid` 下對同一 CAD pad 的 allocation 計算可重用，避免 `Generate`/`Evaluate` 重複全算。
    - `GenerateCadAllocationCompatible` 新增 v2.2 candidate per-CAD memoization，避免同 CAD 在同輪生成重複建候選。
    - 驗證：`NotchTableGeneratorTests` + `NotchV22CompensationServiceTests` 通過。

- [x] **U1.3 Shell console 文字緩衝重構（避免 O(N) 累積拷貝）**
  - 現況：`ShellViewModel` append 仍有 `new StringBuilder(ConsoleText)`，長時間輸出成本高。
  - 方向：改為行緩衝集合（tail buffer）+ 按需渲染字串，不在每次 append 重建整段。
  - 驗收：
    - 高頻 log 下 CPU/GC 壓力下降。
    - 去重、搜尋、filter 與摘要計數行為一致。
  - 完成（2026-02-27）：
    - `ShellViewModel` 改用持久化 `_consoleTextBuffer`，append 路徑移除 `new StringBuilder(ConsoleText)`。
    - reset/rebuild/dedup 流程同步改為共用 buffer，摘要計數與原行為一致。

## 本輪效能優化（2026-02-27，行為等價）
- [x] **P5.1 PadCanvas CAD 索引化（Selection/HitTest）**
  - 目標：`TryHitCad`、框選 CAD 改為空間索引查詢，避免每次線性掃描全部 CAD。
  - 風險與防護：保留線性 fallback；命中判定仍維持「`Bounds + Polygon.Contains + 最小面積優先`」。
  - 驗證：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~PadCanvasHitTestTests|FullyQualifiedName~PadCanvasCacheInvalidationTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 完成（2026-02-27）：
    - 新增 `CadSpatialIndex`，並在 `PadCanvas` 的 CAD data invalidation 時同步清除索引快取。
    - `PadCanvasSelectionEngine.TryHitCad` 與 `SelectPadsInWorldRect`（CAD path）改為先走索引查詢，再線性 fallback。
    - 補強 `PadCanvasCacheInvalidationTests`：`RegularPads` 變更保留 CAD index、`CadPads` 變更會清除 CAD index。

- [x] **P5.2 Notch allocation regular 候選索引（取代 CAD x Regular 全掃）**
  - 目標：`NotchAllocationService`/`NotchV22CompensationService StageA` 改用 regular 候選查詢，避免每顆 CAD 全掃 `grid.Pads`。
  - 風險與防護：候選只做粗篩，`IntersectionAreaWithRect` 精算維持原規則；保留必要 fallback。
  - 驗證：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~NotchV22CompensationServiceTests|FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~RegularGridCandidateQueryTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 完成（2026-02-27）：
    - 新增 `RegularGridCandidateQuery`（以 `grid.XEdges/YEdges` 先縮小 row/col 候選區，再用 `Bounds.Intersects` 精篩）。
    - `NotchAllocationService.BuildAllocations` 改為使用候選 query，而非全掃 `grid.Pads`。
    - `NotchV22CompensationService.RunStageACollectOverlaps` 改為使用候選 query，維持 `IntersectionAreaWithRect` 精算與既有輸出語義。
    - 新增 `RegularGridCandidateQueryTests` 驗證候選查詢與線性掃描等價。

- [x] **P5.3 ToFull owner 判定索引/快取化（副作用受控）**
  - 目標：`StageBBoundaryContext.GetStrictOwnerCadPads` 避免每 regular 全掃 `allCadPads`。
  - 風險與防護：以 deterministic 排序、cache key（regular index + strict ratio）與 fallback 確保結果一致。
  - 驗證：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~NotchV22CompensationServiceTests|FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~WorkflowPipelineServiceTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 完成（2026-02-27）：
    - 新增 `CadBoundsSpatialIndex`，`StageBBoundaryContext` 在 CAD 數量大於門檻時啟用索引查詢候選。
    - `GetStrictOwnerCadPads` 改為：`候選 CAD -> IntersectionAreaWithRect 精算 -> 依 pad.Id 穩定排序`，維持輸出順序與語義。
    - 新增 `Compute_ToFullEnabled_WithManyCadPads_KeepsMultiOwnerGate` 測試，覆蓋高數量 CAD 下的 multi-owner gate 正確性。

- [x] **P5.4 Visible draw list 由 viewport 查詢取代 CAD 全表迭代**
  - 目標：`PadCanvas.VisibleDrawListBuilder` 低細節/正常模式都改為 viewport 候選查詢，降低每幀掃描成本。
  - 風險與防護：保留 decimation 與 highlight/selection 視覺優先序，不改既有顯示語義。
  - 驗證：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~PadCanvasHitTestTests|FullyQualifiedName~PadCanvasCacheInvalidationTests|FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~UiLayoutGuardTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 完成（2026-02-27）：
    - `PadCanvas.Render` 在顯示 CAD 時先確保 `CadSpatialIndex` 存在，避免 draw-list 查詢回退到全表。
    - `PadCanvasVisibleDrawListBuilder` 改為先取 `worldViewport` 內 CAD 候選，再做低細節 decimation 與選取/高亮分類。
    - `UiLayoutGuardTests` shortcut 契約同步到 `FreeformHelperView.InputAndShortcuts.cs`，並新增 draw-list 索引查詢契約字串檢查。

- [x] **P5.5 Step4 visible diff idx assignment 改為 IC-row sequence + manual segment lock**
  - 目標：避免 visible DXF diff idx 指派只靠單點 best match，導致同一 IC row 中段跳號後整段後移。
  - 規則：
    - 預設以 `IC + row` 為 sequence 單位做單調指派。
    - manual override 為硬約束，會把該 row 切成 segment；只重排該段，不波及其他 row/IC。
    - per-IC anchor 若 strict seed 有效，會作為固定點參與 sequence，而非僅做視覺提示。
  - 驗收：
    - 同一 row 中有 manual override 時，前後段會維持單調且不跨段漂移。
    - anchor 落在有效 row 內時會被實際採用；無效時才記錄 ignored。
    - `application` / `ui-core` 測試全綠。
  - 完成（2026-03-01）：
    - 新增 `DxfVisibleIndexAssignmentService`，將 Step4 visible diff idx 指派抽成單一服務入口。
    - `RebuildVisibleDxfIndexMap` 改為透過 service 執行 `override > IC-row sequence > best-match`。
    - `DxfIndexAssignmentSummary` 與 Inspector 文案同步為 `IC-row sequence` 語意。
    - 補測：`DxfVisibleIndexAssignmentServiceTests` 覆蓋 manual override 切段與 anchor 套用案例。

## 本輪 P1 品質落地（2026-02-27）
- [x] **Q4.5 減少 tests analyzer warning 基線（CA1861 / CA1869 / CA1512）**
  - 背景：`build` 與 `ui-core/smoke` 雖可通過，但 tests 專案仍有大量 analyzer warning，會稀釋真正的新問題。
  - 目前狀態（2026-03-04）：
    - 已先修復 `IMPORTS` / `IDE0005` lint 基線回歸，`./scripts/tests/lint.ps1 -AllFiles` 已回到 pass。
    - 第一批 `CA1861` 熱點清理：`ui-core` warning 統計由 `CA1861=108` 降到 `CA1861=19`。
  - 完成（2026-03-04）：
    - 第二批清理已收斂剩餘 `CA1861 / CA1859 / CA1869 / CA1512 / CA1845 / IDE0060`，`build/logs/ui-core-analyzer-batch2.log` 統計全部為 `0`。
    - `./scripts/tests/run-tests.ps1 -Group ui-core` 與 `./scripts/tests/lint.ps1 -AllFiles` 均已通過。
  - 方向：
    - 先從高頻 warning 收斂：`CA1861`（重複常數陣列）、`CA1869`（重複 `JsonSerializerOptions`）、`CA1512`。
    - 只做行為等價修正，不放寬規則掩蓋問題。
  - 驗收：
    - `./scripts/tests/lint.ps1 -AllFiles`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - tests analyzer warning 數明顯下降，且不影響既有測試行為。

- [x] **Q4.6 同步 UI visual baseline（`ConsolePanel.axaml` / `PadInfoPopover.axaml`）**
  - 背景：在 `Q4.5` 聚焦測試過程中，`UiVisualSnapshotTests.CriticalUiFiles_MatchMinimalVisualBaseline` 顯示目前 baseline 與實際 UI 檔案已漂移。
  - 目前觀察（2026-03-04）：
    - `src/FreeformHelper.UI/Views/ConsolePanel.axaml`
    - `src/FreeformHelper.UI/Views/PadInfoPopover.axaml`
  - 完成（2026-03-04）：
    - 已確認這兩個檔案的漂移來自已落地的預期 UI 變更：
      - `ConsolePanel.axaml`：terminal 固定回 `TextEditor` 路徑後的 XAML
      - `PadInfoPopover.axaml`：新版 CAD / Regular 右鍵資訊架構正式套版
    - `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json` 已同步新 hash。
    - 驗證：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`、`dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --no-build --nologo --filter "FullyQualifiedName~UiVisualSnapshotTests"`
  - 方向：
    - 確認這兩個檔案的 UI 變更屬於預期行為。
    - 若預期一致，更新 `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json`。
    - 若非預期，回頭找出真正的 UI 漂移來源並修正產品檔。
  - 驗收：
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --no-build --nologo --filter "FullyQualifiedName~UiVisualSnapshotTests"`

- [x] **Q4.7 Headless AvaloniaEdit / 字型 bootstrap 穩定化**
  - 背景：`ui-core` 的 `HeadlessUiSmokeTests` 目前可重現 `fonts:SystemFonts` 缺失；根因懷疑是 headless test host 移除了 app-level AvaloniaEdit theme。
  - 方向：
    - 移除 headless 專用 `FREEFORMHELPER_HEADLESS_DISABLE_AVALONIAEDIT_THEME` 特判。
    - 補一條 smoke / startup path 測試，明確要求 headless app 也要保有 `AvaloniaEdit` theme。
  - 驗收：
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`
    - `./scripts/tests/run-refactor-gate.ps1`
  - 完成（2026-03-05）：
    - `AvaloniaTestApp` 與 `Program.BuildAvaloniaApp()` 現在都明確設定 `Inter` 為 system font source + default family，避免 headless 路徑再落回 `$Default/SystemFonts`。
    - 新增 `AppFontBootstrapper`，集中 `FontManagerOptions` 與 `WithSystemFontSource` 契約。
    - 新增 `HeadlessAppBootstrap`，在 smoke / snapshot 測試前確保 `App.Initialize()` 已完成，避免 headless session 只建立部分 app 狀態。
    - `HeadlessUiSmokeTests`、`TerminalStartupPathTests`、`UiRenderedVisualSnapshotTests`、`NotchExportSelectionWindowSmokeTests` 已改用共用 bootstrap。
    - 所有 `AvaloniaFact` headless UI 類別已收斂到 `HeadlessUiSerial` collection，避免 `FontManager` / headless session 在 `ui-core` 併跑時互踩。

## 下一輪主線收斂（2026-03-04）
- [x] **R7.1 TODO / 文件同步到 `master + beta-0.1` 基線**
  - 背景：目前 `TODO.md`、部分說明仍殘留舊分支上下文；若不先同步，後續優化會造成文件與主線狀態漂移。
  - 範圍：
    - `TODO.md`
    - `docs/reference/behavior-inventory.md`
    - `docs/guides/settings-entry-matrix.md`
  - 完成（2026-03-04）：
    - `behavior-inventory`、`settings-entry-matrix` 已同步 `master / beta-0.1` 基線。
    - 已補 terminal 固定契約（`TextEditor-only`、app-level theme include、預設展開）與 app settings deferred / `Ctrl+S` top toast 契約。
    - `git grep "refactor-code-reduction-pass1"` 目前只剩 `docs/guides/repo-refactor-scan-2026-02-26.md` 與 `TODO.md` 內的歷史/完成紀錄。
  - 驗收：
    - 文件中的 branch / baseline / terminal contract 與 `master`、`beta-0.1` 一致。
    - `git grep "refactor-code-reduction-pass1"` 只剩歷史完成紀錄，不再出現在目前執行基線描述。

- [x] **R7.2 `PadInfoPopover` 再拆分，降低正式右鍵維護成本**
  - 背景：`src/FreeformHelper.UI/Views/PadInfoPopover.axaml` 仍為 `700+` 行熱點；新版資訊架構已穩定，應拆成 section view 以降低後續調整風險。
  - 方向：
    - 保留現有行為與資料契約。
    - 依 `CAD summary / actions / edit tools / geometry-debug`、`Regular summary / actions / edit tools / geometry-debug` 拆 view。
  - 完成（2026-03-04）：
    - 新增 `src/FreeformHelper.UI/Views/PadInfoSections/CadPadInfoContentView.axaml`
    - 新增 `src/FreeformHelper.UI/Views/PadInfoSections/RegularPadInfoContentView.axaml`
    - `PadInfoPopover.axaml` 主檔已縮減至 shell + DataTemplate routing + confirm footer（`733 -> 42` 行）。
    - 保留原有 binding/command 與 `PadInfoPopover.axaml.cs` close-confirm 行為，不新增第二條工作流入口。
  - 驗收：
    - `PadInfoPopover.axaml` 主檔顯著縮短。
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`

- [x] **R7.3 `RightWorkflowPanel` 分段拆 view，避免 settings / step UI 再回到巨型 XAML**
  - 背景：`src/FreeformHelper.UI/Views/RightWorkflowPanel.axaml` 仍接近 `800` 行，是下一個高風險 UI 熱點。
  - 方向：
    - 以 step section / inspector shell 為單位拆分，不改使用者可見行為。
    - 維持 token 與既有樣式，不在 view 中新增硬編碼。
  - 完成（2026-03-04）：
    - 新增 `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowInspectorView.axaml`
    - 新增 `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml`
    - `RightWorkflowPanel.axaml` 主檔已縮成 shell + shared resources（`796 -> 72` 行）。
    - 原本的 `OpenSettingsRequested` 路由與 step 展開後 `BringIntoView()` 邏輯已搬到 `RightWorkflowSettingsTabView`，使用者可見行為維持不變。
  - 驗收：
    - `RightWorkflowPanel.axaml` 主檔顯著縮短。
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`

- [x] **R7.4 `PadInspector` 主流程再瘦身，收斂到 snapshot builder / formatter / action entry**
  - 背景：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.cs` 仍為 `750+` 行熱點，後續再加 rule/summary 容易失控。
  - 方向：
    - 將 snapshot build、文字格式化、action entry 再拆成單一責任 partial / service。
    - 維持 `selection revision cache` 與目前 inspector 行為不變。
  - 完成（2026-03-05）：
    - 新增 `FreeformHelperViewModel.PadInspector.Formatting.cs`
    - 新增 `FreeformHelperViewModel.PadInspector.Snapshots.cs`
    - 新增 `FreeformHelperViewModel.PadInspector.Trace.cs`
    - `FreeformHelperViewModel.PadInspector.cs` 主檔已收斂為高階 snapshot entry（`757 -> 216` 行），格式化/trace/notch-row helper 已拆出，不改既有輸出與 cache 行為。
  - 驗收：
    - `PadInspector.cs` 主檔顯著縮短。
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`

- [x] **R7.5 固定 `project_3635` 性能回歸門檻（selection / inspector / export）**
  - 背景：目前已有 startup 與 regression 腳本，但 selection / inspector / export 還缺固定門檻，後續優化很難判斷是否退化。
  - 方向：
    - 以 `project_3635.json` 建立 selection latency、inspector refresh、CSV/TXT export 的固定採樣腳本或報告輸出。
    - 先定義量測與門檻，不在同一步混入大規模演算法改寫。
  - 完成（2026-03-05）：
    - `RuntimeQueryResponseBuilder.BuildSelectionPayload(...)` 已補 `timings(summary/inspector/notchPreview/total)`；`query selection` / `query select-cad` 現在可直接取樣 selection pipeline 耗時，不再依賴 Debug log。
    - `query export-notch` 已補 `elapsedMs`，可直接納入 3635 匯出門檻。
    - 新增 `docs/performance/regression-baseline-3635.budget.json` 固定 budget；`scripts/perf/run-3635-regression-baseline.ps1` 會輸出 `gate` 結果，並支援 `-EnforceBudget`。
    - 腳本新增 `-SkipNotchValidation`，讓 selection / inspector / export perf gate 可與 Step5 validation query 解耦。
    - 實跑驗證：`./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -SelectionCycles 2 -OutDir build/perf/3635-regression-verify -SkipNotchValidation -EnforceBudget`，summary 顯示 `gate.pass=true`，本次樣本為 `selection.total.p95=6ms`、`selection.inspector.p95=0ms`、`export.csv.elapsed=9682ms`、`export.txt.elapsed=80ms`。
  - 驗收：
    - `scripts/perf` 或 `docs/performance` 新增對應基線。
    - 可重複執行並比較前後差異。

- [x] **P1.1 測試命名規則策略化（CA1707 僅 tests 放寬）**
  - 目標：保留測試方法底線命名可讀性，避免 CA1707 佔滿 analyzer 噪音。
  - 變更：
    - `.editorconfig` 新增 `[tests/**/*.cs]`，`dotnet_diagnostic.CA1707.severity = none`。
  - 驗證：
    - `./scripts/tests/lint.ps1 -AllFiles`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - 結果：
    - `build` / `ui-core tests` 通過。
    - `lint -AllFiles` 仍受既有基線問題阻擋（非本次變更引入），目前主要為 `ENDOFLINE` 與大量既有 analyzer/doc 警告。

- [x] **P1.2 啟用 GenerateDocumentationFile 並移除 IDE0005 前置噪音**
  - 目標：消除 `EnableGenerateDocumentationFile` 提示，讓 lint 輸出更聚焦。
  - 變更：
    - `Directory.Build.props` 啟用 `GenerateDocumentationFile=true`。
    - 以 `NoWarn += 1591` 避免導入 XML 註解覆蓋率噪音。
  - 驗證：
    - `./scripts/tests/lint.ps1 -AllFiles`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - 結果：
    - `EnableGenerateDocumentationFile` 提示已移除。
    - `lint -AllFiles` 未全綠原因同上（既有基線待後續清理）。

## 本輪新增（2026-02-26 夜間）
- [x] **S1.9 移除啟動畫面 loading overlay（啟動已達可直接進 workspace）**
  - 變更：
    - `MainWindow` 移除 startup overlay 視覺與相關 timer/event 控制邏輯。
    - `App` 啟動流程改為不等待 overlay，直接在 shell ready 後啟動 runtime IPC。
    - `FreeformHelperView` deferred hook 改為單純打點 `workspace.startup-ready-signal`。
    - 清除 overlay 專用 token（`StartupOverlay*`）。
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~PadCanvasCacheInvalidationTests|FullyQualifiedName~PadCanvasHitTestTests|FullyQualifiedName~FreeformHelperViewModelTests|FullyQualifiedName~IndexMappingReportViewModelTests|FullyQualifiedName~IndexMappingSettingsTests|FullyQualifiedName~NotchExportSelectionViewModelTests|FullyQualifiedName~PadInfoViewModelTests|FullyQualifiedName~WorkspaceInteractionStateTests"`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~WorkflowPipelineServiceTests"`

- [x] **R6.1 巨型檔第三輪拆分（P0）**
  - 熱點（>1000 行）：`FreeformHelperViewModel.Operations.cs`、`FreeformHelperViewModel.State.cs`、`FreeformHelperViewModel.Settings.cs`、`PadCanvas.Rendering.cs`、`NotchV22CompensationService.cs`。
  - 方向：延續 single-entry，拆分為流程/狀態/格式化子模組，單檔控制在 < 800 行。
  - 完成（2026-02-27）：
    - `FreeformHelperViewModel.Operations` 新增 `FreeformHelperViewModel.Operations.LayerSelection.cs`，主檔降為 `462` 行。
    - `FreeformHelperViewModel.State` 新增 `FreeformHelperViewModel.State.CanvasAndMapping.cs`，主檔降為 `794` 行。
    - `FreeformHelperViewModel.Settings` 新增 `FreeformHelperViewModel.Settings.DirtyTracking.cs`，主檔降為 `727` 行。
    - `PadCanvas.Rendering` 新增 `PadCanvas.Rendering.NotchPreview.cs`，主檔降為 `792` 行。
    - 既有 `NotchV22CompensationService.Geometry.cs` 保持，`NotchV22CompensationService.cs` 維持 `470` 行。

- [x] **R6.2 View code-behind 減重（P1）**
  - 熱點：`FreeformHelperView.axaml.cs`（966 行）、`FreeformHelperView.Console.cs`（915 行）。
  - 方向：抽離 shortcut/router、console attach/sync、window/shell hook 為獨立 service 或 partial 子域。
  - 完成（2026-02-27）：
    - 新增 `FreeformHelperView.InputAndShortcuts.cs`、`FreeformHelperView.ConsoleLinks.cs`。
    - 行數：`FreeformHelperView.axaml.cs` `631` 行、`FreeformHelperView.Console.cs` `597` 行。

- [x] **R6.3 ManualSizingService 職責切分（P1）**
  - 熱點：`ManualSizingService.cs`（901 行）。
  - 方向：切成計算核心（純算法）+ UI 互動封裝（指令/狀態），補單元測試守門。
  - 完成（2026-02-27）：
    - 新增 `ManualSizingService.Parsing.cs`，主檔降為 `713` 行，保留既有入口與行為。

- [x] **R6.4 NotchDetails 同步阻塞點清理（P1）**
  - 熱點：`FreeformHelperViewModel.NotchDetails.cs` 仍有 `Task.Result` 使用（line 84/232/244）。
  - 方向：改為 async 管線或快取結果快照，避免 UI 執行緒潛在阻塞。
  - 完成（2026-02-26）：
    - `Notch compensation cache` tuple 欄位由 `Result` 更名為 `Compensation`，移除 `entry.Result` 命名歧義，避免把快取欄位誤判為 `Task.Result` 同步阻塞。
    - 對應更新 `FreeformHelperViewModel.Core.cs`、`FreeformHelperViewModel.NotchDetails.cs`。

- [x] **R6.5 全專案 lint 基線重整（P0）**
  - 現況：`lint.ps1 -AllFiles` 會被行尾/using 排序（`ENDOFLINE`/`IMPORTS`）噴大量錯誤，且易受 apphost 鎖檔影響。
  - 方向：
    - 專案統一 CRLF/using 順序（先清 `src`，再清 `tests`）。
    - lint/build script 增加 `UseAppHost=false` 選項，避開外部程序鎖定 `FreeformHelper.UI.exe`。
  - 完成（2026-02-27）：
    - 已新增 `UseNoAppHost` 開關：
      - `scripts/tests/lint.ps1`
      - `scripts/tests/run-tests.ps1`
      - `scripts/tests/run-refactor-gate.ps1`
    - `lint.ps1` 已補 `dotnet` 子命令 exit-code 檢查，避免假成功。
    - `CRLF/IMPORTS` 全量清債完成，`./scripts/tests/run-refactor-gate.ps1 -Configuration Debug -UseNoAppHost -LintAllFiles` 已通過。

## 本輪需求同步（2026-02-26）
- [x] **N0.1 左上 Settings 改為真正分頁**
  - 僅調整左上 `SettingsWindow`（General/Step1..Step5 單頁切換），右側工作流面板不變。
  - 分頁切換時自動回到內容頂部，避免舊版折疊式長頁的閱讀負擔。

- [x] **N0.2 Select notch rows to export 重排資訊層級**
  - 匯出選取視窗改為「高頻重點在上、細節在下」：`Values` 與結果摘要置頂、長描述後置。
  - 新增視窗內 `Export type` 可切換，避免選錯類型需要整段重來。

- [x] **N0.3 Export image 解析度修正**
  - 最終輸出尺寸改為 `A + 2B`（`width/height + padding*2`），並於 UI 顯示即時輸出解析度文字。

- [x] **N0.4 Recompute / compensation 觸發時機說明文件化**
  - 需要補一段對齊目前行為的說明（何時自動算、何時按 Recompute、哪些切換只影響 overlay）。
  - 完成：
    - 已在 `docs/reference/behavior-inventory.md` 新增「Step3 Recompute / Compensation 觸發時機」段落，明確區分：
      - 自動重算觸發
      - 顯示開關（只影響 overlay）
      - Recompute 按鈕的實際用途
      - 對應程式入口檔案

- [x] **N0.5 App settings 寫入時機調整（load project 後延遲）**
  - 規則：載入 project 後，設定變更先暫存，不即時寫入 app settings；改為下一次 `Save Project` 成功時一次 flush。
  - 已實作 deferred persistence，避免測試或工作流中途覆蓋 app-level 預設。

- [x] **N0.6 Save project 快捷鍵 + 上方提示**
  - 新增 `Ctrl+S` 全域快捷鍵（非文字輸入焦點），觸發 `SaveProjectAsync()`。
  - 儲存完成後顯示上方 toast（成功/失敗/取消提示）。

## 本輪掃描新增（2026-02-26）
- [x] **S1.1 Selection/Inspector 延遲第二輪優化（P0）**
  - 目標：降低框選起手延遲；selection summary 與 inspector/notch 計算分級，拖曳期間避免重計。
  - 入口：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.cs`
  - 驗證：`project_3635.json` 框選 + log `Selection updated`（inspector/total ms 降低）。
  - 完成：
    - CAD/REG 單選改為「快速快照 + 背景補全」：同步路徑只保留摘要，完整 rule trace/notch 細節延後刷新。
    - 新增 REG deferred inspector refresh 管線，與 CAD deferred refresh 共用 pending 狀態旗標。
    - match details 加入行數上限，避免大量匹配時產生超長字串造成 UI 卡頓。

- [x] **S1.2 RuntimeQuery IPC 關閉流程去阻塞（P0）**
  - 目標：移除 `RuntimeQueryIpcServer.Dispose()` 內 `_runLoopTask?.Wait(...)` 的同步等待，改 async stop。
  - 入口：`src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`
  - 驗證：啟動/關閉 App 不出現 close hang，IPC stop log 正常。
  - 完成：
    - `RuntimeQueryIpcHost` 新增 `StopAsync()`，`Stop()` 改為 fire-and-forget async stop。
    - `RuntimeQueryIpcServer` 改為 `IAsyncDisposable`，移除同步 `_runLoopTask?.Wait(...)`，改為 `await run loop` 的非阻塞關閉流程。
    - `App` exit hook 改為呼叫 `RuntimeQueryIpcHost.StopAsync()`，避免 UI thread 同步卡住。
    - 新增 `RuntimeQueryIpcTests` 覆蓋 host 啟停與 stop timeout 契約。

- [x] **S1.3 CA1822/CA1865 清債（P0）**
  - 目標：清除目前 `lint -AllFiles` 的 `CA1822`（72）與 `CA1865`（2）warning。
  - 入口：`src/FreeformHelper.UI/Services/*.cs`、`src/FreeformHelper.Application/**/*.cs`
  - 驗證：`./scripts/tests/lint.ps1 -AllFiles` warning 數下降且行為不變。
  - 完成：
    - 已對 `CA1822/CA1865` 套用 analyzer code fix，並補齊剩餘手動修正。
    - `CA1865` 改為 `StartsWith(char)`。
    - `CA1822` 針對純函式 helper 改為 static；對需保留 instance API 的 service 類別補上 `SuppressMessage` 與理由。
    - 最新 analyzer build 統計：`CA1822=0`、`CA1865=0`（以 `build/tmp/analyzer-build-after-s13d.log` 驗證）。

- [x] **S1.4 Lint gate 擴大到 solution（P1）**
  - 目標：新增可選「全解決方案 analyzer gate」，避免只檢查 UI 專案造成品質債漂移。
  - 入口：`scripts/tests/lint.ps1`、`scripts/tests/run-refactor-gate.ps1`
  - 驗證：新增命令可跑完整 solution analyzer，並可被 CI/本地採用。
  - 完成：
    - `lint.ps1` 新增 `-AnalyzerScope UiProject|Solution`（預設 `UiProject`，可選 `Solution`）。
    - `run-refactor-gate.ps1` 新增 `-LintAllFiles` 與 `-LintAnalyzerScope`，可直接切換為全 solution analyzer gate。
    - `scripts/README.md` 與 `tests/README.md` 已補上 full solution lint/gate 命令範例，方便本地與 CI 採用。

- [x] **S1.5 超大檔再拆分（P1）**
  - 目標：持續降低 >1000 行檔案風險（`RuntimeQueryUseCase`、`FreeformHelperViewModel.Settings/State/Operations`、`PadCanvas.Rendering`）。
  - 原則：single-entry 不變、行為不變、每輪拆一個子域並附回歸測試。
  - 完成：
    - `RuntimeQueryUseCase` 完成第二輪拆分：入口/路由保留在 `RuntimeQueryUseCase.cs`，命令實作拆到 `RuntimeQueryUseCase.Commands.cs` 與 `RuntimeQueryUseCase.NotchDetails.cs`。
    - 類別改為 `partial`，維持單一對外 entry（`ExecuteAsync`）與既有 command router 行為不變。
    - 拆分後檔案行數：`RuntimeQueryUseCase.cs` 261 行、`RuntimeQueryUseCase.Commands.cs` 639 行、`RuntimeQueryUseCase.NotchDetails.cs` 512 行（均 < 1000 行）。

- [x] **S1.6 UI 文字溢出守門測試（P1）**
  - 目標：關鍵面板（Settings/Inspector/Notch export）文字必須換行，不超出容器。
  - 驗證：新增/擴充 UI layout guard 測試（headless snapshot 或契約測試）。
  - 完成：
    - `UiLayoutGuardTests` 新增 `CriticalDynamicTextBindings_UseWrapOrTrimmingContract`，鎖定 Settings/Inspector/Notch export 關鍵 dynamic text binding 必須具備 wrap/trim/stepNote 契約。
    - 修正高風險欄位：`SettingsGeneralSectionView` 的 `Description/Summary` 與 `NotchExportSelectionWindow` 的 `SummaryText` 已補 `TextWrapping`，避免超框。
    - 透過 UI snapshot/layout guard 測試群組持續守門，避免後續改版再發生文字超出 container 的回歸。

- [x] **S1.7 啟動路徑優化（P0）**
  - 目標：優先顯示 workspace/grid，將非關鍵初始化後移，並讓 loading 進度更平滑可預期。
  - 完成：
    - `ShellViewModel` 啟動由 `Opened + immediate bootstrap` 執行，移除額外 background dispatch 延遲。
    - `workspace startup overlay` 改為先發出 hide request，再延後執行 console 初始同步。
    - overlay hide 條件新增 workspace data ready 檢查，避免提早隱藏導致透明/空白感。
    - loading progress 改為平滑遞增（deterministic progress），並在 hide 前推到 100%。
    - `StartupOverlayMinVisibleMs` 由 `1200` 下修為 `450`，降低冷啟動等待時間。
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `scripts/tests/run-tests.ps1 -Group ui-core`
    - `scripts/tests/run-tests.ps1 -Group smoke`

- [x] **S1.8 啟動 <1s（P0）最後一哩**
  - 目標：冷啟動 1 秒內可見 grid（以 `workspace.initial-grid-built` marker 驗證）。
  - 完成：
    - `RuntimeQueryIpcHost.Start(...)` 已改為在 `MainWindow` 發出 `StartupOverlayHidden` 事件後才啟動，避免首屏競爭。
    - `AvaloniaEdit` theme 載入改為 console 展開時才初始化；預設 console 由展開改為收合，首屏不再同步載入 theme。
    - 新增 startup budget gate：
      - `scripts/perf/analyze-startup-markers.ps1` 支援 `-InitialGridBudgetMs` + `-FailOnBudgetViolation`
      - `scripts/tests/check-startup-budget.ps1` 一鍵執行量測 + budget 驗證
      - `run-refactor-gate.ps1` 支援 `-IncludeStartupBudget`
    - 最新量測：`workspace.initial-grid-built=692ms`（PASS, budget=1000ms）。

## P0（先做，維持行為不變）
- [x] **Q0.1 導入 linter gate（必做）**
  - 目標：repo 必須有可執行 lint，避免風格與品質持續漂移。
  - 目前現況：無 root `.editorconfig`，`Directory.Build.props` 未啟用 analyzer/lint gate。
  - 任務：
    - 新增 root `.editorconfig`（C# + XAML 基本規範、行寬、命名、空白、using 排序）。
    - 在 `Directory.Build.props` 啟用 `EnableNETAnalyzers`、`AnalysisLevel`、`EnforceCodeStyleInBuild`。
    - 新增 `scripts/tests/lint.ps1`（`dotnet format --verify-no-changes` + analyzer build）。
    - 更新 `scripts/README.md` 與 `tests/README.md` 的 lint 操作說明。
  - 驗證：
    - `./scripts/tests/lint.ps1`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
  - 完成：
    - 已新增 root `.editorconfig`、`Directory.Build.props` analyzer gate、`scripts/tests/lint.ps1`。
    - `lint.ps1` 預設檢查本次變更檔；可用 `-AllFiles` 做全量掃描。

- [x] **Q0.2 清除已失效 Home 模組（減法）**
  - 依據：`HomeView`/`HomeViewModel` 存在，但主視圖已無 Home 分頁路徑。
  - 涉及檔案：
    - `src/FreeformHelper.UI/Views/HomeView.axaml`
    - `src/FreeformHelper.UI/Views/HomeView.axaml.cs`
    - `src/FreeformHelper.UI/ViewModels/HomeViewModel.cs`
    - `src/FreeformHelper.UI/Views/HowToUseView.axaml`（仍提到 Home，需同步文案）
  - 驗證：
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `scripts/tests/run-tests.ps1 -Group ui-core`
  - 完成：
    - 已刪除 `HomeView` / `HomeViewModel` 殘留檔案。
    - `HowToUseView.axaml` 已同步為目前分頁：`Freeform Helper / How To Use / Dev`。

- [x] **Q0.3 完成 `M1` 驗收（Regular layer 跳窗）**
  - 依據：目前 TODO 尚有唯一未驗收項。
  - 驗證案例：
    - `example/BOE36.35/project_3635.json`
    - 切換 `Regular source` / `Regular layer`，檢查無異常跳窗、無超框、寬度固定。
  - 驗證輸出：
    - `build/logs/app.log` 需可追蹤 `Open settings window source/section`。
  - 完成：
    - `regularLayerSelector` Popup 已固定寬度並加入 `PlacementConstraintAdjustment=All`，避免下拉超框。
    - 新增 `UiLayoutGuardTests` 鎖定 regular layer selector 契約與 settings source/section log 格式。
    - 既有 `build/logs/app.log` 已可追蹤 `Open settings window requested: source=..., section=...`。

## P1（架構重構：流程與寫法）
- [x] **Q1.1 拆分 `RuntimeQueryUseCase`（超大檔）**
  - 熱點：`src/FreeformHelper.UI/Services/RuntimeQueryUseCase.cs`（~1962 行）。
  - 問題：命令解析、參數驗證、回應組裝、快取策略全部耦合。
  - 重構方向：
    - `RuntimeQueryCommandRouter`
    - `RuntimeQueryArgumentParser`
    - `RuntimeQueryResponseBuilder`
    - `RuntimeQueryNotchCacheService`
  - 要求：CLI 行為與輸出 JSON 形狀保持不變。
  - 完成：
    - `RuntimeQueryUseCase` 已委派至 `RuntimeQueryCommandRouter`，保留單一對外入口。
    - 參數解析已抽離至 `RuntimeQueryArgumentParser`。
    - 回應 payload 組裝已抽離至 `RuntimeQueryResponseBuilder`。
    - notch query cache 與 metrics 已抽離至 `RuntimeQueryNotchCacheService`。

- [x] **Q1.2 拆分 `FreeformHelperViewModel.Operations`（超大檔）**
  - 熱點：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs`（~1804 行）。
  - 問題：DXF 載入、layer catalog、grid/build、workflow side-effects 混在同一檔。
  - 重構方向（single-entry）：
    - `CadLoadWorkflowService`
    - `GridRebuildOrchestrator`
    - `LayerCatalogStateService`
  - 要求：`TriggerGridRebuildAsync` 對外入口維持單一。
  - 完成：
    - 新增 `CadLoadWorkflowService`，將 Open DXF 工作流從 ViewModel 操作檔抽離。
    - 新增 `GridRebuildOrchestrator`，集中 regular source/grid alignment 的建構決策。
    - 新增 `LayerCatalogStateService`，集中 DXF layer catalog cache 與載入策略。
    - `FreeformHelperViewModel.Operations.cs` 已改為委派 service，並降到約 1296 行。

- [x] **Q1.3 設定變更 side-effects 集中化**
  - 熱點：
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs`
  - 問題：`MarkUnsaved` / `InvalidateDownstreamFromStepX` / `TriggerGridRebuildAsync` 分散多入口。
  - 重構方向：
    - 建立 `SettingsChangePolicy`（輸入：變更種類；輸出：統一 side-effects）。
  - 要求：行為不變、減少重複 if/流程分支。
  - 完成：
    - 新增 `SettingsChangePolicy` 與 `SettingsChangeKind`/`SettingsChangeEffects`。
    - 新增 `FreeformHelperViewModel.SettingsPolicy` 共用套用入口，統一執行 invalidate/rebuild/dirty/persist。
    - `Settings.cs` 的 Step2/Step3/Step4、grid rebuild、recalc bounds 等重複路徑已改用政策入口。
    - `SettingsWindow.cs` 套用同一政策，避免另寫一套 side-effects 流程。

- [x] **Q1.4 `PadInfoViewModels` 拆分**
  - 熱點：`src/FreeformHelper.UI/ViewModels/PadInfoViewModels.cs`（~1697 行）。
  - 重構方向：CAD popover / REG popover / Notch diagnostics / command handlers 分離。
  - 目標：每檔 < 600 行，降低跨區修改風險。
  - 完成：
    - `PadInfoViewModels.cs` 已拆為 `src/FreeformHelper.UI/ViewModels/PadInfo/*` 多檔 partial。
    - 已分離 `CadPadInfoViewModel` 的 core / commands / formatting / notch diagnostics。
    - 已分離 `RegularPadInfoViewModel` 的 core / commands / formatting。
    - 共用型別抽到 `PadInfoCommon.cs`，各檔均 < 600 行。

## P2（演算法與效能）
- [x] **Q2.1 拆分 `NotchV22CompensationService` 計算階段**
  - 熱點：`src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`（~982 行）。
  - 重構方向：
    - Stage A：候選 overlap 蒐集
    - Stage B：boundary/owner/blocker 判斷
    - Stage C：to-full reachability
    - Stage D：ratio/polygon 合成
  - 要求：`NotchV22CompensationServiceTests` 全綠，輸出結果 bitwise 等價或誤差容忍一致。
  - 完成：
    - `Compute()` 已改為 Stage A/B/C/D 管線：`RunStageACollectOverlaps`、`RunStageBBuildBoundaryAndOwnership`、`RunStageCEvaluateToFullAndDiagnostics`、`RunStageDBuildResult`。
    - boundary 與 owner 判斷已抽離到 `StageBBoundaryContext`，集中 strict owner cache 與 boundary index 計算。
    - Stage 中間資料已明確型別化（`OverlappedRegularInfo`、`StageAOverlapResult`、`StageCEvaluationResult`），降低單方法耦合度。
    - Gate 驗證已通過：build、lint、`application/ui-core/smoke` 測試全綠。

- [x] **Q2.2 `PadCanvas` 渲染與輸入責任再切分**
  - 熱點：
    - `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs`（~1199 行）
    - `src/FreeformHelper.UI/Controls/PadCanvas.Input.cs`（~991 行）
  - 方向：render layers（grid/cad/notch/labels）+ interaction handlers（hit test/select/drag）獨立類別。
  - 目標：改善 selection 起手延遲，並降低 overlay 更新阻塞。
  - 完成：
    - 新增 `PadCanvasSelectionEngine`（`src/FreeformHelper.UI/Controls/PadCanvas.SelectionEngine.cs`），集中 hit-test、box-select、pad center 查找等互動邏輯。
    - 新增 `PadCanvasVisibleDrawListBuilder`（`src/FreeformHelper.UI/Controls/PadCanvas.VisibleDrawListBuilder.cs`），集中可見清單與低細節 decimation 規劃。
    - `PadCanvas.Input.cs` / `PadCanvas.Rendering.cs` 已改為單一入口轉呼叫，保留現有行為並降低單檔複雜度。
    - Gate 驗證已通過：build、lint、`application/ui-core/smoke` 測試全綠。

- [x] **Q2.3 `DxfPadImporter` / `DxfRegularMappingAnalyzer` 可維護性重構**
  - 熱點：
    - `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.cs`（~729 行）
    - `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs`（~614 行）
  - 方向：拆 parser/normalize/filter/mapping score builder，保留原演算法結果。
  - 完成：
    - `DxfPadImporter` 已改為 partial 模組：entry、`EntityParsing`、`InsertExpansion`、`Tokenization`、`PolylineCommit`，將 parser/normalize/filter 責任分離且對外 API 不變。
    - `DxfRegularMappingAnalyzer` 已改為 partial，並抽離 `CandidateRangeResolver`、`ScoreBuilder`、`ManualOverrideApplier`、`CandidateDiagnosticsBuilder`，將 mapping score 與候選/override 邏輯拆分。
    - 既有 `DxfPadImporterTests` / `DxfRegularMappingAnalyzerTests` 行為維持，重構後由既有 application gate 回歸驗證。

- [x] **Q2.4 To Full 規則引擎與 legacy path 技術債清理**
  - 涉及：
    - `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs`
    - `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
  - 方向：把 legacy fallback 包裝在 adapter，避免主流程分支散落。
  - 要求：trace 輸出與 rule code 對外格式不變。
  - 完成：
    - 新增 `src/FreeformHelper.Application/Services/NotchToFullRuleDecisionAdapter.cs`，統一封裝 rule-engine 與 legacy fallback 分支。
    - `NotchV22CompensationService` 改為透過 adapter 取得 `NotchToFullRuleDecision`，移除服務內的 legacy rule-code 分支與 `ResolveToFullRuleCode`。
    - legacy trace 規格仍沿用 `legacy.inlineGate`，rule code 常數沿用 `NotchToFullRuleEngine`，對外輸出格式不變。

## P3（UI 排版與可維護性）
- [x] **Q3.1 `Controls.axaml` 模組化**
  - 熱點：`src/FreeformHelper.UI/Styles/Controls.axaml`（~1549 行）。
  - 方向：按領域拆為 `Controls.Form.axaml`、`Controls.Panel.axaml`、`Controls.Tab.axaml` 等，減少單檔修改衝突。
  - 完成：
    - `Controls.axaml` 改為聚合入口，透過 `StyleInclude` 載入子模組，保留原載入順序。
    - 新增 `Controls.Core.axaml`、`Controls.Tab.axaml`、`Controls.Form.axaml`、`Controls.Panel.axaml`、`Controls.Scroll.axaml`、`Controls.PadInfo.axaml`。
    - 所有樣式資源鍵維持不變，避免 UI 行為與 token 契約漂移。

- [x] **Q3.2 `RightWorkflowPanel.axaml` 元件化**
  - 熱點：`src/FreeformHelper.UI/Views/RightWorkflowPanel.axaml`（~1239 行）。
  - 方向：Step1~Step5 區塊拆 user control（只搬 UI，行為不變）。
  - 完成：
    - 新增 `src/FreeformHelper.UI/Views/WorkflowSteps/RightWorkflowStep{1..5}View.*`，把 Step1~Step5 UI 區塊切分為獨立 UserControl。
    - `RightWorkflowPanel.axaml` 改為容器組裝層，保留 `Step2Block`~`Step5Block` 命名以維持既有 BringIntoView/導覽路徑。
    - `RightWorkflowPanel.axaml.cs` 統一用 `OnStepOpenSettingsRequested` 轉發子元件事件，移除重複 handler/section parse。
    - Headless 資源契約已補齊（Step3 `NotchLayerStateGlyph`），build/lint/application/ui-core/smoke gate 全綠。

- [x] **Q3.3 `SettingsWindow.axaml` 元件化**
  - 熱點：`src/FreeformHelper.UI/Views/SettingsWindow.axaml`（~697 行）。
  - 方向：General/Step1..Step5 表單分段控制項，避免單檔過大。
  - 完成：
    - 新增 `src/FreeformHelper.UI/Views/SettingsSections/Settings{General,Step1,Step2,Step3,Step4,Step5}SectionView.*`，將設定內容拆成獨立區塊控制項。
    - `SettingsWindow.axaml` 改為導覽 + 組裝層，保留 `GeneralSection`~`Step5Section` 命名，維持既有 `NavigateToSection`/`BringIntoView` 行為。
    - `SettingsWindow.axaml.cs` 導覽邏輯不需改動即可沿用，完成後 gate（build/lint/application/ui-core/smoke）全綠。

- [x] **Q3.4 修正 HowTo 與現況不一致文案**
  - 檔案：`src/FreeformHelper.UI/Views/HowToUseView.axaml`
  - 問題：仍有 Home 分頁敘述，與現行 UI 不一致。
  - 完成：
    - Home 相關敘述已移除並更新為現行分頁文案。

## P4（測試重整與流程優化）
- [x] **Q4.1 拆分巨型 UI 測試檔**
  - 熱點：`tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.cs`（~1654 行）。
  - 方向：按主題拆檔（selection / notch preview / settings persistence / export / workflow）。
  - 完成：
    - 已拆分為 `FreeformHelperViewModelTests.Basics.cs`、`FreeformHelperViewModelTests.SettingsPersistence.cs`、`FreeformHelperViewModelTests.CommandsAndUndo.cs`、`FreeformHelperViewModelTests.Helpers.cs`。
    - 維持同一個 `partial` 測試類別與原本測試名稱，避免測試篩選與報表路徑變動。
    - 原 `FreeformHelperViewModelTests.cs` 巨型檔已移除，build/lint/application/ui-core/smoke gate 全綠。

- [x] **Q4.2 建立「重構不改行為」回歸基線**
  - 內容：
    - 3635 專案固定回歸腳本（載入、Step1~Step5、CSV/TXT export、selection 延遲採樣）。
    - Runtime query 基線（`status`/`selection`/`notch`/`notch-validation`）。
  - 產物：`docs/performance` + `scripts/perf` 更新基線文件。
  - 完成：
    - 新增 `scripts/perf/run-3635-regression-baseline.ps1`，可固定執行 `project_3635.json` 的 Step1~Step5 回歸，並輸出 CSV/TXT、runtime query 快照與 selection latency 統計。
    - Runtime query 新增 `query export-notch --format csv|txt --path <output>`，支援非互動 Step5 匯出自動化。
    - 文件補齊：`docs/performance/regression-baseline-3635.md`、`docs/performance/perf-baseline-howto.md`、`docs/reference/runtime-cli-plan.md`、`scripts/README.md`。

- [x] **Q4.3 測試分層執行策略文件化**
  - 目標：每次重構固定執行順序與最小集，降低回歸漏網。
  - 對齊：
    - `scripts/tests/run-tests.ps1 -Group application`
    - `scripts/tests/run-tests.ps1 -Group ui-core`
    - `scripts/tests/run-tests.ps1 -Group smoke`
  - 完成：
    - 新增 `scripts/tests/run-refactor-gate.ps1`，把 lint/build/application/ui-core/smoke 固定為一鍵 gate。
    - 新增 `docs/guides/refactor-test-execution-strategy.md`，明確定義最小回歸順序與加跑條件。
    - 更新 `tests/README.md`、`scripts/README.md` 的固定測試順序與命令。

- [x] **Q4.4 lint 全專案 pass（AllFiles）**
  - 目標：`./scripts/tests/lint.ps1 -AllFiles` 在目前分支可穩定通過。
  - 驗證：
    - `./scripts/tests/lint.ps1 -AllFiles`
  - 完成：
    - 已於 `master` / `beta-0.1` 基線實跑 `./scripts/tests/lint.ps1 -AllFiles`，exit code=0。

## 固定 Gate（每個里程碑）
- [x] Build gate：`dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
- [x] Lint gate：`./scripts/tests/lint.ps1`
- [x] 測試 gate：`scripts/tests/run-tests.ps1 -Group application`
- [x] 測試 gate：`scripts/tests/run-tests.ps1 -Group ui-core`
- [x] 測試 gate：`scripts/tests/run-tests.ps1 -Group smoke`
  - 最新驗證：`./scripts/tests/run-refactor-gate.ps1`（2026-02-26）。
