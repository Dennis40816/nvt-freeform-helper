# Settings Entry Matrix（M11 Phase 1~3）
最後更新：2026-08-08

目前文件基線：FreeformHelper 1.3.0 current-state audit，production evidence commit `ef08945`。本文件會刻意分開「目前仍存在的 editor」與「1.3.x normal-flow contract」，避免把完整 persistence schema 誤當成正常流程 UI 清單。

## S11.18 stale-check（2026-03-10）
1. 左側 `DXF edits > Hidden / Combined / Moved` summary badge 具體會開 `DxfEditChangeListWindow`，這是 DXF edit project state 的 modal 檢視入口，不屬設定分叉。
2. `Open DXF` / `Load Project` 共用 `RunWithCadLoadCanvasOverlayAsync(...) -> IsCadLoadCanvasOverlayVisible -> FreeformHelperView` inline spinner overlay，這是固定 workflow 契約，不屬任何設定入口。
3. CAD load overlay 不應再分叉成另一個 top-level loading window，也不應出現在 Display popup / SettingsWindow / Right panel。

## S11.72 stale-check（2026-03-22）
1. `SeeRegular.csv` regular visibility mask 不會在 `Load Project` 時從相鄰目錄自動搜尋；只會還原 project snapshot 明確保存的 path/toggle，embedded project 則優先使用 embedded mask。
2. regular visibility mask 的唯一使用者入口固定在 `RightWorkflowPanel > Step 1 · Geometry match`：
   - `Import Regular Visibility Mask (SeeRegular.csv)`
   - `Use Regular Visibility Mask`
   - `Clear mask`
3. 這條路徑屬 project-restored workflow state：`ProjectUiSnapshot.Import` 保存 mask source path/toggle，embedded project 可保存 mask payload；它不屬 app-general whitelist。
4. `Step 4` diagnostics 直接讀取目前 session mask；`Simulation` 會以 `regular grid ∩ active mask` 建 active surface，同時仍只使用共同生成的 notch table 執行補償。
5. 因此 mask 同時影響 upstream assignment 與 Simulation active surface；不得把它描述成只透過 notch table 間接生效。

## S11.147 stale-check（2026-04-23）
1. Step5 notch version selection 的唯一 project source 是 `Project.Settings.Notch.EnabledVersions`。
2. `UiNotchSnapshot.EnabledVersions` 僅為舊 project JSON 的反序列化相容欄位，不再由目前 `BuildUiSnapshot()` 寫入。
3. Load/Save 不得從 UI snapshot 回推 `EnableV21/EnableV22`，避免 `Settings.Notch.EnabledVersions` 與 UI snapshot 出現第二來源。

## S10.10 stale-check（2026-03-08）
1. 左側 `DXF Settings` panel 已承接 DXF edit session 的高頻入口，不再只是 layer visibility。
2. `DXF edits > Hidden / Combined / Moved` summary badge 會打開 `DXF edit details` modal，支援逐筆 focus 與單筆還原。
3. `Layers > On / Off` 是 aggregate 狀態 + batch action：
   - `On` = 所有 layer 都開啟
   - `Off` = 所有 layer 都關閉
4. `Export Notch Rows` hide / restore 不是設定，但屬固定 workflow 契約：
   - restore 入口：workspace 右上角 `Restore`
   - shortcut：`Ctrl+Shift+E`

## S4.6 stale-check（2026-03-05）
1. 本輪 refactor 僅做結構拆分（selection/notch service partial 化），不改設定入口角色與 single source。
2. app-level settings deferred policy 仍一致：
   - `Load Project` 後 defer：`MarkProjectLoadedForAppGeneralPersistence()`（`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`）
   - `Save Project` 成功後 flush：`FlushDeferredAppGeneralSettingsIfNeeded()`（`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`）
3. `Ctrl+S -> SaveProjectAsync() -> top toast` 契約仍一致：`src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs`。
4. runtime query single-entry（IPC -> `RuntimeQueryUseCase.ExecuteAsync`）仍一致：`src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`。

