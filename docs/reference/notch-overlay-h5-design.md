# H5 Overlay clarity consolidation design
Last updated: 2026-02-22  
Status: Phase 0~4 completed (policy + UI switches + token consolidation + test/documentation synchronization)

## 1. Background
- `H3` completed: `ToRegular` labels and the `ToFull` overlay can be displayed separately.
- `H4 Phase 1` completed: AA preview can be recalculated immediately when Step3 parameters change.
- The next phase, `H5`, aims to consolidate Step3 overlay "semantics, visibility, and color rules" into a single model to avoid confusion for both users and implementation.

## 2. Current pain points (to consolidate)
1. "Compute switches" and "display switches" coexist, but their naming/copy is not fully aligned.  
   `EnableToFull` (computation) vs `ShowNotchCanvasPreview` (display) are easily mistaken for the same thing.
2. The AA quick toolbar has only a `To Full` display switch and lacks a `To Regular label` display switch.
3. The Step3 legend is static and does not clearly reflect the overlay layers actually enabled at the moment.
4. Color and alpha rules for seed/candidate/final are scattered across rendering code, with some still relying on fallback palettes, making them insufficiently controllable.
5. `H6` (step-by-step visualization + autoplay) needs a clear layer model as its foundation; otherwise, behavior will continue to accumulate complexity.

## 3. H5 goals
1. Define all Step3 visual layers through a single overlay model.
2. Separate "computation availability" and "display visibility" into layers and provide clear UI copy.
3. Make stage display rules, color tokens, and draw order predictable.
4. Preserve project snapshot compatibility without breaking existing saved fields.

## 4. Non-goals
- Do not change the To Full geometry algorithm in H5.
- Do not change Step5 export formulas or fields in H5.
- Do not complete the autoplay behavior redesign in H5 (scheduled for `H6`).

## 5. Overlay model (unified semantics)
Manage display by layer, with four fixed layers:

| Layer | Semantics | Data source | Affected by Stage |
| --- | --- | --- | --- |
| `ToRegularLabel` | `To Regular ratio` text for each CAD | `NotchCanvasPreviewItem.ToRegularRatio` | No |
| `ToFullSeed` | Stage1 seed overlap | `ToFullSeedPolygons` | Yes (=1) |
| `ToFullCandidate` | Stage2 candidate regular | `ToFullCandidatePolygons` | Yes (=2) |
| `ToFullFinal` | Stage3 final outline/fill | `ToFullFinalOutlinePolygons` / `ToFullPolygons` | Yes (=3) |

## 6. Visibility rules (single entry point)
Input state:
- `HasPreviewData`: `NotchCanvasPreviewItems.Count > 0`
- `EnableToFull`: Step3 compute switch
- `ShowToFullOverlay`: display switch (continues to use `ShowNotchCanvasPreview`)
- `ShowToRegularLabels`: new display switch (default `true`)
- `PreviewStage`: 1~3

Rules:
1. `ToRegularLabel` display condition: `HasPreviewData && ShowToRegularLabels`
2. Overall display condition for `ToFull*`: `HasPreviewData && EnableToFull && ShowToFullOverlay`
3. `ToFullSeed`: overall condition and `PreviewStage == 1`
4. `ToFullCandidate`: overall condition and `PreviewStage == 2`
5. `ToFullFinal`: overall condition and `PreviewStage == 3`

Suggested implementation:
- Add a policy (such as `NotchOverlayVisibilityPolicy`) to calculate the results above centrally.
- ViewModel and PadCanvas use the policy output consistently instead of scattering `if` conditions directly.

## 7. UI/copy design
1. Step3 (Right panel + SettingsWindow) clearly distinguishes:
   - Computation: `To Regular EN (2.2)`, `To Full EN (2.2)`
   - Display: `Show To Regular labels`, `Show To Full overlay`
2. The Canvas quick toolbar shows two display switches:
   - `To Regular`
   - `To Full`
3. Change the Stage legend to "Layer legend", preserving stage semantics while adding an indication of the current active/inactive state.
4. Fixed tooltip wording:  
   `Compute switch affects ratio/result.` / `Display switch affects AA overlay only.`

## 8. Token consolidation direction
Keep existing token names, fill gaps, and use no inline color values:
- Add (or explicitly separate) `ColorNotchToFullSeedFill`
- Add overlay alpha/line-width tokens (seed/candidate/final independently adjustable)
- Continue using existing `ColorCanvasNotchRatio*` for `ToRegular label` background/border/text colors to avoid introducing another set of names

## 9. Phase breakdown (H5)
### Phase 0 (design inventory) `[Done]`
- Produce this document and freeze the layer model and visibility rules.

### Phase 1 (visibility policy consolidation) `[Done]`
- Add an overlay visibility policy + a single ViewModel entry point.
- Change `PadCanvas` to drive all four display layers using policy results.

### Phase 2 (UI display switch consolidation) `[Done]`
- Add the `Show To Regular labels` setting (SettingsWindow + quick toolbar).
- Update Step3 copy and tooltips together to clearly separate computation vs display.

### Phase 3 (color and drawing rule consolidation) `[Done]`
- Drive all Seed/Candidate/Final palettes and alpha through tokens.
- Confirm draw order: `seed -> candidate -> labels -> final(top)`.

### Phase 4 (tests and documentation) `[Done]`
- Tests:
  - visibility policy unit tests (complete state matrix)
  - ViewModel behavior tests (setting changes take effect immediately)
  - UI guard (required Step3/SettingsWindow fields exist)
- Documentation:
  - `docs/core/notch-2.2-spec.md` (overlay section)
  - `docs/reference/behavior-inventory.md` (Step3 display switch side-effect)
  - Synchronize progress in `TODO.md`

## 10. Acceptance criteria
1. Users can explicitly control `To Regular` and `To Full` display independently.
2. When `EnableToFull=false`, `ToRegular` can still be displayed independently.
3. Stage switching affects only the three To Full layers, not the To Regular label.
4. Add no inline colors/dimensions; all changes use tokens.
5. `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release` passes.

## 11. Risks and rollback
- Risk: naming changes may affect existing bindings and snapshot fields.
- Control: initially retain `ShowNotchCanvasPreview` as a compatibility field, and use backward-compatible defaults for new fields.
- Rollback: if UI copy changes cause confusion, retain the old tooltip wording initially and finalize naming consolidation in H6.
