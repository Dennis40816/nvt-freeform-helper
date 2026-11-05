# 無引用程式碼盤點（src/）

- 基準：`origin/main` @ `5e48bec842d9f0e08ac7d9f6ac64abe801954782`（2026-11-05 取得）
- 性質：僅分析，**未刪除、未修改任何 src/ 內容**。本檔是唯一新增檔案。
- 相依圖：`docs/generated/project-dependency-graph.md`（2026-10-29 產生）。專案結構為 Domain / Application / Infrastructure / UI 加 Tests，與目前 `src/` 一致，因此沿用，沒有重建。

## 1. 方法與限制

雲端環境沒有 `dotnet`，也無法還原私有套件 `Nvt.Core*`，所以**沒有做 Roslyn 語意分析**。本檔是文字層級的比對，再逐項人工核對。

做法：

1. 把 `src/**/*.cs` 的註解移除（字串字面值保留，因此 `nameof`、反射字串、插值字串都算引用），並保留行號。
2. 把 `src/**/*.axaml` 全文視為引用來源（`{Binding}`、`x:Static`、`StaticResource`/`DynamicResource`、`Classes=`、`x:Class`）。
3. `[RelayCommand]` 的方法以 `<Name>Command` 計；`[ObservableProperty]` 欄位以產生的屬性名稱計。
4. `tests/`、`scripts/`、`docs/`（不含 `docs/archive`）、`eng/` 另外計數，用來判斷「只有測試或文件在用」。
5. `NLog.config` 的 `<target xsi:type="InAppConsole">`、`[JsonPropertyName]` 序列化屬性、介面實作、`Program.Main` 都算引用或進入點。

限制（會漏報，不會誤刪）：

- 名稱比對不分型別。同名成員（`Dispose`、`Query`、`SetText` 等）會互相遮蔽，真正無用的同名成員可能沒被列出。
- 只出現在彼此呼叫的死循環（A 呼叫 B、B 呼叫 A、外部都沒用）不會被抓到。
- 私有成員未逐一分析（預期 IDE0051/IDE0052 已在 lint 擋掉）。
- `example/` 是私有 submodule，此環境未初始化，**未檢查其內容**。
- 未涵蓋：未使用的 enum 值、未使用的 `using`、設定項（settings）是否有人讀。這些不在本次要求內。

初篩過程中發現兩個誤報來源並已修正：移除 `//` 註解時曾誤切含 `//` 的字串（例如 `NotchFirmwareCExporter` 的 `$"//   SourceCombineRule: {…}"`），造成 `DescribeSourceCombineRule`、`ToOnOff`、`NotchV22TargetAllocationPolicy.DescribeRule` 被誤列。修正後這三項確認仍在使用，不在本檔清單內。

## 2. 摘要

| 類別 | 可直接刪除 | 需 owner 確認 |
|---|---:|---:|
| 方法 / 屬性 | 5（3.1） | 22 個成員（4.1 五個、4.2 十七個） |
| 常數（`IconGlyphs`） | 5 | 0 |
| 型別 | 0 | 2（測試專用，見 4.2） |
| XAML 樣式選擇器（style class） | 11 組 | 3 組 |
| XAML 資源 `x:Key`（`Tokens.axaml`） | 58 | 9 |
| 檔案 | 0 | 1（`freeform-helper-256.png`，123,532 bytes） |

型別層級的結論：978 個型別宣告中，沒有任何一個型別在 src 內完全無人引用；只有 `UiSnapshotPersistenceContract` 與 `LegacyUiSnapshotFieldAttribute` 僅被測試使用（見 4.2）。曾被文字比對列出的 `AppFontBootstrapper`（`WithAppFonts()` 由 `Program.cs:75` 呼叫）、`NotchAlgorithmVersionDisplay`（`ToDisplayLabel()` 有 10 處以上呼叫）、`CadOutputFwDiffAssignmentContractText`（`ToContractString()` 有多處呼叫）、`InAppConsoleTarget`（NLog.config 註冊）都是擴充方法類別或設定檔註冊，**仍在使用**。

## 3. 可直接刪除（safe-to-delete）

判準：在 `src/`、`tests/`、`scripts/`、`docs/`（非 archive）、`eng/` 都找不到引用，也不涉及序列化、介面實作或反射。刪除前仍請在本機跑 `scripts/tests/lint.ps1 -UseNoAppHost` 與相關 guard 測試。

### 3.1 方法與屬性

