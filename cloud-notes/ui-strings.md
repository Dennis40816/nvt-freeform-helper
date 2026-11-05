# UI string audit: hard-coded user-visible text (origin/main)

Analysis only. No source files were changed. Branch `cloud/ui-strings`, based on `origin/main` @ `5e48bec` (2026-11-05).

## 1. Does the repo have a resource / localization mechanism?

**No.** Every user-visible string is written directly in `.axaml` or C# code. What was checked:

- No `.resx`, `*.resources`, `.po`, or string-table JSON/YAML files in `git ls-files`.
- No `ResourceManager`, `IStringLocalizer`, `Localiz*`, `Strings.*` class, or `UICulture` use under `src/`. The only hit for "localized" is a comment in `Services/StartupPerfTracker.cs:9`.
- No `<x:String>` resource dictionary entries in any `.axaml`.
- `FreeformHelper.UI.csproj:32` only declares `<AvaloniaResource Include="Assets\**" />` (fonts / icons).
- `{x:Static ...}` in AXAML is used for icon glyphs (`icons:IconGlyphs.*`) and enum values, not text.
- `Styles/Tokens.axaml` is the design-token store (colors, sizes, brushes). It holds no UI text.
- No `.po` / `.xlf` files either. No Markdown file in the repo mentions localization, i18n, or a UI language plan. The only related policy is the owner decision (2026-10-05) that documents are English by default and `README.md` has a Traditional Chinese twin (`README.zh-TW.md`, which describes the UI in Chinese prose but quotes the English labels).
- Fonts are ready for Chinese text: `README.md:73` states `Noto Sans TC` is bundled as the Chinese fallback, so a Traditional Chinese UI would not need a font change.

Consequence: there is nothing to "move to". Any cleanup first needs a decision on the mechanism (for example `.resx` + a `{loc:...}` markup extension, or an Avalonia `ResourceDictionary` of `x:String`). This note only inventories the strings.

## 2. Scope, method, and limits

- **AXAML** (sections 5 and 6): all `.axaml` under `src/` except `Styles/**`. Extracted attributes: `Text`, `Content`, `Header`, `Title`, `ToolTip.Tip`, `Tip`, `PlaceholderText`, `Watermark`, `OnContent`, `OffContent`, `Label`, `Description`, `Hint`, `AutomationProperties.Name`; inner text of elements; and the literal part of `StringFormat` / `FallbackValue` / `TargetNullValue` inside bindings. Pure bindings (`{Binding X}`) are not strings and are skipped. `line` is the line of the attribute, not the element.
- **C#** (sections 7 to 9): string literals in `src/FreeformHelper.UI/**/*.cs`, filtered by script, then skimmed by hand. Pure keys, regexes, IPC/CLI protocol names (`Services/RuntimeQuery*`, `ConsoleSourceNames`, `CadLoadSpinner*`), canvas/token resource keys (`Controls/PadCanvas*`), startup perf marks and attribute text were removed. The "where it shows" column is derived from the file name and is approximate (see the hint under each table).
- **Console panel chrome** (filter chips, `Clear`, source names such as `Workflow`) comes from the external `Nvt.Core.Avalonia.LogConsole` package and `Services/ConsoleSourceNames.cs`; the package texts are not inventoried here.
- **Not covered**: strings in `src/FreeformHelper.Application`, `.Domain`, `.Infrastructure` that reach the UI through exception messages or result objects (for example parse and validation errors); strings assembled from several fragments at runtime; text in `Assets/`. These are worth a second pass if localization is actually planned.
- **Extraction is heuristic.** Expect a few non-UI strings in section 7 and a few missing multi-fragment strings. Counts are therefore "about", not exact.
- The `Dev` tab and `Views/DevView.axaml` are shown only when `AppBuildChannel.IsDevelopmentBuild` is true (`App.axaml.cs:74`, `MainWindow.axaml:67-70`). They are listed separately (section 6) and excluded from the counts below.
- **Console panel**: NLog `minlevel="Info"` writes to the in-app console (`NLog.config:29`), so `Info/Warn/Error` log messages are user-visible text too (section 9). `Debug` is only visible if the user lowers the log level in Settings.

### Counts

| Source | Approx. entries |
|---|---:|
| `.axaml` (excluding Dev page) | 1041 |
| `.axaml` Dev page (dev builds only) | 125 |
| C# display text, status messages, dialogs | 1662 |
| C# console log messages (Info/Warn/Error) | 105 |

AXAML entries per screen (top 12):

| Screen | Entries |
|---|---:|
| Workspace tab | 306 |
| Settings window | 194 |
| Export Notch Rows window | 116 |
| Pad info popover | 83 |
| Simulation tab | 79 |
| How To Use tab | 72 |
| Coordinate tab | 63 |
| Verification Workspace | 50 |
| DXF Edit Details | 20 |
| Export DXF layer image | 15 |
| Cascade IC details | 10 |
| Coordinate detail | 10 |

## 3. Inconsistent wording for the same action or concept

Paths are `path:line`. Wording is quoted exactly. "Suggested canonical form" lines are proposals for the owner to accept or change, not decisions.

### 3.1 Open / Load / Import (DXF, project, CSV)

| Where | Wording |
|---|---|
| `Views/FreeformHelperView.axaml:38-40` menu | `Open DXF`, `Save Project`, `Load Project` |
| `Views/FreeformHelperView.Pickers.cs:179` / `:196` file dialogs | `Open DXF`, **`Open Project`** (menu says `Load Project`) |
| `Services/CadLoadWorkflowService.cs:26,30`; `ViewModels/FreeformHelperViewModel.CadLoad.cs:131` | operation `Open DXF`, status `Importing DXF...`, result `DXF imported` / `DXF reloaded` |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:106,178,254` | `Load cancelled.`, `Load project`, `Project loaded: ...` |
| `Views/LeftDxfPanel.axaml:24` | section called `Import` (about the same DXF) |
| `Views/WorkflowSteps/RightWorkflowStep1View.axaml:78` / `Pickers.cs:210` | button `Import mask`; dialog `Import Regular Visibility Mask (SeeRegular.csv)` |
| `Views/WorkspaceSections/SimulationWorkspaceControlsPaneView.axaml:155` / `Views/SimulationWorkspaceView.axaml.cs:215` | `Import CSV`; dialog `Import Diff CSV` |
| `docs/guides/app-user-manual.md:16,26` | "Load DXF" (UI says `Open DXF`) |

Same action, three verbs (Open / Load / Import). Suggested canonical form: `Open` for DXF and project files (menu, dialog, status), `Import` for auxiliary data files (mask, CSV). Make menu `Load Project` and dialog `Open Project` match.

### 3.2 Export vs Save (file output)

| Where | Wording |
|---|---|
| `Views/FreeformHelperView.axaml:43-46` menu | `Export Notch`, `Export DXF (Visible Layers)`, `Export DXF (All Layers)`, `Export DXF Layer Image` |
| `Views/FreeformHelperView.Pickers.cs:255` | dialog for Export Notch is titled **`Save Notch Table`** |
| `Views/FreeformHelperView.Pickers.cs:315-317` | generic dialog titles **`Save Image`**, **`Save DXF`**, **`Save Export File`** |
| `Views/NotchExportSelectionWindow.axaml:17-18` | window `Export Notch Rows`; button `Export selected` / `Export {n} rows` (`Services/NotchExportSelectionSummaryProjector.cs:47`) |
| `Views/WorkflowSteps/RightWorkflowStep5View.axaml:7,85` | card `Step 5 · Notch export`, button `Export` |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:26` | operation name `Export notch table` |
| `Views/DxfLayerImageExportWindow.axaml:12,21` | title `Export DXF layer image` (menu uses Title Case `Export DXF Layer Image`) |
| `Views/CoordinatePlannerWorkspaceView.axaml.cs:209`; `ViewModels/CoordinatePlannerWorkspaceViewModel.Operations.cs:67,80` | `Export Coordinate Preview`, `Export Coordinate Artifacts CSV/JSON` |
| `ViewModels/SimulationWorkspaceViewModel.CopperReplay.cs:62,73` | `Export Copper Path Replay CSV/JSON` |
| `Views/ConsolePanel.axaml.cs:109` | `Save console log` (sentence case, `Save` verb) |

Pattern in use: menu/button says `Export`, file dialog says `Save` (and the dialog title is a generic noun, so `Save Image` does not say which export it belongs to). The rule is not written down, `Save console log` breaks it, and capitalization differs (`Save Notch Table` vs `Save console log`). Suggested canonical form: dialog title = menu label (`Export Notch`, `Export DXF Layer Image`), or state the Export/Save rule once.

### 3.3 Confirm / dismiss buttons

| Meaning | Wording and place |
|---|---|
| Close an informational window | `Done` (`Views/CascadeIcSettingsWindow.axaml:108`), `Close` (`Views/CoordinateArtifactDetailWindow.axaml:82`, `Views/DxfEditChangeListWindow.axaml:324`), `OK` (`Views/WarningDialog.axaml:37`) |
| Commit edits | `Apply` (`Views/PadInfoPopover.axaml:33`, `Views/NotchExportColumnFilterWindow.axaml:99`, `Views/NotchExportFilterBuilderWindow.axaml:97`), `Apply changes` (`Views/PadInfoSections/RegularPadInfoContentView.axaml:289`), `Save Settings` (`Views/SettingsWindow.axaml:131`), `Apply override` (`Views/SimulationWorkspaceView.axaml:156`) |
| Abandon edits | `Discard` (`Views/PadInfoPopover.axaml:36`), `Cancel` (most windows) |
| Yes/no dialogs | `Embed` / `Skip` (`Pickers.cs:164-165`), `Reset` / `Cancel` (`Views/LeftDxfPanel.axaml.cs:48-49`, `Views/SettingsWindow.axaml.cs:70-71`), `Save and exit` / `Exit without saving` (`MainWindow.axaml.cs:123-124`) |

The Cascade IC dialog says `Done` while its own hint (`Views/CascadeIcSettingsWindow.axaml:106`) says "Draft changes apply with Save Settings." The pad info popover has two commit vocabularies for the same data: a close-confirm bar with `Apply` / `Discard` ("Unsaved changes. Apply or discard.", `Views/PadInfoPopover.axaml:25-38`) and inline `Apply changes` / `Reset size` in the Regular pad content (`Views/PadInfoSections/RegularPadInfoContentView.axaml:285-294`).

### 3.4 Hide / Delete / Restore / Show / Undo (DXF pad edits)

