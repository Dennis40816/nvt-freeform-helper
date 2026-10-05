# Avalonia 12 upgrade preparation: XAML, styles, and visual baseline inventory (2026-10)

## Status and counting scope

This document only inventories upgrade touchpoints; **no Avalonia 11 → 12 upgrade has been performed**. The version decision in [TODO.md](../../TODO.md) schedules the upgrade **before 1.3.5**; it is not being performed now. Avalonia, Desktop, Fluent, Inter, Headless, and Headless.XUnit in [Directory.Packages.props](../../Directory.Packages.props) remain at **11.3.12**. AvaloniaEdit is separately pinned to 11.4.1 and should not be conflated with the core version.

The inventory baseline is `7477b07d`, limited to `src/FreeformHelper.UI/**/*.axaml`, related tests, and their public baselines. Dependencies have been checked against [project-dependency-graph.md](../generated/project-dependency-graph.md) and the current ProjectReference entries, with no differences requiring regeneration. No private example data was read. The sandbox has no network access, so the official Avalonia 12 migration guide was not obtained; the risks below are ranked by coupling in this repository, **not confirmed Avalonia 12 breaking changes**. All judgments involving new-version behavior are marked “**pending confirmation against the official migration guide (to check against the official migration guide)**”.

Counts are based on XML elements and attribute values, excluding comments: **69 AXAML files** in total (root 2, Styles 12, Controls 4, Views 51), **607 `<Style Selector>` elements**, **0 `<ControlTheme>` elements**, **8 `<ControlTemplate>` elements**, and **70 `<DataTemplate>` elements**. Comma-separated selectors count as one Style; when a selector contains multiple control types or syntax forms, it is counted in each category, so categories cannot be added to obtain the total. Resource/binding counts are occurrences of markup extensions, not counts of distinct keys/paths.

## Style system and selector touchpoints

[App.axaml](../../src/FreeformHelper.UI/App.axaml) defaults to Dark; Application.Resources merges Tokens, and Application.Styles loads FluentTheme, the AvaloniaEdit Fluent theme, Icons, and Controls in that order. [Controls.axaml](../../src/FreeformHelper.UI/Styles/Controls.axaml) then loads 9 files in the order **Core → Overlay → Tab → Form → Panel → Scroll → Settings → PadInfo → Action**. The order and selector precedence are existing appearance contracts; the effective order of themes and overrides must be checked during the upgrade.

The Styles directory has **577 selectors and 7 ControlTemplate elements**; all 12 files are listed below. The `/template/` column counts selectors containing this syntax; a single selector can cross two template levels.

| File (Styles/) | Selector | ControlTemplate | `:pointerover` | `/template/` | DynamicResource | StaticResource |
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
| **Total** | **577** | **7** | **93** | **46** | **999** | **475** |

There are another **30 selectors** outside Styles: 29 in UserControl.Styles in [WorkspaceHeader.axaml](../../src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml), and 1 in Window.Styles in [CadLoadSpinnerWindow.axaml](../../src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml). These are local style scopes, which are not equivalent to Styles nested inside a Style.

| Syntax/structure | Count across all AXAML | Specific references and upgrade checks |
|---|---:|---|
| `:pointerover` | 93 selectors | Action semantic roles and Core general buttons; check hover/disabled precedence. |
| Other pseudo-classes | `:pressed` 59, `:checked` 37, `:disabled` 20, `:focus` 19, `:selected` 2 | `ToggleButton.shellTab:pointerover` and `:checked` in [Controls.Tab.axaml](../../src/FreeformHelper.UI/Styles/Controls.Tab.axaml), and combined states in Action; the categories overlap. |
| `/template/` | 47 selectors | Scroll 42, Form 3, Overlay 1, WorkspaceHeader 1; the most direct dependency on built-in template trees. |
| `^`, `>` | 0 selectors each | No nesting anchors or direct-child combinators are currently used; only Selector attributes are counted, excluding XML `>` characters. |
| Nested Styles/Styles inside ControlTheme | 0 | Styles are currently flat; if migration to the new version requires nesting, the parsing assumptions of existing string guards must also be checked. |
| Classes, comma unions, descendants, and attribute conditions | In use | `Button.actionButton, ToggleButton.actionButton`, `ToolTip TextBlock`, `primitives|ScrollBar[Orientation=Vertical]`; includes namespaced types and `#PART_*` names. |