| 位置 | 項目 | 無引用證據 | 移除風險 |
|---|---|---|---|
| `src/FreeformHelper.Domain/Notch/NotchTable.cs:44` | `NotchTable.RowsForIc(int)` | `grep -rnw RowsForIc src tests scripts docs eng` 只有宣告 | 低。公開 Domain API，但本 repo 內無呼叫者 |
| `src/FreeformHelper.UI/Controls/SimulationColorScaleResolver.cs:96` | `ResolveSequentialColor(...)` | 全 repo 只有宣告。同類 `ResolvePositiveColor/NegativeColor/SignedColor` 都有呼叫 | 低。對應的 `BrushSimulationScaleSequential` token 也無人用（見 3.4），可一併處理 |
| `src/FreeformHelper.UI/Services/NotchExportService.cs:122` | `NotchExportService.ExportCInitializer(...)` | 全 repo 只有宣告；它只是轉呼叫 `NotchTableExporter.ExportAsCInitializer`，後者由 `FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:20` 與測試直接使用 | 低。移除不影響匯出行為 |
| `src/FreeformHelper.UI/Interaction/WorkspaceInteractionState.cs:36` | `ActivePadInfo` 屬性 | 全 repo 只有宣告（背後欄位 `_activePadInfo` 仍由 `OpenPadInfo`/`ClosePadInfo` 使用，只需刪屬性） | 低 |
| `src/FreeformHelper.UI/Controls/PadCanvas.Perf.cs:151` | `PadCanvasVisibleDrawPerfSnapshot.CacheHit` | 只是 `DrawListCacheHit` 的別名；唯一讀取者（`FreeformHelperViewModel.Selection.cs:158`）讀的是 `DrawListCacheHit`，測試也沒讀 `CacheHit` | 低 |

### 3.2 常數：`src/FreeformHelper.UI/Icons/IconGlyphs.cs`

| 行 | 常數 | 證據 |
|---:|---|---|
| 12 | `Add` (`"ADD"`) | `IconGlyphs.Add` 與字面值 `"ADD"` 在 `.cs`/`.axaml`/tests 都沒出現（`List.Add` 之類不算） |
| 16 | `FilterAltOff` | 同上，字面值 `FILTER_ALT_OFF` 也無人用 |
| 23 | `Shield` | 同上 |
| 24 | `Construction` | 同上（`docs/` 內的 `construction` 只是一般英文用字，不是引用） |
| 44 | `ContentPaste` | 同上；相鄰的 `ContentPasteOff`（43）有人用 |

風險：低。這是集中式字形表，若 owner 想保留作為「已驗證可用的圖示目錄」，可以保留，但目前沒有任何呼叫者。

### 3.3 XAML 樣式選擇器（宣告了 class，但沒有任何元素套用）

檢查範圍：所有 `Classes="..."`、`Classes.xxx=`、`.cs` 內的 `Classes.Add/Set/Remove("...")` 與字串字面值。`Classes="{Binding ...}"` 在 src 內不存在，所以不會有動態套用。

| 位置 | class | 備註 |
|---|---|---|
| `src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml:20` | `workspaceHeaderInspector` | 同檔實際使用的是 `workspaceHeaderInspectorCompact`（:295），舊的 Inspector 系列樣式沒有被套用 |
| 同檔 `:34` | `workspaceHeaderInspectorSecondary`（TextBlock 與 SelectableTextBlock 兩條規則） | 同上 |
| 同檔 `:48` | `workspaceHeaderInspectorCard`（ToggleButton） | 同上 |
| 同檔 `:74` | `workspaceHeaderInspectorChipSecondary` | 同上 |
| 同檔 `:78` | `workspaceHeaderInspectorChipMatch` | 同上 |
| 同檔 `:82` | `workspaceHeaderInspectorChipCompensation` | 同上 |
| 同檔 `:86` | `workspaceHeaderInspectorChipText`（兩條規則） | 同上 |
| 同檔 `:91` | `workspaceHeaderAction`（Button / ToggleButton） | 同上 |
| `src/FreeformHelper.UI/Styles/Icons.axaml:30` | `controls\|FontIcon.symbols.iconXs` | 全 src 無 `iconXs` 用法 |
| `src/FreeformHelper.UI/Styles/Controls.Tab.axaml:36` | `ToggleButton.shellTab.settingsWindowNavTab` | 無元素使用 `settingsWindowNavTab` |
| `src/FreeformHelper.UI/Styles/Controls.Tab.axaml:40` | `Border.rightPanelTabStrip` | 右側面板實際使用 `rightPanelTabHost`（`FreeformHelperView.axaml:207`） |

