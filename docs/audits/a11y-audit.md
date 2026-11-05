# Accessibility audit of the Avalonia views

- Date: 2026-11-05
- Scope: all 67 `.axaml` files under `src/`, plus the code-behind key handlers they rely on
- Base: `main` at `5e48bec`. Branch: `cloud/a11y-audit`
- Line numbers refer to the head of `cloud/a11y-audit`, after the fixes below
- Roadmap item: `UI-A11Y1` in `ROADMAP.md` section 2.6

## Summary

| Area | Before | After this branch | Open |
|---|---|---|---|
| `AutomationProperties.*` in XAML | 0 uses | 312 names | 1 icon button, about 26 bound or composite buttons, about 40 inputs |
| `TabIndex`, `IsTabStop`, `KeyboardNavigation.*` | 0 uses | unchanged (not needed, see A11Y-12) | SelectableTextBlock tab stops (A11Y-05) |
| `IsDefault` / `IsCancel` in dialogs | 0 uses | unchanged | 9 windows (A11Y-06) |
| Focus visuals | 1 control class with no focus ring | unchanged | A11Y-08 |

Metric moved: controls with an accessible name went from 0 to 312 (80 icon-only buttons,
87 text buttons and 145 form inputs).

Severity scale:

- **High**: a screen-reader or keyboard user cannot identify or reach a control.
- **Medium**: the control can be reached, but with extra effort or a wrong announcement.
- **Low**: a polish item, or a gap that only affects a secondary path.

## How the findings were checked

1. A scanner parsed every `.axaml` file and listed each interactive control (Button,
   ToggleButton, CheckBox, ToggleSwitch, TextBox, ComboBox, Slider, ListBox,
   `controls:NumberScrubber`, Expander, MenuItem) with its content, tooltip and the label
   next to it. Controls inside `Style`, `ControlTemplate` and popups were skipped.
2. The automation peers of Avalonia 12.1.1 were queried directly, using the built
   `Avalonia.Controls.dll` from F# interactive:

   | Control | `GetName()` | `GetHelpText()` |
   |---|---|---|
   | `Button { Content = "Plain" }` | `Plain` | (empty) |
   | `Button { Content = TextBlock }` | `Avalonia.Controls.TextBlock` | (empty) |
   | `Button { Content = SelectableTextBlock }` | `Avalonia.Controls.SelectableTextBlock` | (empty) |
   | `Button { Content = StackPanel }` | `Avalonia.Controls.StackPanel` | (empty) |
   | `Button` with only `ToolTip.Tip` | (empty) | the tooltip |
   | `ToggleSwitch`, `CheckBox` with no content | (empty) | (empty) |

   A tooltip is only help text, so a tooltip does not name a control. Content that is not a
   string is read out as its .NET type name.
3. A temporary headless test (not committed) put `Button, SelectableTextBlock, Button` in a
   window, focused the first button and pressed Tab. The focus moved to the
   `SelectableTextBlock`. In Avalonia 12.1.1 `SelectableTextBlock` has `Focusable=true` and
   `IsTabStop=true`.

## Findings

### A11Y-01 Icon-only buttons have no accessible name: High: fixed (80 of 81)

Each button below shows only a `FontIcon` glyph. Its text was only in `ToolTip.Tip`, which
Avalonia exposes as help text. The name was empty.

Fix applied: `AutomationProperties.Name` is now the literal tooltip text without the trailing
period. The new guard `tests/FreeformHelper.Tests/Architecture/AccessibilityNameGuardTests.cs`
fails when a `Button` or `ToggleButton` with the `actionIconButton` class has no name.