## 目的
- 盤點目前可修改設定的 UI 入口（Display popup / SettingsWindow / Right panel）。
- 明確標註 single source，避免同一設定在多入口下規則漂移。
- 作為 `M11` 後續收斂（Phase 2~4）的輸入基線。

## 入口角色（現況）
| 入口 | 定位 | 套用方式 |
| --- | --- | --- |
| `WorkspaceHeader` Display popup | 高頻即時視覺調整 | 直接 TwoWay 綁定 `FreeformHelperViewModel`，立即生效 |
| `SettingsWindow` | Operator-facing 必要設定（General + Step1~5）；不等同完整 persistence schema editor | draft model（`SettingsWindowViewModel`）按 `Save` 才套用 |
| `RightWorkflowPanel` | Workflow 操作 + General 快捷設定 + 設定導引 | General 區保留常用欄位直接編輯；Step 區塊保留快捷操作與 deep link |
| `Left DXF panel` | DXF import/check/edit、layer visibility、DXF edit session 維護 | 直接綁定 VM 命令/狀態，立即生效 |

## 目前已固定契約（2026-03-04）
1. `Load Project` 後 app-level general settings 進入 deferred mode，不立即覆寫使用者 app settings。
2. app-level general settings 只會在下一次 `Save Project` 成功後 flush。
3. `Ctrl+S` 會觸發 `SaveProjectAsync()`，並在 `MainWindow` 顯示 top toast 結果。
4. M11 現況仍把多數欄位放在 `SettingsWindow`；1.3.x 的 normative target 是只保留 normal operator decision，advanced/diagnostic 與 internal/compatibility 欄位不得混入 numbered flow，但仍須 lossless project roundtrip。
5. Validation 流程命名統一為 Step6（`Step 6 - Validation Quick Trace`），不再沿用舊 Step5 wording。

## Single Source（現況）
1. `FreeformHelperViewModel` 是 live UI projection/orchestration，不等於所有設定的 durable owner。
2. `SettingsWindow` 路徑：`CreateSettingsWindowViewModel` -> `SettingsWindowViewModel`（draft）-> `ApplySettingsWindowDraft`。
3. `RightWorkflowPanel` 深連結路徑：`OpenSettingsRequested` -> `FreeformHelperView.OpenSettingsWindowCore(section)` -> `SettingsWindow.NavigateToSection(section)`。
4. `Display popup` 路徑：`src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml` 直接綁定 VM 屬性。

## 1.3.x normal-flow classification（規範）

| 分類 | 可放入 numbered flow | 例子 | Persistence 規則 |
| --- | --- | --- | --- |
| Flow-required | Yes | Grid/AA/source、Step2 detect、Step3 review、Step5 output target/profile | 由 normal editor 套用，完整 project roundtrip |
| Advanced/diagnostic | No；只能在未編號 Diagnostics/專用 workspace | Coordinate calibration、Step4 mapping analyze/report、trace/score tuning | 可編輯或只讀依 diagnostic surface 決定，schema/default/consumer 保留 |
| Internal/compatibility | No editor | legacy aliases、internal limits、retired matching fields | lossless 或已明定 canonicalized roundtrip，不因隱藏 UI 刪欄位 |

硬性決定：`CoordinatePixelWidth/Height` 與 `MappingWeight*`、candidate/confidence/ambiguous thresholds 均不屬 normal operator flow。1.3.0 production UI 尚未完成全部搬移；下表用「現況」誠實列出舊 editor，R13.305 只移除/分層入口，不改 default、consumer 或 persistence schema。

