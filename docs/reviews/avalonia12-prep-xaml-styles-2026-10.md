# Avalonia 12 升級準備：XAML、樣式與視覺基線盤點（2026-10）

## 狀態與計數範圍

本文件僅盤點升級接觸面，**沒有執行 Avalonia 11 → 12 升級**。[TODO.md](../../TODO.md) 的版本決策將升級排在 **1.3.5 之前**，目前不執行；[Directory.Packages.props](../../Directory.Packages.props) 中 Avalonia、Desktop、Fluent、Inter、Headless 與 Headless.XUnit 仍為 **11.3.12**。AvaloniaEdit 是另行定版的 11.4.1，不應混稱為核心版本。

盤點基準為 `7477b07d`，限於 `src/FreeformHelper.UI/**/*.axaml`、相關測試及其公開基線。依賴關係已對照 [project-dependency-graph.md](../generated/project-dependency-graph.md) 與目前 ProjectReference，沒有需要重建的差異。未讀取私有 example 資料。沙箱無網路，未取得 Avalonia 12 官方遷移指南；以下風險是依本庫的耦合程度排序，**不是 Avalonia 12 已確認的 breaking changes**。涉及新版行為的判斷一律為「**待對照官方遷移指南確認（to check against the official migration guide）**」。

計數以 XML 元素及屬性值為準，排除註解：共 **69 份 AXAML**（根目錄 2、Styles 12、Controls 4、Views 51），**607 個 `<Style Selector>`**、**0 個 `<ControlTheme>`**、**8 個 `<ControlTemplate>`**、**70 個 `<DataTemplate>`**。逗號分隔的 selector 算同一個 Style；一個 selector 同時包含多種控制項或語法時，各分類會重複計入，不能相加當總數。資源／binding 數字是 markup extension 的出現次數，不是不同 key／path 的數量。

## 樣式系統與 selector 接觸面

[App.axaml](../../src/FreeformHelper.UI/App.axaml) 預設 Dark；Application.Resources 合併 Tokens，Application.Styles 依序載入 FluentTheme、AvaloniaEdit Fluent 主題、Icons、Controls。[Controls.axaml](../../src/FreeformHelper.UI/Styles/Controls.axaml) 再依 **Core → Overlay → Tab → Form → Panel → Scroll → Settings → PadInfo → Action** 載入 9 份檔案。順序及 selector 優先權是既有外觀契約；升級時須核對主題與覆寫的生效順序。

Styles 目錄共 **577 個 selector、7 個 ControlTemplate**；以下列出全部 12 份檔案。`/template/` 欄計含此語法的 selector 數，單一 selector 可跨兩層 template。

| 檔案（Styles/） | Selector | ControlTemplate | `:pointerover` | `/template/` | DynamicResource | StaticResource |
|---|---:|---:|---:|---:|---:|---:|
| [Controls.axaml](../../src/FreeformHelper.UI/Styles/Controls.axaml) | 0 | 0 | 0 | 0 | 0 | 0 |
| [Controls.Core.axaml](../../src/FreeformHelper.UI/Styles/Controls.Core.axaml) | 179 | 0 | 19 | 0 | 239 | 162 |
| [Controls.Overlay.axaml](../../src/FreeformHelper.UI/Styles/Controls.Overlay.axaml) | 8 | 0 | 0 | 1 | 6 | 8 |
| [Controls.Tab.axaml](../../src/FreeformHelper.UI/Styles/Controls.Tab.axaml) | 30 | 0 | 9 | 0 | 60 | 31 |
| [Controls.Form.axaml](../../src/FreeformHelper.UI/Styles/Controls.Form.axaml) | 34 | 0 | 0 | 3 | 17 | 42 |
| [Controls.Panel.axaml](../../src/FreeformHelper.UI/Styles/Controls.Panel.axaml) | 65 | 2 | 5 | 0 | 70 | 60 |
| [Controls.Scroll.axaml](../../src/FreeformHelper.UI/Styles/Controls.Scroll.axaml) | 59 | 0 | 14 | 42 | 28 | 66 |
| [Controls.Settings.axaml](../../src/FreeformHelper.UI/Styles/Controls.Settings.axaml) | 34 | 0 | 5 | 0 | 58 | 32 |
| [Controls.PadInfo.axaml](../../src/FreeformHelper.UI/Styles/Controls.PadInfo.axaml) | 46 | 1 | 9 | 0 | 69 | 39 |
| [Controls.Action.axaml](../../src/FreeformHelper.UI/Styles/Controls.Action.axaml) | 118 | 4 | 32 | 0 | 286 | 34 |
| [Icons.axaml](../../src/FreeformHelper.UI/Styles/Icons.axaml) | 4 | 0 | 0 | 0 | 5 | 1 |
| [Tokens.axaml](../../src/FreeformHelper.UI/Styles/Tokens.axaml) | 0 | 0 | 0 | 0 | 161 | 0 |
| **合計** | **577** | **7** | **93** | **46** | **999** | **475** |