Fixed files and counts: `CanvasOverlayControls` 3, `ConsolePanel` 1,
`CoordinatePlannerWorkspaceView` 3, `DevView` 23, `DxfEditChangeListWindow` 1,
`FreeformHelperView` 2, `LeftDxfPanel` 7, `NotchExportSelectionWindow` 3,
`CadPadInfoContentView` 6, `RegularPadInfoContentView` 1, `RightWorkflowSettingsTabView` 4,
`SettingsGeneralSectionView` 1, `SimulationWorkspaceView` 1, `RightWorkflowStep1View` to
`RightWorkflowStep5View` 12, `CoordinatePlannerWorkspaceControlsPaneView` 2,
`CoordinatePlannerWorkspaceDetailsPaneView` 4, `SimulationWorkspaceControlsPaneView` 3,
`SimulationWorkspaceDetailsPaneView` 3.

Open:

| File:line | Issue | Severity | Fix |
|---|---|---|---|
| `src/FreeformHelper.UI/Views/PadInfoSections/CadPadInfoContentView.axaml:82` | The info button `padInfoInfoButton` has only a bound tooltip (`ToFullReasonFullText`) | High | Add a short fixed name, for example `AutomationProperties.Name="To Full reason"`. Keep the bound tooltip as help text |

### A11Y-02 Buttons with text-block content announce a type name: High: fixed (87), open (about 26)

A button whose content is a `SelectableTextBlock`, or a panel holding an icon and a text,
announces `Avalonia.Controls.SelectableTextBlock` or `Avalonia.Controls.Grid`. This is the most
common button shape in the app, because labels use `SelectableTextBlock`.

Fix applied: when the content holds exactly one literal text, `AutomationProperties.Name` is now
that text (87 controls in 14 files, mostly `DevView`, `LeftDxfPanel`,
`SimulationWorkspaceControlsPaneView`, `CoordinatePlannerWorkspaceControlsPaneView`,
`DxfEditChangeListWindow`, `SimulationHostView` and `CoordinatePlannerHostView`).

Open. The text is bound, set in code-behind or made of several parts, so a fixed name would be
wrong:

| File:line | Issue | Severity | Fix |
|---|---|---|---|
| `src/FreeformHelper.UI/Views/ConfirmDialog.axaml:28`, `:44` | The button text is set in code-behind (`CancelButtonText`, `ConfirmButtonText`) | High | In `ConfirmDialog.axaml.cs`, set `AutomationProperties.SetName(button, text)` where the text is assigned |
| `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml:130`, `:141`, `:153`, `:164`, `:175` | The DXF edit summary chips show only a count; the meaning is in a bound tooltip | High | Bind the name to the tooltip property, for example `AutomationProperties.Name="{Binding HiddenDxfEditBadgeTooltip}"` |
| `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml:416` | The bulk layer toggle has bound text | Medium | `AutomationProperties.Name="{Binding CadLayerBulkToggleText}"` |
| `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml:468` | The layer toggle in the item template has bound text | Medium | `AutomationProperties.Name="{Binding Name}"` |
| `src/FreeformHelper.UI/Views/DxfEditChangeListWindow.axaml:79`, `:297` | Bound action text | Medium | Bind the name to `BulkActionText` and `SelectedRowActionLabelText` |
| `src/FreeformHelper.UI/Views/WorkspaceSections/SimulationWorkspaceControlsPaneView.axaml:473`, `:491` | Play and loop buttons with bound text | Medium | Bind the name to `PlaybackButtonText` and `PlaybackLoopText` |
| `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowInspectorView.axaml:302` | Bound `Raw x{0}` toggle | Low | Bind the name with the same `StringFormat` |
| `src/FreeformHelper.UI/Views/PadInfoSections/CadPadInfoContentView.axaml:150`, `RegularPadInfoContentView.axaml:151` | Section toggle made of a status text and a toggle text | Medium | Use a fixed name such as `Geometry details` |
| `src/FreeformHelper.UI/Views/NotchExportColumnFilterWindow.axaml:84` | CheckBox in the item template with bound text | Medium | `AutomationProperties.Name="{Binding Display}"` |
| Row buttons: `RightWorkflowPanel.axaml:15`, `PadInfoRelationCardListView.axaml:13`, `DxfEditChangeListWindow.axaml:196`, `CoordinatePlannerWorkspaceDetailsPaneView.axaml:132`, `IndexMappingReportLeftPaneView.axaml:52`, `:138`, `IndexMappingReportRightPaneView.axaml:76`, `NotchExportSelectionRowsPaneView.axaml:138`, `SimulationWorkspaceDetailsPaneView.axaml:178`, `:300` | A data row is one button whose content is several bound columns | Medium | Add one `AutomationName` property to each row view model, built from the existing display texts, and bind the name to it. This keeps one projection per row, as `AGENTS.md` requires |
| `src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml:534`, `WorkspaceSections/IndexMappingReportRightPaneView.axaml:212` | Expander with an element header | Low | `AutomationProperties.Name="Payload details"` and `"Advanced details"` |