| Where | Wording |
|---|---|
| `Views/LeftDxfPanel.axaml:195,202` | tooltip "Hide selected CAD pads ... (shortcut: Delete)", button `Hide selected` |
| `Views/HowToUseView.axaml:137` | "Delete hides selected CAD pads" |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Hidden.cs:72` | status "no **deleted** CAD pads to restore." |
| `ViewModels/FreeformHelperViewModel.DxfEditing.LayerMove.cs:34` | "Hidden pads: {DeletedCadPadCount}" (property is named Deleted) |
| `Views/LeftDxfPanel.axaml:210,217` / `:224,231` | `Undo hide` (restore last batch), `Show all` (restore all manual hides) |
| `Views/DxfEditChangeListWindow.axaml:107,124` / `ViewModels/DxfEditChangeListViewModel.cs:146-147,170` | `Restore selected`, `Hide selected`; row actions `Restore` / `Hide again` |
| `ViewModels/FreeformHelperViewModel.DxfEditing.ChangeList.cs:113-123` | `Auto-hidden duplicate` / `Restored duplicate`; tooltip "Auto-hide this duplicate CAD pad again." |

Restoring hidden pads is called `Undo hide`, `Show all`, `Restore`, `Restore selected`. Hiding is also called deleting in two places. Suggested canonical form: `Hide` / `Restore` pair; drop "delete" from user-facing text.

### 3.5 Clear vs Reset vs Remove

| Wording | Where |
|---|---|
| `Reset all settings` | `Views/SettingsWindow.axaml:127`; status "Settings reset to defaults." `ViewModels/FreeformHelperViewModel.SettingsWindow.cs:283` |
| `Reset DXF edits` | `Views/LeftDxfPanel.axaml.cs:46`; tooltip `Views/LeftDxfPanel.axaml:113` |
| `Reset size` / "Manual sizing reset to default." | `Views/PadInfoSections/RegularPadInfoContentView.axaml:294`; `ViewModels/FreeformHelperViewModel.PadEdits.cs:163` |
| `Reset filters (view all)` (tooltip) | `Views/NotchExportSelectionWindow.axaml:170` |
| `Clear` (filters / search) | `Views/NotchExportFilterBuilderWindow.axaml:91`, `Views/NotchExportColumnFilterWindow.axaml:32`, `Views/IndexMappingReportWindow.axaml:98` (clears the search box) |
| `Clear mask`, `Clear CSV`, `Clear custom`, `Clear all` | `Views/WorkflowSteps/RightWorkflowStep1View.axaml:83`; `Views/WorkspaceSections/SimulationWorkspaceControlsPaneView.axaml:125,164`; `Views/WorkspaceSections/CoordinatePlannerWorkspaceControlsPaneView.axaml:517` |
| "Clear Step N result and all downstream results." | `Views/WorkflowSteps/RightWorkflowStep1View.axaml:27` (and Steps 2 to 5) |
| "Remove the imported regular visibility mask ..." | `Views/WorkflowSteps/RightWorkflowStep1View.axaml:86` (button is `Clear mask`) |

Observed split: Reset = return to defaults, Clear = empty a result or a filter. It is mostly followed, but "Reset filters" (tooltip) and `Clear` (button) name the same filter reset, `Clear all` in the Simulation pane sets manual overrides to nothing while `Zero` sets values to 0, and the mask button says `Clear` where its tooltip says "Remove".

### 3.6 Same concept, different name

| Concept | Variants (number of inventory strings containing the variant) | Examples |
|---|---|---|
| CAD output diff index | `CAD Output FW Diff` (51), `FW Diff Idx` (6), `FW diff` (12), `Diff idx` (11), `diff idx` (8), `CAD Output FW diff` (2), `CAD output FW diff` (2), `DIFF` (1) | `Views/PadInfoSections/CadPadInfoContentView.axaml:252`; `ViewModels/SimulationWorkspaceViewModel.Selection.cs:16`; `Views/PadInfoSections/RegularPadInfoContentView.axaml:70`; `ViewModels/PadInfo/RegularPadInfoViewModel.Core.cs:398`; `Views/SettingsSections/SettingsStep4SectionView.axaml:105`; `Services/QuickFocusQueryParser.cs:77` |
| To Full | `To Full` (49), `ToFull` (27), `to-full` (1) | `Views/CanvasOverlayControls.axaml:93` vs `Views/NotchExportSelectionWindow.axaml:424`; `Controls/WorkspaceHeader.axaml:396` |
| To Regular | `To Regular` (19), `ToRegular` (7), `Undo NF (To Regular)` (3) | `Views/CanvasOverlayControls.axaml:84`; `ViewModels/FreeformHelperViewModel.Core.cs:206`; `Services/NotchDisplayProjector.cs:116` |
| Regular pad | `Regular` (101), `regular` (117), `REG` (76), `regId` (9) | `REG` in tables (`Views/NotchExportFilterBuilderWindow.axaml:42`) vs `Regular id` (`Views/NotchDetailWindow.axaml:62`, `Views/PadInfoSections/RegularPadInfoContentView.axaml:190`); `regId` in `ViewModels/FreeformHelperViewModel.PadInspector.Formatting.cs:119` |
| Freeform direction | `XWay` / `YWay` / `XYWay` in labels (`ViewModels/FreeformHelperViewModel.FreeformStats.cs:35-37`) vs `X-way` / `Y-way` / `XY-way` in tooltips (`Views/PadInfoSections/RegularPadInfoContentView.axaml:269,274,279`; `Views/WorkflowSteps/RightWorkflowStep2View.axaml:98,102,106`) | |
| Regular visibility mask | `Regular Visibility Mask` (7, Title Case) vs `Regular visibility mask` (15, sentence case) vs lower case (2) | `Views/WorkflowSteps/RightWorkflowStep1View.axaml:54,65` vs `ViewModels/FreeformHelperViewModel.RegularVisibilityMask.cs:10,27,56` |
| Notch version | `v2.1` / `v2.2` (16), `V2.1` (1), `Notch 2.2` (8), `C v2.1 (.c)` | `Views/SettingsSections/SettingsStep5SectionView.axaml:43`; `Views/HowToUseView.axaml:101`; `ViewModels/FreeformHelperViewModel.NotchDetails.cs:22`; `ViewModels/FreeformHelperViewModel.Core.cs:189` |
| Inspector panel | `Inspector` (`Views/FreeformHelperView.axaml:228`; `Views/NotchExportSelectionWindow.axaml:261`; `Views/DxfEditChangeListWindow.axaml:245`), `Inspector snapshot` (`Views/RightWorkflowSections/RightWorkflowInspectorView.axaml:13`), "Pad Info" (`Views/HowToUseView.axaml:64`), "pad info" (`:70`) | |
| Focus the pad on the canvas | `Focus` (`Views/RightWorkflowSections/RightWorkflowInspectorView.axaml:36`; `Views/DxfEditChangeListWindow.axaml:288`), `Locate` (`Views/WorkspaceSections/IndexMappingReportRightPaneView.axaml:286`), "workspace locate" (`Views/NotchExportSelectionWindow.axaml:446`), "Focus this validation row on canvas." (`Views/RightWorkflowPanel.axaml:19`) | |
| AA size | `AA size X (mm)` / `AA size Y (mm)` (`Views/SettingsSections/SettingsGeneralSectionView.axaml:68,81`), `AA width (mm)` / `AA height (mm)` (`Views/WorkspaceSections/CoordinatePlannerWorkspaceControlsPaneView.axaml:130,139`), "Panel AA size (mm)" (`Views/HowToUseView.axaml:87`) | |
| Pixel count | `Coordinate pixel X` (`SettingsGeneralSectionView.axaml:165`), `Pixel X` (`CoordinatePlannerWorkspaceControlsPaneView.axaml:153`), `X pixels` (`Views/DxfLayerImageExportWindow.axaml:42`) | |
| Alignment | `Grid alignment mode` (`SettingsGeneralSectionView.axaml:334`) vs "Alignment mode" (`HowToUseView.axaml:88`) | |
| Product name | `FreeformHelper Modern` (`MainWindow.axaml:9`, window title), `Freeform Helper` (tab `MainWindow.axaml:47`, `Views/SettingsWindow.axaml:23`, `MainWindow.axaml.cs:67`), `FreeformHelper` (docs, repo, process) | |

### 3.7 Step headings

| Where | Heading |
|---|---|
| `Views/SettingsWindow.axaml:59-75` (nav) | `Step 1 · Geometry`, `Step 2 · Freeform`, `Step 3 · Notch`, `Step 4 · Index`, `Step 5 · Export` |
| `ViewModels/SettingsWindowViewModel.cs:325-329` (section title) | `Step 1 - Geometry match`, `Step 2 - Freeform tagging`, `Step 3 - Notch compensation`, `Step 4 - Index diagnostics`, `Step 5 - Notch export` |
| `Views/WorkflowSteps/RightWorkflowStep*View.axaml:7,13` (right panel) | `Step 1 · Geometry match`, `Step 2 · Freeform tagging`, `Step 3 · Notch 2.2 compensation`, `Step 4 · Index diagnostics`, `Step 5 · Notch export` |
| `Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml:252` | `Step 6 - Validation Quick Trace` (hyphen, Title Case, Step 6 not listed in Settings) |
| `Views/IndexMappingReportWindow` / `ViewModels/IndexMappingReportViewModel.cs:128-132` | `Step 3` ... `Step 6`, `Simulation` as chips |

Three separators/naming sets for the same five steps (`·` vs `-`; short vs long names; "Notch" vs "Notch compensation" vs "Notch 2.2 compensation").

### 3.8 Case, punctuation, and phrasing style

- **Tooltip punctuation:** 210 of 236 tooltip/tip strings in the AXAML inventory end with a period; the rest do not (examples without: `Views/CanvasOverlayControls.axaml:19` "Fit to content", `:28` "Show right panel", `Views/WorkflowSteps/RightWorkflowStep3View.axaml:146` "Previous stage", `Views/ConsolePanel.axaml:31` "Toggle console panel", `Views/NotchExportSelectionWindow.axaml:316` "Copy payload", `Views/CoordinatePlannerWorkspaceView.axaml:143` "Overlay density").
- **Title Case vs sentence case for the same kind of control:** menu and tabs use Title Case (`Save Project`, `How To Use`, `Export DXF Layer Image`); buttons mostly use sentence case (`Hide selected`, `Show all`, `Match overlaps`); some buttons are mixed (`Use Selected REG` `Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml:278`, `Apply override`). Window titles: `Verification Workspace`, `DXF Edit Details` (Title Case) vs `Coordinate detail`, `Notch detail`, `Cascade IC details`, `Column filter`, `Export DXF layer image` (sentence case).
- **Abbreviation `EN` for "enable":** `XY WAY EN`, `Edge spill EN`, `To Regular EN (derived)`, `To Full EN (derived)` (`Views/SettingsSections/SettingsStep2SectionView.axaml:38,58`; `SettingsStep3SectionView.axaml:98,116`) next to `Use boundary area cap`, `Use target coverage guard`, `Use To Full rule engine` (`SettingsStep3SectionView.axaml:149,182,215`).
- **Label style in the same binding group:** `Open issues: {0}` (`Views/IndexMappingReportWindow.axaml:27`) vs `Open issues {0}` (`:112`); `Status: {0}` (`:24`) vs `status: {0}` (`Views/WorkspaceSections/IndexMappingReportRightPaneView.axaml:104`); `Active step: {0}` vs `Mode {0}` (`IndexMappingReportWindow.axaml:23,117`).
- **Status-chip prefixes:** `DXF edit: ...`, `DXF combine: ...`, `DXF rotate: ...`, `DXF check: ...`, `Export DXF image: ...`, `Export notch: ...`, `Notch 2.2 preview: ...`, `Quick focus: ...`, `Validation: ...`, `Manual sizing: ...`, `Diagnostics: ...`, `Coordinate: ...` vs no prefix: `Matching complete.`, `Matching pads...`, `Building regular grid...`. Message endings alternate between a period and none (for example "Project saved." vs "Project saved: {path}").
- **Spelling:** `cancelled` (`ViewModels/CoordinatePlannerWorkspaceViewModel.Operations.cs:22,120`; `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:106,198`; `ViewModels/SimulationWorkspaceViewModel.CopperReplay.cs:110`) vs `canceled` (`ViewModels/FreeformHelperViewModel.Persistence.cs:104,220,236`).
- **Developer wording shown to users:** 12 status strings say "dialog handler not wired" / "view handler not wired" (for example `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:39`, `ViewModels/FreeformHelperViewModel.Operations.cs:217`). Status strings also carry `key=value` diagnostics (for example `Selection updated: CAD=0, Regular=1, summary=2ms...` in `ViewModels/FreeformHelperViewModel.Selection.cs:118`, `ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:103-398`). Whether these are shown or only logged depends on the sink; they are listed in section 7.
- **Duplicate controls with different descriptions:** closed-polyline import is described three ways: `Views/LeftDxfPanel.axaml:27` "Closed polyline import; reopen DXF after changing this.", `:31` "Only import closed polylines from the DXF.", `Views/SettingsSections/SettingsGeneralSectionView.axaml:241-243` "Import only closed DXF polylines as CAD pads; open lines are ignored." / "Closed DXF shapes only.".

### 3.9 Text that disagrees with the actual UI (stale or wrong)

| Where | Problem |
|---|---|
| `Views/ConfirmDialog.axaml:31,47` | Button tooltips are hard-coded for the DXF-embed case ("Cancel and keep external DXF reference." / "Embed DXF into the project file."). `ConfirmDialog` is also used by the exit prompt (`MainWindow.axaml.cs:120`), `Reset DXF edits` (`Views/LeftDxfPanel.axaml.cs:45`) and `Reset all settings` (`Views/SettingsWindow.axaml.cs:67`), where these tooltips are wrong. `ConfirmDialog.axaml.cs` does not override them. |
| `Views/HowToUseView.axaml:28` | "Top tabs: Freeform Helper, How To Use, Dev." Missing `Simulation` and `Coordinate`; `Dev` exists in dev builds only. |
| `Views/HowToUseView.axaml:35`; `README.zh-TW.md:76` | "Match: Run pad matching" under Menu Bar / "執行 `Match`". The menu has only `File`, `Export`, `Settings` (`Views/FreeformHelperView.axaml:37-49`); the action is the button `Match overlaps` (`RightWorkflowStep1View.axaml:32`). |
| `Views/HowToUseView.axaml:79` | "Use Selection: fill Rows/Cols ..." No control with this text exists (grep over `src/` finds only this line). |
| `Views/HowToUseView.axaml:142` | "Run scripts/build.ps1" but the script is `scripts/build/build.ps1`. |
| `Views/HowToUseView.axaml:102,103` | "Freeform TH (Matching > Freeform axis threshold)" and "Notch TH (Notch threshold Q7)"; Settings labels are `Freeform TH (dominant axis ratio)` (`SettingsStep2SectionView.axaml:22`) and `Notch gate TH v2.1 (Q7/128)` (`SettingsStep5SectionView.axaml:124`). |
| `Views/HowToUseView.axaml:86` | "Padding percent", "Panel AA size (mm)", "Alignment mode"; Settings labels are `Grid padding (%)`, `AA size X/Y (mm)`, `Grid alignment mode`. |
| `README.zh-TW.md:73-78` | Uses `File -> Open DXF`, `Save Project`, `Load Project` (match the menu). Step 4 "執行 `Match`" does not (see above). |

## 4. Where the text lives (for planning a mechanism)

- AXAML text is spread over about 50 files; the biggest are `Views/HowToUseView.axaml` (long help paragraphs), `Views/NotchExportSelectionWindow.axaml`, `Views/SettingsSections/*`, and `Controls/WorkspaceHeader.axaml`.
- C# text is concentrated in `ViewModels/FreeformHelperViewModel.*.cs` (partial class, status messages), `Services/*Projector.cs` and `*Formatter.cs` (display text built in Services), `ViewModels/SimulationWorkspaceViewModel*.cs`, `ViewModels/CoordinatePlannerWorkspaceViewModel*.cs`, `ViewModels/IndexMapping*.cs`.
- Many strings are formatted with `{0}`/interpolation and mix words with data (`IC{n}/diff{m}`, `REG {id}`); a resource scheme must handle format arguments and plural forms (for example `pad(s)`, `row(s)`, `{n} CAD pad{(n == 1 ? "" : "s")} selected` at `FreeformHelperViewModel.DxfEditing.LayerMove.cs:31`).
- Some text is shared across screens by value, not by reference (for example `Highlight line width adjust` in `Controls/WorkspaceHeader.axaml:390` and `SettingsGeneralSectionView.axaml:376`; the two To Regular/To Full summaries in `State.NotchPreview.cs:195-240` and `SettingsWindowViewModel.cs:363-400`).
- `Views/FreeformHelperView.Pickers.cs` builds file-type filters with display names (`DXF`, `All files`, `FreeformHelper Project`, `C source (*.c)`, `CSV (*.csv)`); these are user-visible in the OS dialog.

## 5. Inventory: AXAML (excluding Dev page)

Columns: `path:line` | element.attribute | text | where it shows. Element/attribute shows how the text is used (for example `Button.Content` is a button label, `ToolTip.Tip` is a hover tooltip, `MenuItem.Header` is a menu entry, `Window.Title` is a window title bar).


### `Controls/NumberScrubber.axaml`  -  Number input control (every numeric field)

| line | element.attribute | text |
|---:|---|---|
| 18 | Border.ToolTip.Tip | Drag or Alt + mouse wheel to adjust |

### `Controls/WorkspaceHeader.axaml`  -  Workspace tab > header bar

| line | element.attribute | text |
|---:|---|---|
| 201 | ToggleButton.ToolTip.Tip | Display options |
| 211 | SelectableTextBlock.Text | Display |
| 225 | SelectableTextBlock.Text | Rows |
| 228 | TextBox.PlaceholderText | e.g. 0-3,6 |
| 230 | TextBox.ToolTip.Tip | Rows range (0 = top). Accepts 5 / 5-7 / 5,7. |
| 234 | SelectableTextBlock.Text | Cols |
| 237 | TextBox.PlaceholderText | e.g. 2-6 |
| 239 | TextBox.ToolTip.Tip | Column range. Accepts 5 / 5-7 / 5,7. |
| 243 | SelectableTextBlock.Text | Width (mm) |
| 249 | TextBox.ToolTip.Tip | Target pad width for the manual range (mm). |
| 253 | SelectableTextBlock.Text | Height (mm) |
| 259 | TextBox.ToolTip.Tip | Target pad height for the manual range (mm). |
| 338 | SelectableTextBlock.Text | Display options |
| 340 | SelectableTextBlock.Text | Quick toggles stay on top. Advanced appearance tuning is folded below to reduce clutter. |
| 344 | SelectableTextBlock.Text | Quick toggles |
| 373 | SelectableTextBlock.Text | Line width |
| 375 | SelectableTextBlock.Text | Tune widths first; keep defaults for most color/opacity values unless diagnostics require it. |
| 376 | SelectableTextBlock.Text | CAD line width |
| 382 | NumberScrubber.ToolTip.Tip | CAD stroke width (mm). |
| 383 | SelectableTextBlock.Text | Regular outline width |
| 389 | NumberScrubber.ToolTip.Tip | Regular pad outline width (mm). |
| 390 | SelectableTextBlock.Text | Highlight line width adjust |
| 396 | NumberScrubber.ToolTip.Tip | Adjust thickness for selected/highlight/to-full/override outlines. Negative = thinner. |
| 398 | SelectableTextBlock.Text | Default -1 keeps highlight overlays thinner than normal outlines. |
| 402 | HidePanelBlock.Title | Colors and opacity (advanced) |
| 407 | SelectableTextBlock.Text | Low-frequency appearance tuning for CAD/Regular rendering. |
| 408 | SelectableTextBlock.Text | CAD line color (#RRGGBB) |
| 410 | TextBox.ToolTip.Tip | CAD stroke color as hex (#RRGGBB). |
| 414 | SelectableTextBlock.Text | CAD fill opacity |
| 415 | SelectableTextBlock.ToolTip.Tip | Transparency for CAD fill rendering. |
| 429 | SelectableTextBlock.Text | CAD line opacity |
| 430 | SelectableTextBlock.ToolTip.Tip | Transparency for CAD stroke rendering. |
| 441 | SelectableTextBlock.Text | Regular outline color (#RRGGBB) |
| 443 | TextBox.ToolTip.Tip | Regular outline color as hex (#RRGGBB). |
| 444 | SelectableTextBlock.Text | Regular selection color (#RRGGBB) |
| 446 | TextBox.ToolTip.Tip | Selection color for regular pads (#RRGGBB). |
| 450 | SelectableTextBlock.Text | Regular fill opacity |
| 451 | SelectableTextBlock.ToolTip.Tip | Transparency for regular pad fill rendering. |
| 465 | SelectableTextBlock.Text | Regular line opacity |
| 466 | SelectableTextBlock.ToolTip.Tip | Transparency for regular pad stroke rendering. |
| 480 | SelectableTextBlock.Text | Regular selection fill opacity |
| 481 | SelectableTextBlock.ToolTip.Tip | Transparency for selected regular pad fill. |
| 496 | HidePanelBlock.Title | CAD area buckets (advanced) |
| 501 | SelectableTextBlock.Text | Enable "Color CAD by area" in Quick toggles, then tune tolerance and bucket count. |
| 503 | SelectableTextBlock.Text | Area bucket tolerance (relative) |
| 510 | NumberScrubber.ToolTip.Tip | Relative area tolerance to group CAD pads. |
| 511 | SelectableTextBlock.Text | Max area buckets |
| 517 | NumberScrubber.ToolTip.Tip | Maximum number of color buckets. |
| 519 | SelectableTextBlock.Text | Tolerance groups similar areas (0 = exact). |

### `MainWindow.axaml`  -  Main window

| line | element.attribute | text |
|---:|---|---|
| 9 | Window.Title | FreeformHelper Modern |
| 47 | ToggleButton.Content | Freeform Helper |
| 52 | ToggleButton.Content | Simulation |
| 57 | ToggleButton.Content | Coordinate |
| 62 | ToggleButton.Content | How To Use |
| 67 | ToggleButton.Content | Dev |

### `Views/CanvasOverlayControls.axaml`  -  Workspace tab > canvas overlay toolbar

| line | element.attribute | text |
|---:|---|---|
| 19 | Button.ToolTip.Tip | Fit to content |
| 28 | Button.ToolTip.Tip | Show right panel |
| 45 | Button.ToolTip.Tip | Show left panel |
| 57 | SelectableTextBlock.Text | CAD layer |
| 63 | ToggleSwitch.ToolTip.Tip | Show or hide CAD pads on canvas. |
| 66 | SelectableTextBlock.Text | Regular grid |
| 72 | ToggleSwitch.ToolTip.Tip | Show or hide regular grid on canvas. |
| 75 | SelectableTextBlock.Text | Color by area |
| 81 | ToggleSwitch.ToolTip.Tip | Color CAD pads by area buckets. |
| 84 | SelectableTextBlock.Text | To Regular |
| 90 | ToggleSwitch.ToolTip.Tip | Display switch affects AA overlay only. Compute switch affects ratio/result. |
| 93 | SelectableTextBlock.Text | To Full |
| 100 | ToggleSwitch.ToolTip.Tip | Display switch affects AA overlay only. Compute switch affects ratio/result. |
| 104 | SelectableTextBlock.Text | Alt = Regular priority |
| 105 | SelectableTextBlock.ToolTip.Tip | Selection mode hint: hold Alt to prioritize regular pads while selecting. |

### `Views/CascadeIcSettingsWindow.axaml`  -  Cascade IC details dialog

| line | element.attribute | text |
|---:|---|---|
| 12 | Window.Title | Cascade IC details |
| 19 | SelectableTextBlock.Text | Cascade IC details |
| 21 | SelectableTextBlock.Text | Adjust the IC count and per-IC channel allocation for the generated regular grid. |
| 35 | SettingsInfoLabel.Text | Cascade |
| 36 | SettingsInfoLabel.Tip | Adjust IC count. The per-IC rows below update immediately. |
| 54 | SelectableTextBlock.Text | IC |
| 57 | SelectableTextBlock.Text | X channels |
| 60 | SelectableTextBlock.Text | Y channels |
| 106 | SelectableTextBlock.Text | Draft changes apply with Save Settings. |
| 108 | Button.Content | Done |

### `Views/ConfirmDialog.axaml`  -  Confirm dialog (shared by 4 callers)

| line | element.attribute | text |
|---:|---|---|
| 31 | Button.ToolTip.Tip | Cancel and keep external DXF reference. |
| 47 | Button.ToolTip.Tip | Embed DXF into the project file. |

### `Views/ConsolePanel.axaml`  -  Console panel

| line | element.attribute | text |
|---:|---|---|
| 31 | ToggleButton.ToolTip.Tip | Toggle console panel |

### `Views/CoordinateArtifactDetailWindow.axaml`  -  Coordinate detail window

| line | element.attribute | text |
|---:|---|---|
| 12 | Window.Title | Coordinate detail |
| 31 | SelectableTextBlock.Text | Pixel |
| 35 | SelectableTextBlock.Text | Machine |
| 39 | SelectableTextBlock.Text | Safe machine |
| 43 | SelectableTextBlock.Text | World |
| 52 | SelectableTextBlock.Text | Source |
| 58 | SelectableTextBlock.Text | Source bounds |
| 64 | SelectableTextBlock.Text | Detail |
| 75 | SelectableTextBlock.Text | The canvas keeps this row focused after the dialog closes. |
| 82 | SelectableTextBlock.Text | Close |

### `Views/CoordinatePlannerHostView.axaml`  -  Coordinate tab (host / empty state)

| line | element.attribute | text |
|---:|---|---|
| 43 | SelectableTextBlock.Text | Refresh |
| 77 | SelectableTextBlock.Text | Build from current workspace |

### `Views/CoordinatePlannerWorkspaceView.axaml`  -  Coordinate tab

| line | element.attribute | text |
|---:|---|---|
| 18 | SelectableTextBlock.Text | Coordinate |
| 20 | SelectableTextBlock.Text | Coordinate artifact planner. |
| 122 | Button.ToolTip.Tip | Fit AA canvas |
| 129 | ToggleButton.ToolTip.Tip | Toggle regular grid |
| 136 | ToggleButton.ToolTip.Tip | Toggle regular row and column labels |
| 143 | ComboBox.ToolTip.Tip | Overlay density |

### `Views/DxfEditChangeListWindow.axaml`  -  DXF Edit Details window

| line | element.attribute | text |
|---:|---|---|
| 15 | Window.Title | DXF Edit Details |
| 23 | Button.ToolTip.Tip | Refresh rows |
| 36 | Button.Content (StringFormat) | Hidden {0} |
| 41 | Button.Content (StringFormat) | Duplicate {0} |
| 46 | Button.Content (StringFormat) | Combined {0} |
| 51 | Button.Content (StringFormat) | Moved {0} |
| 56 | Button.Content (StringFormat) | Rotated {0} |
| 76 | SelectableTextBlock.Text | Bulk action follows current tab scope. |
| 107 | SelectableTextBlock.Text | Restore selected |
| 124 | SelectableTextBlock.Text | Hide selected |
| 153 | SelectableTextBlock.Text | CAD |
| 157 | SelectableTextBlock.Text | Layer |
| 161 | SelectableTextBlock.Text | Detail |
| 165 | SelectableTextBlock.Text | Action |
| 172 | SelectableTextBlock.Text | No DXF edit rows in this scope. |
| 245 | SelectableTextBlock.Text | Inspector |
| 261 | SelectableTextBlock.Text | Detail |
| 267 | SelectableTextBlock.Text | Quick action |
| 288 | SelectableTextBlock.Text | Focus |
| 324 | SelectableTextBlock.Text | Close |

### `Views/DxfLayerImageExportWindow.axaml`  -  Export DXF layer image dialog

| line | element.attribute | text |
|---:|---|---|
| 12 | Window.Title | Export DXF layer image |
| 21 | SelectableTextBlock.Text | Export DXF layer image |
| 23 | SelectableTextBlock.Text | Layer X/Y pixels define inner geometry resolution. Final output adds both-side padding (X + 2*paddingX, Y + 2*paddingY). |
| 28 | SelectableTextBlock.Text | DXF layer |
| 42 | SelectableTextBlock.Text | X pixels |
| 51 | SelectableTextBlock.Text | Y pixels |
| 61 | SelectableTextBlock.Text | Line width (px) |
| 73 | SelectableTextBlock.Text | Padding X (px) |
| 82 | SelectableTextBlock.Text | Padding Y (px) |
| 97 | SelectableTextBlock.Text | Format |
| 111 | ToggleSwitch.OnContent | Dark |
| 112 | ToggleSwitch.OffContent | Light |
| 114 | SelectableTextBlock.Text | Dark: deep gray background + white outline |
| 124 | Button.Content | Cancel |
| 126 | Button.Content | Export |

### `Views/DxfOverlapReportWindow.axaml`  -  DXF overlaps report window

| line | element.attribute | text |
|---:|---|---|
| 11 | Window.Title | DXF overlaps report |
| 14 | SelectableTextBlock.Text | DXF overlaps report |
| 26 | Button.Content | Copy all |
| 28 | Button.ToolTip.Tip | Copy overlap report text. |
| 40 | SelectableTextBlock.Text | No issues. |

### `Views/FreeformHelperView.axaml`  -  Workspace tab (menu bar / panel chrome)

| line | element.attribute | text |
|---:|---|---|
| 37 | MenuItem.Header | File |
| 38 | MenuItem.Header | Open DXF |
| 39 | MenuItem.Header | Save Project |
| 40 | MenuItem.Header | Load Project |
| 42 | MenuItem.Header | Export |
| 43 | MenuItem.Header | Export Notch |
| 44 | MenuItem.Header | Export DXF (Visible Layers) |
| 45 | MenuItem.Header | Export DXF (All Layers) |
| 46 | MenuItem.Header | Export DXF Layer Image |
| 48 | MenuItem.Header | Settings |
| 49 | MenuItem.Header | Open Settings |
| 73 | SelectableTextBlock.Text | Notch export panel hidden |
| 75 | Button.Content | Restore (Ctrl+Shift+E) |
| 87 | SelectableTextBlock.Text | DXF Settings |
| 90 | Button.ToolTip.Tip | Hide left panel |
| 218 | TextBlock.Text | Settings |
| 228 | TextBlock.Text | Inspector |
| 236 | Button.ToolTip.Tip | Hide right panel |

### `Views/HowToUseView.axaml`  -  How To Use tab

| line | element.attribute | text |
|---:|---|---|
| 12 | SelectableTextBlock.Text | How To Use |
| 16 | SelectableTextBlock.Text | Workflow, controls, and settings reference. |
| 20 | SelectableTextBlock.Text | Quick Start |
| 22 | SelectableTextBlock.Text | 1) Open DXF → 2) Review grid settings → 3) Match → 4) Tag freeforms → 5) Export. |
| 23 | SelectableTextBlock.Text | Regular pads are generated from AA size + channels even before a DXF is loaded. |
| 26 | SelectableTextBlock.Text | Navigation |
| 28 | SelectableTextBlock.Text | - Top tabs: Freeform Helper, How To Use, Dev. |
| 29 | SelectableTextBlock.Text | - Fit icon: top-right of the canvas. |
| 32 | SelectableTextBlock.Text | Menu Bar |
| 34 | SelectableTextBlock.Text | - File: Open DXF, Save Project, Load Project. |
| 35 | SelectableTextBlock.Text | - Match: Run pad matching. |
| 36 | SelectableTextBlock.Text | - Export: Notch CSV review or C v2.1/v2.2. |
| 39 | SelectableTextBlock.Text | Workspace Header (Display) |
| 41 | SelectableTextBlock.Text | - Display menu: CAD layer, Regular grid, Highlight unmatched/freeform, Color by area. |
| 42 | SelectableTextBlock.Text | - CAD display: line width and line color. |
| 43 | SelectableTextBlock.Text | - Area buckets: CAD-only area grouping colors. |
| 44 | SelectableTextBlock.Text | - Status and selection summaries update automatically. |
| 47 | SelectableTextBlock.Text | DXF Settings (Left Panel) |
| 49 | SelectableTextBlock.Text | - Only closed polylines: import closed shapes only. |
| 50 | SelectableTextBlock.Text | - Layer toggles: hide/show CAD pads by layer after import. |
| 51 | SelectableTextBlock.Text | - Check DXF overlaps: scan duplicates and overlaps on demand. |
| 54 | SelectableTextBlock.Text | Canvas Navigation |
| 56 | SelectableTextBlock.Text | - Middle mouse drag: Pan (recommended). |
| 57 | SelectableTextBlock.Text | - Space + Left mouse drag: Pan (laptop fallback). |
| 58 | SelectableTextBlock.Text | - Mouse wheel: Zoom around cursor. Ctrl + wheel = faster zoom. |
| 59 | SelectableTextBlock.Text | - Shift + Arrow keys: Fine pan. |
| 60 | SelectableTextBlock.Text | - F: Fit to content. Ctrl + A: Select all visible pads. |
| 61 | SelectableTextBlock.Text | - F / Ctrl + A work globally when not editing text. |
| 64 | SelectableTextBlock.Text | Selection & Pad Info |
| 66 | SelectableTextBlock.Text | - Left click: Select pad under cursor. |
| 67 | SelectableTextBlock.Text | - Ctrl + click / Shift + click: Add/remove selection. |
| 68 | SelectableTextBlock.Text | - Alt + click: Prefer regular pad when CAD/regular overlap. |
| 69 | SelectableTextBlock.Text | - Drag empty space: Box select. Ctrl/Shift = add to selection. |
| 70 | SelectableTextBlock.Text | - Double click: Open pad info for the clicked pad. |
| 71 | SelectableTextBlock.Text | - Right click: Open pad info (keeps selection if already selected). |
| 72 | SelectableTextBlock.Text | - Click empty space: Clear selection. |
| 75 | SelectableTextBlock.Text | Manual Sizing (Header Bar) |
| 77 | SelectableTextBlock.Text | - Rows/Cols: enter ranges like 5, 5-7, 5,7. |
| 78 | SelectableTextBlock.Text | - Width/Height: set target size for selected regular pads. |
| 79 | SelectableTextBlock.Text | - Use Selection: fill Rows/Cols from current selection. |
| 80 | SelectableTextBlock.Text | - '*' means mixed values in the current selection. |
| 83 | SelectableTextBlock.Text | Right Panel: General Settings |
| 85 | SelectableTextBlock.Text | - IC layout: Cascade count and per-IC X/Y channels. |
| 86 | SelectableTextBlock.Text | - Grid settings: Padding percent, scan order, and sizing scope (Local/Global). |
| 87 | SelectableTextBlock.Text | - Panel AA size (mm): Physical panel size used to compute pitch. |
| 88 | SelectableTextBlock.Text | - Alignment mode: Choose AA vs DXF bounds. |
| 91 | SelectableTextBlock.Text | Right Panel: Analysis |
| 93 | SelectableTextBlock.Text | - Matching: overlap-based CAD↔Regular relation; one CAD can map to multiple regular pads. |
| 94 | SelectableTextBlock.Text | - Unmatched threshold: Display-only overlap / regular area diagnostic; changing it does not change links. |
| 95 | SelectableTextBlock.Text | - Freeform tagging: Assign X/Y/XY/None to selected pads. |
| 96 | SelectableTextBlock.Text | - Notch export: Enable versions and legacy values. |
| 99 | SelectableTextBlock.Text | Notch Terms & Thresholds |
| 101 | SelectableTextBlock.Text | - BBoxArea: CAD pad bounding-box area, formula = width × height (used by V2.1 REGU_TO_FULL field). |
| 102 | SelectableTextBlock.Text | - Freeform TH (Matching > Freeform axis threshold): decides XWay/YWay/None classification during auto-detect. |
| 103 | SelectableTextBlock.Text | - Notch TH (Notch threshold Q7): after allocation, if max allocation < TH, that CAD is skipped from notch export. |
| 104 | SelectableTextBlock.Text | - Effective boundary: To Full can only start from CAD-overlapped regulars that are connected to boundary seeds (outer edge or inactive-neighbor seed). |
| 105 | SelectableTextBlock.Text | - Multi-owner gate: if one regular is owned by more than one CAD (strict overlap over TH), that regular cannot be a To Full target. |
| 106 | SelectableTextBlock.Text | - Active regular visibility mask: boundary inactive-neighbor check uses current active regular set (match result scope), not all grid cells. |
| 107 | SelectableTextBlock.Text | - Allocation cutoff: reserved for future FW-oriented row minimization (not enabled in current build). |
| 108 | SelectableTextBlock.Text | - Export notch now opens a row picker, so each generated notch row can be included/excluded manually before file output. |
| 124 | SelectableTextBlock.Text | Number Inputs |
| 126 | SelectableTextBlock.Text | - Type a value and press Enter or click away to commit. |
| 127 | SelectableTextBlock.Text | - Up/Down buttons step by the configured increment. |
| 128 | SelectableTextBlock.Text | - Drag the right scrub area to adjust quickly. |
| 129 | SelectableTextBlock.Text | - Alt + mouse wheel changes the value. |
| 132 | SelectableTextBlock.Text | Console & Undo |
| 134 | SelectableTextBlock.Text | - Bottom console streams logs and auto-scrolls to the latest entry. |
| 135 | SelectableTextBlock.Text | - Clear removes entries. |
| 136 | SelectableTextBlock.Text | - Ctrl + Z undoes settings and pad edits (global when not editing text). |
| 137 | SelectableTextBlock.Text | - Delete hides selected CAD pads (global when not editing text). |
| 140 | SelectableTextBlock.Text | Build & Tests |
| 142 | SelectableTextBlock.Text | - Run scripts/build.ps1 to build and execute unit tests. |

### `Views/IndexMappingReportWindow.axaml`  -  Verification Workspace window

| line | element.attribute | text |
|---:|---|---|
| 15 | Window.Title | Verification Workspace |
| 16 | ReviewWorkspaceShell.Title | Verification Workspace |
| 23 | SelectableTextBlock.Text (StringFormat) | Active step: {0} |
| 24 | SelectableTextBlock.Text (StringFormat) | Status: {0} |
| 27 | SelectableTextBlock.Text (StringFormat) | Open issues: {0} |
| 41 | Button.Content (StringFormat) | All {0} |
| 47 | Button.Content (StringFormat) | Changed {0} |
| 53 | Button.Content (StringFormat) | Removed {0} |
| 59 | Button.Content (StringFormat) | Ambiguous {0} |
| 65 | Button.Content (StringFormat) | Duplicate {0} |
| 71 | Button.Content (StringFormat) | Low confidence {0} |
| 77 | Button.Content (StringFormat) | Unmapped {0} |
| 83 | Button.Content (StringFormat) | Mismatch {0} |
| 98 | Button.Content | Clear |
| 107 | SelectableTextBlock.Text (StringFormat) | Rows {0} |
| 112 | SelectableTextBlock.Text (StringFormat) | Open issues {0} |
| 117 | SelectableTextBlock.Text (StringFormat) | Mode {0} |

### `Views/LeftDxfPanel.axaml`  -  Workspace tab > left panel (DXF Settings)

| line | element.attribute | text |
|---:|---|---|
| 24 | SelectableTextBlock.Text | Import |
| 27 | SelectableTextBlock.Text | Closed polyline import; reopen DXF after changing this. |
| 29 | CheckBox.Content | Only closed polylines |
| 31 | CheckBox.ToolTip.Tip | Only import closed polylines from the DXF. |
| 40 | SelectableTextBlock.Text | DXF check |
| 47 | Button.ToolTip.Tip | Scan the current DXF for duplicates or overlaps. |
| 50 | SelectableTextBlock.Text | Overlap check |
| 66 | SelectableTextBlock.Text (StringFormat) | Issues: {0} |
| 71 | Button.ToolTip.Tip | Open overlap details window (copy supported). |
| 78 | Button.ToolTip.Tip | Restore overlap highlight and select those CAD pads. |
| 85 | Button.ToolTip.Tip | Clear overlap highlight and current selection. |
| 104 | SelectableTextBlock.Text | DXF edits |
| 113 | Button.ToolTip.Tip | Reset all DXF edits, including hidden, combined, and layer-move changes. |
| 122 | SelectableTextBlock.Text | Summary |
| 188 | SelectableTextBlock.Text | Quick actions |
| 192 | SelectableTextBlock.Text | Hide / restore |
| 195 | Button.ToolTip.Tip | Hide selected CAD pads from current workflow (shortcut: Delete). |
| 202 | SelectableTextBlock.Text | Hide selected |
| 210 | Button.ToolTip.Tip | Restore last hidden CAD batch. |
| 217 | SelectableTextBlock.Text | Undo hide |
| 224 | Button.ToolTip.Tip | Restore all manually hidden CAD pads. Combined groups and layer moves stay unchanged. |
| 231 | SelectableTextBlock.Text | Show all |
| 240 | SelectableTextBlock.Text | Combine / split |
| 243 | Button.ToolTip.Tip | Combine selected CAD pads using geometry union (preserves shape). |
| 250 | SelectableTextBlock.Text | Combine |
| 256 | Button.ToolTip.Tip | Split selected combined CAD groups back into their source pads. |
| 263 | SelectableTextBlock.Text | Split selected |
| 282 | SelectableTextBlock.Text | Advanced edit |
| 298 | SelectableTextBlock.Text | Rotate |
| 320 | ComboBox.ToolTip.Tip | Rotate all visible baseline CAD pads in the selected layer as one group. |
| 337 | Button.ToolTip.Tip | Rotate the current DXF edit scope as one group around its shared center. 90° units are recommended. |
| 346 | SelectableTextBlock.Text | Move to existing layer |
| 352 | ComboBox.ToolTip.Tip | Target layer for selected CAD pads. |
| 357 | Button.ToolTip.Tip | Move selected CAD pads to target layer. |
| 366 | SelectableTextBlock.Text | Create new layer |
| 371 | TextBox.PlaceholderText | New DXF layer |
| 376 | Button.ToolTip.Tip | Create new layer and move selected CAD pads. |
| 398 | SelectableTextBlock.Text | Layers |
| 422 | SelectableTextBlock.Text | Bounds source layer |
| 425 | SelectableTextBlock.Text | Used when alignment mode is DXF bounds. |
| 430 | ComboBox.ToolTip.Tip | Layer used to determine grid bounds when using DXF alignment. |

### `Views/NotchDetailWindow.axaml`  -  Notch detail window

| line | element.attribute | text |
|---:|---|---|
| 12 | Window.Title | Notch detail |
| 32 | SelectableTextBlock.Text | Below TH (skip notch output) |
| 49 | SelectableTextBlock.Text | Allocations (Q7) |
| 62 | SelectableTextBlock.Text | Regular id |
| 65 | SelectableTextBlock.Text | Coord |
| 68 | SelectableTextBlock.Text | Q7 |
| 72 | SelectableTextBlock.Text | Ratio |

### `Views/NotchExportColumnFilterWindow.axaml`  -  Export Notch Rows > column filter dialog

| line | element.attribute | text |
|---:|---|---|
| 13 | Window.Title | Column filter |
| 29 | Button.Content | Select all |
| 32 | Button.Content | Clear |
| 41 | CheckBox.Content | Use numeric range |
| 47 | SelectableTextBlock.Text | From |
| 50 | TextBox.PlaceholderText | min |
| 54 | SelectableTextBlock.Text | To |
| 57 | TextBox.PlaceholderText | max |
| 61 | SelectableTextBlock.Text | Large value set detected: range mode is recommended. |
| 96 | Button.Content | Cancel |
| 99 | Button.Content | Apply |

### `Views/NotchExportFilterBuilderWindow.axaml`  -  Export Notch Rows > filter helper dialog

| line | element.attribute | text |
|---:|---|---|
| 11 | Window.Title | Filter helper |
| 16 | SelectableTextBlock.Text | Build filter query |
| 20 | SelectableTextBlock.Text | Examples: ic=1 diff=20 status=warning a=20 cad=3777. Plain text still does fuzzy search. |
| 30 | SelectableTextBlock.Text | IC |
| 32 | TextBox.PlaceholderText | e.g. 1 |
| 36 | SelectableTextBlock.Text | Diff |
| 38 | TextBox.PlaceholderText | e.g. 20 |
| 42 | SelectableTextBlock.Text | REG |
| 44 | TextBox.PlaceholderText | e.g. 4820 |
| 48 | SelectableTextBlock.Text | CAD |
| 50 | TextBox.PlaceholderText | e.g. 3777 / none |
| 54 | SelectableTextBlock.Text | Anchor (A) |
| 56 | TextBox.PlaceholderText | e.g. 20 |
| 60 | SelectableTextBlock.Text | Status |
| 64 | ComboBoxItem.Content | Any |
| 65 | ComboBoxItem.Content | Linked |
| 66 | ComboBoxItem.Content | Warning |
| 67 | ComboBoxItem.Content | No CAD |
| 68 | ComboBoxItem.Content | Legacy |
| 72 | SelectableTextBlock.Text | Fuzzy text (optional) |
| 74 | TextBox.PlaceholderText | e.g. CAD 3777 / transfer |
| 81 | SelectableTextBlock.Text | Generated query |
| 91 | Button.Content | Clear |
| 94 | Button.Content | Cancel |
| 97 | Button.Content | Apply |

### `Views/NotchExportSelectionWindow.axaml`  -  Export Notch Rows window

| line | element.attribute | text |
|---:|---|---|
| 17 | Window.Title | Export Notch Rows |
| 18 | ReviewWorkspaceShell.Title | Export Notch Rows |
| 29 | SelectableTextBlock.Text | Notch version |
| 30 | SelectableTextBlock.ToolTip.Tip | Limits visible/exported rows to this scope. When confirming export, single-version choice is also kept for next project save. |
| 47 | SelectableTextBlock.Text | Export format |
| 48 | SelectableTextBlock.ToolTip.Tip | Select output file format for this export run. |
| 68 | Button.Content (StringFormat) | Selected {0} |
| 70 | Button.ToolTip.Tip | Toggle selected-only view filter. |
| 74 | Button.Content (StringFormat) | Transfer {0} |
| 77 | Button.ToolTip.Tip | Show transfer rows only. Click again to clear. |
| 81 | Button.Content (StringFormat) | Linked {0} |
| 84 | Button.ToolTip.Tip | Show linked rows only. Click again to clear. |
| 88 | Button.Content (StringFormat) | Warning {0} |
| 91 | Button.ToolTip.Tip | Show warning rows only. Click again to clear. |
| 95 | Button.Content (StringFormat) | No CAD {0} |
| 98 | Button.ToolTip.Tip | Show rows without CAD mapping only. Click again to clear. |
| 102 | Button.Content (StringFormat) | Legacy {0} |
| 105 | Button.ToolTip.Tip | Show legacy rows only. Click again to clear. |
| 170 | Button.ToolTip.Tip | Reset filters (view all) |
| 181 | Button.ToolTip.Tip | Filter helper |
| 194 | SelectableTextBlock.Text | Sort: |
| 238 | SelectableTextBlock.Text | Search: All |
| 261 | SelectableTextBlock.Text | Inspector |
| 263 | SelectableTextBlock.ToolTip.Tip | Current row details and workspace-linked preview. |
| 283 | SelectableTextBlock.Text | Linked |
| 289 | SelectableTextBlock.Text | Warning |
| 295 | SelectableTextBlock.Text | No CAD |
| 301 | SelectableTextBlock.Text | Legacy |
| 311 | SelectableTextBlock.Text | Payload |
| 312 | SelectableTextBlock.ToolTip.Tip | Raw export payload for current row. |
| 316 | Button.ToolTip.Tip | Copy payload |
| 329 | SelectableTextBlock.Text | Anchor |
| 335 | SelectableTextBlock.Text | Combine |
| 343 | SelectableTextBlock.Text | Target 1 |
| 349 | SelectableTextBlock.Text | Target 2 |
| 360 | SelectableTextBlock.Text | Mapping |
| 361 | SelectableTextBlock.ToolTip.Tip | REG -> CAD link and mapping status. |
| 374 | SelectableTextBlock.Text | Analysis |
| 375 | SelectableTextBlock.ToolTip.Tip | Short outcome and ratio summary. |
| 379 | SelectableTextBlock.Text | Effect |
| 385 | SelectableTextBlock.Text | Target ratio sum |
| 391 | SelectableTextBlock.Text | Combine delta |
| 401 | SelectableTextBlock.Text | Select one row on the left to inspect payload and mapping. |
| 407 | SelectableTextBlock.Text | Simulation safety |
| 408 | SelectableTextBlock.ToolTip.Tip | Step 5 reads the current Simulation safety audit before export. |
| 423 | SelectableTextBlock.Text | To Full coverage |
| 424 | SelectableTextBlock.ToolTip.Tip | Checks whether ToFull-affected target diffs are covered by emitted canonical rows. |
| 433 | SelectableTextBlock.Text | Missing targets |
| 446 | SelectableTextBlock.Text | AA preview / workspace locate |
| 447 | SelectableTextBlock.ToolTip.Tip | Locate selected/export rows by current workspace pad selection. |
| 455 | Button.Content | Hide panel (inspect AA) |
| 469 | SelectableTextBlock.Text | Linked rows: |
| 471 | Button.Content | Select linked rows |
| 474 | Button.Content | Clear linked rows |
| 479 | SelectableTextBlock.Text | Checkbox = single-row selection; linked-row actions are bulk operations. |
| 484 | SelectableTextBlock.Text | Keep this window open, then click CAD / REG pads in workspace to locate linked rows. |
| 514 | Button.Content | Preview |
| 532 | SelectableTextBlock.Text | Low-frequency detail |
| 533 | SelectableTextBlock.ToolTip.Tip | Expanded debug detail for payload columns and comments. |
| 540 | SelectableTextBlock.Text | Payload details |
| 547 | SelectableTextBlock.Text | Column breakdown |
| 554 | SelectableTextBlock.Text | Flags |
| 561 | SelectableTextBlock.Text | Values / Comment |
| 584 | Button.Content | Cancel |

### `Views/PadInfoPopover.axaml`  -  Pad info popover (footer)

| line | element.attribute | text |
|---:|---|---|
| 25 | SelectableTextBlock.Text | Unsaved changes. Apply or discard. |
| 33 | Button.Content | Apply |
| 34 | Button.ToolTip.Tip | Apply changes and close. |
| 36 | Button.Content | Discard |
| 37 | Button.ToolTip.Tip | Discard changes and close. |

### `Views/PadInfoSections/CadPadInfoContentView.axaml`  -  Pad info popover > CAD pad

| line | element.attribute | text |
|---:|---|---|
| 30 | Button.ToolTip.Tip | Focus the best match when available; otherwise highlight related regular pads. |
| 45 | SelectableTextBlock.Text | Geometry seed |
| 54 | SelectableTextBlock.Text | To Full |
| 58 | SelectableTextBlock.Text | To Regular |
| 62 | SelectableTextBlock.Text | Combined |
| 66 | SelectableTextBlock.Text | Confidence |
| 70 | SelectableTextBlock.Text | Source |
| 80 | SelectableTextBlock.Text | Reason |
| 94 | PadInfoSectionFrame.Title | Relation |
| 102 | SelectableTextBlock.Text | Seed match |
| 112 | SelectableTextBlock.Text | Rows |
| 118 | SelectableTextBlock.Text | Targets |
| 124 | SelectableTextBlock.Text | Stage3 |
| 133 | SelectableTextBlock.Text | Owner summary |
| 141 | SelectableTextBlock.Text | Area allocation |
| 148 | PadInfoSectionFrame.Title | Geometry / Debug |
| 151 | Button.ToolTip.Tip | Show or hide geometry, diagnostics, and rule trace details. |
| 176 | SelectableTextBlock.Text | Area |
| 182 | SelectableTextBlock.Text | Vertices |
| 188 | SelectableTextBlock.Text | Centroid |
| 194 | SelectableTextBlock.Text | CAD id |
| 200 | SelectableTextBlock.Text | Layer |
| 208 | SelectableTextBlock.Text | Name |
| 219 | SelectableTextBlock.Text | To Full rule trace |
| 228 | SelectableTextBlock.Text | Inspector rule trace |
| 247 | PadInfoSectionFrame.Title | Overrides |
| 252 | SelectableTextBlock.Text | CAD Output FW Diff |
| 261 | NumberScrubber.ToolTip.Tip | Manual CAD Output FW Diff for this CAD pad (supports jump/offset). |
| 265 | Button.ToolTip.Tip | Apply this FW output override. |
| 273 | Button.ToolTip.Tip | Clear the FW output override. |
| 281 | Button.ToolTip.Tip | Use this CAD pad as the per-IC CAD Output FW Diff start anchor. |
| 290 | Button.ToolTip.Tip | Clear the per-IC CAD Output FW Diff start anchor. |
| 308 | Button.ToolTip.Tip | Select the current CAD area bucket. |
| 314 | SelectableTextBlock.Text | Area bucket action is available for single CAD selection. |

### `Views/PadInfoSections/PadInfoRelationCardListView.axaml`  -  Pad info popover > relation cards

| line | element.attribute | text |
|---:|---|---|
| 41 | SelectableTextBlock.Text | Area / flow |

### `Views/PadInfoSections/RegularPadInfoContentView.axaml`  -  Pad info popover > Regular pad

| line | element.attribute | text |
|---:|---|---|
| 42 | Button.ToolTip.Tip | Focus the best match when available; otherwise highlight related CAD pads. |
| 57 | SelectableTextBlock.Text | Match |
| 66 | SelectableTextBlock.Text | Freeform |
| 70 | SelectableTextBlock.Text | Diff idx |
| 74 | SelectableTextBlock.Text | Diff source |
| 78 | SelectableTextBlock.Text | IC |
| 82 | SelectableTextBlock.Text | Confidence |
| 86 | SelectableTextBlock.Text | Notch rows |
| 95 | SelectableTextBlock.Text | Owner summary |
| 101 | PadInfoSectionFrame.Title | Relation |
| 108 | SelectableTextBlock.Text | Location |
| 114 | SelectableTextBlock.Text | Owners |
| 123 | SelectableTextBlock.Text | CAD overlap areas |
| 129 | SelectableTextBlock.Text | Notch row flow |
| 137 | SelectableTextBlock.Text | Rows |
| 141 | SelectableTextBlock.Text | Cols |
| 149 | PadInfoSectionFrame.Title | Geometry / Debug |
| 152 | Button.ToolTip.Tip | Show or hide geometry and rule trace details. |
| 157 | SelectableTextBlock.Text | geometry + trace |
| 176 | SelectableTextBlock.Text | Area |
| 180 | SelectableTextBlock.Text | Centroid |
| 184 | SelectableTextBlock.Text | Bounds |
| 190 | SelectableTextBlock.Text | Regular id |
| 194 | SelectableTextBlock.Text | Match / freeform |
| 215 | PadInfoSectionFrame.Title | Edit tools |
| 224 | SelectableTextBlock.Text | Pad width (mm) |
| 232 | NumberScrubber.ToolTip.Tip | Overrides pad width for selected rows/cols. |
| 234 | SelectableTextBlock.Text | Mixed values. Enter to override selection. |
| 241 | SelectableTextBlock.Text | Pad height (mm) |
| 249 | NumberScrubber.ToolTip.Tip | Overrides pad height for the selected rows/cols. |
| 251 | SelectableTextBlock.Text | Mixed values. Enter to override selection. |
| 258 | SelectableTextBlock.Text | Freeform |
| 262 | Button.Content | None |
| 264 | Button.ToolTip.Tip | Mark selection as none. |
| 267 | Button.Content | X |
| 269 | Button.ToolTip.Tip | Mark selection as X-way. |
| 272 | Button.Content | Y |
| 274 | Button.ToolTip.Tip | Mark selection as Y-way. |
| 277 | Button.Content | XY |
| 279 | Button.ToolTip.Tip | Mark selection as XY-way. |
| 285 | SelectableTextBlock.Text | Unsaved changes |
| 289 | Button.Content | Apply changes |
| 294 | Button.Content | Reset size |

### `Views/RightWorkflowPanel.axaml`  -  Workspace tab > right panel (validation rows)

| line | element.attribute | text |
|---:|---|---|
| 19 | Button.ToolTip.Tip | Focus this validation row on canvas. |
| 40 | SelectableTextBlock.Text | Source diff |
| 44 | SelectableTextBlock.Text | Target diff |
| 48 | SelectableTextBlock.Text | Ratio |
| 52 | SelectableTextBlock.Text | Area |
| 57 | SelectableTextBlock.Text | Values |
| 61 | SelectableTextBlock.Text | Comment |

### `Views/RightWorkflowSections/RightWorkflowInspectorView.axaml`  -  Workspace tab > right panel > Inspector

| line | element.attribute | text |
|---:|---|---|
| 13 | HidePanelBlock.Title | Inspector snapshot |
| 21 | SelectableTextBlock.Text | Select a CAD or regular pad to inspect. |
| 27 | SelectableTextBlock.Text | Quick focus |
| 32 | TextBox.PlaceholderText | diff 72 / cad 4809 / reg 4616 |
| 33 | TextBox.ToolTip.Tip | Focus by typed target. Examples: diff 72, ic1 diff72, cad 4809, reg 4616. Plain number auto-resolves CAD, REG, then diff. |
| 36 | Button.Content | Focus |
| 38 | Button.ToolTip.Tip | Select and zoom the matching CAD output pad, regular pad, or FW diff. |
| 78 | SelectableTextBlock.Text | Notch / Freeform |
| 89 | SelectableTextBlock.Text | / |
| 92 | SelectableTextBlock.Text | \ |
| 123 | SelectableTextBlock.Text | Notch 2.2 details are computing in background... |
| 139 | SelectableTextBlock.Text | Debug details |
| 160 | SelectableTextBlock.Text | Snapshot details |
| 170 | SelectableTextBlock.Text | Diff source |
| 174 | SelectableTextBlock.Text | Diff idx |
| 179 | SelectableTextBlock.Text | Match confidence |
| 184 | SelectableTextBlock.Text | Match |
| 200 | SelectableTextBlock.Text | Diff source |
| 204 | SelectableTextBlock.Text | Diff idx |
| 209 | SelectableTextBlock.Text | Match confidence |
| 214 | SelectableTextBlock.Text | Match |
| 241 | SelectableTextBlock.Text (StringFormat) | Rule trace (x{0}) |
| 243 | SelectableTextBlock.ToolTip.Tip | Shows rule-by-rule reasoning for the current pad. |
| 268 | SelectableTextBlock.Text | / |
| 271 | SelectableTextBlock.Text | \ |
| 303 | SelectableTextBlock.Text (StringFormat) | Raw x{0} |
| 316 | SelectableTextBlock.Text | / |
| 319 | SelectableTextBlock.Text | \ |

### `Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml`  -  Workspace tab > right panel > Settings

| line | element.attribute | text |
|---:|---|---|
| 9 | HidePanelBlock.Title | Overview |
| 12 | Border.ToolTip.Tip | Project geometry and regular-grid readiness summary. Edit values in Settings > General. |
| 15 | SelectableTextBlock.Text | Project / grid |
| 20 | Button.ToolTip.Tip | Open Settings > General. |
| 28 | SelectableTextBlock.Text | CAD |
| 32 | SelectableTextBlock.ToolTip.Tip | Loaded CAD pad count after DXF import and current DXF edits. |
| 35 | SelectableTextBlock.Text | REG |
| 39 | SelectableTextBlock.ToolTip.Tip | Generated regular-grid pad count. |
| 42 | SelectableTextBlock.Text | IC |
| 47 | SelectableTextBlock.ToolTip.Tip | Cascade IC count from Settings > General. |
| 51 | SelectableTextBlock.Text | X |
| 56 | SelectableTextBlock.ToolTip.Tip | X-channel count per IC from Settings > General. |
| 59 | SelectableTextBlock.Text | Y |
| 64 | SelectableTextBlock.ToolTip.Tip | Y-channel count per IC from Settings > General. |
| 80 | Border.ToolTip.Tip | Current notch allocation semantics. Detailed controls live in Settings > Step 3. |
| 83 | SelectableTextBlock.Text | Notch model |
| 88 | Button.ToolTip.Tip | Open Settings > Step 3. |
| 96 | SelectableTextBlock.Text | Model |
| 103 | SelectableTextBlock.Text | Alloc |
| 138 | Border.ToolTip.Tip | Simulation safety is projected from the current Simulation workspace. Refresh Simulation after geometry or notch changes. |
| 141 | SelectableTextBlock.Text | Simulation safety |
| 152 | SelectableTextBlock.Text | Cap |
| 156 | SelectableTextBlock.ToolTip.Tip | EMS safety cap used by the current Simulation audit. |
| 159 | SelectableTextBlock.Text | Max |
| 165 | SelectableTextBlock.ToolTip.Tip | Maximum After value observed in the current Simulation audit. |
| 168 | SelectableTextBlock.Text | Risk |
| 175 | SelectableTextBlock.ToolTip.Tip | Number of regular pads exceeding the EMS safety cap. |
| 194 | SelectableTextBlock.Text | Export handoff |
| 199 | Button.ToolTip.Tip | Open Settings > Step 5. |
| 207 | SelectableTextBlock.Text | File |
| 211 | SelectableTextBlock.ToolTip.Tip | Step 5 export file type. |
| 214 | SelectableTextBlock.Text | Profile |
| 219 | SelectableTextBlock.ToolTip.Tip | Step 5 export profile. |
| 252 | HidePanelBlock.Title | Step 6 - Validation Quick Trace |
| 263 | Button.ToolTip.Tip | Clear validation trace. |
| 275 | NumberScrubber.ToolTip.Tip | Target REG id to inspect row/flow trace. |
| 278 | Button.Content | Use Selected REG |
| 280 | Button.ToolTip.Tip | Copy currently selected REG id and run validation. |
| 283 | Button.Content | Analyze |
| 285 | Button.ToolTip.Tip | Analyze direct rows and incoming/outgoing allocations for this REG. |
| 291 | SelectableTextBlock.Text (StringFormat) | DIRECT {0} |
| 296 | SelectableTextBlock.Text (StringFormat) | IN {0} |
| 301 | SelectableTextBlock.Text (StringFormat) | OUT {0} |
| 306 | SelectableTextBlock.Text | Click one row to focus source/target pads on AA canvas. |
| 309 | HidePanelBlock.Title | DIRECT details |
| 321 | SelectableTextBlock.Text | No DIRECT rows for current REG. |
| 332 | HidePanelBlock.Title | IN details |
| 344 | SelectableTextBlock.Text | No IN rows for current REG. |
| 355 | HidePanelBlock.Title | OUT details |
| 367 | SelectableTextBlock.Text | No OUT rows for current REG. |
| 381 | SelectableTextBlock.Text | No rows yet. Run Analyze for current REG. |

### `Views/SettingsSections/SettingsGeneralSectionView.axaml`  -  Settings window > General

| line | element.attribute | text |
|---:|---|---|
| 11 | SelectableTextBlock.Text | Grid and mapping |
| 13 | SelectableTextBlock.Text | Set the IC layout, AA size, scan direction, and secondary coordinate/alignment options. |
| 18 | SelectableTextBlock.Text | Core setup · most projects start here. |
| 27 | SettingsInfoLabel.Text | Cascade |
| 27 | SettingsInfoLabel.Tip | Number of IC cascade blocks used to build the full regular grid. |
| 39 | Button.ToolTip.Tip | Edit per-IC X/Y channels. |
| 50 | SettingsInfoLabel.Text | Total X channels |
| 50 | SettingsInfoLabel.Tip | Computed from per-IC X channels in Cascade details. |
| 59 | SettingsInfoLabel.Text | Total Y channels |
| 59 | SettingsInfoLabel.Tip | Computed as the max Y channel count across Cascade details. |
| 68 | SettingsInfoLabel.Text | AA size X (mm) |
| 68 | SettingsInfoLabel.Tip | Active-area width in millimeters for generated regular grid placement. |
| 81 | SettingsInfoLabel.Text | AA size Y (mm) |
| 81 | SettingsInfoLabel.Tip | Active-area height in millimeters for generated regular grid placement. |
| 94 | SettingsInfoLabel.Text | Scan order |
| 94 | SettingsInfoLabel.Tip | Diff-index numbering direction for the generated regular grid. |
| 109 | SettingsInfoLabel.Text | Regular source |
| 109 | SettingsInfoLabel.Tip | Choose generated regular grid or a DXF layer as the regular-pad source. |
| 124 | SettingsInfoLabel.Text | Regular layer |
| 124 | SettingsInfoLabel.Tip | DXF layer used as regular geometry when Regular source is DXF layer. |
| 143 | SettingsInfoLabel.Text | Grid padding (%) |
| 143 | SettingsInfoLabel.Tip | Padding percentage applied when generating the regular grid inside the AA size. |
| 156 | SelectableTextBlock.Text | Advanced mapping · adjust only for coordinate output or panel alignment. |
| 165 | SettingsInfoLabel.Text | Coordinate pixel X |
| 165 | SettingsInfoLabel.Tip | FW reported X-coordinate width used by Coordinate planning and export diagnostics; it does not change regular grid geometry or notch allocation. |
| 177 | SettingsInfoLabel.Text | Coordinate pixel Y |
| 177 | SettingsInfoLabel.Tip | FW reported Y-coordinate height used by Coordinate planning and export diagnostics; it does not change regular grid geometry or notch allocation. |
| 192 | SettingsInfoLabel.Text | Panel bias X |
| 192 | SettingsInfoLabel.Tip | Horizontal panel alignment offset used only in panel alignment mode. |
| 203 | SettingsInfoLabel.Text | Panel bias Y |
| 203 | SettingsInfoLabel.Tip | Vertical panel alignment offset used only in panel alignment mode. |
| 226 | SelectableTextBlock.Text | Import and project behavior |
| 228 | SelectableTextBlock.Text | DXF import scope and project-load behavior. |
| 241 | SettingsInfoLabel.Text | Only closed polylines |
| 241 | SettingsInfoLabel.Tip | Import only closed DXF polylines as CAD pads; open lines are ignored. |
| 243 | SelectableTextBlock.Text | Closed DXF shapes only. |
| 261 | SettingsInfoLabel.Text | Import block polylines |
| 261 | SettingsInfoLabel.Tip | Also scan polylines nested inside DXF BLOCK definitions. |
| 263 | SelectableTextBlock.Text | Include polylines inside BLOCKS. |
| 281 | SettingsInfoLabel.Text | Auto bounds from visible layers |
| 281 | SettingsInfoLabel.Tip | Recompute DXF bounds from currently visible layers after layer filters change. |
| 283 | SelectableTextBlock.Text | Recompute bounds after layer filters change. |
| 301 | SettingsInfoLabel.Text | Apply visual preferences on load |
| 301 | SettingsInfoLabel.Tip | After project load, keep project geometry but reapply app-level display preferences. |
| 303 | SelectableTextBlock.Text | Use app display preferences after load. |
| 322 | SelectableTextBlock.Text | Application appearance |
| 324 | SelectableTextBlock.Text | Workspace display scale and generated grid alignment. |
| 334 | SettingsInfoLabel.Text | Grid alignment mode |
| 334 | SettingsInfoLabel.Tip | Controls how the generated regular grid is aligned to panel AA or CAD geometry. |
| 349 | SettingsInfoLabel.Text | Log level |
| 349 | SettingsInfoLabel.Tip | Minimum log verbosity written to the app console and log file. |
| 364 | SettingsInfoLabel.Text | Global font size (%) |
| 364 | SettingsInfoLabel.Tip | Scales UI text across the app; project geometry is unchanged. |
| 376 | SettingsInfoLabel.Text | Highlight line width adjust |
| 376 | SettingsInfoLabel.Tip | Extra stroke width for selected or highlighted pads on canvas. |
| 392 | ToggleSwitch.OnContent | Local |
| 393 | ToggleSwitch.OffContent | Global |
| 397 | SettingsInfoLabel.Text | Sizing scope |
| 397 | SettingsInfoLabel.Tip | Local adjusts selected rows or columns; Global redistributes the whole generated grid. |
| 399 | SelectableTextBlock.Text | Local adjusts row/column sizing; Global redistributes the whole grid. |
| 413 | SelectableTextBlock.Text | Layer categories |
| 415 | SelectableTextBlock.Text | Batch toggle DXF layers by parsed content category. |
| 440 | Button.Content | On |
| 444 | Button.Content | Off |

### `Views/SettingsSections/SettingsStep1SectionView.axaml`  -  Settings window > Step 1

| line | element.attribute | text |
|---:|---|---|
| 10 | SelectableTextBlock.Text | Geometry match |
| 12 | SelectableTextBlock.Text | Diagnostics threshold only; overlap links stay unchanged. |
| 16 | SettingsInfoLabel.Text | Unmatched TH (overlap / regular area) |
| 17 | SettingsInfoLabel.Tip | Diagnostics threshold for unmatched regular pads; it does not rewrite Step 1 links. |

### `Views/SettingsSections/SettingsStep2SectionView.axaml`  -  Settings window > Step 2

| line | element.attribute | text |
|---:|---|---|
| 10 | SelectableTextBlock.Text | Freeform tagging |
| 12 | SelectableTextBlock.Text | Freeform thresholds and auto-replay behavior. |
| 22 | SettingsInfoLabel.Text | Freeform TH (dominant axis ratio) |
| 22 | SettingsInfoLabel.Tip | Dominant-axis ratio threshold used when Step 2 tags XWay, YWay, or XYWay. |
| 38 | SettingsInfoLabel.Text | XY WAY EN |
| 38 | SettingsInfoLabel.Tip | Allow Step 2 to detect X/Y freeform direction automatically. |
| 40 | SelectableTextBlock.Text | Detect X/Y direction automatically. |
| 58 | SettingsInfoLabel.Text | Edge spill EN |
| 58 | SettingsInfoLabel.Tip | Enable edge-specific freeform rules for boundary spill cases. |
| 60 | SelectableTextBlock.Text | Use edge-specific freeform rules. |
| 78 | SettingsInfoLabel.Text | Load auto-run Step 2 |
| 78 | SettingsInfoLabel.Tip | Replay Step 2 tagging automatically after a project is loaded. |
| 80 | SelectableTextBlock.Text | Replay tagging after project load. |

### `Views/SettingsSections/SettingsStep3SectionView.axaml`  -  Settings window > Step 3

| line | element.attribute | text |
|---:|---|---|
| 10 | SelectableTextBlock.Text | Current compensation model |
| 12 | SelectableTextBlock.Text | Single source of truth for Step 3 behavior. |
| 15 | SettingsInfoLabel.Text | Compensation model |
| 15 | SettingsInfoLabel.Tip | Select the Step 3 compensation model that drives To Regular and To Full behavior. |
| 45 | SelectableTextBlock.Text | Signal semantics |
| 47 | SelectableTextBlock.Text | Area ratio stays primary; To Full only gates boundary support. |
| 67 | SelectableTextBlock.Text | To Regular meaning |
| 69 | SelectableTextBlock.Text | Restore area signal before redistribution. |
| 74 | SelectableTextBlock.Text | To Full meaning |
| 76 | SelectableTextBlock.Text | Allow boundary targets; cap impossible expansion. |
| 87 | SelectableTextBlock.Text | Advanced allocation gates |
| 89 | SelectableTextBlock.Text | Compute gates and diagnostic trace controls. |
| 98 | SettingsInfoLabel.Text | To Regular EN (derived) |
| 98 | SettingsInfoLabel.Tip | Derived from the selected compensation model; restores area-proportional signal before redistribution. |
| 100 | SelectableTextBlock.Text | From selected model. |
| 116 | SettingsInfoLabel.Text | To Full EN (derived) |
| 116 | SettingsInfoLabel.Tip | Derived from the selected compensation model; enables boundary support/cap logic. |
| 118 | SelectableTextBlock.Text | From selected model. |
| 131 | SettingsInfoLabel.Text | Multi-owner strict overlap (%) |
| 131 | SettingsInfoLabel.Tip | Minimum regular-area overlap ratio required before a CAD can own or block a boundary target. |
| 139 | SelectableTextBlock.Text | Higher rejects ambiguous boundary targets sooner. |
| 149 | SettingsInfoLabel.Text | Use boundary area cap |
| 149 | SettingsInfoLabel.Tip | Cap To Full virtual expansion before it contributes to Stage3 gain/allocation. This is the beta0.9 edge EMS guard. |
| 151 | SelectableTextBlock.Text | Limits outside support area against the inside overlap. |
| 163 | SettingsInfoLabel.Text | Virtual cap (%) |
| 163 | SettingsInfoLabel.Tip | Maximum virtual To Full area relative to original overlap area. 100% means outside virtual area cannot exceed inside overlap area. |
| 182 | SettingsInfoLabel.Text | Use target coverage guard |
| 182 | SettingsInfoLabel.Tip | After CurrentGain rows are selected, cap each target FW diff's total coverage to avoid uniform-field EMS overflow. |
| 184 | SelectableTextBlock.Text | Prevents multiple sources from stacking too much signal on one diff. |
| 196 | SettingsInfoLabel.Text | Target cap (%) |
| 215 | SettingsInfoLabel.Text | Use To Full rule engine |
| 215 | SettingsInfoLabel.Tip | ON uses the newer centralized To Full boundary gate engine; OFF falls back to the legacy inline gate path. |
| 217 | SelectableTextBlock.Text | Controls the To Full boundary-gate implementation path. |
| 232 | SettingsInfoLabel.Text | Keep To Full trace |
| 232 | SettingsInfoLabel.Tip | Store per-regular To Full rule decisions for diagnostics and runtime query payloads. |
| 234 | SelectableTextBlock.Text | Adds diagnostics only; notch output is unchanged. |
| 255 | SelectableTextBlock.Text | AA preview display |
| 257 | SelectableTextBlock.Text | Display-only diagnostics; generated notch rows are unchanged. |
| 266 | SettingsInfoLabel.Text | Stage auto play |
| 266 | SettingsInfoLabel.Tip | Loop the Step 3 preview layers automatically in the AA canvas. |
| 268 | SelectableTextBlock.Text | Loop Step 3 preview layers. |
| 280 | SettingsInfoLabel.Text | Interval (ms) |
| 280 | SettingsInfoLabel.Tip | Delay between Step 3 preview layers during auto play. |
| 295 | SettingsInfoLabel.Text | Show To Regular labels |
| 295 | SettingsInfoLabel.Tip | Display To Regular ratio labels on the Step 3 preview canvas only. |
| 297 | SelectableTextBlock.Text | Show To Regular ratios. |
| 312 | SettingsInfoLabel.Text | Show To Full overlay |
| 312 | SettingsInfoLabel.Tip | Display To Full support stages on the Step 3 preview canvas only. |
| 314 | SelectableTextBlock.Text | Show To Full support stages. |
| 329 | SettingsInfoLabel.Text | To Full layer stage |
| 329 | SettingsInfoLabel.Tip | Choose the To Full preview layer: seed, candidate, or final support result. |
| 338 | SelectableTextBlock.Text | Display contract |
| 340 | SelectableTextBlock.Text | Preview switches do not change computation. |

### `Views/SettingsSections/SettingsStep4SectionView.axaml`  -  Settings window > Step 4

| line | element.attribute | text |
|---:|---|---|
| 10 | SelectableTextBlock.Text | Mapping score weights |
| 12 | SelectableTextBlock.Text | Geometry score weights for mapping diagnostics. |
| 22 | SettingsInfoLabel.Text | Weight IoU |
| 22 | SettingsInfoLabel.Tip | Mapping score weight for geometric overlap quality between CAD and regular pads. |
| 35 | SettingsInfoLabel.Text | Weight distance |
| 35 | SettingsInfoLabel.Tip | Mapping score weight for CAD/regular centroid distance. |
| 48 | SettingsInfoLabel.Text | Weight area |
| 48 | SettingsInfoLabel.Tip | Mapping score weight for CAD/regular area similarity. |
| 61 | SettingsInfoLabel.Text | Candidate number |
| 61 | SettingsInfoLabel.Tip | How many geometry candidates are retained for Step 4 mapping diagnostics. |
| 73 | SettingsInfoLabel.Text | Low confidence threshold |
| 73 | SettingsInfoLabel.Tip | Mappings below this confidence are flagged for review. |
| 86 | SettingsInfoLabel.Text | Ambiguous margin (best - 2nd) |
| 86 | SettingsInfoLabel.Tip | If best score minus second score is below this margin, mapping is flagged ambiguous. |
| 103 | SelectableTextBlock.Text | CAD Output FW Diff diagnostics |
| 105 | SelectableTextBlock.Text | CAD output FW diff numbering and visibility. |
| 115 | SettingsInfoLabel.Text | CAD Output FW Diff strategy |
| 115 | SettingsInfoLabel.Tip | Strategy used to assign CAD Output FW Diff indices from geometry seed and overrides. |
| 134 | SettingsInfoLabel.Text | CAD Output FW Diff start |
| 134 | SettingsInfoLabel.Tip | Starting index for automatic CAD Output FW Diff numbering. |
| 146 | SelectableTextBlock.Text | Future validation path |
| 148 | SelectableTextBlock.Text | Reserved for CSV-confirmed geometry seed. |
| 159 | SettingsInfoLabel.Text | Show CAD Output FW Diff markers |
| 159 | SettingsInfoLabel.Tip | Display CAD output diff markers on canvas; mapping data is unchanged. |
| 161 | SelectableTextBlock.Text | Display only; mapping result unchanged. |

### `Views/SettingsSections/SettingsStep5SectionView.axaml`  -  Settings window > Step 5

| line | element.attribute | text |
|---:|---|---|
| 10 | SelectableTextBlock.Text | Safety before FW handoff |
| 26 | SelectableTextBlock.Text | Export target |
| 28 | SelectableTextBlock.Text | Notch versions, output type, and C profile. |
| 38 | SettingsInfoLabel.Text | Export versions |
| 39 | SettingsInfoLabel.Tip | Choose which notch table versions are emitted during export. |
| 43 | SelectableTextBlock.Text | v2.1 |
| 53 | SelectableTextBlock.Text | v2.2 |
| 66 | SettingsInfoLabel.Text | Export type |
| 66 | SettingsInfoLabel.Tip | Choose the generated file type, such as C source or CSV diagnostics. |
| 81 | SettingsInfoLabel.Text | C export profile |
| 81 | SettingsInfoLabel.Tip | Select the C output profile expected by the downstream FW integration. |
| 96 | SettingsInfoLabel.Text | Null value |
| 96 | SettingsInfoLabel.Tip | Placeholder value used for empty entries in exported notch payloads. |
| 112 | SelectableTextBlock.Text | Gate thresholds |
| 114 | SelectableTextBlock.Text | Gate thresholds for v2.1 and v2.2 export. |
| 124 | SettingsInfoLabel.Text | Notch gate TH v2.1 (Q7/128) |
| 124 | SettingsInfoLabel.Tip | v2.1 gate threshold stored in Q7 units where 128 means 100 percent. |
| 137 | SettingsInfoLabel.Text | Notch gate TH v2.2 (%) |
| 137 | SettingsInfoLabel.Tip | v2.2 gate threshold expressed directly as percent. |
| 155 | SettingsInfoLabel.Text | Link TH (v2.1 Q7 <-> v2.2 %) |
| 155 | SettingsInfoLabel.Tip | Keep v2.1 Q7 and v2.2 percent thresholds synchronized while editing. |
| 157 | SelectableTextBlock.Text | Keep v2.1 and v2.2 thresholds synchronized. |

### `Views/SettingsWindow.axaml`  -  Settings window (chrome / nav)

| line | element.attribute | text |
|---:|---|---|
| 12 | Window.Title | Settings |
| 23 | SelectableTextBlock.Text | Freeform Helper |
| 52 | SelectableTextBlock.Text | Navigation |
| 55 | ToggleButton.Content | General |
| 59 | ToggleButton.Content | Step 1 · Geometry |
| 63 | ToggleButton.Content | Step 2 · Freeform |
| 67 | ToggleButton.Content | Step 3 · Notch |
| 71 | ToggleButton.Content | Step 4 · Index |
| 75 | ToggleButton.Content | Step 5 · Export |
| 121 | SelectableTextBlock.Text | Changes apply only after Save Settings. |
| 127 | Button.Content | Reset all settings |
| 129 | Button.Content | Cancel |
| 131 | Button.Content | Save Settings |

### `Views/SimulationHostView.axaml`  -  Simulation tab (host / empty state)

| line | element.attribute | text |
|---:|---|---|
| 46 | SelectableTextBlock.Text | Refresh |
| 92 | SelectableTextBlock.Text | Build from current workspace |
| 117 | SelectableTextBlock.Text | Simulation blocked |
| 132 | SelectableTextBlock.Text | Retry build |
| 153 | SelectableTextBlock.Text | Building simulation workspace |

### `Views/SimulationWorkspaceView.axaml`  -  Simulation tab

| line | element.attribute | text |
|---:|---|---|
| 20 | SelectableTextBlock.Text | Simulation |
| 22 | SelectableTextBlock.Text | Regular-grid surface simulation. |
| 141 | SelectableTextBlock.Text | Simulation manual override |
| 156 | SelectableTextBlock.Text | Apply override |
| 165 | SelectableTextBlock.Text | Use global |
| 175 | Button.ToolTip.Tip | Fit AA canvas |

### `Views/WarningDialog.axaml`  -  Warning dialog

| line | element.attribute | text |
|---:|---|---|
| 37 | Button.Content | OK |

### `Views/WorkflowSteps/RightWorkflowStep1View.axaml`  -  Workspace tab > right panel > Step 1 card

| line | element.attribute | text |
|---:|---|---|
| 7 | HidePanelBlock.Title | Step 1 · Geometry match |
| 13 | SelectableTextBlock.Text | Run overlap first, then tune unmatched TH for diagnostics visibility only. |
| 19 | Button.ToolTip.Tip | Open Settings at Step 1 section. |
| 27 | Button.ToolTip.Tip | Clear Step 1 result and all downstream results. |
| 32 | Button.Content | Match overlaps |
| 35 | Button.ToolTip.Tip | Step 1: build CAD↔Regular overlap links. |
| 46 | SelectableTextBlock.Text | Unmatched diagnostics threshold |
| 48 | SelectableTextBlock.Text (StringFormat) | {}{0:0.####} · diagnostics only; does not change links |
| 54 | SelectableTextBlock.Text | Regular Visibility Mask |
| 65 | SelectableTextBlock.Text | Use Regular Visibility Mask |
| 73 | ToggleSwitch.ToolTip.Tip | Constrain Step 4 geometry seed and Simulation to active regular pads from imported SeeRegular.csv. |
| 78 | Button.Content | Import mask |
| 81 | Button.ToolTip.Tip | Import Regular Visibility Mask (SeeRegular.csv) and build the regular active mask used by Step 4 and Simulation. |
| 83 | Button.Content | Clear mask |
| 86 | Button.ToolTip.Tip | Remove the imported regular visibility mask and return to pure geometry seed. |

### `Views/WorkflowSteps/RightWorkflowStep2View.axaml`  -  Workspace tab > right panel > Step 2 card

| line | element.attribute | text |
|---:|---|---|
| 7 | HidePanelBlock.Title | Step 2 · Freeform tagging |
| 13 | SelectableTextBlock.Text | Step 2 labels regular pads (None/XWay/YWay/XYWay) without changing Step 1 links. |
| 19 | Button.ToolTip.Tip | Open Settings at Step 2 section. |
| 27 | Button.ToolTip.Tip | Clear Step 2 result and all downstream results. |
| 37 | SelectableTextBlock.Text | Freeform threshold |
| 39 | SelectableTextBlock.Text (StringFormat) | {}{0:0.##} · settings control XY / edge spill / load auto-run |
| 44 | Button.Content | Detect freeform |
| 47 | Button.ToolTip.Tip | Step 2: auto-label freeform type from overlap distribution. |
| 57 | SelectableTextBlock.Text | Type |
| 60 | SelectableTextBlock.Text | Count |
| 64 | SelectableTextBlock.Text | Ratio |
| 87 | SelectableTextBlock.Text | Manual override (selected regular pads) |
| 92 | Button.Content | None |
| 94 | Button.ToolTip.Tip | Mark selection as none. |
| 96 | Button.Content | X |
| 98 | Button.ToolTip.Tip | Mark selection as X-way. |
| 100 | Button.Content | Y |
| 102 | Button.ToolTip.Tip | Mark selection as Y-way. |
| 104 | Button.Content | XY |
| 106 | Button.ToolTip.Tip | Mark selection as XY-way. |

### `Views/WorkflowSteps/RightWorkflowStep3View.axaml`  -  Workspace tab > right panel > Step 3 card

| line | element.attribute | text |
|---:|---|---|
| 13 | HidePanelBlock.Title | Step 3 · Notch 2.2 compensation |
| 25 | Button.ToolTip.Tip | Open Settings at Step 3 section. |
| 33 | Button.ToolTip.Tip | Clear Step 3 result and all downstream results. |
| 40 | SelectableTextBlock.Text | Review before export |
| 46 | SelectableTextBlock.Text | These Step 3 guards affect Simulation and Step 5 C export. Review them before FW handoff. |
| 52 | SelectableTextBlock.Text | Export-critical guards |
| 58 | SelectableTextBlock.Text | Target coverage guard |
| 71 | SelectableTextBlock.Text | Target cap (%) |
| 85 | SelectableTextBlock.Text | Boundary virtual-area cap |
| 98 | SelectableTextBlock.Text | Virtual cap (%) |
| 105 | NumberScrubber.ToolTip.Tip | Maximum virtual To Full area relative to original overlap area. |
| 113 | ToggleButton.ToolTip.Tip | Show or hide detailed To Full / To Regular notes. |
| 117 | SelectableTextBlock.Text | ToFull / allocation details |
| 129 | SelectableTextBlock.Text (StringFormat) | Current strict overlap: {0:0.###}% |
| 133 | SelectableTextBlock.Text | Debug step: use stage arrows for fast To Full layer preview; display toggles are in the canvas toolbar and Settings > Step 3. |
| 138 | SelectableTextBlock.Text | To Full layer stage (AA canvas) |
| 146 | Button.ToolTip.Tip | Previous stage |
| 157 | SelectableTextBlock.Text (StringFormat) | Stage {0:0} |
| 163 | Button.ToolTip.Tip | Next stage |
| 173 | SelectableTextBlock.Text | Auto play |
| 182 | CheckBox.Content | Auto |
| 194 | NumberScrubber.ToolTip.Tip | Auto play interval in ms |
| 198 | SelectableTextBlock.Text | ms |
| 210 | SelectableTextBlock.Text | Layer legend |
| 223 | SelectableTextBlock.Text | 1 · Seed |
| 225 | SelectableTextBlock.Text | CAD polygon overlap source |
| 226 | SelectableTextBlock.ToolTip.Tip | CAD polygon overlap source |
| 250 | SelectableTextBlock.Text | 2 · Candidate |
| 252 | SelectableTextBlock.Text | Boundary regulars |
| 253 | SelectableTextBlock.ToolTip.Tip | Boundary regulars |
| 274 | SelectableTextBlock.Text | 3 · Final |
| 276 | SelectableTextBlock.Text | To Full expansion result |
| 277 | SelectableTextBlock.ToolTip.Tip | To Full expansion result |
| 289 | SelectableTextBlock.Text | AA preview follows current CAD selection. Stage 1 shows polygon-overlap seed, Stage 2 shows boundary candidates, Stage 3 shows final To Full. Use clear icon to hide preview. |

### `Views/WorkflowSteps/RightWorkflowStep4View.axaml`  -  Workspace tab > right panel > Step 4 card

| line | element.attribute | text |
|---:|---|---|
| 7 | HidePanelBlock.Title | Step 4 · Index diagnostics |
| 13 | SelectableTextBlock.Text | Index numbering, duplicate diffs, and visibility mask. |
| 19 | Button.ToolTip.Tip | Open Settings at Step 4 section. |
| 27 | Button.ToolTip.Tip | Clear Step 4 result and downstream Step 5/6 results. |
| 37 | SelectableTextBlock.Text | CAD Output FW Diff assignment |
| 46 | SelectableTextBlock.Text | Duplicate diff status |
| 55 | SelectableTextBlock.Text | Regular Visibility Mask |
| 66 | SelectableTextBlock.Text | Duplicate CAD Output FW Diff groups |
| 69 | SelectableTextBlock.Text | These groups currently share one IC+diff key. Select a group, then shift or override the CAD Output FW Diff. |
| 87 | Button.Content | Select |
| 99 | SelectableTextBlock.Text | Offset selected CAD Output FW Diff |
| 112 | NumberScrubber.ToolTip.Tip | Apply one shared diff offset to all selected CAD output pads. |
| 116 | Button.Content | Shift |
| 117 | Button.ToolTip.Tip | Offset selected CAD Output FW Diff together. Empty gaps are allowed, but they will not become table anchors. |
| 120 | SelectableTextBlock.Text | Gap diffs stay empty. They can remain target-only and will not become anchor rows. |
| 123 | Button.Content | Analyze mapping |
| 126 | Button.ToolTip.Tip | Step 4: analyze CAD↔Regular index mapping and open report window. |
| 134 | SelectableTextBlock.Text | Detailed issues open in a separate report window (copy supported). |

### `Views/WorkflowSteps/RightWorkflowStep5View.axaml`  -  Workspace tab > right panel > Step 5 card

| line | element.attribute | text |
|---:|---|---|
| 7 | HidePanelBlock.Title | Step 5 · Notch export |
| 19 | Button.ToolTip.Tip | Open Settings at Step 5 section. |
| 27 | Button.ToolTip.Tip | Clear Step 5 export result and Step 6 validation result. |
| 34 | SelectableTextBlock.Text | Handoff checklist |
| 41 | SelectableTextBlock.Text | Export file |
| 53 | SelectableTextBlock.Text | Profile |
| 62 | SelectableTextBlock.Text | Export policy |
| 71 | SelectableTextBlock.Text | EMS handoff |
| 85 | Button.Content | Export |
| 88 | Button.ToolTip.Tip | Step 5: export notch table using current version/threshold settings. |

### `Views/WorkspaceSections/CoordinatePlannerWorkspaceControlsPaneView.axaml`  -  Coordinate tab > controls pane

| line | element.attribute | text |
|---:|---|---|
| 17 | SelectableTextBlock.Text | Coordinate setup |
| 18 | SelectableTextBlock.Text | Pick references first; edit calibration only when the default mapping is wrong. |
| 26 | Button.ToolTip.Tip | Sync coordinate setup from current workspace. |
| 33 | Button.ToolTip.Tip | Export coordinate preview image (PNG or BMP). |
| 46 | SelectableTextBlock.Text | Reference |
| 53 | SelectableTextBlock.Text | Preview layer |
| 64 | SelectableTextBlock.Text | AA outline |
| 87 | SelectableTextBlock.Text | Calibration |
| 98 | SelectableTextBlock.Text | Edit calibration |
| 107 | SelectableTextBlock.Text | Origin X |
| 116 | SelectableTextBlock.Text | Origin Y |
| 130 | SelectableTextBlock.Text | AA width (mm) |
| 139 | SelectableTextBlock.Text | AA height (mm) |
| 153 | SelectableTextBlock.Text | Pixel X |
| 161 | SelectableTextBlock.Text | Pixel Y |
| 173 | SelectableTextBlock.Text | Copper pillar dia (mm) |
| 192 | SelectableTextBlock.Text | Artifacts |
| 199 | SelectableTextBlock.Text | Guide reference |
| 211 | SelectableTextBlock.Text | Horizontal guides |
| 221 | SelectableTextBlock.Text | Count |
| 230 | SelectableTextBlock.Text | Pitch (mm) |
| 240 | SelectableTextBlock.Text | Positions (mm) |
| 245 | SelectableTextBlock.Text | Inset (mm) |
| 254 | SelectableTextBlock.Text | Vertical guides |
| 264 | SelectableTextBlock.Text | Count |
| 273 | SelectableTextBlock.Text | Pitch (mm) |
| 283 | SelectableTextBlock.Text | Positions (mm) |
| 288 | SelectableTextBlock.Text | Inset (mm) |
| 302 | SelectableTextBlock.Text | BIST center rect |
| 320 | SelectableTextBlock.Text | 4-point array |
| 331 | SelectableTextBlock.Text | Show array |
| 340 | SelectableTextBlock.Text | Use safe AA corners |
| 346 | SelectableTextBlock.Text | Array columns |
| 354 | SelectableTextBlock.Text | Array rows |
| 367 | SelectableTextBlock.Text | Selected handle |
| 379 | SelectableTextBlock.Text | X (mm) |
| 388 | SelectableTextBlock.Text | Y (mm) |
| 413 | SelectableTextBlock.Text | Custom point / path |
| 420 | SelectableTextBlock.Text | Point |
| 422 | TextBox.PlaceholderText | Point label |
| 442 | SelectableTextBlock.Text | Add point |
| 448 | SelectableTextBlock.Text | Path |
| 450 | TextBox.PlaceholderText | Path label |
| 494 | SelectableTextBlock.Text | Add path |
| 517 | SelectableTextBlock.Text | Clear custom |

### `Views/WorkspaceSections/CoordinatePlannerWorkspaceDetailsPaneView.axaml`  -  Coordinate tab > table pane

| line | element.attribute | text |
|---:|---|---|
| 11 | SelectableTextBlock.Text | Coordinate table |
| 37 | Button.ToolTip.Tip | Copy selected coordinate row. |
| 44 | Button.ToolTip.Tip | Copy all coordinate rows. |
| 51 | Button.ToolTip.Tip | Export coordinate rows as CSV. |
| 58 | Button.ToolTip.Tip | Export coordinate rows as JSON. |
| 64 | SelectableTextBlock.Text | Click a row to focus it on the canvas and open full coordinate details. |
| 85 | SelectableTextBlock.Text | Artifact |
| 91 | SelectableTextBlock.Text | Pixel |
| 97 | SelectableTextBlock.Text | Machine |
| 103 | SelectableTextBlock.Text | Safe |

### `Views/WorkspaceSections/IndexMappingReportLeftPaneView.axaml`  -  Verification Workspace > left table

| line | element.attribute | text |
|---:|---|---|
| 14 | SelectableTextBlock.Text | IC |
| 17 | SelectableTextBlock.Text | Diff |
| 20 | SelectableTextBlock.Text | CAD |
| 23 | SelectableTextBlock.Text | REG |
| 26 | SelectableTextBlock.Text | Reason |
| 29 | SelectableTextBlock.Text | Status |
| 34 | SelectableTextBlock.Text | No CAD/REG entity rows under current filter. |
| 107 | SelectableTextBlock.Text (StringFormat) | conf {0} |

### `Views/WorkspaceSections/IndexMappingReportRightPaneView.axaml`  -  Verification Workspace > right pane

| line | element.attribute | text |
|---:|---|---|
| 15 | SelectableTextBlock.Text (FallbackValue) | Decision |
| 18 | SelectableTextBlock.Text (FallbackValue) | Select a row |
| 36 | SelectableTextBlock.Text | Verification summary |
| 41 | SelectableTextBlock.Text (StringFormat) | Mode: {0} |
| 44 | SelectableTextBlock.Text (StringFormat) | Status: {0} |
| 47 | SelectableTextBlock.Text (StringFormat) | Open issues: {0} |
| 51 | SelectableTextBlock.Text (StringFormat) | Active step: {0} |
| 69 | SelectableTextBlock.Text | Step rail |
| 72 | CheckBox.ToolTip.Tip | When enabled, selecting a step rail node also switches the left-table filter to the preferred diagnostics mode for that step. |
| 104 | SelectableTextBlock.Text (StringFormat) | status: {0} |
| 130 | SelectableTextBlock.Text | Entity journey |
| 153 | SelectableTextBlock.Text | Active step detail |
| 164 | SelectableTextBlock.Text | Input |
| 172 | SelectableTextBlock.Text | Decision |
| 181 | SelectableTextBlock.Text | Output |
| 190 | SelectableTextBlock.Text | Impact |
| 199 | SelectableTextBlock.Text | Action |
| 215 | SelectableTextBlock.Text | Advanced details |
| 286 | Button.Content | Locate |
| 292 | Button.Content | Apply diff repair |
| 298 | Button.Content | Apply override |
| 304 | Button.Content | Clear override |
| 310 | Button.Content | Apply visible repairs |
| 316 | Button.Content | Apply segment repairs |
| 323 | SelectableTextBlock.Text | Simulation uses exported notch table payload. |

### `Views/WorkspaceSections/NotchExportSelectionRowsPaneView.axaml`  -  Export Notch Rows > rows table

| line | element.attribute | text |
|---:|---|---|
| 37 | Button.Content | All |
| 41 | Button.Content | None |
| 52 | Button.Content | Apply shown as selection |
| 54 | Button.ToolTip.Tip | Replace selected rows with currently shown rows. |
| 79 | Button.Content | # |
| 82 | Button.ToolTip.Tip | Filter # values (Excel-style). |
| 86 | Button.Content | IC |
| 89 | Button.ToolTip.Tip | Filter IC values (Excel-style). |
| 93 | Button.Content | Diff |
| 96 | Button.ToolTip.Tip | Filter Diff values (Excel-style). |
| 100 | Button.Content | Combine |
| 103 | Button.ToolTip.Tip | Filter Combine values (Excel-style). |
| 107 | Button.Content | REG -> CAD |
| 110 | Button.ToolTip.Tip | Filter REG -> CAD values (Excel-style). |
| 114 | Button.Content | Status |
| 117 | Button.ToolTip.Tip | Filter Status values (Excel-style). |

### `Views/WorkspaceSections/SimulationWorkspaceControlsPaneView.axaml`  -  Simulation tab > controls pane

| line | element.attribute | text |
|---:|---|---|
| 18 | SelectableTextBlock.Text | Simulation controls |
| 19 | SelectableTextBlock.Text | Inputs, view mode, and playback share one simulation snapshot. |
| 29 | SelectableTextBlock.Text | Input |
| 56 | SelectableTextBlock.Text | Global value |
| 64 | SelectableTextBlock.Text | Uniform presets |
| 77 | SelectableTextBlock.Text | Uniform 400 |
| 91 | SelectableTextBlock.Text | Uniform 360 |
| 105 | SelectableTextBlock.Text | Zero |
| 116 | SelectableTextBlock.Text | Random |
| 125 | SelectableTextBlock.Text | Clear all |
| 135 | SelectableTextBlock.Text | CSV projection |
| 155 | SelectableTextBlock.Text | Import CSV |
| 164 | SelectableTextBlock.Text | Clear CSV |
| 194 | SelectableTextBlock.Text | Copper pillar |
| 202 | SelectableTextBlock.Text | Contact model |
| 215 | SelectableTextBlock.Text | Center X |
| 221 | NumberScrubber.ToolTip.Tip | Copper center X in regular-grid world coordinates. |
| 225 | SelectableTextBlock.Text | Center Y |
| 231 | NumberScrubber.ToolTip.Tip | Copper center Y in regular-grid world coordinates. |
| 237 | SelectableTextBlock.Text | Diameter |
| 243 | NumberScrubber.ToolTip.Tip | Copper pillar diameter in the same unit as the regular grid, usually mm. |
| 247 | SelectableTextBlock.Text | Full-pad signal |
| 258 | SelectableTextBlock.Text | Baseline |
| 264 | NumberScrubber.ToolTip.Tip | Base diff added to every regular before copper overlap is applied. |
| 273 | SelectableTextBlock.Text | Path replay |
| 282 | Button.ToolTip.Tip | Run copper path replay |
| 291 | SelectableTextBlock.Text | Start X |
| 301 | SelectableTextBlock.Text | Start Y |
| 314 | SelectableTextBlock.Text | End X |
| 324 | SelectableTextBlock.Text | End Y |
| 334 | SelectableTextBlock.Text | Steps |
| 347 | SelectableTextBlock.Text | Start |
| 356 | SelectableTextBlock.Text | End |
| 365 | SelectableTextBlock.Text | Run |
| 381 | SelectableTextBlock.Text | View |
| 384 | SelectableTextBlock.Text | Version, value mode, color mode, and AA scope. |
| 385 | SelectableTextBlock.ToolTip.Tip | Keyboard: B Before, A After, D Delta, C Changed, V toggles Before/After. |
| 431 | SelectableTextBlock.Text | Playback |
| 434 | SelectableTextBlock.Text | Play imported CSV frames on the same AA grid. |
| 435 | SelectableTextBlock.ToolTip.Tip | Playback only changes the projected numbers; it does not recompute the workspace grid. |
| 445 | Button.ToolTip.Tip | Previous frame |
| 462 | Button.ToolTip.Tip | Next frame |
| 488 | SelectableTextBlock.Text | FPS |

### `Views/WorkspaceSections/SimulationWorkspaceDetailsPaneView.axaml`  -  Simulation tab > details pane

| line | element.attribute | text |
|---:|---|---|
| 25 | SelectableTextBlock.Text | Selected regular values |
| 28 | SelectableTextBlock.Text | Before |
| 32 | SelectableTextBlock.Text | After |
| 36 | SelectableTextBlock.Text | Delta |
| 40 | SelectableTextBlock.Text | % change |
| 45 | SelectableTextBlock.Text | EMS status |
| 50 | SelectableTextBlock.Text | Net flow |
| 96 | SelectableTextBlock.Text | Input source |
| 121 | SelectableTextBlock.Text | EMS safety |
| 151 | SelectableTextBlock.Text | Flow |
| 159 | SelectableTextBlock.Text | Coverage |
| 171 | SelectableTextBlock.Text | Top high-risk diffs |
| 173 | SelectableTextBlock.ToolTip.Tip | Highest After values sorted against the EMS cap. |
| 183 | Button.ToolTip.Tip | Focus this regular pad on the Simulation canvas. |
| 239 | SelectableTextBlock.Text | Copper path replay |
| 250 | Button.ToolTip.Tip | Copy copper path replay. |
| 258 | Button.ToolTip.Tip | Export copper path replay as CSV. |
| 266 | Button.ToolTip.Tip | Export copper path replay as JSON. |
| 274 | SelectableTextBlock.Text | Step |
| 275 | SelectableTextBlock.Text | Center |
| 276 | SelectableTextBlock.Text | Max |
| 277 | SelectableTextBlock.Text | Audit |
| 331 | SelectableTextBlock.Text | Delta validation |
| 344 | SelectableTextBlock.Text | Color scale |
| 391 | SelectableTextBlock.Text | Notch impact |


## 6. Inventory: Dev page (`Views/DevView.axaml`, development builds only)

Shown only when `AppBuildChannel.IsDevelopmentBuild`. Listed for completeness; not part of the shipped English UI in release builds.

| line | element.attribute | text |
|---:|---|---|
| 25 | SelectableTextBlock.Text | Dev page (minimal mode). |
| 28 | SelectableTextBlock.Text | Prototype sections have been trimmed. This page now keeps only lightweight runtime/theme probes. |
| 37 | SelectableTextBlock.Text | CAD load spinner preview |
| 40 | SelectableTextBlock.Text | Same LoadingSpinner control used by the CAD load host window. |
| 45 | Button.Content | Show loading spinner (1.5s) |
| 60 | SelectableTextBlock.Text | Preview area |
| 77 | SelectableTextBlock.Text | Sidebar control states |
| 85 | SelectableTextBlock.Text | Input |
| 88 | SelectableTextBlock.Text | Manual uniform source; visible regulars share one scenario snapshot. |
| 92 | SelectableTextBlock.Text | Regular/FW memory frame input projected onto AA grid. |
| 103 | SelectableTextBlock.Text | EMS safety |
| 120 | SelectableTextBlock.Text | Import |
| 132 | SelectableTextBlock.Text | Clear |
| 146 | SelectableTextBlock.Text | regular_frame_001.csv |
| 148 | SelectableTextBlock.Text | 128 frames, compatible regular grid |
| 150 | SelectableTextBlock.Text | Shape 42 x 36, channel order preserved |
| 162 | SelectableTextBlock.Text | regular_frame_delta_review_long_filename.csv |
| 164 | SelectableTextBlock.Text | Inspector summary wraps while the filename trims safely. |
| 180 | SelectableTextBlock.Text | DXF operation controls |
| 183 | SelectableTextBlock.Text | Free-form edit actions using action role hierarchy. |
| 187 | Button.ToolTip.Tip | Reset all DXF edits. |
| 194 | SelectableTextBlock.Text | Quick actions |
| 198 | SelectableTextBlock.Text | Hide / restore |
| 200 | Button.ToolTip.Tip | Hide selected CAD pads. |
| 207 | SelectableTextBlock.Text | Hide selected |
| 214 | Button.ToolTip.Tip | Restore last hidden CAD batch. |
| 221 | SelectableTextBlock.Text | Undo hide |
| 226 | Button.ToolTip.Tip | Restore all manually hidden CAD pads. |
| 233 | SelectableTextBlock.Text | Show all |
| 242 | SelectableTextBlock.Text | Combine / split |
| 245 | Button.ToolTip.Tip | Combine selected CAD pads using geometry union. |
| 252 | SelectableTextBlock.Text | Combine |
| 257 | Button.ToolTip.Tip | Split selected combined CAD groups back into their source pads. |
| 264 | SelectableTextBlock.Text | Split selected |
| 273 | SelectableTextBlock.Text | Layer bulk toggle |
| 278 | ToggleButton.ToolTip.Tip | All layers are visible. |
| 285 | SelectableTextBlock.Text | All on |
| 289 | ToggleButton.ToolTip.Tip | Every layer is hidden. |
| 296 | SelectableTextBlock.Text | All off |
| 300 | ToggleButton.ToolTip.Tip | Some layers are hidden. |
| 306 | SelectableTextBlock.Text | All off |
| 314 | SelectableTextBlock.Text | Advanced edit |
| 323 | SelectableTextBlock.Text | Advanced edit |
| 337 | SelectableTextBlock.Text | Advanced compact actions |
| 340 | Button.ToolTip.Tip | Rotate selected CAD pads. |
| 346 | Button.ToolTip.Tip | Move selected CAD pads to layer. |
| 352 | Button.ToolTip.Tip | Create layer and move selected CAD pads. |
| 364 | SelectableTextBlock.Text | Simulation |
| 366 | SelectableTextBlock.Text | Regular-grid surface simulation. |
| 374 | SelectableTextBlock.Text | Session ready |
| 379 | SelectableTextBlock.Text | Manual source |
| 384 | SelectableTextBlock.Text | Safety warning |
| 396 | SelectableTextBlock.Text | v1.1 |
| 401 | SelectableTextBlock.Text | Heatmap |
| 406 | SelectableTextBlock.Text | AA area |
| 411 | SelectableTextBlock.Text | Canvas projected from current workspace |
| 417 | Button.ToolTip.Tip | Icon button |
| 424 | ToggleButton.ToolTip.Tip | Selected icon button |
| 431 | Button.ToolTip.Tip | Icon + text action |
| 438 | SelectableTextBlock.Text | Copy result |
| 452 | SelectableTextBlock.Text | Uniform 400 |
| 463 | SelectableTextBlock.Text | Zero |
| 471 | SelectableTextBlock.Text | Primary summary |
| 476 | SelectableTextBlock.Text | Warning status |
| 481 | SelectableTextBlock.Text | Long AA source summary with active layer and coordinate reference details |
| 486 | SelectableTextBlock.Text | Selected REG (R128, C064), AA center 25.000 mm x 18.750 mm, source layer L2 copper, export-ready coordinate status. |
| 492 | SelectableTextBlock.Text | Action role laboratory |
| 498 | SelectableTextBlock.Text | Neutral |
| 503 | SelectableTextBlock.Text | Neutral selected |
| 508 | SelectableTextBlock.Text | Neutral disabled |
| 520 | SelectableTextBlock.Text | Primary |
| 531 | SelectableTextBlock.Text | Danger |
| 536 | Button.ToolTip.Tip | Ghost icon action |
| 543 | ToggleButton.ToolTip.Tip | Checked ghost icon action |
| 551 | Button.ToolTip.Tip | Viewport search action |
| 558 | ToggleButton.ToolTip.Tip | Checked viewport visibility action |
| 564 | Button.ToolTip.Tip | Panel chrome toggle action |
| 577 | Button.ToolTip.Tip | Console copy action |
| 583 | ToggleButton.ToolTip.Tip | Checked console toggle action |
| 593 | SelectableTextBlock.Text | Uniform 400 |
| 598 | SelectableTextBlock.Text | Zero |
| 603 | SelectableTextBlock.Text | Disabled preset |
| 607 | SelectableTextBlock.Text | EMS OK |
| 611 | SelectableTextBlock.Text | Violations 0 |
| 615 | SelectableTextBlock.Text | Max After 486 |
| 619 | SelectableTextBlock.Text | Neutral matrix |
| 624 | SelectableTextBlock.Text | Button |
| 629 | SelectableTextBlock.Text | Checked |
| 633 | Button.ToolTip.Tip | Neutral icon action |
| 645 | SelectableTextBlock.Text | Icon + text |
| 651 | SelectableTextBlock.Text | Disabled |
| 656 | SelectableTextBlock.Text | Primary matrix |
| 661 | SelectableTextBlock.Text | Button |
| 666 | SelectableTextBlock.Text | Checked |
| 670 | Button.ToolTip.Tip | Primary icon action |
| 682 | SelectableTextBlock.Text | Icon + text |
| 688 | SelectableTextBlock.Text | Disabled |
| 693 | SelectableTextBlock.Text | Danger matrix |
| 698 | SelectableTextBlock.Text | Button |
| 703 | SelectableTextBlock.Text | Checked |
| 707 | Button.ToolTip.Tip | Danger icon action |
| 719 | SelectableTextBlock.Text | Icon + text |
| 725 | SelectableTextBlock.Text | Disabled |
| 730 | SelectableTextBlock.Text | Ghost matrix |
| 735 | SelectableTextBlock.Text | Button |
| 740 | SelectableTextBlock.Text | Checked |
| 744 | Button.ToolTip.Tip | Ghost icon action |
| 756 | SelectableTextBlock.Text | Icon + text |
| 762 | SelectableTextBlock.Text | Disabled |
| 767 | SelectableTextBlock.Text | Viewport matrix |
| 772 | Button.ToolTip.Tip | Fit viewport |
| 779 | ToggleButton.ToolTip.Tip | Toggle viewport visibility |
| 786 | Button.ToolTip.Tip | Disabled viewport search |
| 793 | SelectableTextBlock.Text | Console matrix |
| 803 | Button.ToolTip.Tip | Copy console text |
| 810 | ToggleButton.ToolTip.Tip | Toggle console filter |
| 817 | Button.ToolTip.Tip | Disabled console delete |
| 825 | SelectableTextBlock.Text | Chip matrix |
| 830 | SelectableTextBlock.Text | Preset |
| 835 | SelectableTextBlock.Text | Checked |
| 840 | SelectableTextBlock.Text | Disabled |
| 844 | SelectableTextBlock.Text | Status |
| 848 | SelectableTextBlock.Text | Success |
| 852 | SelectableTextBlock.Text | Warning |
| 856 | SelectableTextBlock.Text | Danger |


## 7. Inventory: C# display text (ViewModels, Services, code-behind)

Hints for reading the table:
- `status chip` = passed to `SetStatus` / `ReportStatus` etc.; shown in the workspace header status text (`Controls/WorkspaceHeader.axaml:277`, binding `StatusDisplayText`). Per-view `StatusText` properties are shown in the Simulation, Coordinate, Notch Export and Verification windows.
- "where" is derived from the file name and may be off by a screen; check the file when it matters.
- Interpolated strings keep their `{...}` placeholders. Fragments of one multi-part string appear as separate rows.


### `MainWindow.axaml.cs`  -  Main window > exit confirm dialog (window name arg)

| line | text | kind |
|---:|---|---|
| 67 | Freeform Helper | dialog text |
| 121 | Unsaved project | dialog text |
| 122 | Project has not been saved. Save before exiting? | dialog text |
| 123 | Save and exit | dialog text |
| 124 | Exit without saving | dialog text |

### `Services/CadLayerUiSnapshotBuilder.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 125 | Auto (visible layers) | display text (VM property / projector) |
| 171 | Hidden bounds layer is valid. Showing {displayNames.Count}/{distinctNames.Count}. | display text (VM property / projector) |
| 172 | Hidden bounds layer is still valid. | display text (VM property / projector) |
| 188 | Select layer | display text (VM property / projector) |

### `Services/CadLoadUseCase.cs`  -  DXF open (status chip + console)

| line | text | kind |
|---:|---|---|
| 86 | Failed to load CAD from both path and embedded DXF. | display text (VM property / projector) |

### `Services/CadLoadWorkflowService.cs`  -  DXF open (status chip + console)

| line | text | kind |
|---:|---|---|
| 26 | Open DXF | display text (VM property / projector) |
| 30 | Importing DXF... | status chip |
| 47 | DXF import failed: file not found. | status chip |

### `Services/CoordinatePlannerActiveAreaResolver.cs`  -  Coordinate tab

| line | text | kind |
|---:|---|---|
| 36 | AA outline: regular grid bounds | display text (VM property / projector) |
| 49 | AA outline: layer {normalizedLayerName} | display text (VM property / projector) |
| 61 | AA outline: regular grid bounds (selected layer {normalizedLayerName} has no CAD pads) | display text (VM property / projector) |
| 69 | AA outline: regular grid bounds (selected layer {normalizedLayerName} not found) | display text (VM property / projector) |

### `Services/CoordinatePlannerWorkspaceUseCase.cs`  -  Coordinate tab

| line | text | kind |
|---:|---|---|
| 39 | Guide reference: regular grid bounds | display text (VM property / projector) |
| 40 | Guide reference: {activeAreaResolution.SourceText} | display text (VM property / projector) |

### `Services/DxfLayerImageExportService.cs`  -  Export DXF layer image failure (status chip)

| line | text | kind |
|---:|---|---|
| 34 | No CAD pads in selected layer. | exception message (surfaces as "<operation> failed: ...") |
| 39 | Image size must be >= 2x2. | exception message (surfaces as "<operation> failed: ...") |
| 44 | Line width must be > 0. | exception message (surfaces as "<operation> failed: ...") |
| 49 | Padding must be >= 0. | exception message (surfaces as "<operation> failed: ...") |
| 55 | Selected layer bounds are empty. | exception message (surfaces as "<operation> failed: ...") |
| 73 | Output image resolution is too large. | exception message (surfaces as "<operation> failed: ...") |
| 78 | Output image size must be >= 2x2. | exception message (surfaces as "<operation> failed: ...") |
| 232 | Failed to encode DXF layer image. | exception message (surfaces as "<operation> failed: ...") |

### `Services/GridRebuildOrchestrator.cs`  -  Right panel / General > regular source hints

| line | text | kind |
|---:|---|---|
| 33 | Regular layer pads={padCount}, current X*Y={x}*{y}={expected}. | display text (VM property / projector) |
| 40 | Keep Y={y} -> set X={keepYSuggestedX.Value}. | display text (VM property / projector) |
| 47 | Keep X={x} -> set Y={keepXSuggestedY.Value}. | display text (VM property / projector) |
| 53 | Closest exact pair: X={bestX}, Y={bestY}. | display text (VM property / projector) |
| 59 | Cascade={cascade} exceeds suggested X={hintXForCascade}; set Cascade <= {hintXForCascade}. | display text (VM property / projector) |
| 75 | DXF not loaded; unable to use DXF layer regular source. | display text (VM property / projector) |
| 76 | Load DXF first.\\nThen select a regular source layer. | display text (VM property / projector) |
| 82 | Select a regular source layer. | display text (VM property / projector) |
| 83 | Select a DXF layer for regular source. | display text (VM property / projector) |
| 93 | Selected regular source layer has no visible pads. | display text (VM property / projector) |
| 94 | Selected regular layer has no pads. | display text (VM property / projector) |
| 107 | DXF regular source mismatch. {mismatchHint} | display text (VM property / projector) |
| 108 | Regular source count mismatch.\\n{mismatchHint} | display text (VM property / projector) |
| 129 | Regular source mismatch.\\n{mismatchHint} | display text (VM property / projector) |
| 146 | No visible DXF pads (all layers off). | display text (VM property / projector) |
| 147 | No visible DXF pads.\\nTurn on a layer. | display text (VM property / projector) |
| 155 | Ready (DXF not loaded; using panel AA alignment). | display text (VM property / projector) |

### `Services/IndexMappingAlgorithmTraceProjector.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 17 | DxfCadOutputFwDiffAssignment + MaskAudit | display text (VM property / projector) |

### `Services/ManualSizingService.Distribution.cs`  -  Manual sizing validation (status chip)

| line | text | kind |
|---:|---|---|
| 122 | No auto cells available to keep total size constant. | display text (VM property / projector) |
| 128 | Manual sizes exceed available total. | display text (VM property / projector) |

### `Services/ManualSizingService.Parsing.cs`  -  Manual sizing validation (status chip)

| line | text | kind |
|---:|---|---|
| 14 | Grid not built. | display text (VM property / projector) |
| 50 | Grid not built. | display text (VM property / projector) |
| 103 | Invalid range format: '{part}'. Expected 'start-end'. | display text (VM property / projector) |
| 110 | Invalid numbers in range: '{part}'. | display text (VM property / projector) |
| 124 | Index {i} in range '{part}' is out of bounds ({min}-{max}). | display text (VM property / projector) |
| 134 | Invalid index format: '{part}'. | display text (VM property / projector) |
| 140 | Index {index} is out of bounds ({min}-{max}). | display text (VM property / projector) |

### `Services/NotchApplySimulationReviewUseCase.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 150 | Xch/Ych {DeclaredCols.Value} x {DeclaredRows.Value} | display text (VM property / projector) |
| 151 | Xch/Ych not declared | display text (VM property / projector) |
| 153 | frame {FirstFrameCols.Value} x {FirstFrameRows.Value} | display text (VM property / projector) |
| 154 | frame shape unknown | display text (VM property / projector) |
| 173 | Frame {Frame.FrameIndex.Value} | display text (VM property / projector) |
| 174 | Frame {SourceFrameIndex + 1} | display text (VM property / projector) |

### `Services/NotchDetailUseCase.cs`  -  Notch detail window

| line | text | kind |
|---:|---|---|
| 158 | TH={thresholdQ7}/128, Max={maxQ7}/128 | display text (VM property / projector) |
| 159 | TH=0 (disabled) | display text (VM property / projector) |

### `Services/NotchDisplayProjector.cs`  -  Pad info / Inspector notch lines

| line | text | kind |
|---:|---|---|
| 116 | Undo NF (To Regular): {toRegularRatio:P2} | display text (VM property / projector) |
| 117 |  (disabled) | display text (VM property / projector) |
| 117 |  (enabled) | display text (VM property / projector) |
| 117 | To Full: {toFullRatio:P2} | display text (VM property / projector) |
| 118 | Combined ratio: {combinedValueText} | display text (VM property / projector) |
| 127 | Stage3 area: {stage3Area.Value:0.####} {PadInfoUnits.SquareMillimeter} | display text (VM property / projector) |
| 128 | Stage3 area: - | display text (VM property / projector) |
| 143 | Targets: - | display text (VM property / projector) |
| 148 | Targets: 0 | display text (VM property / projector) |
| 152 | Targets: {effectiveCount} effective / {targets.Count} total | display text (VM property / projector) |
| 171 | IC{target.IcIndex + 1}/diff{target.DiffIndex}   | display text (VM property / projector) |
| 202 | Self before To Full | display text (VM property / projector) |
| 204 | Target | display text (VM property / projector) |
| 205 | Below gate | display text (VM property / projector) |
| 214 | REG {regularArea.RegularPadId} | display text (VM property / projector) |
| 215 | IC{target.IcIndex + 1}/diff{target.DiffIndex} | display text (VM property / projector) |
| 244 | CAD {id} | display text (VM property / projector) |
| 369 | IC | display text (VM property / projector) |
| 373 |   REG | display text (VM property / projector) |
| 376 | Area  ov= | display text (VM property / projector) |
| 385 | Owners  | display text (VM property / projector) |
| 390 | Share   | display text (VM property / projector) |
| 392 | Flags  boundary= | display text (VM property / projector) |
| 401 | Rule    | display text (VM property / projector) |

### `Services/NotchExportErrorFormatter.cs`  -  Export notch failure message (status chip)

| line | text | kind |
|---:|---|---|
| 7 | Combine ratio exceeds 255% | display text (VM property / projector) |
| 9 | Suggested action: lower To Regular/To Full or disable To Full EN, then export again. | display text (VM property / projector) |
| 59 | CAD {match.Groups[ | display text (VM property / projector) |
| 60 | To Regular {match.Groups[ | display text (VM property / projector) |
| 61 | To Full {match.Groups[ | display text (VM property / projector) |
| 62 | raw {match.Groups[ | display text (VM property / projector) |

### `Services/NotchExportSelectionColumnFilterService.cs`  -  Export Notch Rows window

| line | text | kind |
|---:|---|---|
| 68 | Filter # | display text (VM property / projector) |
| 69 | Filter IC | display text (VM property / projector) |
| 70 | Filter Diff | display text (VM property / projector) |
| 71 | Filter Match | display text (VM property / projector) |
| 72 | Filter REG -> CAD | display text (VM property / projector) |
| 73 | Filter Status | display text (VM property / projector) |
| 74 | Column filter | display text (VM property / projector) |

### `Services/NotchExportSelectionProjectionBuilder.cs`  -  Export Notch Rows window

| line | text | kind |
|---:|---|---|
| 110 | None | display text (VM property / projector) |
| 122 | Search: {trimmedSearchKeyword} | display text (VM property / projector) |
| 124 | Mode: {filterState.RowDisplayModeLabel} | display text (VM property / projector) |
| 126 | Selection: Selected only | display text (VM property / projector) |
| 128 | Header: {activeColumnFilterSummaryText} | display text (VM property / projector) |
| 133 | Active filter: text={(hasSearchKeyword ? trimmedSearchKeyword :  | display text (VM property / projector) |
| 134 | mode={filterState.RowDisplayModeLabel} \|  | display text (VM property / projector) |
| 135 | selected={selectedScopeText} \|  | display text (VM property / projector) |
| 136 | header={headerScopeText} \|  | display text (VM property / projector) |
| 137 | shown={visibleCount}. | display text (VM property / projector) |
| 138 | Showing {visibleCount} rows | display text (VM property / projector) |
| 423 | None | display text (VM property / projector) |
| 438 | IC | display text (VM property / projector) |
| 439 | Diff | display text (VM property / projector) |
| 440 | Match | display text (VM property / projector) |
| 441 | REG->CAD | display text (VM property / projector) |
| 442 | Status | display text (VM property / projector) |
| 443 | Field | display text (VM property / projector) |

### `Services/NotchExportSelectionSummaryProjector.cs`  -  Export Notch Rows window

| line | text | kind |
|---:|---|---|
| 43 | Selected {selectedCount}/{allRowCount} rows · shown {visibleCount}. | display text (VM property / projector) |
| 45 | v2.2 {v22RowCount} · warning {warningRowCount} · legacy {legacyRowCount} · CAD linked {cadLinkedRowCount} · no CAD {cadMissingRowCount} | display text (VM property / projector) |
| 47 | Export selected | display text (VM property / projector) |
| 47 | Export {selectedCount} rows | display text (VM property / projector) |

### `Services/NotchValidationTraceService.cs`  -  Right panel > Settings > Step 6 validation

| line | text | kind |
|---:|---|---|
| 57 | IC{row.IcIndex + 1}/diff{row.DiffIndex} | display text (VM property / projector) |
| 72 | REG {row.RegularPadIndex}, CAD {(row.CadPadId ?? 0)} | display text (VM property / projector) |
| 92 | IN | display text (VM property / projector) |
| 103 | IC{sourceRow.IcIndex + 1}/diff{flow.SourceDiffIndex} | display text (VM property / projector) |
| 104 | IC{flow.TargetIcIndex + 1}/diff{flow.TargetDiffIndex} | display text (VM property / projector) |
| 105 | REG {flow.SourceRegularPadId} -> diff {flow.TargetDiffIndex} | display text (VM property / projector) |
| 125 | OUT | display text (VM property / projector) |
| 136 | IC{sourceRow.IcIndex + 1}/diff{flow.SourceDiffIndex} | display text (VM property / projector) |
| 137 | IC{flow.TargetIcIndex + 1}/diff{flow.TargetDiffIndex} | display text (VM property / projector) |
| 138 | REG {flow.SourceRegularPadId} -> diff {flow.TargetDiffIndex} | display text (VM property / projector) |

### `Services/PreviewImageWriter.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 67 | The preview image could not be decoded. | exception message (surfaces as "<operation> failed: ...") |

### `Services/ProjectPersistenceUseCase.cs`  -  Save project (status chip)

| line | text | kind |
|---:|---|---|
| 90 | Embed DXF skipped: source file not available. | display text (VM property / projector) |
| 111 | Embed SeeRegular mask skipped: source file not available. | display text (VM property / projector) |

### `Services/QuickFocusQueryParser.cs`  -  Inspector > Quick focus

| line | text | kind |
|---:|---|---|
| 28 | Quick focus: input a CAD id, REG id, or FW diff. | display text (VM property / projector) |
| 38 | Quick focus: input a CAD id, REG id, or FW diff. | display text (VM property / projector) |
| 77 | Quick focus: missing numeric id. Use CAD 4809, REG 4616, or DIFF 72. | display text (VM property / projector) |

### `Services/SelectionSummaryUseCase.cs`  -  Workspace header > selection summary

| line | text | kind |
|---:|---|---|
| 22 | No selection. | display text (VM property / projector) |
| 22 | Selected: {totalCount} pads. | display text (VM property / projector) |
| 23 | CAD: {cadCount} pads. | display text (VM property / projector) |
| 24 | Regular: {regularCount} pads. | display text (VM property / projector) |
| 114 | Regular: Rows {rowLabel}, Cols {colLabel} ({regularCount}) | display text (VM property / projector) |
| 134 | Regular: {regularCount} pads (non-rectangular) | display text (VM property / projector) |
| 147 | Selection is not a full rectangle; rows/cols range not applied. | display text (VM property / projector) |
| 156 | No selection. | display text (VM property / projector) |
| 157 | Selected: CAD={cadCount}. | display text (VM property / projector) |
| 158 | Selected: Regular={regularCount}. | display text (VM property / projector) |
| 159 | Selected: CAD={cadCount}, Regular={regularCount}. | display text (VM property / projector) |
| 167 | CAD: {cadCount} pads | display text (VM property / projector) |
| 172 | Regular: Rows {rowLabel}, Cols {colLabel} ({regularCount}) | display text (VM property / projector) |
| 266 | No selection. | display text (VM property / projector) |
| 267 | Select regular pads to edit size. | display text (VM property / projector) |
| 268 | CAD: 0 pads. | display text (VM property / projector) |
| 269 | Regular: 0 pads. | display text (VM property / projector) |

### `Services/SimulationSafetyOverviewProjector.cs`  -  Simulation tab / Step 5 safety summaries

| line | text | kind |
|---:|---|---|
| 21 | Run Simulation to audit current notch output before FW handoff. | display text (VM property / projector) |
| 26 | Top risk diffs: - | display text (VM property / projector) |
| 36 | Simulation not run | status chip |
| 38 | Max After - | display text (VM property / projector) |
| 59 |  (stale) | display text (VM property / projector) |
| 86 | Simulation unavailable | display text (VM property / projector) |
| 101 | Workspace changed after this Simulation result. Refresh Simulation before export. | display text (VM property / projector) |
| 105 |  at REG {audit.MaxAfterRegularPadId.Value.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 109 | Current Simulation is under EMS cap. Max After {SimulationSafetyTextProjector.FormatValue(audit.MaxAfterValue)}. | display text (VM property / projector) |
| 133 | REG {item.RegularPadId}/D{item.DiffIndex} {SimulationSafetyTextProjector.FormatValue(item.AfterValue)} ({marginText}) | display text (VM property / projector) |

### `Services/SimulationSafetyTextProjector.cs`  -  Simulation tab / Step 5 safety summaries

| line | text | kind |
|---:|---|---|
| 8 | Export blocked by EMS safety | dialog/file-dialog text |
| 18 | Simulation safety: After > {capText} is EMS risk. 400 is diagnostic input, not a correctness target. | display text (VM property / projector) |
| 20 | EMS cap {capText} | display text (VM property / projector) |
| 28 | At uniform 400, target cap {targetCapText}% maps to After {mappedAfterText};  | display text (VM property / projector) |
| 29 | compare with EMS cap {emsCapText}. | display text (VM property / projector) |
| 34 | Target guard: cap CurrentGain target coverage at {targetCoverageCapPercent:0.#}%. | display text (VM property / projector) |
| 35 | Target guard: OFF; target coverage can exceed EMS diagnostic cap. | display text (VM property / projector) |
| 39 | Checklist: Simulation audit, EMS cap, selected rows, and C/runtime parity before FW handoff. | display text (VM property / projector) |
| 40 | Checklist unavailable until an export type is selected. | display text (VM property / projector) |
| 43 | Export safety: run Simulation audit before FW handoff; any After > {capText} needs explicit review. | display text (VM property / projector) |
| 51 | Simulation not run | display text (VM property / projector) |
| 72 | EMS risk | display text (VM property / projector) |
| 75 | EMS OK | display text (VM property / projector) |
| 75 | audit warning | display text (VM property / projector) |
| 82 | EMS cap: After <= {FormatValue(afterCap)} | display text (VM property / projector) |
| 85 | Near cap | status chip |
| 91 | EMS safety: no simulation cells. | display text (VM property / projector) |
| 94 | Max After {FormatValue(audit.MaxAfterValue)} | display text (VM property / projector) |
| 95 | cap {FormatValue(audit.AfterCap)} | display text (VM property / projector) |
| 98 | EMS OK: {maxText} <= {capText}. | display text (VM property / projector) |
| 102 |  at REG {audit.MaxAfterRegularPadId.Value.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 104 | EMS risk: {audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)} pad(s) exceed {capText}. {maxText}{location}. | display text (VM property / projector) |
| 111 | Physical audit: no simulation cells. | display text (VM property / projector) |
| 115 | global flow residual OK | display text (VM property / projector) |
| 116 | global flow residual {FormatSignedValue(audit.GlobalFlowAudit.ResidualValue)} | display text (VM property / projector) |
| 118 | net-flow OK | display text (VM property / projector) |
| 119 | net-flow residuals {audit.TotalNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 121 | target coverage <= {FormatValue(audit.TargetCoveragePercentCap)}% | display text (VM property / projector) |
| 122 | target coverage risks {audit.TotalTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 123 | Physical audit: {globalText}; {netFlowText}; {coverageText}. | display text (VM property / projector) |
| 139 | REG - | display text (VM property / projector) |
| 145 | REG {violation.RegularPadId} After {FormatValue(violation.AfterValue)} | display text (VM property / projector) |
| 156 | Simulation audit: not run | display text (VM property / projector) |
| 161 | EMS risk {audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 172 | Simulation audit is not available in the current workspace.  | display text (VM property / projector) |
| 173 | Open Simulation to review After <= {capText} before FW handoff. | display text (VM property / projector) |
| 177 |  at REG {audit.MaxAfterRegularPadId.Value.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 179 | Max After {FormatValue(audit.MaxAfterValue)}{maxLocation} | display text (VM property / projector) |
| 181 | Blocked: {audit.TotalViolationCount.ToString(CultureInfo.InvariantCulture)} pad(s) exceed EMS cap {capText}. {maxText}. | display text (VM property / projector) |
| 182 | Safe for EMS cap {capText}: {maxText}. | display text (VM property / projector) |
| 189 | Export blocked (EMS risk) | display text (VM property / projector) |
| 201 | High-risk diffs: - | display text (VM property / projector) |
| 211 | EMS risk: +{FormatValue(distance)} over cap | display text (VM property / projector) |
| 212 | EMS OK: {FormatValue(distance)} margin | display text (VM property / projector) |
| 220 | +{FormatValue(distance)} over | display text (VM property / projector) |
| 229 | +{FormatValue(distance)} over cap | display text (VM property / projector) |
| 230 | margin {FormatValue(distance)} | display text (VM property / projector) |
| 231 | #{item.Rank} REG {item.RegularPadId} · IC {item.IcIndex + 1} · FW Diff {item.DiffIndex} · After {FormatValue(item.AfterValue)} · {marginText} | display text (VM property / projector) |

### `Services/SimulationWorkspaceUseCase.cs`  -  Simulation tab / Step 5 safety summaries

| line | text | kind |
|---:|---|---|
| 65 | Manual baseline | display text (VM property / projector) |
| 82 | CSV frame | display text (VM property / projector) |
| 105 | Copper path sweep | display text (VM property / projector) |
| 105 | Copper pillar | display text (VM property / projector) |
| 149 | Manual baseline | display text (VM property / projector) |
| 164 | Manual baseline | display text (VM property / projector) |
| 170 | Manual baseline is incompatible with the current regular grid. | display text (VM property / projector) |
| 219 | Copper pillar | display text (VM property / projector) |
| 228 | Copper projection is incompatible with the current regular grid. | display text (VM property / projector) |
| 236 | Copper pillar | display text (VM property / projector) |
| 242 | Copper projection is incompatible with the current regular grid. | display text (VM property / projector) |

### `Services/WorkflowPipelineService.cs`  -  Project-load replay / downstream clear (status chip + console)

| line | text | kind |
|---:|---|---|
| 46 | Project load auto replay started: rebuilding Step 1 match. | display text (VM property / projector) |
| 47 | Project load Step 2 auto replay started. | display text (VM property / projector) |
| 48 | Project load auto replay started. | display text (VM property / projector) |
| 56 | Project load auto replay complete: Step 1 match restored. | display text (VM property / projector) |
| 57 | Project load Step 2 auto replay complete. | display text (VM property / projector) |
| 58 | Project load auto replay complete. | display text (VM property / projector) |
| 65 | unknown reason | display text (VM property / projector) |
| 69 | Project load auto replay skipped: Step 1 ({suffix}). | display text (VM property / projector) |
| 70 | Project load auto replay skipped: Step 2 ({suffix}). | display text (VM property / projector) |
| 71 | Project load auto replay skipped: {suffix}. | display text (VM property / projector) |
| 91 | Step 1 changed: cleared Step 2-5 results. | display text (VM property / projector) |
| 92 | Step 2 changed: cleared Step 3-5 results. | display text (VM property / projector) |
| 93 | Step 3 changed: cleared Step 4-5 results. | display text (VM property / projector) |
| 94 | Step 4 changed: cleared Step 5 result. | display text (VM property / projector) |

### `Services/WorkflowStepGateService.cs`  -  Step prerequisites (status chip)

| line | text | kind |
|---:|---|---|
| 18 | Unknown workflow step. | display text (VM property / projector) |
| 26 | Step 1: import DXF and build grid first. | display text (VM property / projector) |
| 37 | Step 2: import DXF and run Step 1 first. | display text (VM property / projector) |
| 42 | Step 2: run Step 1 (Match) first. | display text (VM property / projector) |
| 53 | Step 3: import DXF and build grid first. | display text (VM property / projector) |
| 58 | Step 3: run Step 1 (Match) first. | display text (VM property / projector) |
| 69 | Step 4: import DXF and build grid first. | display text (VM property / projector) |
| 74 | Step 4: run Step 1 (Match) first. | display text (VM property / projector) |
| 85 | Step 5: import DXF and run Step 1 first. Step 4 is optional diagnostics. | display text (VM property / projector) |
| 90 | Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional. | display text (VM property / projector) |

### `ViewModels/CoordinatePlannerHostViewModel.cs`  -  Coordinate tab

| line | text | kind |
|---:|---|---|
| 8 | Coordinate | dialog/file-dialog text |
| 9 | Build from the current workspace, then inspect AA corners, guide lines, BIST rectangle, and copper-pillar-safe machine coordinates on the same canvas. | display text (VM property / projector) |
| 10 | Freeform Helper workspace changed. Refresh coordinate planning to use the latest AA/grid state. | display text (VM property / projector) |

### `ViewModels/CoordinatePlannerWorkspaceViewModel.ArrayHandles.cs`  -  Coordinate tab

| line | text | kind |
|---:|---|---|
| 23 | 4-point array {0} selected. | display text (VM property / projector) |
| 58 | 4-point array {0} set to ({1:0.###}, {2:0.###}) mm. | display text (VM property / projector) |

### `ViewModels/CoordinatePlannerWorkspaceViewModel.Operations.cs`  -  Coordinate tab

| line | text | kind |
|---:|---|---|
| 15 | Coordinate image export is unavailable in the current host. | status chip |
| 21 | Coordinate preview exported. | display text (VM property / projector) |
| 22 | Coordinate preview export cancelled. | display text (VM property / projector) |
| 29 | Select a coordinate row before copying a selected artifact. | status chip |
| 47 | Coordinate clipboard copy is unavailable in the current host. | status chip |
| 53 | No coordinate rows are available to copy. | status chip |
| 59 | Copied | display text (VM property / projector) |
| 60 | Coordinate clipboard copy failed. | display text (VM property / projector) |
| 67 | Export Coordinate Artifacts CSV | display text (VM property / projector) |
| 70 | CSV | display text (VM property / projector) |
| 80 | Export Coordinate Artifacts JSON | display text (VM property / projector) |
| 83 | JSON | display text (VM property / projector) |
| 100 | Coordinate text export is unavailable in the current host. | status chip |
| 106 | No coordinate rows are available to export. | status chip |
| 119 | Exported {fileTypeName} | display text (VM property / projector) |
| 120 | Coordinate {fileTypeName} export cancelled. | display text (VM property / projector) |
| 125 | Point | display text (VM property / projector) |
| 134 | Custom point '{label}' added. | status chip |
| 139 | Path | display text (VM property / projector) |
| 151 | Custom path '{label}' added. | status chip |
| 160 | Custom coordinate artifacts cleared. | status chip |
| 172 | Coordinate planner synced from current workspace. | status chip |
| 186 | AA corners are unavailable for 4-point array initialization. | status chip |
| 200 | 4-point array corners synced from current safe AA corners. | status chip |
| 241 | AA corner | display text (VM property / projector) |
| 250 | 4-point corner | display text (VM property / projector) |
| 259 | Array dot | display text (VM property / projector) |
| 285 | Coordinate planner ready. Corners {0}, guides {1}, BIST {2}, array dots {3}. | display text (VM property / projector) |
| 334 | Default | display text (VM property / projector) |
| 355 | Kind | display text (VM property / projector) |
| 358 | Label | display text (VM property / projector) |
| 359 | Pixel | display text (VM property / projector) |
| 361 | Machine | display text (VM property / projector) |
| 363 | Safe | display text (VM property / projector) |
| 374 | Kind | display text (VM property / projector) |
| 375 | Label | display text (VM property / projector) |
| 376 | Pixel | display text (VM property / projector) |
| 377 | Machine | display text (VM property / projector) |
| 378 | Safe | display text (VM property / projector) |
| 379 | Source | display text (VM property / projector) |
| 384 | coordinate row(s) | display text (VM property / projector) |
| 384 | selected coordinate row | display text (VM property / projector) |
| 454 | Regular only | display text (VM property / projector) |
| 455 | All active CAD | display text (VM property / projector) |
| 465 | Regular grid bounds | display text (VM property / projector) |
| 467 | Layer: {name} | display text (VM property / projector) |
| 557 | pitch {0:0.###} mm, inset {1:0.###} | display text (VM property / projector) |
| 581 | Point | display text (VM property / projector) |
| 584 | Machine ({0:0.###}, {1:0.###}) | display text (VM property / projector) |
| 594 | Path | display text (VM property / projector) |
| 597 | Machine ({0:0.###}, {1:0.###}) -> ({2:0.###}, {3:0.###}) · {4} step(s) | display text (VM property / projector) |

### `ViewModels/CoordinatePlannerWorkspaceViewModel.cs`  -  Coordinate tab

| line | text | kind |
|---:|---|---|
| 118 | AA outline: regular grid bounds | display text (VM property / projector) |
| 119 | Guide reference: AA outline | display text (VM property / projector) |
| 148 | AA outline | display text (VM property / projector) |
| 149 | Regular grid | display text (VM property / projector) |
| 153 | Count | display text (VM property / projector) |
| 154 | Pitch | display text (VM property / projector) |
| 155 | Positions | display text (VM property / projector) |
| 159 | TL | display text (VM property / projector) |
| 160 | TR | display text (VM property / projector) |
| 161 | BR | display text (VM property / projector) |
| 162 | BL | display text (VM property / projector) |
| 202 | Focus | display text (VM property / projector) |
| 203 | Essentials | display text (VM property / projector) |
| 204 | All | display text (VM property / projector) |
| 356 | Default | display text (VM property / projector) |
| 389 | Point | display text (VM property / projector) |
| 398 | Path | display text (VM property / projector) |
| 452 | Coordinate planner ready. | display text (VM property / projector) |
| 463 | AA {0:0.###} x {1:0.###} mm · {2} x {3} px | display text (VM property / projector) |
| 469 | Layer: {SelectedLayerOption.Display} | display text (VM property / projector) |
| 478 | Machine: origin ({0:0.###}, {1:0.###}) · size {2:0.###} x {3:0.###} mm | display text (VM property / projector) |
| 484 | Pixels: {ClampPositiveInt(PixelWidth)} x {ClampPositiveInt(PixelHeight)} | display text (VM property / projector) |
| 489 | Copper pillar: {0:0.###} mm dia | display text (VM property / projector) |
| 493 | Guides: H {BuildGuideModeSummaryText(SelectedHorizontalGuideGenerationOption.Mode, ClampNonNegativeInt(HorizontalGuideCount), HorizontalGuidePitch, HorizontalGuideInset, HorizontalGuidePositionListText)} / V {BuildGuideModeSummaryText(SelectedVerticalGuideGenerationOption.Mode, ClampNonNegativeInt(VerticalGuideCount), VerticalGuidePitch, VerticalGuideInset, VerticalGuidePositionListText)} | display text (VM property / projector) |
| 516 | Sort: {0} {1} | display text (VM property / projector) |
| 522 | Safe clamps by copper radius. | display text (VM property / projector) |
| 523 | Safe = raw until copper dia is set. | display text (VM property / projector) |
| 531 | BIST center blank rect disabled. | display text (VM property / projector) |
| 537 | BIST rect px ({0:0.###}, {1:0.###}) -> ({2:0.###}, {3:0.###}) · machine ({4:0.###}, {5:0.###}) -> ({6:0.###}, {7:0.###}) | display text (VM property / projector) |
| 555 | 4-point array disabled. | display text (VM property / projector) |
| 560 | 4-point array: {0} x {1} dot(s), corners {2}, plotted dots {3}. | display text (VM property / projector) |
| 584 | Corner | display text (VM property / projector) |
| 592 | Custom artifacts: {0} point(s), {1} path(s). | display text (VM property / projector) |
| 595 | Custom artifacts disabled. | display text (VM property / projector) |

### `ViewModels/DevViewModel.cs`  -  Dev tab

| line | text | kind |
|---:|---|---|
| 14 | Dev preview | display text (VM property / projector) |

### `ViewModels/DxfEditChangeListViewModel.cs`  -  DXF Edit Details window

| line | text | kind |
|---:|---|---|
| 57 | CAD {CadPadId} | display text (VM property / projector) |
| 57 | CAD {CadPadId} · {CadName} | display text (VM property / projector) |
| 71 | DXF edit details | display text (VM property / projector) |
| 72 | Review modified CAD pads and apply scoped actions. | display text (VM property / projector) |
| 146 | Restore | display text (VM property / projector) |
| 147 | Hide again | display text (VM property / projector) |
| 148 | Showing {Rows.Count} rows | display text (VM property / projector) |
| 149 | Selected {SelectedVisibleCount}/{Rows.Count} shown | display text (VM property / projector) |
| 150 | Hidden {HiddenCount} · Duplicate {DuplicateCount} · Combined {CombinedCount} · Moved {MovedCount} · Rotated {RotatedCount} | display text (VM property / projector) |
| 151 | No DXF edit selected. | display text (VM property / projector) |
| 170 | Restore selected | display text (VM property / projector) |
| 171 | Clear selected groups | display text (VM property / projector) |
| 172 | Restore selected layers | display text (VM property / projector) |
| 173 | Restore selected geometry | display text (VM property / projector) |
| 174 | Apply selected | display text (VM property / projector) |
| 180 | Hidden | display text (VM property / projector) |
| 181 | Duplicate | display text (VM property / projector) |
| 182 | Combined | display text (VM property / projector) |
| 183 | Moved | display text (VM property / projector) |
| 184 | Rotated | display text (VM property / projector) |
| 185 | Changed | display text (VM property / projector) |
| 278 | Restore | display text (VM property / projector) |
| 283 | Hide again | display text (VM property / projector) |

### `ViewModels/DxfLayerImageExportViewModel.cs`  -  Export DXF layer image dialog

| line | text | kind |
|---:|---|---|
| 51 | PNG | display text (VM property / projector) |
| 52 | BMP | display text (VM property / projector) |
| 53 | JPG | display text (VM property / projector) |
| 98 | Final output: {OutputWidthPixels} x {OutputHeightPixels} px (layer {ClampPixels(WidthPixels)} x {ClampPixels(HeightPixels)} + padding). | display text (VM property / projector) |

### `ViewModels/DxfOverlapReportViewModel.cs`  -  DXF overlaps report / left panel

| line | text | kind |
|---:|---|---|
| 10 | No overlap issues. | display text (VM property / projector) |
| 20 | Issues | dialog/file-dialog text |
| 20 | Issues ({IssueCount}) | dialog/file-dialog text |

### `ViewModels/DxfOverlapResult.cs`  -  DXF overlaps report / left panel

| line | text | kind |
|---:|---|---|
| 26 | DXF check: not run. | display text (VM property / projector) |

### `ViewModels/FreeformAutoDetectResult.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 18 | Freeform stats: not run. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.AreaBuckets.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 19 | Area bucket select requires Color by area. | status chip |
| 31 | Area bucket select: no matching visible pad. | status chip |
| 40 | Area bucket selected: {bucketIds.Count} pads. | status chip |

### `ViewModels/FreeformHelperViewModel.CadLoad.cs`  -  DXF open (status chip)

| line | text | kind |
|---:|---|---|
| 103 |  Auto-hidden same-layer duplicates: {outcome.DuplicateSanitization.ExcludedPadIds.Count}. | display text (VM property / projector) |
| 107 | DXF imported ({sourceLabel}). CAD pads: {activePadCount}/{padCount}.{duplicateNote} | display text (VM property / projector) |
| 112 | Project loaded. DXF ({sourceLabel}), CAD pads: {activePadCount}/{padCount}.{duplicateNote} | display text (VM property / projector) |
| 115 | Project loaded: {projectPath}. DXF ({sourceLabel}), CAD pads: {activePadCount}/{padCount}.{duplicateNote} | display text (VM property / projector) |
| 124 | CAD load auto-hidden same-layer exact duplicate pads. groups={0}, duplicateIds={1}. | display text (VM property / projector) |
| 131 | DXF imported | display text (VM property / projector) |
| 131 | DXF reloaded | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.CadVisibility.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 62 | DXF duplicate pads auto-hidden. total={0}, ids={1}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.CoordinatePlanner.cs`  -  Coordinate tab (status chip)

| line | text | kind |
|---:|---|---|
| 31 | Coordinate: import DXF and build grid first. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Core.cs`  -  Dropdown option labels (Workspace + Settings)

| line | text | kind |
|---:|---|---|
| 159 | Use panel AA | display text (VM property / projector) |
| 160 | Use DXF bounds | display text (VM property / projector) |
| 164 | Generated grid | display text (VM property / projector) |
| 165 | DXF layer pads | display text (VM property / projector) |
| 169 | Rotate the currently selected baseline CAD pads as one rigid group. | display text (VM property / projector) |
| 169 | Selected pads | display text (VM property / projector) |
| 170 | Rotate all visible baseline CAD pads in the selected rotation layer as one rigid group. | display text (VM property / projector) |
| 170 | Target layer | display text (VM property / projector) |
| 179 | Geometry seed | display text (VM property / projector) |
| 180 | Use each CAD pad's geometry-seeded regular diff directly. Choose this when hardware behavior should follow the primary regular one-to-one. | display text (VM property / projector) |
| 183 | Geometry seed (unique per IC) | display text (VM property / projector) |
| 184 | Use geometry seed directly, but keep diff idx unique within each IC. Conflicts stay unresolved instead of shifting other CAD pads. | display text (VM property / projector) |
| 188 | CSV review (.csv) | display text (VM property / projector) |
| 194 | Release (minimal FW) | display text (VM property / projector) |
| 195 | Debug (trace + sim mask) | display text (VM property / projector) |
| 201 | Current (Gain) | display text (VM property / projector) |
| 202 | To Regular x To Full (current model). | display text (VM property / projector) |
| 205 | Conservative (No Gain) | display text (VM property / projector) |
| 206 | Apply ToRegular source combine; ToFull only gates boundary support. | display text (VM property / projector) |
| 209 | Disabled | display text (VM property / projector) |
| 210 | Turn off To Full and To Regular for baseline comparison. | display text (VM property / projector) |
| 224 | Info | display text (VM property / projector) |
| 229 | Auto (visible layers) | display text (VM property / projector) |
| 234 | Select layer | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.ChangeList.cs`  -  DXF Edit Details window (row text) + status chip

| line | text | kind |
|---:|---|---|
| 25 | DXF edit details: dialog handler not wired. | status chip |
| 62 | Hidden by combine | display text (VM property / projector) |
| 63 | Layer {pad.Layer} | display text (VM property / projector) |
| 64 | Group {combineGroupId} | display text (VM property / projector) |
| 66 | Combine group {combineGroupId} hides this source pad. Source pads: {sourceCount}. | display text (VM property / projector) |
| 67 | Combine group {combineGroupId} hides this source pad. | display text (VM property / projector) |
| 69 | Clear group | display text (VM property / projector) |
| 70 | Clear this combine group and restore its source pads. | display text (VM property / projector) |
| 81 | Hidden | display text (VM property / projector) |
| 82 | Layer {pad.Layer} | display text (VM property / projector) |
| 83 | Manual hide | display text (VM property / projector) |
| 84 | Hidden manually from the current DXF edit session. | display text (VM property / projector) |
| 86 | Restore | display text (VM property / projector) |
| 87 | Restore this hidden CAD pad. | display text (VM property / projector) |
| 103 | Layer {duplicatePad.Layer} | display text (VM property / projector) |
| 104 | Layer {duplicatePad.Layer} · Canonical CAD {canonicalCadId} | display text (VM property / projector) |
| 106 | Exact same-layer duplicate pad detected during DXF load. | display text (VM property / projector) |
| 107 | Exact same-layer duplicate of CAD {canonicalCadId} ({canonicalPad.Name}). | display text (VM property / projector) |
| 113 | Auto-hidden duplicate | display text (VM property / projector) |
| 113 | Restored duplicate | display text (VM property / projector) |
| 116 | Exact duplicate | display text (VM property / projector) |
| 117 | Canonical CAD {canonicalCadId} | display text (VM property / projector) |
| 120 | Hide again | display text (VM property / projector) |
| 120 | Restore | display text (VM property / projector) |
| 122 | Restore this duplicate CAD pad into the active working set. | display text (VM property / projector) |
| 123 | Auto-hide this duplicate CAD pad again. | display text (VM property / projector) |
| 144 | Synthetic | display text (VM property / projector) |
| 145 | Layer {pad.Layer} | display text (VM property / projector) |
| 146 | Group {groupId} · {sourceCount} src | display text (VM property / projector) |
| 146 | Synthetic pad | display text (VM property / projector) |
| 148 | Combine group {groupId} output. Source pads: {sourceCount}. | display text (VM property / projector) |
| 149 | Synthetic CAD pad generated by combine. | display text (VM property / projector) |
| 151 | Clear group | display text (VM property / projector) |
| 152 | Clear this combine group and restore its source pads. | display text (VM property / projector) |
| 170 | Moved | display text (VM property / projector) |
| 171 | Layer {currentPad.Layer} | display text (VM property / projector) |
| 173 | Original layer: {baselinePad.Layer} -> Current layer: {currentPad.Layer}. | display text (VM property / projector) |
| 175 | Restore layer | display text (VM property / projector) |
| 176 | Move this CAD pad back to layer '{baselinePad.Layer}'. | display text (VM property / projector) |
| 194 | Rotated | display text (VM property / projector) |
| 195 | Layer {currentPad.Layer} | display text (VM property / projector) |
| 196 | Geometry differs from DXF baseline | display text (VM property / projector) |
| 197 | Polygon vertices were rotated relative to the imported DXF baseline. Restore resets this pad geometry only. | display text (VM property / projector) |
| 199 | Restore geometry | display text (VM property / projector) |
| 200 | Restore this CAD pad geometry back to DXF baseline. | display text (VM property / projector) |
| 215 | CAD {focusCadPadId} is not visible. | display text (VM property / projector) |
| 217 | DXF edit focused: CAD {focusCadPadId} (CAD output={cadCount}). | display text (VM property / projector) |
| 267 | DXF restore hidden CAD pad | display text (VM property / projector) |
| 268 | DXF edit: restored CAD {cadPadId}. | display text (VM property / projector) |
| 269 | DXF edit restored CAD pad {cadPadId} | display text (VM property / projector) |
| 282 | DXF clear combined group | display text (VM property / projector) |
| 283 | DXF combine: cleared group {groupId} | display text (VM property / projector) |
| 284 | DXF combine cleared group | display text (VM property / projector) |
| 335 | DXF edit: restored CAD {cadPadId} to layer '{baselinePad.Layer}'. | status chip |
| 338 | DXF layer restore applied. cadId={0}, layer={1}. | display text (VM property / projector) |
| 342 | DXF restore CAD layer | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Combine.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 10 | DXF combine: import DXF first. | display text (VM property / projector) |
| 22 | DXF combine: select at least 2 CAD pads. | status chip |
| 33 | DXF combine: select at least 2 CAD output pads. | status chip |
| 43 | DXF combine: failed to build combined shape. | status chip |
| 85 | DXF combine: merged {sourcePads.Count} pads into {combinedPads.Count}. | status chip |
| 93 | DXF combine CAD pads | display text (VM property / projector) |
| 100 | DXF combine: no combined pad. | status chip |
| 113 | DXF combine: select combined CAD pads first. | status chip |
| 120 | DXF clear selected combined CAD pads | display text (VM property / projector) |
| 121 | DXF combine: cleared selected groups | display text (VM property / projector) |
| 122 | DXF combine cleared selected groups | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Hidden.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 9 | DXF edit: import DXF first. | display text (VM property / projector) |
| 20 | DXF edit: select CAD pads first. | status chip |
| 42 | DXF edit: hid {selected.Count} CAD pad(s). | status chip |
| 45 | DXF hide CAD pads | display text (VM property / projector) |
| 50 | DXF edit: import DXF first. | display text (VM property / projector) |
| 61 | DXF restore hidden CAD pads | display text (VM property / projector) |
| 62 | DXF edit: restored {count} CAD pad(s). | display text (VM property / projector) |
| 63 | DXF edit restored last batch | display text (VM property / projector) |
| 72 | DXF edit: no deleted CAD pads to restore. | status chip |
| 78 | DXF edit: import DXF first. | display text (VM property / projector) |
| 87 | DXF edit: no manual hidden CAD pads to restore. | status chip |
| 94 | DXF restore all hidden CAD pads | display text (VM property / projector) |
| 95 | DXF edit: restored all manual hidden CAD pads ({count}). | display text (VM property / projector) |
| 96 | DXF edit restored all manual hidden CAD pads | display text (VM property / projector) |
| 102 | DXF restore duplicate CAD pad | display text (VM property / projector) |
| 103 | DXF duplicate: restored {count} CAD pad(s). | display text (VM property / projector) |
| 108 | DXF rehide duplicate CAD pad | display text (VM property / projector) |
| 109 | DXF duplicate: auto-hidden {count} CAD pad(s) again. | display text (VM property / projector) |
| 176 | DXF duplicate visibility updated. autoHide={0}, changed={1}, autoHiddenTotal={2}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Reset.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 12 | DXF edit: no hidden/combined/layer edits. | status chip |
| 55 | DXF edit: reset hidden ({restored}), combined ({removedCombined}), layer edits ({restoredLayerMoves}), rotated ({restoredRotations}). | status chip |
| 58 | DXF edit reset all edits. hidden={0}, combined={1}, relayered={2}, rotated={3}. | display text (VM property / projector) |
| 64 | DXF reset all edits | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 23 | Export DXF image: import DXF first. | status chip |
| 27 | DXF not loaded | display text (VM property / projector) |
| 28 | Export DXF image requires an imported DXF.\\nPlease load a DXF file first. | display text (VM property / projector) |
| 36 | Export DXF image: dialog handler not wired. | status chip |
| 42 | Export DXF image: dialog handler not wired. | status chip |
| 50 | Export DXF image: no CAD pads available. | status chip |
| 62 | Export DXF image: no layer found. | status chip |
| 81 | Export DXF image: selected layer has no visible pad. | status chip |
| 85 | dxf_{SanitizeFileNameToken(request.Value.LayerName)} | display text (VM property / projector) |
| 103 | DXF image exported ({request.Value.Format}, layer={request.Value.LayerName}, pads={layerPads.Count}). | status chip |
| 118 | Export DXF image failed | status chip |
| 125 | Export DXF: import DXF first. | display text (VM property / projector) |
| 132 | Export DXF: dialog handler not wired. | status chip |
| 143 | Export DXF: no CAD output pads. | display text (VM property / projector) |
| 144 | Export DXF: no CAD pads available. | display text (VM property / projector) |
| 160 | DXF exported ({(visibleOnly ?  | status chip |
| 165 | Export DXF failed | status chip |

### `ViewModels/FreeformHelperViewModel.DxfEditing.Geometry.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 17 | Rotated pads: {RotatedCadPadCount}. Click to open geometry-rotation details. | display text (VM property / projector) |
| 24 | DXF rotate: import DXF first. | display text (VM property / projector) |
| 33 | DXF rotate: input a non-zero angle first. | status chip |
| 49 | DXF rotate: no CAD pad rotated. | status chip |
| 65 | DXF rotate: rotated {result.ChangedCount} CAD pad(s) by {result.AppliedDegrees:0.###}°. | status chip |
| 68 | DXF rotate applied. changedPads={0}, degrees={1:0.###}. | display text (VM property / projector) |
| 72 | DXF rotate CAD pads | display text (VM property / projector) |
| 125 | DXF rotate: select rotation layer first. | display text (VM property / projector) |
| 127 | DXF rotate: selected layer has no visible baseline CAD pads. | display text (VM property / projector) |
| 128 | DXF rotate: select baseline CAD pads first. | display text (VM property / projector) |
| 183 | DXF edit: restored CAD {cadPadId} geometry. | status chip |
| 186 | DXF geometry restore applied. cadId={0}. | display text (VM property / projector) |
| 189 | DXF restore CAD geometry | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.LayerMove.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 34 | Hidden pads: {DeletedCadPadCount}. Click to open hidden CAD edit details. | display text (VM property / projector) |
| 37 | Combined groups: {CombinedCadPadCount}. Click to open synthetic combine details. | display text (VM property / projector) |
| 41 | Duplicate groups: {DuplicateCadPadCount}. Auto-hidden exact duplicates: {_cadEdit.AutoHiddenDuplicateCadPadIds.Count}, restored: {GetRestoredDuplicateCadPadIds().Count}. Click to open duplicate details. | display text (VM property / projector) |
| 44 | Moved pads: {RelayeredCadPadCount}. Click to open layer-move details. | display text (VM property / projector) |
| 57 | DXF edit: select target layer first. | status chip |
| 61 | move to existing layer | display text (VM property / projector) |
| 69 | DXF edit: input new layer name first. | status chip |
| 73 | create layer and move | display text (VM property / projector) |
| 79 | DXF edit: import DXF first. | display text (VM property / projector) |
| 91 | DXF edit: select CAD pads first. | status chip |
| 119 | DXF edit: selected pads are already in layer '{targetLayerName}'. | status chip |
| 135 | DXF edit: moved {changedCount} CAD pad(s) to layer '{targetLayerName}'. | status chip |
| 138 | DXF layer move applied. op={0}, targetLayer={1}, changedPads={2}. | display text (VM property / projector) |
| 143 | DXF move CAD pads to layer | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfEditing.Persistence.cs`  -  Left panel > DXF edits (status chip)

| line | text | kind |
|---:|---|---|
| 212 | DXF edit replay skipped combined group {0}: source pad missing in baseline. | display text (VM property / projector) |
| 230 | DXF edit replay skipped combined group {0}: output pad snapshot invalid. | display text (VM property / projector) |
| 240 | DXF edit replay skipped combined group {0}: output pad id {1} conflicts. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfIndexing.Duplicates.cs`  -  Right panel > Step 4 (status chip)

| line | text | kind |
|---:|---|---|
| 28 | Duplicate CAD Output FW Diff: none. | display text (VM property / projector) |
| 74 | IC{group.IcIndex + 1} · diff {group.DiffIndex} | display text (VM property / projector) |
| 80 | raw {entry.RawBestDiffIndex} | display text (VM property / projector) |
| 81 | raw - | display text (VM property / projector) |
| 83 | masked {entry.MaskedBestDiffIndex} | display text (VM property / projector) |
| 84 | masked - | display text (VM property / projector) |
| 86 | CAD {entry.CadPadId} [{entry.LayerName}] -> current {entry.CurrentDiffIndex}, {rawText}, {maskedText} | display text (VM property / projector) |
| 118 | Duplicate CAD Output FW Diff: none. | display text (VM property / projector) |
| 138 | Duplicate CAD Output FW Diff IC{group.IcIndex + 1}/diff{group.DiffIndex} is not visible. | display text (VM property / projector) |
| 140 | Duplicate CAD Output FW Diff focused: IC{group.IcIndex + 1}/diff{group.DiffIndex} ({cadCount} CAD pad(s)). | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfIndexing.Shift.cs`  -  Right panel > Step 4 (status chip)

| line | text | kind |
|---:|---|---|
| 24 | Diff shift: select CAD output pads in AA first. | display text (VM property / projector) |
| 32 | Diff shift target: {visibleSelected.Count} CAD pad(s), current range {diffs.First()}-{diffs.Last()}, offset {Step4Index.ShiftDelta:0}. | display text (VM property / projector) |
| 51 | CAD Output FW Diff offset: select CAD output pads first. | status chip |
| 83 | CAD Output FW Diff offset: selected CAD pads already match the requested offset. | status chip |
| 96 | CAD Output FW Diff offset | display text (VM property / projector) |
| 105 | Diff idx batch shift applied. selected={0}, delta={1}, missing={2}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfIndexing.cs`  -  Right panel > Step 4 (status chip)

| line | text | kind |
|---:|---|---|
| 55 | CAD Output FW Diff override | display text (VM property / projector) |
| 72 | CAD Output FW Diff override clear: CAD {cadPadId} has no override. | status chip |
| 81 | CAD Output FW Diff override clear | display text (VM property / projector) |
| 87 | CAD Output FW Diff override cleared: CAD {cadPadId}. | status chip |
| 105 | CAD Output FW Diff anchor ignored: CAD {cadPadId} is not visible. | status chip |
| 122 | CAD Output FW Diff anchor clear ignored: CAD {cadPadId} is not visible. | status chip |
| 128 | CAD Output FW Diff anchor clear: IC {icIndex + 1} has no anchor. | status chip |
| 136 | CAD Output FW Diff anchor cleared: IC {icIndex + 1} uses scan-order first CAD. | status chip |
| 157 | CAD Output FW Diff override saved: IC {targetIc.Value + 1}, CAD {cadPadId} -> {index}. | display text (VM property / projector) |
| 158 | CAD Output FW Diff override saved: CAD {cadPadId} -> {index}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.DxfQuality.cs`  -  Left panel > DXF check (status chip)

| line | text | kind |
|---:|---|---|
| 23 | DXF check: no DXF loaded. | display text (VM property / projector) |
| 30 | DXF check: no CAD output pads (all layers off). | display text (VM property / projector) |
| 36 | DXF overlap check | display text (VM property / projector) |
| 41 | DXF check: running... | display text (VM property / projector) |
| 48 | DXF check failed. | display text (VM property / projector) |
| 49 | DXF overlap check failed | status chip |
| 74 | DXF check: no overlaps detected. | display text (VM property / projector) |
| 80 | DXF warnings: {report.Summary}. | display text (VM property / projector) |
| 98 | DXF check: no overlap result. | status chip |
| 111 | DXF check: highlight + select ({ids.Count} pads). | status chip |
| 119 | DXF check: highlight cleared. | status chip |
| 127 | DXF overlap details: view handler not wired. | status chip |
| 135 | No overlap issues. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.FreeformStats.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 27 | Freeform stats: no regular pads. | display text (VM property / projector) |
| 34 | None | display text (VM property / projector) |
| 35 | XWay | display text (VM property / projector) |
| 36 | YWay | display text (VM property / projector) |
| 37 | XYWay | display text (VM property / projector) |
| 41 | Freeform stats ({source}): total={stats.TotalCount}, tagged={stats.TaggedCount}, none={stats.NoneCount}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.IndexMapping.cs`  -  Right panel > Step 4 (status chip)

| line | text | kind |
|---:|---|---|
| 14 | Step 4: import DXF and build grid first. | display text (VM property / projector) |
| 19 | Diagnostics: no CAD output pads. | display text (VM property / projector) |
| 29 | DXF/Regular mapping analysis | display text (VM property / projector) |
| 38 | Diagnostics: running... | display text (VM property / projector) |
| 40 | Running mapping diagnostics... | status chip |
| 46 | Mapping analyze failed | status chip |
| 68 | Diagnostics warnings: {res.Report.Summary} | display text (VM property / projector) |
| 69 | Diagnostics OK: {res.Report.Summary} | display text (VM property / projector) |
| 137 | Mapping issue locate: target not visible. | display text (VM property / projector) |
| 139 | Mapping diagnostics focused: CAD={visibleCadCount}, Regular={visibleRegularCount}. | display text (VM property / projector) |
| 146 | Mapping override: target regular pad not in current grid. | status chip |
| 153 | Mapping override saved: CAD {cadId} -> regular id {regularIndex}. | status chip |
| 162 | Mapping override clear: CAD {cadId} has no saved override. | status chip |
| 168 | Mapping override cleared: CAD {cadId}. | status chip |
| 177 | Diff repair override ignored: CAD {cadId} is not visible. | status chip |
| 197 | Diagnostics OK: map={report.MappedCount}, low={report.LowConfidenceCount}, amb={report.AmbiguousCount}, dup={report.DuplicateDiffCount}. | display text (VM property / projector) |
| 202 | Diagnostics warn: map={report.MappedCount}, unmapCad={report.UnmappedCadCount}, unmapReg={report.UnmappedRegularCount},  | display text (VM property / projector) |
| 203 | low={report.LowConfidenceCount}, amb={report.AmbiguousCount}, dup={report.DuplicateDiffCount}, mask={report.MaskChangedCount}/{report.MaskRemovedCount}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.ManualSizing.cs`  -  Workspace header > manual sizing

| line | text | kind |
|---:|---|---|
| 90 | Grid not built. | display text (VM property / projector) |
| 101 | No manual range. | display text (VM property / projector) |
| 106 | Rows: invalid. | display text (VM property / projector) |
| 111 | Cols: invalid. | display text (VM property / projector) |
| 118 | Manual range: Rows {rowLabel}, Cols {colLabel}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`  -  Right panel > Step 1/2 (status chip)

| line | text | kind |
|---:|---|---|
| 44 | Match | display text (VM property / projector) |
| 47 | Step 1: import DXF and build grid first. | display text (VM property / projector) |
| 54 | Match task | display text (VM property / projector) |
| 60 | Matching pads... | display text (VM property / projector) |
| 66 | Match failed | status chip |
| 77 | Matching complete. | display text (VM property / projector) |
| 133 | Project loaded: no CAD output pads; Step 1 auto replay skipped. | display text (VM property / projector) |
| 141 | Project loaded: replaying Step 1 match... | display text (VM property / projector) |
| 151 | project load | display text (VM property / projector) |
| 152 | Project loaded: Step 1 match restored. | display text (VM property / projector) |
| 171 | Project loaded: Step 2 auto replay skipped (CAD/grid missing). | display text (VM property / projector) |
| 187 | project load auto-detect | display text (VM property / projector) |
| 192 | Project loaded: Step 2 freeform restored. | display text (VM property / projector) |
| 208 | Step 2: import DXF and run Step 1 first. | display text (VM property / projector) |
| 234 | Auto detect failed | status chip |

### `ViewModels/FreeformHelperViewModel.NotchDetails.Compensation.Diagnostics.cs`  -  Notch detail / Step 3 (status chip)

| line | text | kind |
|---:|---|---|
| 33 | IC | display text (VM property / projector) |
| 72 |  more regular(s) | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.NotchDetails.cs`  -  Notch detail / Step 3 (status chip)

| line | text | kind |
|---:|---|---|
| 22 | Notch 2.2 preview: build grid first. | status chip |
| 33 | Notch 2.2 preview: select at least one CAD pad. | status chip |
| 50 | Notch 2.2 preview: selected CAD has no effective To Full result. | status chip |
| 63 | CAD {items[0].CadPadId} | display text (VM property / projector) |
| 66 | To Full effective ON | display text (VM property / projector) |
| 67 | To Full mixed ({enabledCount} ON, {disabledCount} OFF) | display text (VM property / projector) |
| 68 | Notch 2.2 preview updated: {scopeText}, {toFullState}. | status chip |
| 152 | Notch detail: dialog handler not wired. | status chip |
| 159 | Notch detail: import DXF and build grid first. | status chip |
| 166 | Notch detail: select a CAD pad first. | status chip |
| 177 | Notch detail: selected CAD pad not found. | status chip |
| 198 | Notch detail opened: cadId={0}, dxfIndex={1}, allocations={2}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs`  -  Export / Simulation progress (status chip + console)

| line | text | kind |
|---:|---|---|
| 398 | cached(cad=0x{6:X},grid=0x{7:X},rev={8},settings={9}). | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.NotchValidation.cs`  -  Right panel > Settings > Step 6 validation (status chip)

| line | text | kind |
|---:|---|---|
| 19 | Validation: select one REG first. | display text (VM property / projector) |
| 20 | Validation: no Regular selected. | status chip |
| 57 | REG {report.RegularPadId} \| IC{report.IcIndex + 1}/diff{report.DiffIndex} \| rows={mergedItems.Count} (direct={directItems.Count}, in={incomingItems.Count}, out={outgoingItems.Count}) | display text (VM property / projector) |
| 59 | Validation updated: REG {report.RegularPadId}, rows={report.DirectRows.Count}, in={report.IncomingRows.Count}, out={report.OutgoingRows.Count}. | status chip |
| 78 | Validation: build grid first. | display text (VM property / projector) |
| 86 | Validation: REG {regularPadId} not found. | display text (VM property / projector) |
| 93 | Validation: no generated notch table. Run Step 5 Export once first. | display text (VM property / projector) |
| 197 | Validation focus skipped: no target pad available. | status chip |
| 205 | Validation focus: target pads are not visible. | display text (VM property / projector) |
| 207 | Validation focus: {item.KindText} {item.RowText} (CAD={cadCount}, REG={regularCount}). | display text (VM property / projector) |
| 256 | IC {row.IcIndex + 1} | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs`  -  Workspace operations (status chip)

| line | text | kind |
|---:|---|---|
| 56 | Layer filter failed | status chip |
| 79 | CAD Output FW Diff assignment: no CAD output pads. | display text (VM property / projector) |
| 240 | IC{kv.Key + 1}->CAD {kv.Value} | display text (VM property / projector) |
| 243 | anchor=none | display text (VM property / projector) |
| 245 | scan={SelectedScanOrder} | display text (VM property / projector) |
| 246 | IC groups={padsByIc.Count} | display text (VM property / projector) |
| 249 | geometry seed | display text (VM property / projector) |
| 250 | geometry seed (unique per IC) | display text (VM property / projector) |
| 252 | override > direct geometry-seed | display text (VM property / projector) |
| 253 | override > direct geometry-seed (per-IC unique) | display text (VM property / projector) |
| 263 | CAD Output FW Diff assignment: override={assignment.OverrideAssignedCount}, auto={assignment.AutoAssignedCount}, unresolved={unresolvedCount}, {icText}, {anchorText}, {scanOrderText}. Mode: {modeText}. Priority: {priorityText}{qualityText}. | display text (VM property / projector) |
| 356 | Auto-applied csv local repair suggestions: count={0}, samples={1}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Operations.LayerSelection.Categories.cs`  -  Left panel > Layers (labels + status chip)

| line | text | kind |
|---:|---|---|
| 74 | Off | display text (VM property / projector) |
| 74 | On | display text (VM property / projector) |
| 75 | DXF layer category {display} {action} | display text (VM property / projector) |
| 106 | Layers containing LWPOLYLINE/POLYLINE | display text (VM property / projector) |
| 106 | Polyline | display text (VM property / projector) |
| 107 | Block | display text (VM property / projector) |
| 107 | Layers with entities in BLOCKS section | display text (VM property / projector) |
| 108 | Layers containing TEXT/MTEXT | display text (VM property / projector) |
| 108 | Text | display text (VM property / projector) |
| 109 | Unknown | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Operations.LayerSelection.Options.cs`  -  Left panel > Layers (labels + status chip)

| line | text | kind |
|---:|---|---|
| 69 | DXF regular source: load DXF first. | display text (VM property / projector) |
| 76 | DXF regular source: select a regular layer. | display text (VM property / projector) |
| 86 | Regular layer '{layerName}' has no visible pads. | display text (VM property / projector) |
| 95 | Regular layer '{layerName}': pads={layerPads.Count}, matches X*Y={x}*{y}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Operations.LayerSelection.cs`  -  Left panel > Layers (labels + status chip)

| line | text | kind |
|---:|---|---|
| 37 | No layers | display text (VM property / projector) |
| 39 | All on | display text (VM property / projector) |
| 40 | All off | display text (VM property / projector) |
| 43 | Load a DXF before changing layer visibility. | display text (VM property / projector) |
| 45 | All layers are visible. Click to hide every layer. | display text (VM property / projector) |
| 46 | Layer visibility is not fully on. Click to show every layer. | display text (VM property / projector) |
| 146 | Select all DXF layers | display text (VM property / projector) |
| 180 | Deselect all DXF layers | display text (VM property / projector) |
| 219 | DXF layer selection | display text (VM property / projector) |
| 271 | DXF layer quick select | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Operations.cs`  -  Workspace operations (status chip)

| line | text | kind |
|---:|---|---|
| 217 | Open DXF: dialog handler not wired. | status chip |
| 258 | DXF import failed | status chip |
| 270 | Grid rebuild | display text (VM property / projector) |
| 274 | Rebuild grid failed | status chip |
| 283 | Building regular grid... | status chip |
| 319 | Freeform stats: run Step 2 auto-detect. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Overrides.cs`  -  Freeform tagging (status chip)

| line | text | kind |
|---:|---|---|
| 20 | Freeform tagging: build grid first. | status chip |
| 26 | Freeform tagging: select regular pads first. | status chip |
| 37 | Freeform tagging: build grid first. | status chip |
| 43 | Freeform tagging: no regular pads selected. | status chip |
| 54 | manual override | display text (VM property / projector) |
| 63 | Freeform set to {type} for {touched} regular pads. | status chip |

### `ViewModels/FreeformHelperViewModel.PadEdits.cs`  -  Pad edits / manual sizing (status chip)

| line | text | kind |
|---:|---|---|
| 52 | CAD custom value | display text (VM property / projector) |
| 56 | CAD pad #{cadIds[0]} custom value set to {value:0.####}. | display text (VM property / projector) |
| 57 | CAD pads updated: {cadIds.Count} items. | display text (VM property / projector) |
| 73 | Manual sizing: build grid first. | status chip |
| 98 | Manual sizing: {result.Error} | status chip |
| 109 | Manual sizing | display text (VM property / projector) |
| 111 | Manual sizing applied. | status chip |
| 134 | Manual sizing: build grid first. | status chip |
| 150 | Reset sizing: {result.Error} | status chip |
| 161 | Reset manual sizing | display text (VM property / projector) |
| 163 | Manual sizing reset to default. | status chip |

### `ViewModels/FreeformHelperViewModel.PadInspector.Formatting.cs`  -  Right panel > Inspector / pad info

| line | text | kind |
|---:|---|---|
| 16 | Manual override | display text (VM property / projector) |
| 21 | Anchor saved (inactive in current mode) | display text (VM property / projector) |
| 25 | Auto (geometry seed) | display text (VM property / projector) |
| 26 | Auto (geometry seed, unique per IC) | display text (VM property / projector) |
| 35 | Unmatched | display text (VM property / projector) |
| 40 | Strict | display text (VM property / projector) |
| 45 | Probable | display text (VM property / projector) |
| 48 | Auto | display text (VM property / projector) |
| 58 | Unmatched | display text (VM property / projector) |
| 83 | Unmatched | display text (VM property / projector) |
| 118 | IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} | display text (VM property / projector) |
| 119 | regId {regularPadId} | display text (VM property / projector) |
| 128 | IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} (regId {regularPadId}) | display text (VM property / projector) |
| 129 | regId {regularPadId} | display text (VM property / projector) |
| 138 | Unmatched | display text (VM property / projector) |
| 153 | CAD idx {dxfListText} (CAD id {cadListText}) | display text (VM property / projector) |
| 156 | CAD id {cadListText} | display text (VM property / projector) |
| 165 | Unmatched | display text (VM property / projector) |
| 187 | idx {dxfIndex} | display text (VM property / projector) |
| 188 | id {link.CadPadId} | display text (VM property / projector) |
| 189 | CAD {indexText}: {(link.CadCoverage * 100):0.#}% | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.PadInspector.Snapshots.cs`  -  Right panel > Inspector / pad info

| line | text | kind |
|---:|---|---|
| 75 | Grid not built. | display text (VM property / projector) |
| 90 | Computing in background. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.PadInspector.Trace.cs`  -  Right panel > Inspector / pad info

| line | text | kind |
|---:|---|---|
| 20 | Computing in background. | display text (VM property / projector) |
| 25 | Diff idx | display text (VM property / projector) |
| 28 | Diff idx value: {dxfIndex.Value} | display text (VM property / projector) |
| 29 | Diff idx unresolved (-). | display text (VM property / projector) |
| 35 | Match | display text (VM property / projector) |
| 36 | No matched regular pad | display text (VM property / projector) |
| 37 | Best overlap target: none. | display text (VM property / projector) |
| 46 | IC{best.IcIndex.Value + 1}/diff{best.DiffIndex.Value} | display text (VM property / projector) |
| 49 | Match | display text (VM property / projector) |
| 50 | Matched regular pad count: {matchedRegularDetails.Count} | display text (VM property / projector) |
| 51 | Best target: {bestKey}. CAD coverage: {best.CadCoverage:P1}. Regular coverage: {best.RegularCoverage:P1}. | display text (VM property / projector) |
| 58 | No row | display text (VM property / projector) |
| 60 | Deferred notch-row eligibility is running. | display text (VM property / projector) |
| 63 | Notch export | display text (VM property / projector) |
| 70 | Notch 2.2 | display text (VM property / projector) |
| 71 | Not available | display text (VM property / projector) |
| 72 | Grid is not ready. | display text (VM property / projector) |
| 77 | To Regular | display text (VM property / projector) |
| 79 | Undo NF ratio from overlap-area / regular-area sum. | display text (VM property / projector) |
| 81 | To Full | display text (VM property / projector) |
| 82 | Disabled | display text (VM property / projector) |
| 82 | Enabled | display text (VM property / projector) |
| 97 | Identity | display text (VM property / projector) |
| 98 | IC{regularPad.IcIndex + 1}/diff{regularPad.DiffIndex} | display text (VM property / projector) |
| 99 | R{regularPad.Row}  C{regularPad.Col}  REG{regularPad.RegularPadId} | display text (VM property / projector) |
| 105 | Match | display text (VM property / projector) |
| 106 | No matched CAD pad | display text (VM property / projector) |
| 107 | Best overlap target: none. | display text (VM property / projector) |
| 119 | Match | display text (VM property / projector) |
| 120 | Matched CAD pad count: {matchedCadDetails.Count} | display text (VM property / projector) |
| 121 | Best target: {bestKey}. Regular coverage: {best.RegularCoverage:P1}. CAD coverage: {best.CadCoverage:P1}. | display text (VM property / projector) |
| 125 | Diff source | display text (VM property / projector) |
| 128 | Exact CAD Output FW Diff match: Yes. | display text (VM property / projector) |
| 129 | Exact CAD Output FW Diff match: No. | display text (VM property / projector) |
| 131 | Freeform | display text (VM property / projector) |
| 140 | OFF | display text (VM property / projector) |
| 140 | ON | display text (VM property / projector) |
| 145 | Diagnostics: unavailable. | display text (VM property / projector) |
| 155 | Diagnostics: unavailable. | display text (VM property / projector) |
| 162 | Raw: {firstLine} | display text (VM property / projector) |
| 177 | Rule: {parsed.ReasonText} | display text (VM property / projector) |
| 271 | No | display text (VM property / projector) |
| 271 | Yes | display text (VM property / projector) |
| 279 | Computing | display text (VM property / projector) |
| 280 | Queued | display text (VM property / projector) |
| 281 | Detailed trace is loading in background. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.PadInspector.cs`  -  Right panel > Inspector / pad info

| line | text | kind |
|---:|---|---|
| 54 | Unmatched | display text (VM property / projector) |
| 55 | Matched regular pad(s): {matchedRegularIds.Count}. | display text (VM property / projector) |
| 148 | Notch rows: {notchEligibility.EstimatedRowCount} ({eligibleVersionText}) | display text (VM property / projector) |
| 149 | Notch rows: 0 ({notchEligibility.Reason}) | display text (VM property / projector) |
| 154 | Notch rows: computing... | display text (VM property / projector) |
| 217 | Unmatched | display text (VM property / projector) |
| 218 | Matched CAD pad(s): {matchedCadIds.Count}. | display text (VM property / projector) |
| 223 | Unmatched | display text (VM property / projector) |
| 228 | None | display text (VM property / projector) |
| 233 | No Step 2 freeform tag. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs`  -  Right panel > Inspector / pad info

| line | text | kind |
|---:|---|---|
| 265 | Pad inspector deferred refresh failed: CAD={0}. | display text (VM property / projector) |
| 331 | Pad inspector deferred refresh failed: REG={0}. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.PadInspectorSummary.cs`  -  Right panel > Inspector / pad info

| line | text | kind |
|---:|---|---|
| 62 | IC - | display text (VM property / projector) |
| 62 | IC {cad.IcIndex.Value + 1} | display text (VM property / projector) |
| 65 | CAD #{displayIndex} (id {cad.CadPadId}) | display text (VM property / projector) |
| 66 | CAD {cad.CadPadId} | display text (VM property / projector) |
| 73 | Diff source: {cad.CadOutputFwDiffAssignmentModeText} | display text (VM property / projector) |
| 74 | Match: {matchCount} regular pad(s), confidence {confidenceText} | display text (VM property / projector) |
| 82 | REG {regular.RegularPadId} · Diff {regular.DiffIndex} | display text (VM property / projector) |
| 83 | REG {regular.RegularPadId} · IC {regular.IcIndex + 1} · Diff idx {regular.DiffIndex} | display text (VM property / projector) |
| 84 | Diff source: {regular.DiffSource} | display text (VM property / projector) |
| 85 | Match: {matchCount} CAD pad(s), confidence {confidenceText} | display text (VM property / projector) |
| 121 | Freeform | display text (VM property / projector) |
| 131 | Source | display text (VM property / projector) |
| 147 | Notch 2.2 | display text (VM property / projector) |
| 147 | Unavailable | display text (VM property / projector) |
| 148 | Notch rows | display text (VM property / projector) |
| 157 | To Full | display text (VM property / projector) |
| 163 | Reason | display text (VM property / projector) |
| 169 | Freeform | display text (VM property / projector) |
| 177 | To Regular | display text (VM property / projector) |
| 180 | Combined | display text (VM property / projector) |
| 184 | Notch rows | display text (VM property / projector) |
| 391 | No Step 2 freeform tag. | display text (VM property / projector) |
| 391 | None | display text (VM property / projector) |
| 397 | Override | display text (VM property / projector) |
| 397 | Saved/manual override keeps this regular pad as {regularPad.Freeform}. | display text (VM property / projector) |
| 400 | Current value comes from Step 2 auto-detect ({regularPad.Freeform}). | display text (VM property / projector) |
| 400 | Step 2 auto-detect | display text (VM property / projector) |
| 407 | Identity | display text (VM property / projector) |
| 412 | Match | display text (VM property / projector) |
| 417 | Notch eligibility | display text (VM property / projector) |
| 424 | Notch compute | display text (VM property / projector) |
| 429 | Debug raw | display text (VM property / projector) |
| 432 | Other | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs`  -  File / Export menu flows (status chip)

| line | text | kind |
|---:|---|---|
| 29 | CSV review | display text (VM property / projector) |
| 55 | notch_{versions[0].ToDisplayLabel()} | display text (VM property / projector) |
| 124 | Notch row preview target is not visible. | display text (VM property / projector) |
| 126 | Notch row focused: CAD={cadCount}, REG={regularCount}. | display text (VM property / projector) |
| 183 | Scan CAD | display text (VM property / projector) |
| 248 | Scan CAD | display text (VM property / projector) |
| 313 | versions=- | display text (VM property / projector) |
| 321 | toFullCoverage=n/a | display text (VM property / projector) |
| 325 | toFullCoverage=miss {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount} | display text (VM property / projector) |
| 326 | toFullCoverage=ok {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount} | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Persistence.Project.cs`  -  File / Export menu flows (status chip)

| line | text | kind |
|---:|---|---|
| 39 | Save project: dialog handler not wired. | display text (VM property / projector) |
| 44 | Save project | display text (VM property / projector) |
| 66 | Project saved: {result.Path} | display text (VM property / projector) |
| 67 | Project saved: {result.Path}. {result.EmbedWarning} | display text (VM property / projector) |
| 82 | Save project failed | status chip |
| 99 | Load project: dialog handler not wired. | display text (VM property / projector) |
| 100 | Load project: dialog handler not wired. | display text (VM property / projector) |
| 106 | Load cancelled. | display text (VM property / projector) |
| 153 | Argument '--path' is required. | dialog text |
| 161 | Invalid project path '{path}': {pathError!.Message} | dialog text |
| 168 | Project file not found: {normalizedPath} | dialog text |
| 174 | Load project failed: {normalizedPath} | dialog text |
| 178 | Load project | display text (VM property / projector) |
| 198 | Load cancelled (pending edits were not committed). | dialog text |
| 218 | Load project failed | status chip |
| 254 | Project loaded: {path} | display text (VM property / projector) |
| 357 | disabled by setting | display text (VM property / projector) |
| 437 | Project loaded: {path} | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Persistence.cs`  -  File / Export menu flows (status chip)

| line | text | kind |
|---:|---|---|
| 26 | Export notch table | display text (VM property / projector) |
| 50 | Export notch: import DXF and build grid first. | display text (VM property / projector) |
| 63 | Export notch: dialog handler not wired. | display text (VM property / projector) |
| 112 | Writing file to '{path}' | display text (VM property / projector) |
| 149 | Checking workflow prerequisites | display text (VM property / projector) |
| 154 | Step 5: export is blocked by workflow prerequisites. | display text (VM property / projector) |
| 163 | Generating notch table | display text (VM property / projector) |
| 181 | Export notch: no rows generated. | display text (VM property / projector) |
| 207 | Waiting row selection dialog | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.ProjectSession.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 36 | A project is already loading. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.RegularVisibilityMask.cs`  -  Right panel > Step 1 mask (status chip)

| line | text | kind |
|---:|---|---|
| 10 | Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 geometry seed and Simulation. | display text (VM property / projector) |
| 27 | Regular visibility mask import: dialog handler not wired. | status chip |
| 33 | Regular visibility mask import: build regular grid first. | status chip |
| 56 | Regular visibility mask imported: {0}. Active={1}, inactive={2}, frames={3}. | display text (VM property / projector) |
| 134 | Regular visibility mask cleared. Step 4 uses pure geometry seed. | status chip |
| 164 | Regular visibility mask loaded: source={0}, active={1}, inactive={2}, frames={3}, enabled={4}. | display text (VM property / projector) |
| 175 | Regular visibility mask ignored: source={0}, diagnostic={1}. | display text (VM property / projector) |
| 183 | Regular visibility mask failed: source={0}, diagnostic={1}. | display text (VM property / projector) |
| 249 | Regular visibility mask enabled. | display text (VM property / projector) |
| 250 | Regular visibility mask disabled. Step 4 uses pure geometry seed. | display text (VM property / projector) |
| 258 | Regular visibility mask enabled: {Path.GetFileName(result.SourcePath)} loaded, active={result.ActiveRegularCount}, inactive={result.InactiveRegularCount}, frames={result.FrameCount}. Step 4 geometry seed only uses active regulars. | display text (VM property / projector) |
| 260 | Regular visibility mask loaded but disabled: {Path.GetFileName(result.SourcePath)}. Step 4 uses pure geometry seed. | display text (VM property / projector) |
| 262 | Regular visibility mask: {Path.GetFileName(result.SourcePath)} incompatible with current grid {grid?.Rows ?? 0}x{grid?.Cols ?? 0}. Ignored. {result.Diagnostic} | display text (VM property / projector) |
| 264 | Regular visibility mask: failed to load {Path.GetFileName(result.SourcePath)}. Ignored. {result.Diagnostic} | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Selection.Locate.cs`  -  Canvas selection / Quick focus (status chip)

| line | text | kind |
|---:|---|---|
| 33 | Auto focus | display text (VM property / projector) |
| 34 | Auto focus | display text (VM property / projector) |
| 35 | Auto focus | display text (VM property / projector) |
| 38 | Quick focus | display text (VM property / projector) |
| 58 | Quick focus | display text (VM property / projector) |
| 78 | Quick focus | display text (VM property / projector) |
| 124 | IC {icIndex.Value + 1}  | display text (VM property / projector) |
| 132 | Quick focus: {icText}CAD {query.Value} is not visible. | display text (VM property / projector) |
| 133 | Quick focus: {icText}REG {query.Value} is not visible. | display text (VM property / projector) |
| 134 | Quick focus: {icText}FW diff {query.Value} is not visible. | display text (VM property / projector) |
| 135 | Quick focus: {icText}{query.Value} did not match visible CAD, REG, or FW diff. | display text (VM property / projector) |
| 230 | Owner CAD highlight skipped: no target ids. | status chip |
| 237 | Owner CAD highlight skipped: targets are not visible. | status chip |
| 246 | Owner CAD highlighted: {visibleCadIds.Count} pad(s). | display text (VM property / projector) |
| 254 | Regular target highlight skipped: no target ids. | status chip |
| 261 | Regular target highlight skipped: targets are not visible. | status chip |
| 270 | Regular target highlighted: {visibleRegularIds.Count} pad(s). | display text (VM property / projector) |
| 279 | CAD {cadPadId} is not visible. | status chip |
| 286 | CAD {cadPadId} has no overlap match. | status chip |
| 294 | CAD {cadPadId} is not visible. | display text (VM property / projector) |
| 296 | Match focused: CAD {cadPadId} -> {regularCount} regular pad(s). | display text (VM property / projector) |
| 297 | Match highlighted: CAD {cadPadId} -> {regularCount} regular pad(s). | display text (VM property / projector) |
| 305 | Regular {regularPadId} is not visible. | status chip |
| 312 | Regular {regularPadId} has no overlap match. | status chip |
| 320 | Regular {regularPadId} is not visible. | display text (VM property / projector) |
| 322 | Match focused: Regular {regularPadId} -> {cadCount} CAD pad(s). | display text (VM property / projector) |
| 323 | Match highlighted: Regular {regularPadId} -> {cadCount} CAD pad(s). | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Selection.cs`  -  Canvas selection / Quick focus (status chip)

| line | text | kind |
|---:|---|---|
| 118 | Selection updated: CAD={0}, Regular={1}, summary={2}ms, inspector={3}ms, notchPreview={4}ms, total={5}ms. {6} \| {7} | display text (VM property / projector) |
| 133 | canvasPerf=n/a | display text (VM property / projector) |
| 140 | canvasSelect(rev={0},ms={1},cadCand={2},regCand={3},cadIdx={4},regIdx={5}) draw(rev={6},ms={7},cadCand={8},regCand={9},visCad={10},visReg={11},stepCad={12},stepReg={13},decCad={14},decReg={15},qHit={16},listHit={17}) | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Settings.Orchestration.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 83 | CAD Output FW Diff anchor cleared for all ICs. | status chip |
| 98 | CAD Output FW Diff anchor ignored: CAD {cadId} is not visible. | status chip |
| 104 | CAD Output FW Diff anchor saved: IC {icIndex + 1} -> CAD {cadId}. | status chip |

### `ViewModels/FreeformHelperViewModel.Settings.Sync.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 153 | No manual range. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Settings.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 130 | IC {setting.IcIndex} {e.PropertyName} | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.SettingsWindow.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 46 | Select layer | display text (VM property / projector) |
| 283 | Settings reset to defaults. | status chip |

### `ViewModels/FreeformHelperViewModel.Simulation.cs`  -  Simulation build (status chip)

| line | text | kind |
|---:|---|---|
| 39 | Simulation: import DXF and build grid first. | display text (VM property / projector) |
| 50 | Build simulation workspace | display text (VM property / projector) |
| 53 | Preparing simulation workspace... | display text (VM property / projector) |
| 56 | Generating simulation notch table... | display text (VM property / projector) |
| 60 | Simulation workspace | display text (VM property / projector) |
| 72 | Simulation: no notch rows generated under current settings. | display text (VM property / projector) |
| 89 | Resolving simulation diff grid... | display text (VM property / projector) |
| 101 | Simulation warning: duplicate CAD output FW diff at IC{0}/diff{1}; results may include ambiguous mappings. | display text (VM property / projector) |
| 118 | Finalizing simulation workspace... | display text (VM property / projector) |
| 136 | Simulation ready. Rows={0}. | display text (VM property / projector) |
| 146 | Simulation prewarm ready. Rows={0}. | display text (VM property / projector) |
| 147 | Simulation prewarm ready with duplicate-diff warning. Rows={0}. | display text (VM property / projector) |
| 159 | Simulation build failed: {ex.Message} | display text (VM property / projector) |
| 163 | Simulation workspace failed | status chip |

### `ViewModels/FreeformHelperViewModel.State.Notch.cs`  -  Workspace header / right panel state text

| line | text | kind |
|---:|---|---|
| 401 | Notch export: not run. | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.State.NotchPreview.cs`  -  Right panel > Step 3 + Settings > Step 3 summaries

| line | text | kind |
|---:|---|---|
| 159 | Current layer: Stage 1 (Seed) | display text (VM property / projector) |
| 160 | Current layer: Stage 2 (Candidate) | display text (VM property / projector) |
| 161 | Current layer: Stage 3 (Final) | display text (VM property / projector) |
| 168 | AutoPlay ON ({NotchPreviewAutoPlayIntervalMs:0} ms, loop 1->2->3) | display text (VM property / projector) |
| 169 | AutoPlay OFF ({NotchPreviewAutoPlayIntervalMs:0} ms) | display text (VM property / projector) |
| 176 | To Full rule engine: ON (trace ON) | display text (VM property / projector) |
| 177 | To Full rule engine: ON (trace OFF) | display text (VM property / projector) |
| 178 | To Full rule engine: OFF (legacy gate path) | display text (VM property / projector) |
| 186 | Allocation model: Stage3 effective-area gain; ToFull-expanded area splits the combined source signal. | display text (VM property / projector) |
| 187 | Allocation model: SourceArea-dominant; ToFull is support/cap, not direct full-area weight. | display text (VM property / projector) |
| 188 | Allocation model: disabled baseline. | display text (VM property / projector) |
| 195 | Stage3 gain allocation | display text (VM property / projector) |
| 196 | Source-area dominant | display text (VM property / projector) |
| 197 | Disabled baseline | display text (VM property / projector) |
| 202 | ToRegular: undo NF area flattening and restore area-proportional signal before redistribution. | display text (VM property / projector) |
| 203 | ToRegular: disabled for baseline comparison. | display text (VM property / projector) |
| 206 | ON - restore area signal | display text (VM property / projector) |
| 207 | OFF - baseline | display text (VM property / projector) |
| 210 | ToRegular ON | display text (VM property / projector) |
| 211 | ToRegular OFF | display text (VM property / projector) |
| 214 | ToFull: boundary support/cap/allowance only; target amount remains area-proportional. | display text (VM property / projector) |
| 215 | ToFull: disabled for baseline comparison. | display text (VM property / projector) |
| 218 | ON - support/cap only | display text (VM property / projector) |
| 219 | OFF - no boundary support | display text (VM property / projector) |
| 222 | ToFull ON | display text (VM property / projector) |
| 223 | ToFull OFF | display text (VM property / projector) |
| 226 | Boundary cap: virtual ToFull area <= inside overlap x {BoundaryVirtualAreaCapPercent:0.#}%. | display text (VM property / projector) |
| 227 | Boundary cap: OFF; Stage3 uses raw ToFull reachable area. | display text (VM property / projector) |
| 230 | Boundary cap {BoundaryVirtualAreaCapPercent:0.#}% | display text (VM property / projector) |
| 231 | Boundary cap OFF | display text (VM property / projector) |
| 239 | Target guard {TargetCoverageCapPercent:0.#}% | display text (VM property / projector) |
| 240 | Target guard OFF | display text (VM property / projector) |
| 258 | Hide ToFull details | display text (VM property / projector) |
| 259 | Show ToFull details | display text (VM property / projector) |
| 268 | No export type selected | display text (VM property / projector) |
| 272 | No export profile selected | display text (VM property / projector) |
| 277 | Review window selection | display text (VM property / projector) |
| 304 | Active | display text (VM property / projector) |
| 304 | Hidden | display text (VM property / projector) |
| 306 | Active | display text (VM property / projector) |
| 306 | Hidden | display text (VM property / projector) |
| 308 | Active | display text (VM property / projector) |
| 308 | Hidden | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.State.SizingAndPitch.cs`  -  Workspace header / right panel state text

| line | text | kind |
|---:|---|---|
| 250 | Pitch X | display text (VM property / projector) |
| 255 | Pitch Y | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.Undo.cs`  -  Undo (status chip)

| line | text | kind |
|---:|---|---|
| 76 | Undo: {entry.Description} | status chip |

### `ViewModels/FreeformHelperViewModel.UndoHooks.cs`  -  Undo entry names (status chip "Undo: ...")

| line | text | kind |
|---:|---|---|
| 14 | Toggle CAD layer | display text (VM property / projector) |
| 15 | Toggle regular grid | display text (VM property / projector) |
| 16 | Toggle unmatched highlight | display text (VM property / projector) |
| 17 | Toggle freeform highlight | display text (VM property / projector) |
| 18 | Toggle CAD area colors | display text (VM property / projector) |
| 19 | Toggle CAD Output FW Diff overlay | display text (VM property / projector) |
| 20 | Toggle To Regular labels | display text (VM property / projector) |
| 23 | Import closed polylines | display text (VM property / projector) |
| 24 | Import block polylines | display text (VM property / projector) |
| 27 | CAD line width | display text (VM property / projector) |
| 28 | Regular line width | display text (VM property / projector) |
| 29 | Highlight line width | display text (VM property / projector) |
| 30 | CAD line color | display text (VM property / projector) |
| 31 | Regular line color | display text (VM property / projector) |
| 32 | Regular selection color | display text (VM property / projector) |
| 33 | Regular selection fill opacity | display text (VM property / projector) |
| 36 | Area bucket tolerance | display text (VM property / projector) |
| 37 | Max area buckets | display text (VM property / projector) |
| 40 | X channels | display text (VM property / projector) |
| 41 | Y channels | display text (VM property / projector) |
| 42 | Cascade count | display text (VM property / projector) |
| 43 | Scan order | display text (VM property / projector) |
| 44 | Alignment mode | display text (VM property / projector) |
| 45 | Grid padding | display text (VM property / projector) |
| 46 | AA width | display text (VM property / projector) |
| 47 | AA height | display text (VM property / projector) |
| 50 | Match threshold | display text (VM property / projector) |
| 51 | Freeform TH | display text (VM property / projector) |
| 52 | Auto XY detect | display text (VM property / projector) |
| 53 | Edge freeform | display text (VM property / projector) |
| 54 | CAD Output FW Diff start | display text (VM property / projector) |
| 55 | CAD Output FW Diff auto mode | display text (VM property / projector) |
| 56 | CAD Output FW Diff anchor | display text (VM property / projector) |
| 59 | Export v2.1 | display text (VM property / projector) |
| 60 | Export v2.2 | display text (VM property / projector) |
| 61 | Compensation model | display text (VM property / projector) |
| 62 | To Regular | display text (VM property / projector) |
| 63 | To Full | display text (VM property / projector) |
| 64 | To Full owner TH | display text (VM property / projector) |
| 66 | Null value | display text (VM property / projector) |
| 67 | Notch gate v2.1 | display text (VM property / projector) |
| 68 | Notch gate TH | display text (VM property / projector) |
| 69 | Link notch TH | display text (VM property / projector) |
| 72 | Manual width | display text (VM property / projector) |
| 73 | Manual height | display text (VM property / projector) |
| 74 | Manual rows range | display text (VM property / projector) |
| 75 | Manual cols range | display text (VM property / projector) |

### `ViewModels/FreeformHelperViewModel.WorkflowSteps.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 9 | Notch export: not run. | display text (VM property / projector) |
| 180 | Step 1 result cleared. | status chip |
| 185 | Step 2 result cleared. | status chip |
| 190 | Step 3 result cleared. | status chip |
| 195 | Step 4 result cleared. | status chip |
| 200 | Step 5 result cleared. | status chip |

### `ViewModels/FreeformHelperViewModel.WorkspaceHeader.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 20 | CAD layer | display text (VM property / projector) |
| 21 | Regular grid | display text (VM property / projector) |
| 22 | Highlight unmatched regular | display text (VM property / projector) |
| 23 | Highlight freeform regular | display text (VM property / projector) |
| 24 | Color CAD by area | display text (VM property / projector) |

### `ViewModels/FwDiffAssignmentSettings.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 30 | CAD Output FW Diff assignment (per-IC): auto, scan-order based. | display text (VM property / projector) |

### `ViewModels/IndexMappingCandidateViewModel.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 21 | #{Rank} diff{DiffIndex} (IC{IcIndex}, regId {RegularPadIndex}), score={Score:0.###} | display text (VM property / projector) |

### `ViewModels/IndexMappingDecisionRowViewModel.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 160 | IC {IcIndex}, row {RowIndex}, segment {SegmentIndex} | display text (VM property / projector) |
| 161 | IC/row/segment: n/a | display text (VM property / projector) |
| 163 | Segment scope: {SegmentKey}, members={SegmentMemberCount} | display text (VM property / projector) |
| 164 | Segment scope: n/a | display text (VM property / projector) |
| 173 | SIM | display text (VM property / projector) |
| 187 | reason={DecisionReasonCodeText}, source={DecisionSourceText}, mode={DecisionModeText}, confidence={(DecisionConfidence.HasValue ? DecisionConfidence.Value.ToString( | display text (VM property / projector) |
| 189 | Diff preview: {CurrentDisplay} -> diff {diff} (passive={(PassiveCompensationDiffIndex.HasValue ?  | display text (VM property / projector) |
| 191 | Diff preview: no repair suggestion, passive compensation diff {passiveDiff}. | display text (VM property / projector) |
| 192 | Diff preview: no repair suggestion. | display text (VM property / projector) |
| 234 | Override: none. | display text (VM property / projector) |
| 260 | CAD {cadPadId} | display text (VM property / projector) |
| 262 | REG {regularPadIndex} | display text (VM property / projector) |
| 266 | Visible CAD/Regular counts diverge. | display text (VM property / projector) |
| 267 | CAD has no regular match. | display text (VM property / projector) |
| 267 | Current diff idx {unmappedCadDiff} has no regular match. | display text (VM property / projector) |
| 268 | REG {unmappedReg} / diff {unmappedRegDiff} has no CAD. | display text (VM property / projector) |
| 268 | Regular pad has no CAD. | display text (VM property / projector) |
| 269 | Assigned diff is below confidence threshold. | display text (VM property / projector) |
| 269 | Assigned diff {assignedDiff} is below confidence threshold. | display text (VM property / projector) |
| 270 | Assigned diff is close to another candidate. | display text (VM property / projector) |
| 270 | Assigned diff {ambiguousDiff} is close to another candidate. | display text (VM property / projector) |
| 271 | Current diff idx is assigned more than once. | display text (VM property / projector) |
| 271 | Current diff idx {duplicateDiff} is assigned more than once. | display text (VM property / projector) |
| 285 | diff {currentDiff} | display text (VM property / projector) |
| 286 | diff {rawDiff} | display text (VM property / projector) |
| 286 | diff {topCandidate.DiffIndex} | display text (VM property / projector) |
| 287 | diff {suggestedDiff} | display text (VM property / projector) |
| 287 | diff {topCandidate.DiffIndex} | display text (VM property / projector) |
| 292 | Raw geometry seed: REG {issueReg} / diff {issueDiff} | display text (VM property / projector) |
| 293 | Raw geometry seed: none | display text (VM property / projector) |
| 294 | Masked geometry seed: not available for this diagnostics row. | display text (VM property / projector) |
| 296 | Suggested: REG {topCandidate.RegularPadIndex} / diff {topCandidate.DiffIndex} | display text (VM property / projector) |
| 298 | Suggested: REG {reg} / diff {diff} | display text (VM property / projector) |
| 299 | Suggested: none | display text (VM property / projector) |
| 301 | Offset support: not instrumented for issue rows. | display text (VM property / projector) |
| 336 | #{index + 1} diff{candidate.DiffIndex} (IC{candidate.IcIndex}, regId {candidate.RegularPadIndex}), regular={candidate.RegularCoverage:P1}, cad={candidate.CadCoverage:P1} | display text (VM property / projector) |
| 343 | G#{index + 1} diff{candidate.DiffIndex} (IC{candidate.IcIndex}, regId {candidate.RegularPadIndex}), regular={candidate.RegularCoverage:P1}, cad={candidate.CadCoverage:P1} | display text (VM property / projector) |
| 352 | CAD {row.CadPadId} | display text (VM property / projector) |
| 354 | Primary diff idx {currentDiff}. | display text (VM property / projector) |
| 355 | Primary diff idx none. | display text (VM property / projector) |
| 356 | diff {current} | display text (VM property / projector) |
| 357 | diff {rawDiff} | display text (VM property / projector) |
| 359 | diff {repairedDiff} | display text (VM property / projector) |
| 361 | diff {suggestedDiff} | display text (VM property / projector) |
| 363 | diff {maskedDiff} | display text (VM property / projector) |
| 368 | Action: inspect CSV-active candidates before overriding Step 4. | display text (VM property / projector) |
| 369 | Action: verify whether masked geometry seed should become the CAD Output FW Diff. | display text (VM property / projector) |
| 408 | Override: CAD -> regular id {index}. | display text (VM property / projector) |
| 412 | Override: none. | display text (VM property / projector) |

### `ViewModels/IndexMappingMaskAuditRowViewModel.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 83 | Changed | display text (VM property / projector) |
| 84 | Removed | display text (VM property / projector) |
| 85 | No raw geometry seed | display text (VM property / projector) |
| 86 | Unchanged | display text (VM property / projector) |
| 90 | Coverage: regular={regularCoverage:P1}, cad={cadCoverage:P1} | display text (VM property / projector) |
| 96 | Segment offset: {offset:+#;-#;0}, support={OffsetSupportCount}, ratio={OffsetSupportRatio.GetValueOrDefault():P1}, confidence={SegmentConfidence.GetValueOrDefault():P1} | display text (VM property / projector) |
| 97 | Segment offset: none | display text (VM property / projector) |
| 100 | IC {icIndex}, row {rowIndex}, segment {segmentIndex} | display text (VM property / projector) |
| 101 | IC/row/segment: n/a | display text (VM property / projector) |
| 103 | reason={ReasonCodeText}, source={DecisionSourceText}, mode={ModeText}, repairCandidates={RepairCandidateCount}, segment={SegmentKeyText}, size={SegmentMemberCount}, offset={DetectedOffset?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 105 | Raw geometry seed: REG {rawReg} / diff {rawDiff} | display text (VM property / projector) |
| 106 | Raw geometry seed: none | display text (VM property / projector) |
| 108 | Masked geometry seed: REG {maskedReg} / diff {maskedDiff} | display text (VM property / projector) |
| 109 | Masked geometry seed: none | display text (VM property / projector) |
| 111 | Suggested: REG {suggestedReg} / diff {suggestedDiff} | display text (VM property / projector) |
| 113 | Suggested diff idx: {diff} | display text (VM property / projector) |
| 114 | Suggested: none | display text (VM property / projector) |

### `ViewModels/IndexMappingReportIssueViewModel.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 15 | Count mismatch | display text (VM property / projector) |
| 16 | Unmapped CAD | display text (VM property / projector) |
| 17 | Unmapped Regular | display text (VM property / projector) |
| 18 | Low confidence | display text (VM property / projector) |
| 19 | Ambiguous | display text (VM property / projector) |
| 20 | Duplicate diff | display text (VM property / projector) |
| 26 | Action: verify layer filters, grid channels, and bounds layer. | display text (VM property / projector) |
| 27 | Action: increase candidate number, check bounds, and confirm CAD visibility. | display text (VM property / projector) |
| 28 | Action: confirm bounds/padding and layer filters; review grid build inputs. | display text (VM property / projector) |
| 29 | Action: inspect geometry, tune weights/threshold, or add an override. | display text (VM property / projector) |
| 30 | Action: inspect best/second candidates, tune weights, or add an override. | display text (VM property / projector) |
| 31 | Action: inspect the duplicated current diff idx, then confirm whether one CAD should move to its raw or masked geometry seed. | display text (VM property / projector) |

### `ViewModels/IndexMappingReportViewModel.Actions.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 32 | Segment preview: select a row. | display text (VM property / projector) |
| 37 | Segment preview: selected row has no segment metadata. | display text (VM property / projector) |
| 42 | Segment preview: {SelectedDecision.SegmentKey}, repair rows {repairRows.Count}/{memberCount}. | display text (VM property / projector) |
| 63 | Select a row first. | display text (VM property / projector) |
| 69 | Selected row has no locate target. | display text (VM property / projector) |
| 75 | Locate handler not available. | display text (VM property / projector) |
| 80 | Diagnostic row focused on canvas. | display text (VM property / projector) |
| 80 | Diagnostic target is not visible. | display text (VM property / projector) |
| 87 | Select a row first. | display text (VM property / projector) |
| 93 | Selected row cannot create an override. | display text (VM property / projector) |
| 99 | Override handler not available. | display text (VM property / projector) |
| 108 | Mapping override applied. | display text (VM property / projector) |
| 112 | Failed to apply mapping override. | display text (VM property / projector) |
| 120 | Select a row first. | display text (VM property / projector) |
| 128 | Selected row has no diff repair suggestion. | display text (VM property / projector) |
| 134 | Diff override handler not available. | display text (VM property / projector) |
| 140 | Diff repair override applied. | display text (VM property / projector) |
| 141 | Failed to apply diff repair override. | display text (VM property / projector) |
| 148 | Select a row first. | display text (VM property / projector) |
| 154 | Selected row has no segment metadata. | display text (VM property / projector) |
| 160 | Diff override handler not available. | display text (VM property / projector) |
| 167 | Selected segment has no diff repair suggestions. | display text (VM property / projector) |
| 185 | Segment diff repair applied for {appliedCount}/{segmentRows.Count} rows. | display text (VM property / projector) |
| 192 | Diff override handler not available. | display text (VM property / projector) |
| 199 | No visible rows have diff repair suggestions. | display text (VM property / projector) |
| 217 | Visible diff repairs applied for {appliedCount}/{visibleRows.Count} rows. | display text (VM property / projector) |
| 224 | Select a row first. | display text (VM property / projector) |
| 230 | Selected row has no CAD override to clear. | display text (VM property / projector) |
| 236 | Override clear handler not available. | display text (VM property / projector) |
| 245 | Mapping override cleared. | display text (VM property / projector) |
| 249 | Failed to clear mapping override. | display text (VM property / projector) |

### `ViewModels/IndexMappingReportViewModel.AlgorithmTrace.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 12 | Trace: not requested. | status chip |
| 28 | Trace: aggregate row (select CAD/REG entity). | status chip |
| 37 | Trace: ready ({cached.Count.ToString(CultureInfo.InvariantCulture)} entries). | status chip |
| 54 | Trace: loading... | status chip |
| 62 | Trace: stale (selection changed). | status chip |
| 70 | Trace: stale (selection changed). | status chip |
| 76 | Trace: ready ({trace.Count.ToString(CultureInfo.InvariantCulture)} entries). | status chip |
| 81 | Trace: failed ({ex.GetType().Name}). | status chip |

### `ViewModels/IndexMappingReportViewModel.Selection.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 93 | entity: none | display text (VM property / projector) |
| 98 | aggregate: {SelectedDecision.CategoryText} | display text (VM property / projector) |
| 103 | CAD {cadId} | display text (VM property / projector) |
| 108 | REG {regularPadId} | display text (VM property / projector) |
| 111 | entity: aggregate diagnostics row | display text (VM property / projector) |
| 118 | journey: select a CAD/REG row to inspect the full step path. | display text (VM property / projector) |
| 123 | journey: aggregate diagnostics row. Select a CAD/REG entity row to inspect step-by-step attribution. | display text (VM property / projector) |
| 126 | journey: raw {SelectedDecision.RawDisplay} -> assigned {SelectedDecision.CurrentDisplay} -> suggested {SelectedDecision.SuggestedDisplay} ({SelectedDecision.DecisionReasonCodeText}/{SelectedDecision.DecisionSourceText}) | display text (VM property / projector) |
| 133 | trace direction: n/a | display text (VM property / projector) |
| 138 | trace direction: duplicate-conflict path (needs review) | display text (VM property / projector) |
| 139 | trace direction: direct/in/out path coherent | display text (VM property / projector) |

### `ViewModels/IndexMappingReportViewModel.Verification.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 76 | Notch input readiness | display text (VM property / projector) |
| 76 | Step 3 | display text (VM property / projector) |
| 77 | Diff assignment decision | display text (VM property / projector) |
| 77 | Step 4 | display text (VM property / projector) |
| 78 | Notch table row integrity | display text (VM property / projector) |
| 78 | Step 5 | display text (VM property / projector) |
| 79 | Step 6 | display text (VM property / projector) |
| 79 | Validation trace consistency | display text (VM property / projector) |
| 80 | Before/After/Delta sanity | display text (VM property / projector) |
| 80 | Simulation | display text (VM property / projector) |
| 140 | rows={TotalDecisionCount} | display text (VM property / projector) |
| 144 | entity={SelectedEntityText}, segment={SelectedDecision.SegmentKey}, members={SelectedDecision.SegmentMemberCount} | display text (VM property / projector) |
| 145 | entity={SelectedEntityText}, segment=n/a | display text (VM property / projector) |
| 152 | changed={ChangedByMaskDecisionCount}, lowConf={LowConfidenceDecisionCount} | display text (VM property / projector) |
| 155 | assigned={SelectedDecision.CurrentDisplay}, suggested={SelectedDecision.SuggestedDisplay}, reason={SelectedDecision.DecisionReasonCodeText} | display text (VM property / projector) |
| 162 | mismatch={CountMismatchDecisionCount}, unmapped={UnmappedDecisionCount} | display text (VM property / projector) |
| 165 | row={SelectedDecision.RowIndex?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 172 | duplicate={DuplicateDiffDecisionCount} | display text (VM property / projector) |
| 175 | trace={SelectedDecision.CandidateLines.Count.ToString(CultureInfo.InvariantCulture)}, direction={BuildStep6TraceDirectionText()} | display text (VM property / projector) |
| 182 | maskChanged={ChangedByMaskDecisionCount}, maskRemoved={RemovedByMaskDecisionCount} | display text (VM property / projector) |
| 185 | entity={SelectedEntityText}, impact={SelectedDecision.SuggestedDisplay} | display text (VM property / projector) |
| 191 | Confirm input rows are coherent before table export. | dialog text |
| 192 | Confirm {SelectedEntityText} can enter notch table generation. | dialog text |
| 199 | Review reason code and seed/mask decision source. | display text (VM property / projector) |
| 204 | Apply diff repair for {SelectedEntityText}. | display text (VM property / projector) |
| 209 | Apply regular override for {SelectedEntityText}. | display text (VM property / projector) |
| 212 | Review decision source for {SelectedEntityText}. | display text (VM property / projector) |
| 219 | Check final notch row payload before export. | display text (VM property / projector) |
| 223 | Apply segment repairs around {SelectedDecision.SegmentKey}. | display text (VM property / projector) |
| 224 | Check row payload fields for {SelectedEntityText}. | display text (VM property / projector) |
| 230 | Validate trace direction and resolve duplicate conflicts. | display text (VM property / projector) |
| 231 | Locate {SelectedEntityText} on AA and verify trace direction. | display text (VM property / projector) |
| 237 | Replay notch impact using the same exported table contract. | display text (VM property / projector) |
| 238 | Replay simulation for {SelectedEntityText} and compare before/after. | display text (VM property / projector) |
| 246 | rows={TotalDecisionCount}, filter={SelectedFilterLabel} | display text (VM property / projector) |
| 283 | openIssues={OpenIssueCount}, countMismatch={CountMismatchDecisionCount} | display text (VM property / projector) |
| 284 | Diff preview: n/a. | display text (VM property / projector) |
| 287 | visible rows {VisibleDecisionCount}/{TotalDecisionCount} | display text (VM property / projector) |
| 288 | duplicate rows={DuplicateDiffDecisionCount}, selected={SelectedEntityText} | display text (VM property / projector) |

### `ViewModels/IndexMappingReportViewModel.cs`  -  Verification Workspace window

| line | text | kind |
|---:|---|---|
| 89 | No SeeRegular-based CSV confirmation data. | display text (VM property / projector) |
| 90 | Changed by mask: {MaskChangedCount}, removed by mask: {MaskRemovedCount}. | display text (VM property / projector) |
| 106 | Showing {VisibleDecisionCount} entity rows under {SelectedFilterLabel}. | display text (VM property / projector) |
| 108 | No entity rows under {SelectedFilterLabel}. Aggregate diagnostics: {VisibleAggregateDecisionCount}. | display text (VM property / projector) |
| 109 | No rows under {SelectedFilterLabel}. | display text (VM property / projector) |
| 111 | Aggregate diagnostics ({VisibleAggregateDecisionCount}) | display text (VM property / projector) |
| 112 | Aggregate diagnostics | display text (VM property / projector) |
| 114 | Aggregate signals (for example count mismatch) are listed separately and do not block entity review. | display text (VM property / projector) |
| 115 | No aggregate diagnostics. | display text (VM property / projector) |
| 128 | Step 3 | display text (VM property / projector) |
| 129 | Step 4 | display text (VM property / projector) |
| 130 | Step 5 | display text (VM property / projector) |
| 131 | Step 6 | display text (VM property / projector) |
| 132 | Simulation | display text (VM property / projector) |
| 134 | rev {TableRevision.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 137 | Changed by mask | display text (VM property / projector) |
| 138 | Removed by mask | display text (VM property / projector) |
| 139 | Ambiguous | display text (VM property / projector) |
| 140 | Duplicate diff | display text (VM property / projector) |
| 141 | Low confidence | display text (VM property / projector) |
| 142 | Unmapped | display text (VM property / projector) |
| 143 | Count mismatch | display text (VM property / projector) |
| 144 | all diagnostics | display text (VM property / projector) |
| 146 | Open issues {OpenIssueCount} · Active {ActiveVerificationStepText} · {TableRevisionText} | display text (VM property / projector) |
| 158 | status: {GetActiveStepNode()?.StatusText ?? OverallStatusText} | status chip |
| 164 | No row selected. | display text (VM property / projector) |
| 165 | Select a row in the verification table. | display text (VM property / projector) |
| 168 | reasonCode: {SelectedDecision?.DecisionReasonCodeText ??  | display text (VM property / projector) |
| 169 | decisionSource: {SelectedDecision?.DecisionSourceText ??  | display text (VM property / projector) |
| 170 | Raw geometry seed: n/a | display text (VM property / projector) |
| 171 | Masked geometry seed: n/a | display text (VM property / projector) |
| 172 | primaryAssigned: {SelectedDecision?.CurrentDisplay ??  | display text (VM property / projector) |
| 174 | passiveCompensationDiff: diff {passiveDiff} | display text (VM property / projector) |
| 175 | passiveCompensationDiff: none | display text (VM property / projector) |
| 177 | repairSuggestion: diff {repairDiff} | display text (VM property / projector) |
| 178 | repairSuggestion: none | display text (VM property / projector) |
| 180 | confidence: {confidence.ToString( | display text (VM property / projector) |
| 181 | confidence: n/a | display text (VM property / projector) |
| 182 | Offset support: n/a | display text (VM property / projector) |
| 183 | candidate attribution ({Step4CandidateAttributionLines.Count.ToString(CultureInfo.InvariantCulture)}) | dialog/file-dialog text |
| 185 | row: {(SelectedDecision?.RowIndex?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 186 | IC: {(SelectedDecision?.IcIndex?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 187 | regular: {(SelectedDecision?.LocateRegularPadIndex?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 188 | payload: n/a | display text (VM property / projector) |
| 189 | status: {SelectedDecision?.StatusDisplay ??  | status chip |
| 190 | validationRegularPadId: {(SelectedDecision?.LocateRegularPadIndex?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 191 | traceCount: {SelectedDecision?.CandidateLines.Count.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 193 | views: Before / After / Delta / Changed only ({ActiveVerificationStepText}) | display text (VM property / projector) |
| 194 | color mode: AUTO / TH ({SelectedFilterLabel}) | display text (VM property / projector) |
| 196 | impact: select a row to inspect linked regular/notch rows. | display text (VM property / projector) |
| 197 | impact: linked CAD {SelectedDecision.CadPadId?.ToString(CultureInfo.InvariantCulture) ??  | display text (VM property / projector) |
| 198 | scope: {SelectedFilterLabel}, visible rows {VisibleDecisionCount}/{TotalDecisionCount} | display text (VM property / projector) |
| 199 | Step sync off | display text (VM property / projector) |
| 199 | Step sync on | display text (VM property / projector) |
| 215 | Search key / reason / source / diff | display text (VM property / projector) |
| 221 | Trace: not requested. | status chip |

### `ViewModels/LayerSourceSelection.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 27 | Auto (visible layers) | display text (VM property / projector) |
| 28 | Hidden bounds layer is still valid. | display text (VM property / projector) |
| 29 | Select layer | display text (VM property / projector) |

### `ViewModels/ManualSizingInput.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 39 | No manual range. | display text (VM property / projector) |

### `ViewModels/MappingDiagnosticsResult.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 23 | Diagnostics: not run. | display text (VM property / projector) |

### `ViewModels/NotchDetailViewModel.cs`  -  Notch detail window

| line | text | kind |
|---:|---|---|
| 52 | CAD id {CadPad.Id} | dialog/file-dialog text |
| 52 | CAD idx {DxfIndex.Value} (CAD id {CadPad.Id}) | dialog/file-dialog text |
| 53 | Layer: - | display text (VM property / projector) |
| 53 | Layer: {CadPad.Layer} | display text (VM property / projector) |
| 54 | Freeform: None | display text (VM property / projector) |
| 54 | Freeform: {FreeformType} | display text (VM property / projector) |
| 78 | IC{IcIndex} R{Row} C{Col} | display text (VM property / projector) |

### `ViewModels/NotchExportColumnFilterDialogViewModel.cs`  -  Export Notch Rows > column filter dialog

| line | text | kind |
|---:|---|---|
| 33 | Column filter | display text (VM property / projector) |
| 63 | 0/0 selected | display text (VM property / projector) |
| 126 | Range: {fromText} ~ {toText} | display text (VM property / projector) |

### `ViewModels/NotchExportFileTypeMetadata.cs`  -  Export type dropdown / file dialog

| line | text | kind |
|---:|---|---|
| 32 | CSV review | display text (VM property / projector) |
| 35 | Unknown | display text (VM property / projector) |

### `ViewModels/NotchExportSelectionViewModel.Models.cs`  -  Export Notch Rows window

| line | text | kind |
|---:|---|---|
| 81 | IC {IcIndex + 1} | display text (VM property / projector) |
| 111 | IC {Row.IcIndex + 1} | display text (VM property / projector) |
| 121 | REG {RegularText} -> CAD {CadText} | display text (VM property / projector) |
| 122 | REG {RegularText} -> CAD - | display text (VM property / projector) |
| 133 | REG {RegularText} -> CAD {CadText} | display text (VM property / projector) |
| 137 | REG {RegularText} -> CAD {CadText} | display text (VM property / projector) |
| 153 | Legacy payload: use Values row for detail. | display text (VM property / projector) |
| 156 | Anchor diff {node.AnchorDiffIndex}, combine {node.CombinePercent}%,  | display text (VM property / projector) |
| 157 | target1 diff {node.TargetDiffIndex1} ({node.TargetRatioPercent1}%),  | display text (VM property / projector) |
| 158 | target2 diff {node.TargetDiffIndex2} ({node.TargetRatioPercent2}%). | display text (VM property / projector) |
| 165 | No-op: keep anchor diff only (no target switch). | display text (VM property / projector) |
| 170 | Legacy row: interpreted by legacy firmware path. | display text (VM property / projector) |
| 183 | NONE | display text (VM property / projector) |
| 193 | anchor_diff={node.AnchorDiffIndex}, combine={node.CombinePercent},  | display text (VM property / projector) |
| 194 | target1_diff={FormatDiff(node.TargetDiffIndex1)}, target1_ratio={node.TargetRatioPercent1},  | display text (VM property / projector) |
| 195 | target2_diff={FormatDiff(node.TargetDiffIndex2)}, target2_ratio={node.TargetRatioPercent2}, flags={node.Flags} | display text (VM property / projector) |
| 257 | No CAD mapping | display text (VM property / projector) |
| 262 | Linked, but payload is no-op | display text (VM property / projector) |
| 267 | Legacy mapping | display text (VM property / projector) |
| 270 | Linked mapping | display text (VM property / projector) |
| 291 | No CAD | display text (VM property / projector) |
| 296 | Legacy | display text (VM property / projector) |
| 301 | Linked | display text (VM property / projector) |
| 346 | Anchor {node.AnchorDiffIndex}, combine {node.CombinePercent}%,  | display text (VM property / projector) |
| 347 | target1 {FormatDiff(node.TargetDiffIndex1)} ({node.TargetRatioPercent1}%),  | display text (VM property / projector) |
| 348 | target2 {FormatDiff(node.TargetDiffIndex2)} ({node.TargetRatioPercent2}%). | display text (VM property / projector) |
| 365 | Diff {DiffText} | display text (VM property / projector) |
| 366 | REG {RegularText} | display text (VM property / projector) |
| 367 | CAD {CadText} | display text (VM property / projector) |

### `ViewModels/NotchExportSelectionViewModel.cs`  -  Export Notch Rows window

| line | text | kind |
|---:|---|---|
| 36 | Mode: Transfer-only | display text (VM property / projector) |
| 38 | Selection: Selected only | display text (VM property / projector) |
| 40 | Header: None | display text (VM property / projector) |
| 43 | None | display text (VM property / projector) |
| 44 | Active filter: text=* \| mode=Transfer-only \| selected=all \| header=None \| shown=0. | display text (VM property / projector) |
| 45 | Showing 0 rows | display text (VM property / projector) |
| 56 | Selected 0/0 rows · shown 0. | display text (VM property / projector) |
| 57 | v2.2 0 · warning 0 · legacy 0 · CAD linked 0 · no CAD 0 | display text (VM property / projector) |
| 58 | Export selected | display text (VM property / projector) |
| 69 | Need full AA view? Hide panel, inspect in workspace, then press Ctrl+Shift+E to restore. | display text (VM property / projector) |
| 71 | Review, filter, inspect, and export notch mappings. | display text (VM property / projector) |
| 72 | Search by row / diff / mapping / CAD... | display text (VM property / projector) |
| 90 | All | display text (VM property / projector) |
| 91 | Linked | display text (VM property / projector) |
| 92 | Warning | display text (VM property / projector) |
| 93 | No CAD | display text (VM property / projector) |
| 94 | Legacy | display text (VM property / projector) |
| 99 | Row ID | display text (VM property / projector) |
| 100 | Diff ↑ | display text (VM property / projector) |
| 101 | Match ↓ | display text (VM property / projector) |
| 102 | IC / Diff | display text (VM property / projector) |
| 233 | No row selected. | display text (VM property / projector) |
| 250 | Row {visibleIndex + 1}/{Rows.Count} | display text (VM property / projector) |
| 371 | ToFull coverage n/a | display text (VM property / projector) |
| 375 | ToFull coverage missing {audit.MissingTargetDiffCount}/{audit.ExpectedTargetDiffCount} | display text (VM property / projector) |
| 376 | ToFull coverage ok {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount} | display text (VM property / projector) |
| 383 | No ToFull-driven target diff was emitted in this table. | display text (VM property / projector) |
| 387 | Detected {audit.MissingTargetDiffCount} missing ToFull target diff(s) across {audit.BucketCount} source bucket(s). | display text (VM property / projector) |
| 388 | All {audit.ExpectedTargetDiffCount} ToFull target diff(s) are covered across {audit.BucketCount} source bucket(s). | display text (VM property / projector) |
| 395 | Audit skipped because no canonical source bucket carried a ToFull-only target leg. | display text (VM property / projector) |
| 398 | Expected target diffs={audit.ExpectedTargetDiffCount} \| covered={audit.CoveredTargetDiffCount} \| missing={audit.MissingTargetDiffCount}. | display text (VM property / projector) |
| 405 | No missing ToFull target diff. | display text (VM property / projector) |
| 484 | All versions | display text (VM property / projector) |

### `ViewModels/NotchValidationDisplayState.cs`  -  Right panel > Settings > Step 6 validation

| line | text | kind |
|---:|---|---|
| 19 | Validation: select REG, then run Analyze. | display text (VM property / projector) |

### `ViewModels/PadInfo/CadPadInfoViewModel.Construction.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 66 | CAD pad | dialog/file-dialog text |
| 66 | CAD pads | dialog/file-dialog text |
| 67 | Selected: {SelectedCount} pads | display text (VM property / projector) |
| 67 | Single pad | display text (VM property / projector) |
| 87 | CAD pads · {Subtitle} | display text (VM property / projector) |
| 88 | CAD {pads[0].Id} · Diff idx {DxfIndexText} | display text (VM property / projector) |
| 91 | Notch rows: - | display text (VM property / projector) |
| 92 | Undo NF (To Regular): - | display text (VM property / projector) |
| 93 | To Full: - | display text (VM property / projector) |
| 94 | Stage3 area: - | display text (VM property / projector) |
| 95 | Targets: - | display text (VM property / projector) |
| 104 | Unmatched | display text (VM property / projector) |
| 115 | Mixed | display text (VM property / projector) |
| 117 | Mixed | display text (VM property / projector) |
| 118 | Mixed | display text (VM property / projector) |
| 119 | Mixed | display text (VM property / projector) |
| 146 | CAD {cadPadId} · IC {icText} · Diff idx {DxfIndexText} | display text (VM property / projector) |
| 201 | Notch rows: unknown (open from selection inspector) | display text (VM property / projector) |

### `ViewModels/PadInfo/CadPadInfoViewModel.Core.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 71 | Stage3 area: | display text (VM property / projector) |
| 85 | Notch rows: | display text (VM property / projector) |
| 98 | Hide details | display text (VM property / projector) |
| 98 | Show details | display text (VM property / projector) |
| 101 | Manual override | display text (VM property / projector) |
| 103 | Anchor saved (inactive in current mode) | display text (VM property / projector) |
| 105 | Auto (geometry seed) | display text (VM property / projector) |
| 106 | Auto (geometry seed, unique per IC) | display text (VM property / projector) |

### `ViewModels/PadInfo/CadPadInfoViewModel.Formatting.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 16 | Unmatched | display text (VM property / projector) |
| 41 | Unmatched | display text (VM property / projector) |
| 64 | IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} | display text (VM property / projector) |
| 65 | regId {regularPadId} | display text (VM property / projector) |
| 74 | IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} (regId {regularPadId}) | display text (VM property / projector) |
| 75 | regId {regularPadId} | display text (VM property / projector) |

### `ViewModels/PadInfo/CadPadInfoViewModel.NotchDiagnostics.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 60 | 1 reg | display text (VM property / projector) |
| 63 | CAD {ownerCadId} ({countText}) | display text (VM property / projector) |

### `ViewModels/PadInfo/RegularPadInfoViewModel.Commands.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 55 | Unmatched | display text (VM property / projector) |
| 70 | CAD idx {dxfListText} (CAD id {cadListText}) | display text (VM property / projector) |
| 73 | CAD id {cadListText} | display text (VM property / projector) |
| 83 | Unmatched | display text (VM property / projector) |
| 96 | idx {mappedIndex.Value} | display text (VM property / projector) |
| 97 | id {link.CadPadId} | display text (VM property / projector) |
| 98 | CAD {indexText}: {(link.CadCoverage * 100):0.#}% | display text (VM property / projector) |

### `ViewModels/PadInfo/RegularPadInfoViewModel.Core.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 128 | Hide details | display text (VM property / projector) |
| 128 | Show details | display text (VM property / projector) |
| 173 | Regular pad | dialog/file-dialog text |
| 173 | Regular pads | dialog/file-dialog text |
| 174 | Selected: {SelectedCount} pads | display text (VM property / projector) |
| 174 | Single pad | display text (VM property / projector) |
| 182 | Owners: - | display text (VM property / projector) |
| 183 | Notch rows: generate Step 5 first. | display text (VM property / projector) |
| 191 | Regular pads · {Subtitle} | display text (VM property / projector) |
| 205 | Mixed | display text (VM property / projector) |
| 206 | Mixed | display text (VM property / projector) |
| 227 | Row {RowText}, Col {ColText} | display text (VM property / projector) |
| 230 | REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · Diff idx {diffIndexText} | display text (VM property / projector) |
| 242 | Match {MatchText}, Freeform {FreeformText} | display text (VM property / projector) |
| 277 | Match {MatchText}, Freeform {FreeformText} | display text (VM property / projector) |
| 298 | CAD {owner.CadId} {owner.RatioPercent:0.##}% | display text (VM property / projector) |
| 312 | Notch rows: {relatedRows.Count} (REG {pad.RegularPadId}) | display text (VM property / projector) |
| 315 | CAD - | display text (VM property / projector) |
| 315 | CAD {row.CadPadId.Value} | display text (VM property / projector) |
| 316 | #{row.RowNumber} {row.Version.ToDisplayLabel()} IC{row.IcIndex + 1} diff{row.DiffIndex} {cadText} | display text (VM property / projector) |
| 322 | Row | display text (VM property / projector) |
| 323 | FW diff {row.DiffIndex} | display text (VM property / projector) |
| 324 | Step 5 | display text (VM property / projector) |
| 332 | Notch rows: 0 (REG {pad.RegularPadId}) | display text (VM property / projector) |
| 338 | Mixed | display text (VM property / projector) |
| 398 | CAD Output FW diff {outputDiff.Value} | display text (VM property / projector) |
| 399 | CAD Output FW diff - | display text (VM property / projector) |
| 400 | REG {link.RegularCoverage:P0} / CAD {link.CadCoverage:P0} | display text (VM property / projector) |
| 401 | CAD {link.CadPadId} overlap {link.OverlapArea:0.####} {PadInfoUnits.SquareMillimeter}; regular coverage {link.RegularCoverage:P2}; CAD coverage {link.CadCoverage:P2}. | display text (VM property / projector) |
| 407 | CAD {link.CadPadId} | display text (VM property / projector) |
| 408 | Overlap | display text (VM property / projector) |

### `ViewModels/PadInfo/RegularPadInfoViewModel.Formatting.cs`  -  Pad info popover

| line | text | kind |
|---:|---|---|
| 56 | X {bounds.MinX:0.###} - {bounds.MaxX:0.###}\\nY {bounds.MinY:0.###} - {bounds.MaxY:0.###} | display text (VM property / projector) |

### `ViewModels/PadInspectorDisplayLine.cs`  -  Pad info / right panel > Inspector

| line | text | kind |
|---:|---|---|
| 29 | Normal | display text (VM property / projector) |

### `ViewModels/PadInspectorSnapshot.cs`  -  Pad info / right panel > Inspector

| line | text | kind |
|---:|---|---|
| 101 | None | display text (VM property / projector) |
| 102 | No Step 2 freeform tag. | display text (VM property / projector) |

### `ViewModels/RegularVisibilityMaskUiState.cs`  -  Right panel > Step 1/4 mask summary

| line | text | kind |
|---:|---|---|
| 29 | Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 and Simulation. | display text (VM property / projector) |

### `ViewModels/SettingsWindowViewModel.Cascade.cs`  -  Settings window

| line | text | kind |
|---:|---|---|
| 13 | IC {CascadeIcSettings.Count} · X {XChannels:0} · Y {YChannels:0} | display text (VM property / projector) |
| 129 | IC {IcIndex} | display text (VM property / projector) |

### `ViewModels/SettingsWindowViewModel.cs`  -  Settings window

| line | text | kind |
|---:|---|---|
| 47 | Select layer | display text (VM property / projector) |
| 318 | To Full rule engine: ON (trace ON) | display text (VM property / projector) |
| 319 | To Full rule engine: ON (trace OFF) | display text (VM property / projector) |
| 320 | To Full rule engine: OFF (legacy gate path) | display text (VM property / projector) |
| 324 | General | display text (VM property / projector) |
| 325 | Step 1 - Geometry match | display text (VM property / projector) |
| 326 | Step 2 - Freeform tagging | display text (VM property / projector) |
| 327 | Step 3 - Notch compensation | display text (VM property / projector) |
| 328 | Step 4 - Index diagnostics | display text (VM property / projector) |
| 329 | Step 5 - Notch export | display text (VM property / projector) |
| 330 | Settings | display text (VM property / projector) |
| 335 | Project layout, DXF import, display, and generated regular grid settings. | display text (VM property / projector) |
| 336 | Overlap diagnostics and unmatched highlighting. | display text (VM property / projector) |
| 337 | Freeform classification and project-load replay behavior. | display text (VM property / projector) |
| 338 | Compensation model, allocation semantics, and preview diagnostics. | display text (VM property / projector) |
| 339 | FW output diff visibility and mapping confidence thresholds. | display text (VM property / projector) |
| 340 | Export versions, file type, gate thresholds, and handoff safety. | display text (VM property / projector) |
| 341 | Edit project settings. | display text (VM property / projector) |
| 354 | Allocation model: Stage3 effective-area gain; ToFull-expanded area splits the combined source signal. | display text (VM property / projector) |
| 355 | Allocation model: SourceArea-dominant; ToFull is support/cap, not direct full-area weight. | display text (VM property / projector) |
| 356 | Allocation model: disabled baseline. | display text (VM property / projector) |
| 363 | Stage3 gain allocation | display text (VM property / projector) |
| 364 | Source-area dominant | display text (VM property / projector) |
| 365 | Disabled baseline | display text (VM property / projector) |
| 370 | ToRegular: undo NF area flattening and restore area-proportional signal before redistribution. | display text (VM property / projector) |
| 371 | ToRegular: disabled for baseline comparison. | display text (VM property / projector) |
| 374 | ON - restore area signal | display text (VM property / projector) |
| 375 | OFF - baseline | display text (VM property / projector) |
| 378 | ToFull: boundary support/cap/allowance only; target amount remains area-proportional. | display text (VM property / projector) |
| 379 | ToFull: disabled for baseline comparison. | display text (VM property / projector) |
| 382 | ON - support/cap only | display text (VM property / projector) |
| 383 | OFF - no boundary support | display text (VM property / projector) |
| 386 | Boundary cap: virtual ToFull area <= inside overlap x {BoundaryVirtualAreaCapPercent:0.#}%. | display text (VM property / projector) |
| 387 | Boundary cap: OFF; Stage3 uses raw ToFull reachable area. | display text (VM property / projector) |
| 390 | Boundary cap {BoundaryVirtualAreaCapPercent:0.#}% | display text (VM property / projector) |
| 391 | Boundary cap OFF | display text (VM property / projector) |
| 399 | Target guard {TargetCoverageCapPercent:0.#}% | display text (VM property / projector) |
| 400 | Target guard OFF | display text (VM property / projector) |

### `ViewModels/ShellViewModel.Workspaces.cs`  -  Shell / tab switching

| line | text | kind |
|---:|---|---|
| 94 | Opening simulation workspace... | display text (VM property / projector) |

### `ViewModels/SimulationHostViewModel.cs`  -  Simulation tab

| line | text | kind |
|---:|---|---|
| 8 | Simulation | dialog/file-dialog text |
| 9 | Build from the current Freeform Helper workspace, then simulate regular-pad values directly on the AA canvas. | display text (VM property / projector) |
| 10 | Freeform Helper workspace changed. Refresh simulation to use the latest project/grid state. | display text (VM property / projector) |
| 11 | Preparing simulation workspace... | display text (VM property / projector) |

### `ViewModels/SimulationWorkspaceViewModel.CopperReplay.cs`  -  Simulation tab

| line | text | kind |
|---:|---|---|
| 29 | Copper path start captured from current center. | status chip |
| 35 | Copper path end captured from current center. | status chip |
| 42 | Copper path replay clipboard copy is unavailable in the current host. | status chip |
| 48 | Run copper path replay before copying rows. | status chip |
| 55 | Copied {CopperPathReplayRows.Count.ToString(CultureInfo.InvariantCulture)} copper path replay row(s). | display text (VM property / projector) |
| 56 | Copper path replay clipboard copy failed. | display text (VM property / projector) |
| 62 | Export Copper Path Replay CSV | display text (VM property / projector) |
| 65 | CSV | display text (VM property / projector) |
| 73 | Export Copper Path Replay JSON | display text (VM property / projector) |
| 76 | JSON | display text (VM property / projector) |
| 91 | Copper path replay text export is unavailable in the current host. | status chip |
| 97 | Run copper path replay before exporting {fileTypeName}. | status chip |
| 109 | Exported {fileTypeName} copper path replay row(s): {CopperPathReplayRows.Count.ToString(CultureInfo.InvariantCulture)}. | display text (VM property / projector) |
| 110 | Copper path replay {fileTypeName} export cancelled. | display text (VM property / projector) |
| 127 | Focused copper replay {row.StepText} at {row.CenterText}. | status chip |

### `ViewModels/SimulationWorkspaceViewModel.Selection.cs`  -  Simulation tab

| line | text | kind |
|---:|---|---|
| 16 | REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · FW Diff Idx {GetFwDiffIndex(pad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(pad.RegularPadId)}\\n | display text (VM property / projector) |
| 17 | Before {FormatWholeNumberDisplay(cell.BeforeValue)}\\n | display text (VM property / projector) |
| 18 | After {FormatWholeNumberDisplay(cell.AfterValue)}\\n | display text (VM property / projector) |
| 19 | Delta {cell.DeltaValue.ToString( | display text (VM property / projector) |
| 155 | Randomized {visiblePads.Count} regular pad(s) in {SelectedAreaOption.Display}. | status chip |
| 185 | Manual preset applied: uniform {FormatWholeNumberDisplay(value)}. | status chip |
| 365 | Anchor retain | display text (VM property / projector) |
| 366 | FW Diff Idx {action.AnchorDiffIndex} keeps {action.SourceRetainedPercent}% | display text (VM property / projector) |
| 367 | Combine {action.CombinePercent}% · CAD {sourceCadText} | display text (VM property / projector) |
| 381 | Target receive | display text (VM property / projector) |
| 382 | From FW Diff Idx {action.AnchorDiffIndex} at {leg.RatioPercent}% | display text (VM property / projector) |
| 383 | CAD {sourceCadText} · anchor REG {action.RegularPadId} | display text (VM property / projector) |
| 436 | Rows: - | display text (VM property / projector) |
| 439 | Rows: # | display text (VM property / projector) |

### `ViewModels/SimulationWorkspaceViewModel.Source.cs`  -  Simulation tab

| line | text | kind |
|---:|---|---|
| 145 | Simulation contract not supported under current version. | status chip |
| 173 | All regular pads | display text (VM property / projector) |
| 180 | IC {icIndex + 1} | display text (VM property / projector) |

### `ViewModels/SimulationWorkspaceViewModel.Validation.cs`  -  Simulation tab

| line | text | kind |
|---:|---|---|
| 16 | Potential mismatch pads: {DiffViolationPadCount.ToString(CultureInfo.InvariantCulture)}. | display text (VM property / projector) |
| 17 | No mismatch pads detected between notch expected delta and simulation delta. | display text (VM property / projector) |
| 24 | REG - | display text (VM property / projector) |
| 32 | REG {regularPadId} | display text (VM property / projector) |
| 48 | Net-flow {SimulationNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 50 | Coverage {SimulationTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 55 | Max After {FormatSafetyValue(_simulationSafetyAudit.MaxAfterValue)} | display text (VM property / projector) |
| 56 | Max After - | display text (VM property / projector) |
| 61 | Violations {SimulationSafetyViolationCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 68 | No simulation cells. | display text (VM property / projector) |
| 96 | NetFlow {FormatSignedSafetyValue(flow.NetFlow)} · source {FormatSignedSafetyValue(flow.SourceDelta)} · target {FormatSignedSafetyValue(flow.TargetDelta)} | display text (VM property / projector) |
| 119 | Select a regular pad to inspect source/target legs. | display text (VM property / projector) |
| 125 | No notch source/target leg touches this selected FW diff. | display text (VM property / projector) |
| 193 | REG {item.RegularPadId.ToString(CultureInfo.InvariantCulture)} · IC {item.IcIndex + 1} · FW Diff {item.DiffIndex.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 194 | After {FormatSafetyValue(item.AfterValue)} | display text (VM property / projector) |
| 273 | Source leg: anchor FW Diff {action.AnchorDiffIndex} retain {action.SourceRetainedPercent}% => {FormatSignedSafetyValue(delta)} · {sourceRowsText} | display text (VM property / projector) |
| 275 | Source retain | display text (VM property / projector) |
| 276 | FW Diff {action.AnchorDiffIndex} keeps {action.SourceRetainedPercent}% | display text (VM property / projector) |
| 277 | Combine {action.CombinePercent}% · CAD {FormatCadPadId(action.CadPadId)} | display text (VM property / projector) |
| 291 | Target leg: from FW Diff {action.AnchorDiffIndex} ratio {leg.RatioPercent}% => {FormatSignedSafetyValue(leg.DeltaValue)} · {sourceRowsText} | display text (VM property / projector) |
| 293 | Target receive | display text (VM property / projector) |
| 294 | From FW Diff {action.AnchorDiffIndex} at {leg.RatioPercent}% | display text (VM property / projector) |
| 295 | Anchor REG {action.RegularPadId} · CAD {FormatCadPadId(action.CadPadId)} | display text (VM property / projector) |
| 324 | Focused {item.LocationText} from Simulation safety risk list. | status chip |

### `ViewModels/SimulationWorkspaceViewModel.cs`  -  Simulation tab

| line | text | kind |
|---:|---|---|
| 89 | Manual | display text (VM property / projector) |
| 90 | CSV | display text (VM property / projector) |
| 91 | Copper | display text (VM property / projector) |
| 97 | Grounded copper (400) | display text (VM property / projector) |
| 99 | Grounded copper: full CAD pad contact is modeled as 400 diff. | display text (VM property / projector) |
| 102 | Finger press (~360) | display text (VM property / projector) |
| 104 | Finger press: same CAD overlap model, lower full-contact signal than grounded copper. | display text (VM property / projector) |
| 109 | Before | display text (VM property / projector) |
| 110 | After | display text (VM property / projector) |
| 111 | Delta | display text (VM property / projector) |
| 112 | Changed only | display text (VM property / projector) |
| 117 | AUTO | display text (VM property / projector) |
| 118 | TH | display text (VM property / projector) |
| 237 | Simulation ready. Use Manual to set baseline values, or import CSV for frame playback. | display text (VM property / projector) |
| 340 | Grid {_session.RowCount} x {_session.ColumnCount} · REG {_session.PadCount} · Rows {_session.Table.Rows.Count} | display text (VM property / projector) |
| 345 | Source: CSV | display text (VM property / projector) |
| 346 | Source: Copper | display text (VM property / projector) |
| 347 | Source: Manual | display text (VM property / projector) |
| 351 | CSV not loaded | display text (VM property / projector) |
| 351 | CSV {CsvFiles.Count} file(s) | display text (VM property / projector) |
| 352 | Signal peak {FormatWholeNumberDisplay(CopperPeakValue)} | display text (VM property / projector) |
| 353 | Manual baseline {GlobalValue.ToString( | display text (VM property / projector) |
| 357 | Compatible frames {FrameOptions.Count} | display text (VM property / projector) |
| 357 | Import CSV to project frames onto the current regular grid. | display text (VM property / projector) |
| 359 | Overrides {_manualOverridesByRegularPadId.Count} | display text (VM property / projector) |
| 363 | Contract: FW/tool rectangular diff frame | display text (VM property / projector) |
| 364 | Contract: CAD physical overlap | display text (VM property / projector) |
| 365 | Contract: regular/FW memory frame | display text (VM property / projector) |
| 370 | CSV is treated as a rectangular FW/tool diff frame, projected onto the regular grid, then passed through the current notch table. | display text (VM property / projector) |
| 372 | Copper computes CAD pad overlap first, maps signal to CAD Output FW Diff, then shows regular/FW Before and notch After. | display text (VM property / projector) |
| 374 | Manual writes Before directly into regular/FW memory. Use it for uniform-field and single-diff checks; it does not model CAD pad geometry. | display text (VM property / projector) |
| 377 | CSV row origin: imported top row maps to AA visual top row. Use CSV only for FW/tool rectangular diff frames. | display text (VM property / projector) |
| 381 | Files {CsvFiles.Count} · Compatible frames {_csvDataset.CompatibleFrameCount}/{_csvDataset.Frames.Count} · Expected grid {_session.FwDiffGrid.Cols} x {_session.FwDiffGrid.Rows} | display text (VM property / projector) |
| 382 | No CSV loaded. | display text (VM property / projector) |
| 385 | Frame {SelectedFrameSliderIndex + 1}/{FrameOptions.Count} | display text (VM property / projector) |
| 387 | Copper frame | display text (VM property / projector) |
| 388 | Manual frame | display text (VM property / projector) |
| 389 | Version: {SelectedVersionOption.Display} | display text (VM property / projector) |
| 390 | View: {SelectedCanvasViewOption.Display} | display text (VM property / projector) |
| 392 | Color: TH scale ({ColorThresholdValue.ToString( | display text (VM property / projector) |
| 393 | Color: AUTO scale | display text (VM property / projector) |
| 396 | Mode ready · {AreaSummaryText} | display text (VM property / projector) |
| 397 | Pause | display text (VM property / projector) |
| 397 | Play | display text (VM property / projector) |
| 401 | Loop off | display text (VM property / projector) |
| 401 | Loop on | display text (VM property / projector) |
| 406 | Manual baseline | display text (VM property / projector) |
| 407 | AUTO color scale | display text (VM property / projector) |
| 407 | TH color scale | display text (VM property / projector) |
| 410 | AUTO percentile-clamped scale: ordinary values stay green, strongly negative values trend blue, and stronger positive values move through yellow/orange to red based on the visible distribution. | display text (VM property / projector) |
| 411 | TH signed scale: negative values trend blue, values near 0 trend green, and positive values move through yellow/orange to red, using TH as the scale reference. | display text (VM property / projector) |
| 413 | AUTO percentile-clamped scale: regular values stay green at baseline, then move through yellow/orange to red as they rise within the visible distribution. | display text (VM property / projector) |
| 414 | TH mode: lower diff trends blue, near-threshold values trend green/yellow, and higher diff trends orange/red, using TH as the color scale reference. | display text (VM property / projector) |
| 426 | TH {ColorThresholdValue.ToString( | display text (VM property / projector) |
| 434 | Duplicate FW Diff Idx keys: {DuplicateDiffResolutionCount.ToString(CultureInfo.InvariantCulture)} (strategy: {DuplicateDiffResolutionStrategyText}). | display text (VM property / projector) |
| 435 | No duplicate active FW Diff Idx keys detected. | display text (VM property / projector) |
| 442 | IC/diff - | display text (VM property / projector) |
| 460 | Select a regular pad to inspect notch impact. | display text (VM property / projector) |
| 463 | No notch rows affect this regular under the current simulation version. | display text (VM property / projector) |
| 465 | REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · FW Diff Idx {GetFwDiffIndex(pad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(pad.RegularPadId)} | display text (VM property / projector) |
| 466 | No regular selected. | display text (VM property / projector) |
| 495 | Global baseline | display text (VM property / projector) |
| 495 | Manual override | display text (VM property / projector) |
| 496 | CSV frame projection | display text (VM property / projector) |
| 496 | CSV not loaded | display text (VM property / projector) |
| 497 | CAD overlap -> CAD Output FW Diff | display text (VM property / projector) |
| 502 | No projected regular pads under {SelectedAreaOption.Display}. | display text (VM property / projector) |
| 506 | X {CopperCenterX.ToString( | display text (VM property / projector) |
| 512 | Input path: {SelectedCopperContactModelOption.Display} CAD overlap -> CAD Output FW Diff -> regular/FW Before -> notch After. | display text (VM property / projector) |
| 515 | Move the mouse over the AA canvas to update signal in real time. Copper mode runs EMS, net-flow, and geometry audit through the same simulation gate. | display text (VM property / projector) |
| 516 | Select Copper to drive Before by movable CAD overlap area. | display text (VM property / projector) |
| 520 | Replay rows {_copperPathReplayArtifactSnapshot.StepCount.ToString(CultureInfo.InvariantCulture)} · EMS {_copperPathReplayArtifactSnapshot.TotalEmsViolationCount.ToString(CultureInfo.InvariantCulture)} · net-flow {_copperPathReplayArtifactSnapshot.TotalNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)} · coverage {_copperPathReplayArtifactSnapshot.TotalTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)} | display text (VM property / projector) |
| 521 | Replay rows: not run. | display text (VM property / projector) |
| 631 | Declared Xch/Ych: none. | display text (VM property / projector) |
| 637 | not declared | display text (VM property / projector) |
| 674 | Copper path replay: not run. | display text (VM property / projector) |
| 679 | Max After - | display text (VM property / projector) |
| 680 | Max After {worst.MaxAfterValue.ToString( | display text (VM property / projector) |
| 689 | Copper path replay: {result.Steps.Count.ToString(CultureInfo.InvariantCulture)} point(s) · {worstText} · {statusText}. | display text (VM property / projector) |

### `ViewModels/Step4IndexState.cs`  -  Workspace (status chip)

| line | text | kind |
|---:|---|---|
| 29 | Duplicate CAD Output FW Diff: none. | display text (VM property / projector) |

### `Views/ConsolePanel.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 109 | Save console log | dialog/file-dialog text |
| 114 | Log file | dialog/file-dialog text |

### `Views/CoordinatePlannerWorkspaceView.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 187 | All files | display text (VM property / projector) |
| 209 | Export Coordinate Preview | dialog/file-dialog text |
| 214 | PNG | display text (VM property / projector) |
| 215 | BMP | display text (VM property / projector) |
| 216 | All files | display text (VM property / projector) |

### `Views/DevView.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 67 | FontSymbolsOutlined: not found | display text (VM property / projector) |
| 73 | FontSymbolsOutlined: resource ok | display text (VM property / projector) |
| 77 | Probe: baseDir={baseDir} | display text (VM property / projector) |
| 78 | Theme: requested={app?.RequestedThemeVariant} actual={ActualThemeVariant} | display text (VM property / projector) |
| 79 | Token BrushBgApp: found={brushBgFound} type={brushBg?.GetType().Name ??  | display text (VM property / projector) |
| 80 | Token BrushTextPrimary: found={brushTextFound} type={brushText?.GetType().Name ??  | display text (VM property / projector) |
| 81 | Token BrushBgElevated: found={brushElevatedFound} type={brushElevated?.GetType().Name ??  | display text (VM property / projector) |
| 83 | Font Material Symbols loaded={fontLoaded} | display text (VM property / projector) |

### `Views/FreeformHelperView.Canvas.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 103 | Target allocation highlighted | display text (VM property / projector) |

### `Views/FreeformHelperView.InputAndShortcuts.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 112 | Project saved. | display text (VM property / projector) |
| 112 | Save project failed. | display text (VM property / projector) |

### `Views/FreeformHelperView.NotchExportSelection.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 93 | Export panel hidden. Use top-right Restore or Ctrl+Shift+E. | display text (VM property / projector) |

### `Views/FreeformHelperView.Pickers.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 162 | Embed DXF | display text (VM property / projector) |
| 163 | Embed the current DXF into the project file?\\nThis increases file size but keeps the project self-contained. | display text (VM property / projector) |
| 164 | Embed | display text (VM property / projector) |
| 165 | Skip | display text (VM property / projector) |
| 179 | Open DXF | dialog/file-dialog text |
| 182 | DXF | dialog/file-dialog text |
| 183 | All files | dialog/file-dialog text |
| 196 | Open Project | dialog/file-dialog text |
| 199 | FreeformHelper Project | dialog/file-dialog text |
| 200 | All files | dialog/file-dialog text |
| 210 | Import Regular Visibility Mask (SeeRegular.csv) | dialog/file-dialog text |
| 213 | CSV | dialog/file-dialog text |
| 214 | All files | dialog/file-dialog text |
| 226 | Save Project | dialog/file-dialog text |
| 231 | FreeformHelper Project | dialog/file-dialog text |
| 232 | All files | dialog/file-dialog text |
| 255 | Save Notch Table | dialog/file-dialog text |
| 262 | All files | dialog/file-dialog text |
| 271 | C source (*.c) | dialog/file-dialog text |
| 272 | CSV (*.csv) | dialog/file-dialog text |
| 306 | All files | dialog/file-dialog text |
| 315 | Save Image | display text (VM property / projector) |
| 316 | Save DXF | display text (VM property / projector) |
| 317 | Save Export File | display text (VM property / projector) |
| 325 | PNG image | display text (VM property / projector) |
| 326 | BMP image | display text (VM property / projector) |
| 327 | JPG image | display text (VM property / projector) |
| 328 | DXF file | display text (VM property / projector) |

### `Views/FreeformHelperView.SettingsWindow.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 31 | General | display text (VM property / projector) |

### `Views/LeftDxfPanel.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 46 | Reset DXF edits | display text (VM property / projector) |
| 47 | Reset all DXF edits, including hidden pads, combined pads, and layer moves? | display text (VM property / projector) |
| 48 | Reset | display text (VM property / projector) |
| 49 | Cancel | display text (VM property / projector) |

### `Views/NotchExportFilterBuilderWindow.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 84 | Linked | display text (VM property / projector) |
| 85 | Warning | display text (VM property / projector) |
| 86 | No CAD | display text (VM property / projector) |
| 87 | Legacy | display text (VM property / projector) |
| 93 | status={normalized} | display text (VM property / projector) |
| 112 | (empty) | display text (VM property / projector) |

### `Views/SettingsWindow.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 68 | Reset all settings | display text (VM property / projector) |
| 69 | Reset all settings to defaults? This also overwrites app-level general settings for next startup. | display text (VM property / projector) |
| 70 | Reset | display text (VM property / projector) |
| 71 | Cancel | display text (VM property / projector) |

### `Views/SimulationWorkspaceView.axaml.cs`  -  See file (view code-behind)

| line | text | kind |
|---:|---|---|
| 215 | Import Diff CSV | dialog/file-dialog text |
| 218 | CSV | display text (VM property / projector) |
| 219 | All files | display text (VM property / projector) |
| 256 | All files | display text (VM property / projector) |


## 8. File dialogs (OS-level text)

These appear in the system file picker. They are inside section 7 as well; repeated here because they are a separate surface.

| path:line | text |
|---|---|
| `Services/SimulationSafetyTextProjector.cs:8` | Export blocked by EMS safety |
| `ViewModels/CoordinatePlannerHostViewModel.cs:8` | Coordinate |
| `ViewModels/DxfOverlapReportViewModel.cs:20` | Issues |
| `ViewModels/DxfOverlapReportViewModel.cs:20` | Issues ({IssueCount}) |
| `ViewModels/IndexMappingReportViewModel.cs:183` | candidate attribution ({Step4CandidateAttributionLines.Count.ToString(CultureInfo.InvariantCulture)}) |
| `ViewModels/NotchDetailViewModel.cs:52` | CAD id {CadPad.Id} |
| `ViewModels/NotchDetailViewModel.cs:52` | CAD idx {DxfIndex.Value} (CAD id {CadPad.Id}) |
| `ViewModels/PadInfo/CadPadInfoViewModel.Construction.cs:66` | CAD pad |
| `ViewModels/PadInfo/CadPadInfoViewModel.Construction.cs:66` | CAD pads |
| `ViewModels/PadInfo/RegularPadInfoViewModel.Core.cs:173` | Regular pad |
| `ViewModels/PadInfo/RegularPadInfoViewModel.Core.cs:173` | Regular pads |
| `ViewModels/SimulationHostViewModel.cs:8` | Simulation |
| `Views/ConsolePanel.axaml.cs:109` | Save console log |
| `Views/ConsolePanel.axaml.cs:114` | Log file |
| `Views/CoordinatePlannerWorkspaceView.axaml.cs:187` | All files |
| `Views/CoordinatePlannerWorkspaceView.axaml.cs:209` | Export Coordinate Preview |
| `Views/CoordinatePlannerWorkspaceView.axaml.cs:214` | PNG |
| `Views/CoordinatePlannerWorkspaceView.axaml.cs:215` | BMP |
| `Views/CoordinatePlannerWorkspaceView.axaml.cs:216` | All files |
| `Views/FreeformHelperView.Pickers.cs:162` | Embed DXF |
| `Views/FreeformHelperView.Pickers.cs:163` | Embed the current DXF into the project file?\\nThis increases file size but keeps the project self-contained. |
| `Views/FreeformHelperView.Pickers.cs:164` | Embed |
| `Views/FreeformHelperView.Pickers.cs:165` | Skip |
| `Views/FreeformHelperView.Pickers.cs:179` | Open DXF |
| `Views/FreeformHelperView.Pickers.cs:182` | DXF |
| `Views/FreeformHelperView.Pickers.cs:183` | All files |
| `Views/FreeformHelperView.Pickers.cs:196` | Open Project |
| `Views/FreeformHelperView.Pickers.cs:199` | FreeformHelper Project |
| `Views/FreeformHelperView.Pickers.cs:200` | All files |
| `Views/FreeformHelperView.Pickers.cs:210` | Import Regular Visibility Mask (SeeRegular.csv) |
| `Views/FreeformHelperView.Pickers.cs:213` | CSV |
| `Views/FreeformHelperView.Pickers.cs:214` | All files |
| `Views/FreeformHelperView.Pickers.cs:226` | Save Project |
| `Views/FreeformHelperView.Pickers.cs:231` | FreeformHelper Project |
| `Views/FreeformHelperView.Pickers.cs:232` | All files |
| `Views/FreeformHelperView.Pickers.cs:255` | Save Notch Table |
| `Views/FreeformHelperView.Pickers.cs:262` | All files |
| `Views/FreeformHelperView.Pickers.cs:271` | C source (*.c) |
| `Views/FreeformHelperView.Pickers.cs:272` | CSV (*.csv) |
| `Views/FreeformHelperView.Pickers.cs:306` | All files |
| `Views/FreeformHelperView.Pickers.cs:315` | Save Image |
| `Views/FreeformHelperView.Pickers.cs:316` | Save DXF |
| `Views/FreeformHelperView.Pickers.cs:317` | Save Export File |
| `Views/FreeformHelperView.Pickers.cs:325` | PNG image |
| `Views/FreeformHelperView.Pickers.cs:326` | BMP image |
| `Views/FreeformHelperView.Pickers.cs:327` | JPG image |
| `Views/FreeformHelperView.Pickers.cs:328` | DXF file |
| `Views/SimulationWorkspaceView.axaml.cs:215` | Import Diff CSV |
| `Views/SimulationWorkspaceView.axaml.cs:218` | CSV |
| `Views/SimulationWorkspaceView.axaml.cs:219` | All files |
| `Views/SimulationWorkspaceView.axaml.cs:256` | All files |


## 9. Inventory: console log messages (Info / Warn / Error)

Shown in the Console panel because NLog `minlevel="Info"` routes to the in-app console target (`NLog.config:29`). Source names such as `Workflow`, `CadLoad` come from `Services/ConsoleSourceNames.cs`.

| path:line | level | text |
|---|---|---|
| `App.axaml.cs:144` | Error | UI thread unhandled exception. |
| `Logging/EmergencyLogSink.cs:12` | Error | {0} failed. |
| `Logging/EmergencyLogSink.cs:13` | Error | {0} failed. |
| `Logging/EmergencyLogSink.cs:18` | Error | UI event {0} failed. |
| `Program.cs:57` | Error | Fatal exception during app startup. |
| `Program.cs:105` | Error | Unhandled exception. |
| `Program.cs:109` | Error | Unhandled exception: {0} |
| `Program.cs:116` | Error | Unobserved task exception. |
| `Program.cs:139` | Info | Startup log level override applied from app settings: {0} |
| `Program.cs:143` | Warn | Failed to apply startup log level override. |
| `Services/AppGeneralSettingsStore.cs:71` | Warn | Failed to load app general settings from '{0}'. |
| `Services/CadLoadSpinnerProcessHost.cs:245` | Warn | Failed to sync CAD load spinner visibility state. |
| `Services/CadLoadSpinnerProcessHost.cs:331` | Warn | CAD load spinner warmup failed. |
| `Services/CadLoadSpinnerProcessHost.cs:695` | Warn | Failed to cleanup CAD load spinner process. |
| `Services/CadLoadWorkflowService.cs:31` | Info | Importing DXF. |
| `Services/CadLoadWorkflowService.cs:76` | Info | Open DXF timing: import={0} ms, layerCatalog={1} ms, apply={2} ms, rebuild={3} ms, total={4} ms, pads={5}, includeBlock={6}, onlyClosed={7}. |
| `Services/ConsoleLinkOpener.cs:91` | Warn | Console link open refused (not an http or https link): raw={0}. |
| `Services/ConsoleLinkOpener.cs:108` | Warn | Console link open failed (path not found): raw={0}. |
| `Services/ConsoleLinkOpener.cs:156` | Warn | Console link open failed: target={0}. |
| `Services/GridRebuildOrchestrator.cs:105` | Warn | DXF regular source mismatch. {0} |
| `Services/GridRebuildOrchestrator.cs:121` | Warn | DXF regular source build skipped: {0} |
| `Services/GridRebuildOrchestrator.cs:144` | Info | Grid build skipped: no CAD output pads for bounds. |
| `Services/GridRebuildOrchestrator.cs:156` | Info | Grid alignment fallback: DXF bounds requested but no DXF loaded. |
| `Services/LayerCatalogStateService.cs:79` | Warn | Failed to parse DXF layer catalog from path: {0} |
| `Services/LayerCatalogStateService.cs:104` | Info | DXF layer catalog loaded from path ({0} layers). |
| `Services/LayerCatalogStateService.cs:135` | Info | DXF layer catalog loaded from embedded bytes ({0} layers). |
| `Services/LayerCatalogStateService.cs:143` | Warn | Failed to parse DXF layer catalog from embedded bytes. |
| `Services/RuntimeQueryIpcHost.cs:74` | Info | Runtime query IPC server started. pipe={0} |
| `Services/RuntimeQueryIpcHost.cs:77` | Info | Runtime query IPC server stopped. |
| `Services/RuntimeQueryIpcHost.cs:80` | Warn | Runtime query IPC wait failed. |
| `Services/RuntimeQueryIpcHost.cs:83` | Warn | Runtime query IPC request handling failed. |
| `Services/RuntimeQueryIpcHost.cs:86` | Warn | Runtime query execution failed. |
| `Services/RuntimeQueryIpcHost.cs:95` | Warn | Runtime query IPC pipe creation failed. |
| `Services/UiEventFailureReporter.cs:13` | Error | UI event {0} failed. |
| `ViewModels/FreeformHelperViewModel.AppSettings.cs:102` | Warn | Persist app general settings failed. |
| `ViewModels/FreeformHelperViewModel.AppSettings.cs:146` | Info | App general settings loaded: {0} |
| `ViewModels/FreeformHelperViewModel.AppSettings.cs:157` | Info | Load project: app visual preferences layer applied. |
| `ViewModels/FreeformHelperViewModel.CadLoad.cs:132` | Info | {0}: {1} (pads={2}) |
| `ViewModels/FreeformHelperViewModel.CadLoad.cs:136` | Info | DXF loaded from embedded data (pads={0}) |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Combine.cs:44` | Warn | DXF combine failed: union output empty. |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Combine.cs:86` | Info | DXF combine applied. source={0}, outputPads={1}, hiddenTotal={2}, combinedTotal={3}, holes={4}. |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Edits.Hidden.cs:43` | Info | DXF edit hid {0} pad(s). Hidden total={1}. |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:104` | Info | DXF layer image exported to {0}. layer={1}, pads={2}, size={3}x{4}, lineWidth={5}, paddingX={6}, paddingY={7}, format={8}, dark={9}. |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:119` | Error | Export DXF image failed. |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:161` | Info | DXF exported to {0} (mode={1}, pads={2}). |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:166` | Error | Export DXF failed. |
| `ViewModels/FreeformHelperViewModel.DxfEditing.Export.cs:206` | Info | DXF visible export appended regular source layer pads: layer={0}, appended={1}, total={2}. |
| `ViewModels/FreeformHelperViewModel.DxfQuality.cs:43` | Info | DXF overlap check started (visible pads={0}). |
| `ViewModels/FreeformHelperViewModel.DxfQuality.cs:50` | Error | DXF overlap check failed. |
| `ViewModels/FreeformHelperViewModel.DxfQuality.cs:76` | Info | DXF check: no overlaps detected (pads={0}). |
| `ViewModels/FreeformHelperViewModel.DxfQuality.cs:82` | Warn | DXF quality warnings: {0} |
| `ViewModels/FreeformHelperViewModel.DxfQuality.cs:85` | Warn | DXF issue: {0} |
| `ViewModels/FreeformHelperViewModel.DxfQuality.cs:128` | Warn | DXF overlap details requested but view handler is not wired. |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:41` | Info | Analyzing DXF/Regular mapping (cadVisible={0}, regular={1}). |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:47` | Error | Mapping analyze failed. |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:95` | Warn | DXF/Regular mapping warnings: {0} |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:98` | Warn | Mapping issue: {0} |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:103` | Info | DXF/Regular mapping OK: {0} |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:154` | Info | Mapping override saved: CAD {0} -> regular id {1}. |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:169` | Info | Mapping override cleared for CAD {0}. |
| `ViewModels/FreeformHelperViewModel.IndexMapping.cs:182` | Info | Diff repair override applied: CAD {0} -> diff {1}. |
| `ViewModels/FreeformHelperViewModel.MatchWorkflow.cs:61` | Info | Matching pads. |
| `ViewModels/FreeformHelperViewModel.MatchWorkflow.cs:67` | Error | Match failed. |
| `ViewModels/FreeformHelperViewModel.MatchWorkflow.cs:78` | Info | Matching complete. |
| `ViewModels/FreeformHelperViewModel.NotchDetails.AutoPlay.cs:126` | Warn | Notch preview auto-play loop crashed. |
| `ViewModels/FreeformHelperViewModel.NotchDetails.cs:153` | Warn | Notch detail requested but dialog handler is not wired. |
| `ViewModels/FreeformHelperViewModel.NotchDetails.cs:178` | Warn | Notch detail skipped: selected CAD pad not found (id={0}). |
| `ViewModels/FreeformHelperViewModel.NotchValidation.cs:121` | Info | Validation bucket rebuilt: tableRows={0}, null={1}, elapsed={2}ms. |
| `ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs:57` | Error | Layer filter failed. |
| `ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs:124` | Warn | Auto CAD Output FW Diff assignment issues. mode={0}, duplicateOverrides={1}, invalidOverrides={2}, autoConflicts={3}, autoUnmatched={4}, overrideMismatch={5}. |
| `ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs:133` | Warn | Auto CAD Output FW Diff conflicts sample: {0} |
| `ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs:138` | Warn | Auto CAD Output FW Diff unmatched sample: {0} |
| `ViewModels/FreeformHelperViewModel.Operations.LayerSelection.Options.cs:55` | Info | Auto-hidden regular source layer from CAD view: {0} |
| `ViewModels/FreeformHelperViewModel.Operations.cs:259` | Error | DXF import failed. |
| `ViewModels/FreeformHelperViewModel.Operations.cs:275` | Error | Rebuild grid failed. |
| `ViewModels/FreeformHelperViewModel.Operations.cs:284` | Info | Building regular grid. |
| `ViewModels/FreeformHelperViewModel.Operations.cs:340` | Info | Grid built. Rows={0}, Cols={1}, Pads={2}. |
| `ViewModels/FreeformHelperViewModel.PadEdits.cs:99` | Warn | Manual sizing apply failed. Error={0} |
| `ViewModels/FreeformHelperViewModel.PadEdits.cs:114` | Info | Manual sizing apply requested. Rows={0}, Cols={1}, Width={2}, Height={3}. |
| `ViewModels/FreeformHelperViewModel.PadEdits.cs:151` | Warn | Manual sizing reset failed. Error={0} |
| `ViewModels/FreeformHelperViewModel.PadEdits.cs:166` | Info | Manual sizing reset requested. Rows={0}, Cols={1}. |
| `ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:77` | Info | {0} export stage 3 switched file type to {1}. |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:46` | Info | Saving project. |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:69` | Info | Project saved: {0} |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:83` | Error | Save project failed. |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:182` | Info | Loading project from path: {0} |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:219` | Error | Load project failed. |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:233` | Info | Loading project: {0} |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:255` | Info | Project loaded: {0} |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:315` | Info | Load project timing: total={0} ms, persistence={1} ms, apply-state={2} ms, dxf={3} ms, rebuild={4} ms. |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:321` | Info | Load project timing detail: step1-auto-replay={0} ms (replayed={1}). |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:322` | Info | Load project timing detail: step2-auto-replay={0} ms (replayed={1}). |
| `ViewModels/FreeformHelperViewModel.Persistence.Project.cs:395` | Info | preconditions not met |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:120` | Info | {0} notch export completed: path={1}, rows={2}. |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:128` | Error | {0} export failed. |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:159` | Warn | {0} export blocked at stage 1: {1} |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:182` | Warn | {0} export stage 2 produced no rows. |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:186` | Info | {0} export stage 2 generated {1} row(s). |
| `ViewModels/FreeformHelperViewModel.Persistence.cs:249` | Info | {0} export stage 3 kept {1}/{2} row(s). |
| `ViewModels/FreeformHelperViewModel.Progress.cs:76` | Info | {0} finished in {1} ms. |
| `ViewModels/FreeformHelperViewModel.Progress.cs:115` | Info | {0} finished in {1} ms. |
| `ViewModels/FreeformHelperViewModel.Simulation.cs:167` | Error | Simulation workspace failed. |
| `ViewModels/ShellViewModel.Workspaces.cs:206` | Error | Derived workspace prewarm failed after workspace source change. |
| `Views/DevView.axaml.cs:28` | Info | DevView theme probe: Requested={0} Actual={1} |
| `Views/FreeformHelperView.SettingsWindow.cs:29` | Info | Open settings window requested: source={0}, section={1}. |