Styles 以外另有 **30 個 selector**：[WorkspaceHeader.axaml](../../src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml) 的 UserControl.Styles 有 29 個，[CadLoadSpinnerWindow.axaml](../../src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml) 的 Window.Styles 有 1 個。這是局部樣式範圍，不等同於 Style 內嵌套 Style。

| 語法／結構 | 全部 AXAML 數量 | 具體參照與升級核對點 |
|---|---:|---|
| `:pointerover` | 93 個 selector | Action 的語意角色與 Core 的一般按鈕；核對 hover／disabled 的生效優先權。 |
| 其他 pseudo-class | `:pressed` 59、`:checked` 37、`:disabled` 20、`:focus` 19、`:selected` 2 | [Controls.Tab.axaml](../../src/FreeformHelper.UI/Styles/Controls.Tab.axaml) 的 `ToggleButton.shellTab:pointerover`、`:checked`，以及 Action 的組合狀態；分類有交集。 |
| `/template/` | 47 個 selector | Scroll 42、Form 3、Overlay 1、WorkspaceHeader 1；最直接依賴內建範本樹。 |
| `^`、`>` | 各 0 個 selector | 目前未用 nesting anchor 或直接子節點組合符；只計 Selector 屬性，沒有把 XML 的 `>` 算入。 |
| 巢狀 Style／ControlTheme 內 Style | 0 | 目前是平坦 Style；若新版遷移要求改成 nesting，既有字串 guards 的解析假設也須核對。 |
| 類別、逗號聯集、後代與屬性條件 | 已使用 | `Button.actionButton, ToggleButton.actionButton`、`ToolTip TextBlock`、`primitives|ScrollBar[Orientation=Vertical]`；含命名空間型別與 `#PART_*` 名稱。 |