Buttons with `Content="{Binding ...}"` (23) are fine when the bound value is a string, because the
peer then uses the string.

### A11Y-03 Form inputs have no accessible name: High: fixed (145), open (about 40)

The label of each field is a separate `SettingsInfoLabel` or `SelectableTextBlock`. Nothing
connects it to the input, so a screen reader announced only the control type.

Fix applied: `AutomationProperties.Name` is now the literal text of the label just before the
input (145 controls in 18 files: all `SettingsSections`, `WorkspaceHeader` display menu,
`CanvasOverlayControls`, `DxfLayerImageExportWindow`, the Notch export filter windows,
`CoordinatePlannerWorkspaceControlsPaneView`, `SimulationWorkspaceControlsPaneView`, pad info
sizing, and the workflow steps). The coordinate planner guide fields repeat the labels `Count`,
`Pitch (mm)`, `Positions (mm)` and `Inset (mm)`, so they get the group as a prefix
(`Horizontal guides: Count`).

Open. No adjacent literal label:

| File:line | Issue | Severity | Fix |
|---|---|---|---|
| `src/FreeformHelper.UI/Views/IndexMappingReportWindow.axaml:92`, `NotchExportSelectionWindow.axaml:166`, `RightWorkflowSections/RightWorkflowInspectorView.axaml:31` | Search boxes with only a watermark | High | `AutomationProperties.Name="Search"` (or the field's purpose); a watermark is not a name |
| `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml:312`, `:328`, `:337`, `:361`, `:382`, `:440` | DXF edit rotation, target layer and bounds fields; the label is a heading two levels up or a literal tooltip | High | Use the tooltip or heading text as the name, for example `Rotation scope`, `Rotation angle`, `Target layer`, `New layer name`, `Bounds source layer` |
| `src/FreeformHelper.UI/Views/WorkspaceSections/SimulationWorkspaceControlsPaneView.axaml:34`, `:410`, `:417`, `:424`, `:431`, `:438`, `:505` | Input source, view options and timeline slider | High | Name them from the section headings (`Input`, `View`, `Frame`) |
| `src/FreeformHelper.UI/Views/WorkspaceSections/CoordinatePlannerWorkspaceControlsPaneView.axaml:456`, `:462`, `:488`, `:494`, `:503`, `:509`, `:517` | X/Y scrubbers of custom points and paths | Medium | Names such as `Point X (mm)` |
| `src/FreeformHelper.UI/Views/CascadeIcSettingsWindow.axaml:79`, `:85` | Per-IC X/Y channels in an item template | Medium | Bind a name that includes the IC index |
| `src/FreeformHelper.UI/Views/SettingsSections/SettingsGeneralSectionView.axaml:30`, `:409` | Cascade scrubber (label outside its inner grid) and the `Local/Global` switch (label after the control) | Medium | `Cascade`, `Sizing scope` |
| `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml:143`, `WorkflowSteps/RightWorkflowStep3View.axaml:193`, `WorkflowSteps/RightWorkflowStep4View.axaml:107`, `RightWorkflowSections/RightWorkflowSettingsTabView.axaml:273`, `PadInfoSections/CadPadInfoContentView.axaml:256` | Scrubbers with only a tooltip or a label inside another panel | Medium | Use the short tooltip or label text |
| `src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml:144` | Overlay density ComboBox with only a tooltip | Medium | `AutomationProperties.Name="Overlay density"` |
| `src/FreeformHelper.UI/Views/DxfLayerImageExportWindow.axaml:117` | ToggleSwitch with no label next to it | Medium | Name it from its row text |
| `src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml:362` | View menu switch in an item template, bound tooltip `{Binding Label}` | Medium | `AutomationProperties.Name="{Binding Label}"` |
| `src/FreeformHelper.UI/Views/DxfEditChangeListWindow.axaml:148`, `:208`, `NotchExportSelectionWindow.axaml:507`, `WorkspaceSections/NotchExportSelectionRowsPaneView.axaml:70`, `:152` | Select-all and row selection CheckBoxes with no content | Medium | `Select all rows` for the header boxes. Bind row boxes to the row automation name (see A11Y-02) |
| `src/FreeformHelper.UI/Views/DxfOverlapReportWindow.axaml:43` | Read-only report TextBox | Low | `AutomationProperties.Name="Overlap report"` |
| ListBoxes: `DxfEditChangeListWindow.axaml:179`, `NotchExportColumnFilterWindow.axaml:67`, `CoordinatePlannerWorkspaceDetailsPaneView.axaml:116`, `IndexMappingReportLeftPaneView.axaml:37`, `NotchExportSelectionRowsPaneView.axaml:121`, `SimulationWorkspaceDetailsPaneView.axaml:284` | Lists have no name | Low | Name each list after its panel title |

### A11Y-04 NumberScrubber does not pass its name to the inner TextBox: High: open

`src/FreeformHelper.UI/Controls/NumberScrubber.axaml:8`. A `NumberScrubber` is a `UserControl`.
The focusable part is the inner `TextBox x:Name="InputBox"`. This branch names the outer control
(about 90 places), but keyboard focus lands on the inner TextBox. That TextBox has no name, so
the name is not announced when the field gets focus.

Fix: in `NumberScrubber.axaml.cs`, copy `AutomationProperties.NameProperty` from the host to
`InputBox` when it changes (one property-changed handler), or give the control an automation
peer that exposes the value. One change in the control fixes all scrubbers. Add a headless test:
focus a named scrubber and check that `ControlAutomationPeer.CreatePeerForElement(focused).GetName()`
equals the name.

### A11Y-05 Every SelectableTextBlock is a Tab stop: High: open

There are 1156 `SelectableTextBlock` elements in the views. They are used for labels, notes,
values, and the text inside buttons. In Avalonia 12.1.1 each one has `Focusable=true` and
`IsTabStop=true`, and the headless check confirmed that Tab moves focus into a
`SelectableTextBlock`. A keyboard user has to press Tab through every label between two inputs,
including the text inside each button.

Fix: one style in `src/FreeformHelper.UI/Styles/Controls.Core.axaml`:

```xml
<Style Selector="SelectableTextBlock">
  <Setter Property="IsTabStop" Value="False"/>
</Style>
```

Mouse selection and copy still work, because `Focusable` stays `true`. This branch does not apply
it, because it changes keyboard behavior in the whole app. `AGENTS.md` asks for a Dev page
preview first. Add a headless tab-walk guard test with the change. Where a text is only a
label inside a button, a plain `TextBlock` is the better element.

### A11Y-06 Dialogs do not support Enter and Escape: Medium: open

No window sets `IsDefault` or `IsCancel`, and no dialog code-behind handles `Key.Enter` or
`Key.Escape`. Keyboard users must Tab to the button.

| File:line | Buttons | Fix |
|---|---|---|
| `src/FreeformHelper.UI/Views/WarningDialog.axaml:35` | `OK` | `IsDefault="True" IsCancel="True"` |
| `src/FreeformHelper.UI/Views/ConfirmDialog.axaml:28`, `:44` | Cancel (danger), Confirm | `IsCancel="True"` on Cancel. Ask the owner whether Enter confirms; the dialog decides whether a DXF is embedded |
| `src/FreeformHelper.UI/Views/SettingsWindow.axaml:129`, `:131` | Cancel, Save Settings | `IsCancel` on Cancel, `IsDefault` on Save |
| `src/FreeformHelper.UI/Views/DxfLayerImageExportWindow.axaml:131`, `:133` | Cancel, Export | `IsCancel`, `IsDefault` |
| `src/FreeformHelper.UI/Views/NotchExportColumnFilterWindow.axaml:98`, `:101` | Cancel, Apply | `IsCancel`, `IsDefault` |
| `src/FreeformHelper.UI/Views/NotchExportFilterBuilderWindow.axaml:102`, `:105` | Cancel, Apply | `IsCancel`, `IsDefault` (Enter inside the query TextBox must still work) |
| `src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml:589`, `:592` | Cancel, Export | `IsCancel`, `IsDefault` |
| `src/FreeformHelper.UI/Views/CascadeIcSettingsWindow.axaml:109` | Done | `IsDefault="True" IsCancel="True"` |
| `src/FreeformHelper.UI/Views/CoordinateArtifactDetailWindow.axaml:79`, `DxfEditChangeListWindow.axaml:325` | Close | `IsCancel="True"` |

This changes behavior, so it is not a mechanical fix. Add one headless test per dialog that
presses Escape and checks that the window closed with the cancel result.

### A11Y-07 Report windows have no keyboard close: Low: open

`src/FreeformHelper.UI/Views/NotchDetailWindow.axaml`, `DxfOverlapReportWindow.axaml` and
`IndexMappingReportWindow.axaml` have no close button and no Escape handling. Only Alt+F4 or the
title bar closes them. Fix: handle Escape in one shared place (for example a small base window
or attached behavior used by all secondary windows), not in each window.

### A11Y-08 The bulk layer toggle has no focus ring: Medium: open

`src/FreeformHelper.UI/Styles/Controls.Core.axaml:961` sets `FocusAdorner` to `{x:Null}` on
`ToggleButton.dxfLayerBulkToggleButton` (setter at line 973), and no `:focus-visible` style
replaces it. Used at `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml:416` and in the Dev page
preview (`DevView.axaml`). Keyboard focus on this toggle is invisible.

Fix: add a `ToggleButton.dxfLayerBulkToggleButton:focus-visible` style that uses the same
focus border tokens as `Button.actionNeutral:focus-visible`
(`src/FreeformHelper.UI/Styles/Controls.Action.axaml:156`). Preview it on the Dev page first.

The other places that remove `FocusAdorner` have a replacement and are fine:

- action buttons: `:focus-visible` styles in `Controls.Action.axaml` for neutral, primary, danger,
  ghost, viewport overlay, panel chrome, console header and chip actions;
- `TextBox.numberScrubberInput`: the outer border gets the `focused` class
  (`Controls.Form.axaml:165`, `Controls.Settings.axaml:162`).

Other custom button classes (`settingsNavItem`, `shellTab`, `workspaceDataRow`,
`workspaceTableHeaderButton` and so on) keep the Fluent theme focus adorner.

### A11Y-09 Canvas controls have no name and no shortcut hint: Medium: open

The pad canvases are focusable and have keyboard shortcuts (`F` fit, arrows pan, `Space` and
`Alt` modifiers, `Ctrl+A` select all; `src/FreeformHelper.UI/Controls/PadCanvas.Input.Navigation.cs:58`).
They have no accessible name:

- `src/FreeformHelper.UI/Views/FreeformHelperView.axaml:127`
- `src/FreeformHelper.UI/Views/SimulationWorkspaceView.axaml:106`
- `src/FreeformHelper.UI/Views/CoordinatePlannerWorkspaceView.axaml:93`

Fix: add `AutomationProperties.Name` (`Pad canvas`, `Simulation canvas`, `Coordinate canvas`)
and `AutomationProperties.HelpText` that lists the keys.

### A11Y-10 Keyboard shortcuts are not exposed: Low: open

Shortcuts are handled in code (`src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs`:
`Ctrl+S`, `Ctrl+Shift+E`, `Delete`, `Ctrl+Z`, `Ctrl+A`; `DxfEditChangeListWindow.axaml.cs:40`:
`Ctrl+A`). No XAML uses `HotKey`, `InputGesture` or `AutomationProperties.AcceleratorKey`, and the
tooltips do not mention the keys. Fix: set `AutomationProperties.AcceleratorKey` on the matching
buttons and add the key to their tooltip text. Keep the handler in its single entry point.

### A11Y-11 Hover-only and pointer-only paths: Medium: open

| File:line | Issue | Severity | Fix |
|---|---|---|---|
| `src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml:199` | The display options popup opens on hover and pointer press (`PointerEntered`, `PointerPressed`) | Medium | Check that Space/Enter on the toggle opens the popup, that focus moves into it, and that Escape closes it and returns focus. Add a headless test |
| `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml:468` | The layer toggle handles `PointerPressed` for modifier clicks; there is no keyboard path for the same action | Low | Offer the same action through a key (for example `Shift+Space`) or a context command |

### A11Y-12 Tab order: no change needed

No file uses `TabIndex`. A scan of every `Grid` compared the visual order (`Grid.Row`,
`Grid.Column`) of the interactive children with their document order. They match in every file.
The only difference is the display options `Popup` in `WorkspaceHeader.axaml`, which is its own
focus scope. Setting `TabIndex` would only add a second order to keep in sync, so this branch
sets none. The real tab-order problem is A11Y-05.

The two read-only TextBoxes in `src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml:251` and
`:262` have `Focusable="False"`. That is fine for Tab, and they now have names.

## Fixes applied on this branch

| Commit | Change | Controls |
|---|---|---|
| `fix(a11y): name icon-only buttons for screen readers` | A11Y-01 | 80 |
| `fix(a11y): name buttons whose content is a text block` | A11Y-02 | 87 |
| `fix(a11y): name form inputs from their visible labels` | A11Y-03 | 145 |
| `test(a11y): guard automation names on icon-only buttons` | guard for A11Y-01 | 1 test |

Each commit only adds `AutomationProperties.Name` attributes. Rendering does not change. The
source-hash snapshot `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json`
tracks the XAML text of nine files. The entries of the touched files were refreshed with
`FH_UI_BASELINE_MODE=apply`.

Verification after each commit, on Linux with .NET SDK 10.0.301:

- `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`: 0 errors, no XAML warnings.
- `dotnet test tests/FreeformHelper.Tests`: 2942 passed (2943 with the guard), 43 skipped,
  3 failed. The same 3 tests fail on the base commit in this environment:
  `NotchDisplayProjectorTests.Build_FromPadInspectorSnapshot_ProducesSharedDisplayProjection`,
  `PadInfoViewModelTests.CadPadInfo_UsesSharedNotchDisplayProjectionTexts` and
  `FreeformHelperViewModelTests.ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads`.
  The last one compares `\r\n` output, so it looks like a Linux line-ending issue.
- Not run: `scripts/dev/prepare-ui-workspace.ps1`, `scripts/tests/lint.ps1` and
  `eng/core-health/repo-health.ps1`, because this environment has no `pwsh`. Line endings were
  kept CRLF by hand. The private `example/` submodule was not available, so the example-data
  tests were skipped.

## Suggested order for the open items

1. A11Y-04 (one control change fixes about 90 fields) and A11Y-05 (one style plus a guard test).
2. A11Y-02 and A11Y-03 open rows: bound names and row automation names.
3. A11Y-06 and A11Y-07 dialog keys, after the owner decides about Enter in `ConfirmDialog`.
4. A11Y-08 focus ring, A11Y-09 canvas names, then A11Y-10 and A11Y-11.