風險：低，主要是視覺回歸。移除後請在 Dev 頁與主視窗各看一次（AGENTS.md 的 UI 規則）。`UiLayoutGuardTests` 對 `Controls.Tab.axaml` 內容有文字斷言，刪除後需跑該測試類別。

### 3.4 `Tokens.axaml` 無引用資源（58 項）

判準：`x:Key` 在整個 `src/**/*.axaml` 與 `src/**/*.cs`（含 `GetResourceBrush("...")` 這類字串查找）都沒有引用，`tests/`、`scripts/`、`docs/` 也沒有。主題字典中 Light/Dark 同名 key 視為同一個資源。檔案：`src/FreeformHelper.UI/Styles/Tokens.axaml`。

Brush（18）。這些 Brush 無人用，但它們背後的 `Color…` token 多數仍由程式碼以 `GetResourceColor("Color…")` 讀取，**只刪 Brush，不要連帶刪 Color**：

- 模擬色階：`BrushSimulationScaleNeutral`:409、`BrushSimulationScaleSequential`:421、`BrushSimulationScaleDiverging`:433
- Panel block：`BrushPanelBlockCollapsedBackground/Hover/Pressed`:509–511、`BrushPanelBlockExpandedBackground/Hover/Pressed`:512–514。這 6 個 Brush 刪除後，對應的 `ColorPanelBlock*` 6 個 key（Light/Dark 各一）也不再有任何引用，可在同一次處理
- Canvas：`BrushCanvasAxisLabel`:534、`BrushCanvasIcBlockStroke`:536、`BrushCanvasRegularFreeform`:540、`BrushCanvasRegularUnmatched`:541、`BrushCanvasCadFillDefault`:543、`BrushCanvasCadFillSelected`:544
- Notch：`BrushNotchCadStroke`:545、`BrushNotchRegularStroke`:547、`BrushNotchXY`:556

複合 Inset（13，單一用途組合）：`Inset14_6`:590、`Inset10_8_10_12`:596、`InsetRight10`:598、`InsetRight12Bottom16`:601、`InsetRight12Bottom6`:604、`InsetRight6Bottom2`:606、`InsetRight8Bottom8`:609、`InsetBottom2`:611、`InsetBottom20`:615、`InsetTop4Bottom10`:616、`InsetLeft4`:625、`InsetLeft6Bottom6`:626、`InsetLeft18`:629

尺寸、時間與其他功能專屬 token（27）：

- `BorderNotchExportActiveRow`:643、`BorderTabThickness`:648、`FontSizePanelBlockTitle`:674、`SplitterRowThickness`:699、`PanelToggleHeight`:702、`RegularLayerSelectorWidth`:746
- `NotchApplySimulationFrameDropdownWidth`:776、`…ModeDropdownWidth`:777、`…AreaDropdownWidth`:778、`…ThresholdWidth`:779、`…FrameRailMinWidth`:780、`…AaMinHeight`:781、`…RegularListMinHeight`:782、`…HistogramMinHeight`:783、`…HeatmapMinHeight`:784、`…HotspotListMaxHeight`:785、`…AaBadgeFontSize`:799、`…AaBadgePaddingX`:800、`…AaBadgePaddingY`:801
- `NotchExportMiniPreviewMinHeight`:823、`SettingsContentMaxWidth`:849、`ViewMenuCloseDelayMs`:873、`MainMenuCloseDelayMs`:874
- `CanvasNotchToFullSeedFillOpacity`:927、`CanvasNotchToFullCandidateFillOpacity`:928、`NotchPreviewFillAlpha`:946、`DevSwatchHeight`:955

風險：低到中。XAML 資源在 runtime 才解析，編譯期不會報錯，所以刪除後務必啟動 UI 並開 Dev 頁、設定視窗、Notch 模擬頁各一次，並跑 `UiLayoutGuardTests`（它會讀 `Tokens.axaml` 做對比度與 hex 色彩斷言）。

## 4. 需 owner 確認（needs-owner-check）

### 4.1 舊版專案檔相容欄位（有序列化引用，**不是無用**）

這些屬性在 src 內沒有程式碼讀取，但它們靠 `[JsonPropertyName]` 在載入舊版專案檔（`ProjectFile` JSON）時把舊欄位搬到新欄位，是向下相容路徑。刪除會讓舊專案檔的舊欄位被忽略（資料靜默遺失）。