**Highest-priority check**: starting at [Controls.Scroll.axaml:63](../../src/FreeformHelper.UI/Styles/Controls.Scroll.axaml#L63), the four `PART_LineUpButton`/`Down`/`Left`/`Right` buttons are hidden; the file has **8 selectors containing `#PART_*`** in total, with the other set in scrollDevCandidate. The file also uses two-level traversal through `ScrollViewer /template/ ScrollBar /template/ RepeatButton` and `ListBox /template/ ScrollViewer /template/ ScrollBar`. The [ComboBox Popup and ContentPresenter](../../src/FreeformHelper.UI/Styles/Controls.Form.axaml#L138) in Form, the [ToolTip ContentPresenter](../../src/FreeformHelper.UI/Styles/Controls.Overlay.axaml#L51) in Overlay, and the Thumb template traversal in WorkspaceHeader also depend on upstream structure. These names, tree structures, and selector rules all await confirmation against the official migration guide.

## Built-in controls: full template replacements and local style overrides

This repository has **no ControlTheme and no BasedOn inheritance chain for it**. The current replacement mechanism is `Style → Setter Property="Template" → ControlTemplate`; not every Style should be treated as a full template replacement. The selector counts below are the counts of Styles containing the control type name across all 69 AXAML files (including descendant targets).

| Built-in control | Selector count | Full template replacement count | Current state and priority |
|---|---:|---:|---|
| Button | 173 | 2 | **High**: [Action:32, 81](../../src/FreeformHelper.UI/Styles/Controls.Action.axaml#L32) provides Border + ContentPresenter for icon actions and general action/text/chip controls; there are also many state overrides. |
| ToggleButton | 161 | 2 | **High**: [Action:51, 100](../../src/FreeformHelper.UI/Styles/Controls.Action.axaml#L51) uses the same type of replacement; check that checked/disabled states and TemplateBinding remain consistent. |
| Window | 1 | 1 | **High**: [CadLoadSpinnerWindow:20](../../src/FreeformHelper.UI/Views/CadLoadSpinnerWindow.axaml#L20) replaces the borderless transparent window template with Panel + `PART_ContentPresenter`. |
| TextBox | 21 | 0 | **Medium**: Form/Settings/Panel/WorkspaceHeader override fonts, sizes, focus, numberScrubber, and consoleSearch; they retain the built-in template. |
| ComboBox | 16 | 0 | **High**: regularLayerSelector in Form traverses Popup/ContentPresenter, with Popup width bound to `$parent[ComboBox].Bounds.Width`; it does not replace the entire template. |
| ToolTip | 3 | 0 | **High**: Overlay overrides the control itself, text descendants, and ContentPresenter inside the template; tooltip-open smoke must accompany it. |
| ScrollViewer | 42 | 0 | **High**: template/scrollbar overrides and Viewport.Width binding in Scroll; this is not a custom ScrollViewer template. |
| ScrollBar/Thumb/RepeatButton | 54/20/9 | 0 each | **High**: the ScrollBar type includes the `primitives|` prefix; Thumb also appears in WorkspaceHeader, and RepeatButton also appears in NumericUpDown in Form. |
| Expander | 0 | 0 | **Medium**: 3 instances remain, in [NotchExportSelectionWindow:527](../../src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml#L527), [IndexMappingReportLeftPaneView:128](../../src/FreeformHelper.UI/Views/WorkspaceSections/IndexMappingReportLeftPaneView.axaml#L128), and [IndexMappingReportRightPaneView:212](../../src/FreeformHelper.UI/Views/WorkspaceSections/IndexMappingReportRightPaneView.axaml#L212), using the built-in theme. |
| TabControl/TabItem | 0/1 | 0 each | **Low**: both have 0 AXAML instances; TabItem in Form:209 has only a font rule. Controls.Tab mainly styles ToggleButton navigation, not TabControl templates. |
| DataGrid | 0 | 0 | **Low**: 0 AXAML instances; this XAML inventory found no overrides and makes no inference about other C# usage. |
| ListBox/ListBoxItem | 9/4 | 0 each | **Medium to high**: list and item states in Core; Scroll traverses the scrolling template of workspaceDataList. |
| NumericUpDown/ToggleSwitch/CheckBox/MenuItem | 12/3/3/2 | 0 each | **Medium**: local overrides in Form/Settings and elsewhere; prioritize checking traversal into NumericUpDown's built-in RepeatButton. |

Thus, there are **5 full template replacements for built-in controls** (Button 2, ToggleButton 2, Window 1). Another **3** are templates for this repository's custom controls: HidePanelBlock in [Controls.Panel:12](../../src/FreeformHelper.UI/Styles/Controls.Panel.axaml#L12), ReviewWorkspaceShell in [Controls.Panel:55](../../src/FreeformHelper.UI/Styles/Controls.Panel.axaml#L55), and PadInfoSectionFrame in [Controls.PadInfo:61](../../src/FreeformHelper.UI/Styles/Controls.PadInfo.axaml#L61). They depend on XAML behavior such as TemplateBinding/TemplatedParent and should be medium-priority checks, but they do not override the built-in Expander. The repository has **52 TemplateBinding occurrences** in total (Styles 51, spinner view 1). All compatibility with the new version described above awaits confirmation against the official migration guide.

## Binding and resource conventions

### Compiled bindings and x:DataType

[UI.csproj](../../src/FreeformHelper.UI/FreeformHelper.UI.csproj) and [Directory.Build.props](../../Directory.Build.props) do not set `AvaloniaUseCompiledBindingsByDefault`; `buildTransitive/Avalonia.props:9` in the locally restored 11.3.12 package defaults to false, and the actual evaluation from `dotnet msbuild ... -getProperty:AvaloniaUseCompiledBindingsByDefault` is also **false**.

- AXAML contains **0 `x:CompileBindings` attributes**, **0 `{CompiledBinding ...}` occurrences**, and **0 `{ReflectionBinding ...}` occurrences**; ordinary `{Binding ...}` occurs **1,526 times** (root 12, Controls 41, Styles 17, Views 1,456). This project does not already use compiled bindings throughout.
- **2 `x:DataType` attributes, concentrated in 1 file**: the window type at [NotchExportColumnFilterWindow:5](../../src/FreeformHelper.UI/Views/NotchExportColumnFilterWindow.axaml#L5), and the Option DataTemplate at [the same file:81](../../src/FreeformHelper.UI/Views/NotchExportColumnFilterWindow.axaml#L81). Declaring a data type does not mean compiled binding is enabled.
- Across all 70 DataTemplate elements, there are also **9 ordinary `DataType="{x:Type ...}"` attributes**: MainWindow 5, PadInfoPopover 2, RightWorkflowInspectorView 2; these match template types and must not be counted as `x:DataType`.
- **Medium-priority check**: `$parent[...]` in ordinary Binding, RelativeSource/TemplatedParent, DataTemplate data scope, and default compilation settings. Whether changes are needed awaits confirmation against the official migration guide; this task adds no types and does not switch binding modes.

### Tokens, DynamicResource, and StaticResource

[Tokens.axaml](../../src/FreeformHelper.UI/Styles/Tokens.axaml) has **2 ThemeDictionary elements, Dark/Light**, each containing **176 Color + 1 BoxShadows**. The shared layer has **540 resource definitions**: 153 SolidColorBrush, 2 LinearGradientBrush, 270 Double, 80 Thickness, 12 CornerRadius, 2 FontFamily, 2 Decimal, and 19 GridLength. Excluding the keys of the Dark/Light dictionary containers themselves, there are **894 definitions and 717 distinct keys** in total; identical keys in the two themes count as separate definitions.

| Scope | DynamicResource | StaticResource |
|---|---:|---:|
| App／MainWindow | 4 | 3 |
| Controls | 32 | 83 |
| Styles (including Tokens) | 999 | 475 |
| Views (including subdirectories) | 343 | 1,118 |
| **Total** | **1,378** | **1,679** |

Existing patterns mostly use DynamicResource for brushes/colors that support theme switching, and StaticResource for fixed spacing, thickness, corner radii, fonts, and sizes; not every DynamicResource is a color, as illustrated by IconSizeMd in Action. The **161 DynamicResource occurrences** in Tokens refer to colors or other theme resources. Under [AGENTS.md](../../AGENTS.md) and [ui-density-token-rules.md](../guides/ui-density-token-rules.md), Views/Controls must not add inline colors or sizes, and new UI resources use tokens and DynamicResource for theme support.

**Medium-priority check**: merged dictionary order, ThemeDictionary lookup/switching, DynamicResource propagation into template content, and StaticResource resolution timing. Do not preemptively swap the two forms throughout the repository for the upgrade; whether the new version changes these mechanisms awaits confirmation against the official migration guide.

## Coverage of static guards and visual baselines

### Guards that parse XAML

[UiLayoutGuardTests.cs](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs) has **16 Facts**, covering viewport bounded width, dynamic text wrapping/ellipsis, shared actions and fonts, tooltips/contrast, input borders, popovers, inline colors, keyboard shortcuts, and canvas tokens. These tests read source text; they are not a full Avalonia XAML compiler or rendering verification.

The most direct style guard is [HoverAffordances_KeepContrastTooltipsAndInputBorderScope:467](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs#L467): it calls [AssertNoStyleSetterCollisions:918](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs#L918), scans top-level AXAML files in Styles, and checks duplicate Setters within a Style, conflicts between Foreground and attached Foreground, disabled states, and Dark/Light contrast (4.5). Other checks include the ComboBox Popup contract (:549) and StylesOutsideTokens_DoNotUseInlineHexColors (:683).

[ExtractStyleBlocks:1053](../../tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs#L1053) uses Regex to extract flat `<Style Selector="...">...</Style>` blocks; Setters, DynamicResource, Dark/Light color codes, and brush mappings are also parsed with Regex/strings. **If the styles later move to ControlTheme, Style nesting, or a different attribute order/serialization format, guard parsing may need to be updated as well**. Formatting-related failures must not be treated directly as new-version UI defects, and assertions must not be relaxed to conceal behavioral differences.

[check-xaml-action-roles.ps1](../../scripts/tests/check-xaml-action-roles.ps1) loads files through XmlReader/**XDocument** and preserves line numbers (from :37), scans action classes in Views/Controls (currently 55 AXAML files), and parses Setters that are direct children of Styles in Controls.Action. It has **3 explicit style contracts** (XAR101 icon size/clipping, XAR102 chip hover, XAR103 chip pressed, from :368); it checks selector string fragments and specific Setters, without parsing full selector semantics or runtime precedence.

**High-priority check**: parser assumptions and runtime-only styles. Existing dual verification for tooltips consists of the static guard above plus the tooltip-open and string-tooltip foreground smoke tests in [HeadlessUiSmokeTests:69, 103](../../tests/FreeformHelper.Tests/UI/Smoke/HeadlessUiSmokeTests.cs#L69); the class has **10 AvaloniaFacts** in total, also covering cap tooltips, the spinner, and console rendering. New-version compatibility awaits confirmation against the official migration guide.

### Two types of visual snapshots

| Tests and baselines | Count/method | Limitations during the upgrade |
|---|---|---|
| [UiVisualSnapshotTests](../../tests/FreeformHelper.Tests/UI/Snapshots/UiVisualSnapshotTests.cs); [ui-visual-minimal-baseline.json](../../tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json) | **1 Fact, 9 source files**; only converts CRLF to LF, computes SHA-256, and compares exactly. | These are source text snapshots, not images; even if a package upgrade changes rendering, the test may pass if the source is unchanged. |
| [UiRenderedVisualSnapshotTests](../../tests/FreeformHelper.Tests/UI/Snapshots/UiRenderedVisualSnapshotTests.cs); [ui-rendered-visual-baseline.json](../../tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json) | **1 AvaloniaFact, 3 surfaces**; captures BGRA using headless + Skia, reduces it to a 16×16 average-luminance hash (256 bits), and compares against Hamming distance thresholds. | This is not a pixel-by-pixel zero-difference check; the hash may miss local font/color differences, so key screens still require manual confirmation even when the test passes. |

The source baseline contains 9 files: `Controls/WorkspaceHeader.axaml`, `Styles/Controls.axaml`, `Views/CanvasOverlayControls.axaml`, `ConsolePanel.axaml`, `FreeformHelperView.axaml`, `LeftDxfPanel.axaml`, `PadInfoPopover.axaml`, `RightWorkflowPanel.axaml`, and `SettingsWindow.axaml` (all relative to `src/FreeformHelper.UI/`). **Tokens and the 9 split Controls files are not directly included in the source hash baseline**; Controls.axaml hashes only the include list, without recursively hashing included content.

| Rendered surface key | Fixed size | Maximum Hamming distance |
|---|---:|---:|
| MainWindow.ConsoleExpanded | 1280×900 | 36 |
| SettingsWindow.Default | 960×760 | 24 |
| HowToUseView.Default | 1000×700 | 24 |

Both JSON files are version `1.0`, with **no checked-in PNG baselines**. The rendered test also does not save actual/diff images; failure messages list the surface, expected/actual hashes, and distance. DevView, tooltip-open, ComboBox popup, expanded Expander, and all action states are not in the explicit coverage list of these 3 surfaces and must be covered by guards/smoke/manual previews.

### How to review rendering differences

1. Preserve the original baselines. Under the same OS, font sources, theme, dimensions, and test mode, first run the 11.3.12 comparison, then run class-filtered guards, snapshots, and related smoke tests on the future upgrade branch. `FH_UI_BASELINE_MODE=dry-run` in [UiBaselineUpdateMode.cs](../../tests/FreeformHelper.Tests/UI/Snapshots/UiBaselineUpdateMode.cs) allows checking without writing baselines; the unset default is also Check.
2. Locate differences using the failed surface/hash/distance, manually capture before/after screens under the same conditions, and compare fonts, clipping, spacing, state colors, and template content. Existing tests do not produce image diffs, so an automated image review process cannot be claimed. Also preview the Action role laboratory in [DevView](../../src/FreeformHelper.UI/Views/DevView.axaml), check checked/disabled/hover states, the console toolbar, viewport actions, and passive badges according to [ui-action-role-visual-qa.md](../guides/ui-action-role-visual-qa.md), and open popups/tooltips/Expanders.
3. Classify differences as template/selector mismatches, resource/theme issues, font environment differences, or confirmed new-version rendering differences, with sources and before/after evidence; Avalonia 12 causes await confirmation against the official migration guide. Fix unexpected differences under the no-visual-change requirement and owner visual decisions in the [1.3.x roadmap](../guides/refactor-roadmap-1.3.x.md); obtain the corresponding decision first for necessary appearance changes, rather than updating expected values to make tests pass.
4. Only explicitly accepted UI differences may use Apply/`FH_UI_BASELINE_MODE=apply` in the existing [update-ui-baseline.ps1](../../scripts/tests/update-ui-baseline.ps1) in a separate task; review the differences in both JSON files and rerun validation. Apply is not a review; rendered Apply updates all surface hashes when the threshold is exceeded. This task does not run Apply at all. The script's first test run may trigger build/restore, so it cannot be used directly in this sandbox, where restore is prohibited.

## Fonts and the areas most likely to need changes

[Tokens:673–674](../../src/FreeformHelper.UI/Styles/Tokens.axaml#L673) defines **2 FontFamily tokens**: `FontFamilyUi = Segoe UI Variable Text` and `FontFamilyCode = Consolas, Cascadia Mono, Segoe UI Variable Text`. Styles such as Form usually retrieve the Ui font through StaticResource; [Controls.Core:826](../../src/FreeformHelper.UI/Styles/Controls.Core.axaml#L826) uses FontFamilyCode, while the editor in [ConsolePanel:138](../../src/FreeformHelper.UI/Views/ConsolePanel.axaml#L138) specifies the same candidate font list directly instead of referencing the token. Token names alone do not establish that actual rendering uses Windows Segoe.

Production font sources currently come from [Program.cs:74–77](../../src/FreeformHelper.UI/Program.cs#L74) and [AppFontBootstrapper.cs](../../src/FreeformHelper.UI/Services/AppFontBootstrapper.cs): the system font source points to **`avares://Avalonia.Fonts.Inter/Assets`**, **1 InterFontCollection** is registered, and WithInterFont is used; the default family is Inter, with **2 additional mappings** from Segoe UI Variable Text/Segoe UI to Inter. The Consolas/Cascadia Mono candidates in the Code token and CJK glyph fallback still require validation in the actual environment; not all glyphs are assumed to come from Inter.

The headless host in [AvaloniaTestApp.cs:14–27](../../tests/FreeformHelper.Tests/UI/TestHost/AvaloniaTestApp.cs#L14) uses Skia, `UseHeadlessDrawing=false`, and the same FontManagerOptions, registers **1 EmbeddedFontCollection** pointing to the same Inter Assets under **`fonts:SystemFonts`**, and then uses WithInterFont. Production and headless registration differ; this document only identifies font sources and their relationship to the baselines, without expanding into C# API migration. Font collections/system sources, fallback, glyph metrics, and new-version rendering behavior all await confirmation against the official migration guide.

| Check priority | Areas that may need changes | Basis/validation |
|---|---|---|
| **Highest** | Scroll `/template/` and `PART_*`; ComboBox Popup, ToolTip ContentPresenter, and WorkspaceHeader Slider Thumb | Depends on upstream template structure and names; first check the migration guide and actual new-version templates, then run static guards, popup/tooltip smoke tests, and Dev previews. |
| **High** | 5 full replacement templates for built-in controls, action states/load order | Directly overrides built-in visuals and state presentation; check TemplateBinding, checked/disabled/focus, and the transparent spinner. |
| **High** | Regex style guards, rendered baselines, and font hosts | The parser supports only the existing flat form; rendering and font changes may affect hashes and must not be automatically accepted or accommodated by raising thresholds. |
| **Medium** | 3 custom control templates, Binding/x:DataType, theme resource lookup, Expander | Uses existing XAML/resource/built-in theme behavior; change only when the guide or actual tests prove it necessary. |
| **Low** | Unused `^`/`>`/ControlTheme, TabControl/DataGrid XAML overrides | Current counts are 0, with no existing syntax/templates to migrate; do not add mechanisms for hypothetical future needs. |

The above is a checklist awaiting verification, **not confirmed Avalonia 12 change requirements**. This task only adds this document; it does not modify code, XAML, packages, build settings, baselines, TODO, or the roadmap, and does not perform an official-guide review or visual upgrade.