## 設定入口矩陣（現況盤點）
| 設定群組 | 主要屬性（single source） | Display popup | SettingsWindow | Right panel / diagnostic workspace | 備註 |
| --- | --- | --- | --- | --- | --- |
| 顯示開關 | `ShowCad`/`ShowRegular`/`HighlightUnmatched`/`HighlightFreeform`/`ColorCadByArea` | Yes | No | No | `Display` 為高頻入口 |
| CAD/Regular 視覺樣式 | `CadLineWidth`/`RegularLineWidth`/`CadLineColorHex`/`RegularLineColorHex`/`RegularSelectedColorHex`/`CadFillOpacity`/`CadLineOpacity`/`RegularFillOpacity`/`RegularLineOpacity`/`RegularSelectedFillOpacity` | Yes | Partial（目前僅 `HighlightStrokeWidthAdjust`） | No | 樣式入口仍以 `Display` 為主 |
| Highlight 線寬 | `HighlightStrokeWidthAdjust` | Yes | Yes | No | 已修復 Settings 遺漏欄位 |
| Grid / AA / source / alignment | `CascadeNum`/`XChannels`/`YChannels`/`GridPaddingPercent`/`SelectedScanOrderOption`/`SelectedRegularSourceModeOption`/`SelectedRegularSourceLayerOption`/`ActiveAreaWidth`/`ActiveAreaHeight`/`SelectedGridAlignmentOption`/`PanelBiasX`/`PanelBiasY` | No | Yes | Yes（TwoWay） | Right panel 保留可編輯快捷；與 SettingsWindow 共用 single source |
| Import / 行為開關 | `ImportOnlyClosedPolylines`/`ImportBlockPolylines`/`RecalcBoundsOnLayerFilter`/`ApplyAppVisualPreferencesOnProjectLoad` | No | Yes | No | General 完整入口 |
| Coordinate calibration | `CoordinatePixelWidth`/`CoordinatePixelHeight` | No | Yes（現況 legacy entry） | Coordinate workspace 另有 diagnostic editor | Normal-flow = No；R13.305 移除 General editor，保留專用 calibration surface、project/app-view 相容值與 consumer |
| Step1 參數 | `MatchThreshold` | No | Yes | 摘要 + Open Settings | Right panel 不直接編輯 |
| Step1 regular visibility mask workflow | `RegularVisibilityMaskSummary` / `IsRegularVisibilityMaskEnabled`（session state） | No | No | Yes | `Import Regular Visibility Mask (SeeRegular.csv)` / `Use Regular Visibility Mask` / `Clear mask`；不搜尋鄰檔，但可由 project snapshot/embedded mask 還原 |
| Step2 參數 | `FreeformAxisThreshold`/`EnableAutoDetectXy`/`EnableFreeformEdgeSpecialization`/`AutoReplayStep2AfterProjectLoad` | No | Yes | 摘要 + Open Settings | Right panel 保留執行與 override 操作 |
| Step3 參數 | `EnableToRegular`/`EnableToFull`/`EnableToFullRuleEngine`/`EnableToFullRuleTrace`/`ToFullStrictOverlapPercent`/`EnableBoundaryVirtualAreaCap`/`BoundaryVirtualAreaCapPercent`/`EnableTargetCoverageGuard`/`TargetCoverageCapPercent`/`ShowNotchCanvasPreview`/`ShowNotchToRegularLabels`/`NotchPreviewVisualizationStep`/`NotchPreviewAutoPlayEnabled`/`NotchPreviewAutoPlayIntervalMs` | No | Yes | Partial（guard/caps、stage/autoplay 快捷） | 快捷與Settings draft Save共用settings plan；boundary cap屬Step3 computation invalidation，target guard/cap只失效final Step5 projection並保留Step3／export batch |
| Step4 參數 | `MappingWeight*`/`MappingCandidateNumber`/`MappingLowConfidenceThreshold`/`MappingAmbiguousMargin`/`SelectedCadOutputFwDiffAutoModeOption`/`CadOutputFwDiffIndexStart`/`CadOutputFwDiffIndexAnchorCadId`/`ShowDiffIndexOverlay` | No | Yes（現況 legacy entry） | 摘要 + Open Settings | Normal-flow = No（scoring）；R13.305 將 Analyze/report/必要 override 移到未編號 Diagnostics。Draft Save目前只mark unsaved、不立即invalidate Step4，真正Analyze時才同步／invalidate（R13.302b debt） |
| Step5 參數 | `EnableV21`/`EnableV22(v2.2)`/`SelectedNotchExportFileTypeOption`/`SelectedNotchExportProfileOption`/`NullValue`/`NotchThresholdQ7`/`NotchThresholdPercent`/`LinkNotchThresholds`（`LenScale` 為 internal legacy，不在 UI 顯示） | No | Yes | 摘要 + Open Settings | Right panel 保留 Export 操作 |
| Step6 驗證 | `NotchValidationRegularPadId` | No | No | Yes | 屬 workflow 操作，不屬設定收斂範圍 |
| DXF edit session | `HiddenCadPadIds`/`DxfCombinedCadGroups`/`DxfCadLayerOverrides`（project state） | No | No | No | 左側 `DXF edits` 操作；summary badge 會開 `DxfEditChangeListWindow` |
| CAD load overlay workflow | `IsCadLoadCanvasOverlayVisible`（暫態 UI state） | No | No | No | `Open DXF` / `Load Project` 共用 `RunWithCadLoadCanvasOverlayAsync(...)`；`FreeformHelperView` 只顯示 inline centered spinner |
| Layer visibility aggregate | `LayerToggles` | No | No | No | 左側 `Layers On / Off` 為 aggregate 狀態 + batch action |