| 位置 | 項目 | 對應新欄位 |
|---|---|---|
| `src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:96` | `DxfVisibleIndexOverrides`（`dxfVisibleIndexOverrides`） | `CadOutputFwDiffIndexOverrides` |
| 同檔 `:112` | `DxfVisibleIndexAnchorCadPadId` | `CadOutputFwDiffIndexAnchorCadPadId` |
| 同檔 `:132` | `DxfVisibleIndexAnchorCadPadByIc` | `CadOutputFwDiffIndexAnchorCadPadByIc` |
| `src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:399` | `UiImportSnapshot.RegularSignalMaskSourcePath` | `RegularVisibilityMaskSourcePath` |
| 同檔 `:422` | `UiImportSnapshot.UseRegularSignalMask` | `UseRegularVisibilityMask` |

需要 owner 決定：是否還有使用者持有這些舊版專案檔？可否宣告停止相容？此外 `example/`（私有 submodule）內的專案檔是否含這些欄位，本環境無法確認。

### 4.2 只有測試在用的正式碼（production 無呼叫者）

這些成員在 `src/` 內只有宣告，只有 `tests/` 呼叫。刪除會連帶要改測試；是否保留取決於 owner 想不想把它們移到測試輔助碼。

| 位置 | 項目 | 測試引用 |
|---|---|---|
| `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs:29` | `AnalyzeWithTypedOverrides` | `DxfManualOverrideIdentityContractTests` 4 處 |
| `src/FreeformHelper.Domain/Pads/RegularPad.cs:75` | `RegularFwDiffIndex`（`DiffIndex` 的別名） | `TypedMappingBoundaryTests` 等約 15 處；`docs/` 也提到 |
| `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.Tokenization.cs:9` | `TokenizationCount` | `OpenDxfSinglePassTests`；計數器本身在正式碼遞增，屬於測試探針 |
| `src/FreeformHelper.UI/Controls/PadCanvas.Perf.cs:24` | `GetRenderPerfSnapshot` | `PadCanvasRenderPerfTests` |
| `src/FreeformHelper.UI/Services/ConsoleLinkOpener.cs:54` | `GetUnavailableReason` | `ConsoleLinkOpenerTests` 約 11 處 |
| `src/FreeformHelper.UI/Services/ManualSizingService.Operations.cs:192` | `SetPadDimensions` | `ManualSizingServiceTests` 2 處 |
| `src/FreeformHelper.UI/Services/WorkflowPipelineService.cs:110` | `NormalizeStepOrDefault` | `WorkflowPipelineServiceTests` 1 處 |
| `src/FreeformHelper.UI/Services/CadLoadSpinnerDebugState.cs:140` | `ResetForTest` | 測試 2 處 |
| `src/FreeformHelper.UI/Services/CadLoadSpinnerProcessHost.cs:665` / `:667` | `GetLatestStateSyncTask`、`GetVisibilityStateSnapshot` | 測試 6 / 11 處 |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.AppSettings.cs:17` | `AppGeneralPersistenceForTests` | 測試 2 處 |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:292` / `:346` / `:349` | `WaitForGridRebuildIdleAsync`、`WorkflowDataForTests`、`NotchCacheForTests` | 測試 44 / 2 / 1 處 |
| `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.RegularVisibilityMask.cs:21` | `RegularVisibilityMaskForTests` | 測試 5 處 |
| `src/FreeformHelper.UI/ViewModels/SimulationWorkspaceViewModel.Source.cs:109` | `GetSimulationScenarioSnapshot` | 測試 6 處 |
| `src/FreeformHelper.UI/Views/FreeformHelperView.CadLoadSpinner.cs:14` | `CadLoadSpinnerHostForTest` | 測試 1 處 |
| `src/FreeformHelper.Infrastructure/Project/UiSnapshotPersistenceContract.cs:2` | 整個 `UiSnapshotPersistenceContract` 類別（含 `RootProperties`:13、`SectionProperties`:26） | `UiSnapshotPersistenceContractTests` 的契約守門測試 |
| `src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:4` | `LegacyUiSnapshotFieldAttribute` | 上述契約測試以反射過濾 |

多數是刻意的測試接縫（`InternalsVisibleTo("FreeformHelper.Tests")`），建議保留，除非 owner 要把接縫收斂到測試專案。

### 4.3 可能無人使用，但有政策或測試牽連的樣式