**最高優先核對**：[Controls.Scroll.axaml:63](../../src/FreeformHelper.UI/Styles/Controls.Scroll.axaml#L63) 起隱藏四個 `PART_LineUpButton`／`Down`／`Left`／`Right`；全檔共 **8 個含 `#PART_*` 的 selector**，另一組位於 scrollDevCandidate。該檔也使用 `ScrollViewer /template/ ScrollBar /template/ RepeatButton` 與 `ListBox /template/ ScrollViewer /template/ ScrollBar` 的兩層穿透。Form 的 [ComboBox Popup 與 ContentPresenter](../../src/FreeformHelper.UI/Styles/Controls.Form.axaml#L138)、Overlay 的 [ToolTip ContentPresenter](../../src/FreeformHelper.UI/Styles/Controls.Overlay.axaml#L51)，以及 WorkspaceHeader 的 Thumb 範本穿透也依賴上游結構。這些名稱、樹形及 selector 規則皆待對照官方遷移指南確認。

## 內建控制項：完整範本替換與局部樣式覆寫

本庫 **沒有 ControlTheme、沒有其 BasedOn 繼承鏈**。目前的替換方式是 `Style → Setter Property="Template" → ControlTemplate`，不可將所有 Style 都當作完整範本替換。下表的 selector 數是全 69 份 AXAML 中含控制項型別名稱的 Style 數（含後代目標）。

| 內建控制項 | Selector 數 | 完整範本替換數 | 現況與優先程度 |
|---|---:|---:|---|
| Button | 173 | 2 | **高**：[Action:32、81](../../src/FreeformHelper.UI/Styles/Controls.Action.axaml#L32) 為 icon action 與一般 action／text／chip 提供 Border + ContentPresenter；另有大量狀態覆寫。 |
| ToggleButton | 161 | 2 | **高**：[Action:51、100](../../src/FreeformHelper.UI/Styles/Controls.Action.axaml#L51) 同類替換；核對 checked／disabled 與 TemplateBinding 保持一致。 |
| Window | 1 | 1 | **高**：[CadLoadSpinnerWindow:20](../../src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml#L20) 以 Panel + `PART_ContentPresenter` 替換無邊框透明視窗範本。 |
| TextBox | 21 | 0 | **中**：Form／Settings／Panel／WorkspaceHeader 覆寫字型、尺寸、focus、numberScrubber 與 consoleSearch；沿用內建範本。 |
| ComboBox | 16 | 0 | **高**：Form 的 regularLayerSelector 穿透 Popup／ContentPresenter，Popup 寬度綁 `$parent[ComboBox].Bounds.Width`；不是替換整個範本。 |
| ToolTip | 3 | 0 | **高**：Overlay 覆寫本體、文字後代與 template 內 ContentPresenter；必須搭配 tooltip-open smoke。 |
| ScrollViewer | 42 | 0 | **高**：Scroll 的 template／捲軸覆寫與 Viewport.Width binding；不是自製 ScrollViewer 範本。 |
| ScrollBar／Thumb／RepeatButton | 54／20／9 | 各 0 | **高**：ScrollBar 型別含 `primitives|` 前綴；Thumb 另見 WorkspaceHeader、RepeatButton 另見 Form 的 NumericUpDown。 |
| Expander | 0 | 0 | **中**：仍有 3 個實例，位於 [NotchExportSelectionWindow:527](../../src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml#L527)、[IndexMappingReportLeftPaneView:128](../../src/FreeformHelper.UI/Views/WorkspaceSections/IndexMappingReportLeftPaneView.axaml#L128)、[IndexMappingReportRightPaneView:212](../../src/FreeformHelper.UI/Views/WorkspaceSections/IndexMappingReportRightPaneView.axaml#L212)，沿用內建主題。 |
| TabControl／TabItem | 0／1 | 各 0 | **低**：兩者 AXAML 實例皆 0；Form:209 的 TabItem 只有字型規則。Controls.Tab 實際主要樣式化 ToggleButton 導覽，不是 TabControl 範本。 |
| DataGrid | 0 | 0 | **低**：AXAML 實例 0；此 XAML 盤點未發現其覆寫，不推論其他 C# 使用情形。 |
| ListBox／ListBoxItem | 9／4 | 各 0 | **中至高**：Core 的列表與 item 狀態；Scroll 穿透 workspaceDataList 的捲動範本。 |
| NumericUpDown／ToggleSwitch／CheckBox／MenuItem | 12／3／3／2 | 各 0 | **中**：Form／Settings 等局部覆寫；NumericUpDown 的內建 RepeatButton 穿透優先核對。 |

因此，**內建控制項完整替換合計 5 個**（Button 2、ToggleButton 2、Window 1）。另 **3 個**是本庫自訂控制項範本：[Controls.Panel:12](../../src/FreeformHelper.UI/Styles/Controls.Panel.axaml#L12) 的 HidePanelBlock、[Controls.Panel:55](../../src/FreeformHelper.UI/Styles/Controls.Panel.axaml#L55) 的 ReviewWorkspaceShell、[Controls.PadInfo:61](../../src/FreeformHelper.UI/Styles/Controls.PadInfo.axaml#L61) 的 PadInfoSectionFrame。它們依賴 TemplateBinding／TemplatedParent 等 XAML 行為，應列為中優先核對，但不是覆寫內建 Expander。全庫共有 **52 次 TemplateBinding**（Styles 51、spinner view 1）。以上新版相容性均待對照官方遷移指南確認。

## Binding 與資源慣例

### Compiled bindings 與 x:DataType

[UI.csproj](../../src/FreeformHelper.UI/FreeformHelper.UI.csproj) 與 [Directory.Build.props](../../Directory.Build.props) 未設定 `AvaloniaUseCompiledBindingsByDefault`；本機已還原 11.3.12 套件的 `buildTransitive/Avalonia.props:9` 預設為 false，`dotnet msbuild ... -getProperty:AvaloniaUseCompiledBindingsByDefault` 的實際評估也是 **false**。

- AXAML 內 **0 個 `x:CompileBindings`**、**0 次 `{CompiledBinding ...}`**、**0 次 `{ReflectionBinding ...}`**；一般 `{Binding ...}` 共 **1,526 次**（根目錄 12、Controls 41、Styles 17、Views 1,456）。這不是已全面使用 compiled bindings 的專案。
- **2 個 `x:DataType`，集中在 1 份檔案**：[NotchExportColumnFilterWindow:5](../../src/FreeformHelper.UI/Views/NotchExportColumnFilterWindow.axaml#L5) 的視窗型別，以及 [同檔:81](../../src/FreeformHelper.UI/Views/NotchExportColumnFilterWindow.axaml#L81) 的 Option DataTemplate。宣告資料型別不等同於已啟用 compiled binding。
- 全部 70 個 DataTemplate 中另有 **9 個普通 `DataType="{x:Type ...}"`**：MainWindow 5、PadInfoPopover 2、RightWorkflowInspectorView 2；它們是模板型別匹配，不能混算為 `x:DataType`。
- **中優先核對**：一般 Binding 的 `$parent[...]`、RelativeSource／TemplatedParent、DataTemplate 資料範圍與預設編譯設定。是否需要改動，待對照官方遷移指南確認；本次不補型別、不切換 binding 模式。

### Tokens、DynamicResource 與 StaticResource

[Tokens.axaml](../../src/FreeformHelper.UI/Styles/Tokens.axaml) 有 **Dark／Light 2 個 ThemeDictionary**，每個含 **176 個 Color + 1 個 BoxShadows**。共用層有 **540 個資源定義**：153 SolidColorBrush、2 LinearGradientBrush、270 Double、80 Thickness、12 CornerRadius、2 FontFamily、2 Decimal、19 GridLength。排除 Dark／Light 字典容器自身的 key 後，合計 **894 個定義、717 個不同 key**；兩個 theme 的相同 key 分別計定義。

| 範圍 | DynamicResource | StaticResource |
|---|---:|---:|
| App／MainWindow | 4 | 3 |
| Controls | 32 | 83 |
| Styles（含 Tokens） | 999 | 475 |
| Views（含子目錄） | 343 | 1,118 |
| **合計** | **1,378** | **1,679** |

既有模式多以 DynamicResource 取得可切換主題的 brush／color，以 StaticResource 取得固定 spacing、厚度、圓角、字型與尺寸；並非所有 DynamicResource 都是顏色，例如 Action 的 IconSizeMd。Tokens 內 **161 次 DynamicResource** 接到顏色或其他主題資源。依 [AGENTS.md](../../AGENTS.md) 與 [ui-density-token-rules.md](../guides/ui-density-token-rules.md)，Views／Controls 不新增 inline 顏色或尺寸，新 UI resource 使用 token 與 DynamicResource 支援 theme。

**中優先核對**：合併字典順序、ThemeDictionary 的查找／切換、DynamicResource 傳遞至 template 內容，以及 StaticResource 的解析時點。不要為升級預先把兩種寫法全面互換；新版是否改變上述機制，待對照官方遷移指南確認。

## 靜態 guards 與視覺基線的涵蓋範圍

### 解析 XAML 的 guards

[UiLayoutGuardTests.cs](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs) 有 **16 個 Fact**，涵蓋 viewport bounded width、動態文字換行／省略、共用 action 與字型、tooltip／對比、input border、popover、inline color、快捷鍵及 canvas token。這些測試讀取來源文字；不是完整 Avalonia XAML 編譯器或渲染驗證。

最直接的樣式 guard 是 [HoverAffordances_KeepContrastTooltipsAndInputBorderScope:467](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs#L467)：它呼叫 [AssertNoStyleSetterCollisions:918](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs#L918)，逐一掃 Styles 頂層 AXAML，檢查同一 Style 重複 Setter、Foreground 與附加 Foreground 衝突、disabled 狀態及 Dark／Light 對比（4.5）。另有 ComboBox Popup 契約（:549）、StylesOutsideTokens_DoNotUseInlineHexColors（:683）等。

[ExtractStyleBlocks:1053](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs#L1053) 用 Regex 擷取平坦的 `<Style Selector="...">...</Style>`；Setter、DynamicResource、Dark／Light 色碼及 brush mapping 也以 Regex／字串解析。**若未來改成 ControlTheme、Style nesting 或不同屬性順序／序列化形式，guards 可能需同步調整解析**，不能把格式造成的失敗直接當成新版 UI 缺陷，也不能以放寬斷言掩蓋行為差異。

[check-xaml-action-roles.ps1](../../scripts/tests/check-xaml-action-roles.ps1) 則透過 XmlReader／**XDocument** 載入並保留行號（:37 起），掃 Views／Controls（目前 55 份 AXAML）的 action classes，並解析 Controls.Action 的 Style 直接子 Setter。它有 **3 個明列 style contracts**（XAR101 icon 尺寸／裁切、XAR102 chip hover、XAR103 chip pressed，:368 起）；會檢查 selector 字串片段與指定 Setter，不解析完整 selector 語意或 runtime precedence。

**高優先核對**：parser 假設與 runtime-only style。Tooltip 既有雙重驗證為上述 static guard，加上 [HeadlessUiSmokeTests:69、103](../../tests/FreeformHelper.Tests/UI/Smoke/HeadlessUiSmokeTests.cs#L69) 的開啟及字串 tooltip 前景 smoke；該類共 **10 個 AvaloniaFact**，另含 cap tooltip、spinner 與 console 渲染。新版相容性待對照官方遷移指南確認。

### 兩種 visual snapshot

| 測試與基線 | 數量／方法 | 升級時的限制 |
|---|---|---|
| [UiVisualSnapshotTests](../../tests/FreeformHelper.Tests/UI/Snapshots/UiVisualSnapshotTests.cs)；[ui-visual-minimal-baseline.json](../../tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json) | **1 個 Fact、9 個來源檔**；只將 CRLF 轉 LF，計 SHA-256，精確比對。 | 是來源文字快照，不是圖片；套件升級即使改變渲染，來源未改也可能通過。 |
| [UiRenderedVisualSnapshotTests](../../tests/FreeformHelper.Tests/UI/Snapshots/UiRenderedVisualSnapshotTests.cs)；[ui-rendered-visual-baseline.json](../../tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json) | **1 個 AvaloniaFact、3 個 surface**；headless + Skia 擷取 BGRA，縮成 16×16 平均亮度 hash（256 bits），依 Hamming distance 門檻比對。 | 非逐像素零差異檢查；hash 可能漏掉局部字型／顏色差異，通過也須人工確認重點畫面。 |

來源基線的 9 個檔案：`Controls/WorkspaceHeader.axaml`、`Styles/Controls.axaml`、`Views/CanvasOverlayControls.axaml`、`ConsolePanel.axaml`、`FreeformHelperView.axaml`、`LeftDxfPanel.axaml`、`PadInfoPopover.axaml`、`RightWorkflowPanel.axaml`、`SettingsWindow.axaml`（皆以 `src/FreeformHelper.UI/` 為根）。**Tokens 與 Controls 的 9 份分檔沒有直接列入來源 hash 基線**；Controls.axaml 只 hash include 清單，沒有遞迴 hash include 內容。

| Rendered surface key | 固定尺寸 | 最大 Hamming distance |
|---|---:|---:|
| MainWindow.ConsoleExpanded | 1280×900 | 36 |
| SettingsWindow.Default | 960×760 | 24 |
| HowToUseView.Default | 1000×700 | 24 |

兩份 JSON 都是 version `1.0`，**沒有 checked-in PNG 基線**。Rendered test 也不保存 actual／diff 圖片；失敗訊息列 surface、expected／actual hash 與 distance。DevView、tooltip-open、ComboBox popup、Expander 展開及所有 action 狀態不在這 3 個 surface 的明列覆蓋清單中，須由 guards／smoke／人工預覽補足。

### 渲染差異如何審查

1. 保留原基線，在相同 OS、字型來源、theme、尺寸與測試模式下，先跑 11.3.12 的對照，再跑未來升級分支的 class-filtered guards、snapshots 與相關 smoke。透過 [UiBaselineUpdateMode.cs](../../tests/FreeformHelper.Tests/UI/Snapshots/UiBaselineUpdateMode.cs) 的 `FH_UI_BASELINE_MODE=dry-run` 可檢查而不寫基線；未設定時也是 Check。
2. 依失敗 surface／hash／distance 找到差異，人工擷取同條件前後畫面並比較字型、裁切、間距、狀態顏色與 template 內容。現有測試不產生圖片 diff，不能聲稱已有自動圖片審查流程。補看 [DevView](../../src/FreeformHelper.UI/Views/DevView.axaml) 的 Action role laboratory，依 [ui-action-role-visual-qa.md](../guides/ui-action-role-visual-qa.md) 檢查 checked／disabled／hover、console toolbar、viewport actions 與 passive badges，並開啟 popup／tooltip／Expander。
3. 將差異分為範本／selector 未命中、resource／theme、字型環境或經確認的新版渲染差異，附來源與前後證據；Avalonia 12 原因待對照官方遷移指南確認。依 [1.3.x roadmap](../guides/refactor-roadmap-1.3.x.md) 的 no-visual-change 與 owner 視覺決策，修正非預期差異；必要外觀變更先取得相應決定，不用更新 expected 取得綠燈。
4. 只有明確接受的 UI 差異才另案使用既有 [update-ui-baseline.ps1](../../scripts/tests/update-ui-baseline.ps1) 的 Apply／`FH_UI_BASELINE_MODE=apply`，檢視兩份 JSON 的差異並重跑驗證。Apply 並非審查，rendered Apply 在超出門檻時會更新所有 surface hash；本次完全不執行 Apply。該腳本首輪測試可能觸發 build／restore，不能直接套用於本次禁止 restore 的沙箱。

## 字型與最可能需要調整的部分

[Tokens:673–674](../../src/FreeformHelper.UI/Styles/Tokens.axaml#L673) 定義 **2 個 FontFamily token**：`FontFamilyUi = Segoe UI Variable Text`，`FontFamilyCode = Consolas, Cascadia Mono, Segoe UI Variable Text`。Form 等樣式通常 StaticResource 取 Ui 字型；[Controls.Core:826](../../src/FreeformHelper.UI/Styles/Controls.Core.axaml#L826) 使用 FontFamilyCode，而 [ConsolePanel:138](../../src/FreeformHelper.UI/Views/ConsolePanel.axaml#L138) 的 editor 直接寫同一候選字型清單，並非引用 token。不能只看 token 名稱就認定實際渲染採 Windows Segoe。

目前 production 字型來源由 [Program.cs:74–77](../../src/FreeformHelper.UI/Program.cs#L74) 與 [AppFontBootstrapper.cs](../../src/FreeformHelper.UI/Services/AppFontBootstrapper.cs) 提供：system font source 指向 **`avares://Avalonia.Fonts.Inter/Assets`**，註冊 **1 個 InterFontCollection**，並 WithInterFont；default family 為 Inter，另有 **2 個 mapping** 將 Segoe UI Variable Text／Segoe UI 導到 Inter。Code token 的 Consolas／Cascadia Mono 候選與 CJK glyph fallback 仍需以實際環境驗證，不假定全部字形來自 Inter。

[AvaloniaTestApp.cs:14–27](../../tests/FreeformHelper.Tests/UI/TestHost/AvaloniaTestApp.cs#L14) 的 headless host 使用 Skia、`UseHeadlessDrawing=false`、相同 FontManagerOptions，並在 **`fonts:SystemFonts`** 註冊指向同一 Inter Assets 的 **1 個 EmbeddedFontCollection**，再 WithInterFont。production 與 headless 的註冊方式不同；本文件只標記字型來源與基線關係，不展開 C# API 遷移。字型 collection／system source、fallback、glyph metrics 與新版渲染行為皆待對照官方遷移指南確認。

| 優先核對 | 可能需要改動的部位 | 判斷依據／驗證 |
|---|---|---|
| **最高** | Scroll 的 `/template/`、`PART_*`；ComboBox Popup、ToolTip ContentPresenter、WorkspaceHeader 的 Slider Thumb | 依賴上游範本形狀與名稱；先核對遷移指南及實際新版範本，再跑 static guard、popup／tooltip smoke 與 Dev 預覽。 |
| **高** | 5 個內建控制項完整替換範本、action 狀態／載入順序 | 直接覆寫內建視覺與狀態呈現；核對 TemplateBinding、checked／disabled／focus 與透明 spinner。 |
| **高** | Regex style guards、rendered 基線與字型 host | parser 只支援既有平坦寫法；渲染與字型變化可能影響 hash，不能自動接受或提高門檻。 |
| **中** | 3 個自訂控制項範本、Binding／x:DataType、theme resource 查找、Expander | 沿用 XAML／resource／內建 theme 行為；只在指南或實際測試證明需要時修改。 |
| **低** | 未使用的 `^`／`>`／ControlTheme、TabControl／DataGrid XAML 覆寫 | 目前計數為 0，沒有既有語法／範本要遷移；不為假設中的未來需求新增機制。 |

以上是待核對清單，**不構成已確認的 Avalonia 12 修改需求**。此次只新增本文件；沒有修改程式、XAML、套件、build 設定、基線、TODO 或 roadmap，也沒有執行官方指南查核或視覺升級。