## 持久化層級（對照）
- Project（完整快照）：`ProjectSettings + ProjectUiSnapshot`（儲存/載入 round-trip）。
- Project（DXF edit session）：`HiddenCadPadIds + DxfCombinedCadGroups + DxfCadLayerOverrides`。
- App General（白名單）：`UiViewSnapshot + UiImportSnapshot + Behavior.ApplyVisualPreferencesOnProjectLoad`。
- Coordinate pixel X/Y 目前同時位於 project `UiViewSnapshot` 與 app-general view clone；啟用 app visual preference overlay 時，app-general 值可覆蓋 project view。是否繼續把 calibration 視為 visual preference 由 R13.301/R13.305 決定，本文件不偷改現行 precedence。
- Step4 scoring 只由 `ProjectSettings.IndexMapping` 保存；legacy `CandidatePaddingCells` 會 canonicalize 成 `CandidateNumber`，其餘 weights/thresholds/internal limits 仍由 analyzer 消費。
- App General 寫入時機：`Load Project` 後進入 deferred；下一次 `Save Project` 成功才 flush 到 app settings。
- Default：程式預設值（未命中前兩層時）。

## Current flow 與 target flow

- 1.3.0 current UI 仍呈現 `Step1 -> Step2 -> Step3 -> Step4 -> Step5`，Step6 validation 亦顯示於 numbered settings tab；這是已登記的 presentation debt，不代表 Step4/6 是 export prerequisite。
- R13.305 target 為 `Step1 -> Step2 -> Step3 -> Step5`；Step4 mapping diagnostics 與 Step6 validation 搬到未編號 Diagnostics/Inspector，仍可手動執行與讀取報告。
- 入口搬移前須由 R13.301/R13.302 固定 draft apply、dirty-field 與 typed invalidation，避免隱藏 editor 時順便改變 settings side effects。

## M11 Phase 2 收斂結果
1. `RightWorkflowPanel > General Settings` 保留可編輯快捷欄位（Grid / AA / source / alignment）。
2. `Display popup` 定位為高頻即時視覺項；M11 當時將 `SettingsWindow` 作為完整入口，該結論已被 R13.305 的 operator-facing target 取代。
3. Step3 保留 workflow 快捷（stage/autoplay）於右側 panel；不是正常人工決策的欄位移到 advanced/diagnostic 或只保留 persistence，不再以「完整 schema 都可編輯」為目標。
4. Phase 3 已補 UI guard + ViewModel draft/apply 測試，固定入口契約（含 Right panel General 主要 TwoWay 欄位）。
