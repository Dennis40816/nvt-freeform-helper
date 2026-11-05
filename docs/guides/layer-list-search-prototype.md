# Layer list search (prototype)

Status: prototype on branch `cloud/layer-search-main`. Not merged. No issue is linked yet.

## What it does

The left panel (`LeftDxfPanel`) has a small search box above the DXF layer list. Typing filters the list by layer name. The clear button (close icon) empties the box. While a search is active, a muted line shows "N of M layers".

Matching rule: the query is split on whitespace, and a layer is shown when its name contains every term, ignoring case. A blank query shows every layer. Example: `pad top` matches `PAD_TOP` and `TOP_PAD_2`, not `PAD_BOTTOM`.

The search only hides rows. It never changes which layers are selected. Toggling a row in a filtered list changes the real layer selection, because the filtered list holds the same `LayerToggle` objects.

## How it works

| Piece | File | Role |
| --- | --- | --- |
| Matcher | `src/FreeformHelper.Application/Services/LayerNameSearch.cs` | Pure match rule. The single source of truth for "does this name match". |
| ViewModel | `src/FreeformHelper.UI/ViewModels/LayerListSearchViewModel.cs` | Holds `SearchText`, projects `VisibleLayers`, exposes `HasSearchText`, `MatchSummary`, `ClearSearchCommand`. Rebuilds the projection when the text or the source collection changes. |
| Hook | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.LayerSearch.cs` | `LayerSearch` property, plus `OnLayerTogglesChanged` that re-attaches the search when a DXF load replaces `LayerToggles`. |
| View | `src/FreeformHelper.UI/Views/LeftPanelSections/LayerListSearchBox.axaml` | `TextBox` + clear `Button` + summary text. Compiled bindings (`x:DataType`). Uses only existing tokens and styles: `Space4`, `workspaceSearchClearButton`, `iconSm`, `stepNote`. No new colors, sizes, or fonts. |
| Panel change | `src/FreeformHelper.UI/Views/LeftDxfPanel.axaml` | Adds the search box and binds the layer `ItemsControl` to `LayerSearch.VisibleLayers` instead of `LayerToggles`. |

Tests: `tests/FreeformHelper.Tests/Application/Services/LayerNameSearchTests.cs`, `tests/FreeformHelper.Tests/UI/ViewModels/LayerListSearchViewModelTests.cs`, and two static guard lines in `UiLayoutGuardTests` for the panel hook.

## Side effects (explicit)

- Changing the search text: rebuilds `VisibleLayers`. No selection change, no canvas rebuild, no fit/zoom, no unsaved mark.
- Loading a new DXF: the search re-attaches to the new layer list. The search text is kept.
- The search text is not saved in the project or the app settings.

## What is left

1. **Bulk toggle scope.** The "All / None" bulk toggle above the list still acts on all layers, including the hidden ones. Decide whether it should act only on the visible matches while a search is active. If yes, route it through `LayerListSearchViewModel.VisibleLayers` in the existing single bulk-toggle entry point.
2. **Ctrl+click solo.** `FreeformHelperView.OnLayerTogglePointerPressed` uses the row's `LayerToggle` object, not an index, so it stays correct while filtered. Solo still hides the layers that the search hides too. Confirm that this is the wanted behavior.
3. **Run on real input.** Not yet run end to end on a real DXF. This cloud box has no display and the private `example/` submodule was not initialized. Run the app, load a DXF with many layers, and check typing, clearing, and toggling.
4. **Dev page preview.** The repo rule asks for a Dev page preview of new control use. The box reuses existing styles only, but it has not been checked on the Dev page or in the dark theme.
5. **Headless smoke test.** Add a headless test that opens `LeftDxfPanel`, types into the box, and checks the rendered row count.
6. **Lint and health gates.** `scripts/tests/lint.ps1` and `eng/core-health/repo-health.ps1` need `pwsh`, which this box does not have. Run both before opening a PR.
7. **Docs.** If kept, add a line to `docs/reference/behavior-inventory.md` and a `ROADMAP.md` entry with an issue link.
8. **Rename only.** Layers renamed in place (not replaced) do not refresh the filter. Today names are never changed in place; the list is replaced on load.