| 位置 | class | 為何不放進「可直接刪除」 |
|---|---|---|
| `src/FreeformHelper.UI/Styles/Controls.Tab.axaml:57`（及 :70、:75 的 `:pointerover`/`:checked`） | `ToggleButton.rightPanelTab` | 全 src 無元素套用，但 `UiLayoutGuardTests.cs:87` 斷言這條選擇器必須存在。刪除須同步改測試 |
| `src/FreeformHelper.UI/Styles/Controls.Tab.axaml:129–199`（12 條規則） | `Button.icon` / `ToggleButton.icon` / `.icon.ghost` | 全 src 無 `Classes="icon"` 或 `ghost` 用法；註解寫「panel collapse/expand affordances」。名稱太泛用，需 owner 確認不是留給外部或未來功能 |
| `src/FreeformHelper.UI/Styles/Controls.Action.axaml:5`、`:78`、`:97` | `actionButton` | 無元素使用，但 `scripts/tests/check-xaml-action-roles.ps1:315` 與 `docs/guides/ui-action-role-system.md:74` 把它列為合法的 action primitive。屬設計系統詞彙，需 owner 決定 |

### 4.4 `Tokens.axaml` 的通用尺度值（9 項）

這些無人引用，但屬於通用尺度（spacing / radius / icon stroke），刪除會讓尺度出現缺口，且 AGENTS.md 要求缺 token 時要先補進 `Tokens.axaml`，所以保留空位可能是刻意的。

`Space0`:559、`Space18`:569、`Space24`:571、`Inset1`:574、`RadiusTopLg`:657、`RadiusPillLeft`:660、`RadiusPillRight`:661、`IconStroke`:684、`IconStrokeThin`:685

### 4.5 檔案

| 檔案 | 大小 | 證據 | 需確認原因 |
|---|---:|---|---|
| `src/FreeformHelper.UI/Assets/AppIcon/freeform-helper-256.png` | 123,532 bytes | src、tests、scripts、docs（含 README）、eng 都沒有引用。同目錄的 `.ico` 有被 `MainWindow.axaml:10` 與 `FreeformHelper.UI.csproj:8` 使用 | 可能供 GitHub 社群預覽、安裝程式、打包或商店素材等 repo 外部用途。另外 `Assets\**` 是 `AvaloniaResource`，這張圖目前會被嵌入 exe，移除可減少輸出大小（指標：UI 組件大小，約 -120 KB） |

其他非程式碼檔案（`NLog.config`、`app.manifest`、`licenses/Inter/LICENSE`、`HowTo/*.png`、`packages.lock.json`）都有引用或屬於建置必要，未列入。

## 5. 確認為誤報、仍在使用（不要刪）

初篩或次級掃描列出、但人工核對後確認仍在使用的項目，列出以免後續重複查證：

- `BooleanGlyphConverter.ConvertBack`（`Converters/BooleanGlyphConverter.cs:16`）：`IValueConverter` 介面要求
- `ProjectFile.ExtensionData`（`ProjectFile.cs:145`）：`[JsonExtensionData]`
- `Program.Main`：進入點
- `InAppConsoleTarget`：`NLog.config:23` 以 `xsi:type="InAppConsole"` 註冊
- `AppFontBootstrapper`、`NotchAlgorithmVersionDisplay`、`CadOutputFwDiffAssignmentContractText`：擴充方法類別，類別名稱不出現在呼叫端
- `ConsoleSession.SetText`：`IConsoleClipboard` 介面實作
- `IBackgroundWorkRunner` / `FireAndForgetBackgroundWorkRunner`、`IIndexMappingTraceBuilder` / `IndexMappingTraceBuilder`、`LegacyAppGeneralSettingsDocument`、`CadLoadSpinnerCommandLine`、`SharedCadLoadSpinnerHost` 等僅在單檔使用的 internal 型別：檔內有實際使用，且部分有測試實作
- 全部 `[ObservableProperty]` 欄位：以產生的屬性名稱核對後，皆有 XAML 或程式碼使用

## 6. 建議順序

1. 先處理第 3.1、3.2：9 個成員，影響面最小。
2. 再處理第 3.3、3.4：純 XAML 清理，建議同一個 PR，並在 PR 說明移動的指標（例如 `Tokens.axaml` 行數、未使用資源數）。
3. 第 4 節逐項由 owner 決定；4.1 需要先確認舊專案檔政策。
4. 重做這份盤點的最佳方式是用 Roslyn `SymbolFinder.FindReferencesAsync` 在本機（有完整套件還原）跑一次語意分析，取代本檔的名稱比對，可補上同名成員與死循環這兩類漏報。

## 7. 重現方式

單項核對（把 `<Name>` 換成要查的名稱）：

```bash
grep -rnw "<Name>" src tests scripts docs eng --include=*.cs --include=*.axaml --include=*.ps1 --include=*.md
```

樣式 class 是否被套用：

```bash
grep -rnE 'Classes(\.[A-Za-z]+)?="([^"]*[ ,])?<class>([ ",][^"]*)?"' src --include=*.axaml
```
