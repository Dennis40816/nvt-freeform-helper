# Refactor Test Plan (Phase 1~2 Baseline)
Last updated: 2026-02-10

## 1. Purpose
- Lock down the existing acceptable behavior before the final overlap / notch refactoring to prevent functional regressions during refactoring.
- Establish a "feature-to-test" matrix: every user-visible feature must have at least one repeatable verification entry point (unit / integration / VM / UI guard).

## 2. Test Layers and Execution Order
1. **Domain / Application Unit** (fastest, run first)  
   `tests/FreeformHelper.Tests/Application/**/*Tests.cs`
2. **ViewModel behavior tests** (commands and state transitions)  
   `tests/FreeformHelper.Tests/UI/ViewModels/*Tests.cs`
3. **UI Guard tests** (guards against XAML layout/token mistakes)  
   `tests/FreeformHelper.Tests/UI/Snapshots/UiLayoutGuardTests.cs`
4. **Overall build gate**  
   `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`

Gate rules (during refactoring):
- PR / daily integration must pass all of 1~4 at a minimum.
- A new feature without tests must not replace the existing workflow (it may only be behind flag).

## 3. Feature Coverage Matrix

### A. DXF Import/Filtering/Editing
- A01 Importing closed polylines filters correctly.
- A02 CAD output pads are correct after a layer toggle, and hidden pads do not flow back in.
- A03 DXF hide / restore last / restore all are reversible and do not damage indexes.
- A04 The pad count from Export DXF (visible/all) matches the current state.

### B. Grid Construction and Indexing
- B01 Grid construction aligned to Panel AA is correct (row/col/count).
- B02 DXF bounds alignment falls back to Panel AA when no DXF is available, with an explicit status.
- B03 Bounds are correct when a bound layer is specified (still usable when hidden).
- B04 After a Scan order change, regular diff idx is reordered correctly.
- B05 Grid construction still works after removing legacy limits (high X / high IC).

### C. CAD↔Regular overlap / mapping
- C01 overlap match reports progress (0→1) without interrupting the UI.
- C02 When a CAD spans multiple regulars, bidirectional links are complete (CadToRegular + RegularToCad).
- C03 unmatched threshold affects only marking; direct/draft changes only redraw, without rebuilding the grid or changing links.
- C04 index diagnostics can produce count mismatch / low confidence / ambiguous categories.
- C05 mapping override apply/clear can be persisted and remains consistent after load.

### D. Freeform Detection and Manual Overrides
- D01 auto-detect (X dominant) marks XWay.
- D02 auto-detect (Y dominant) marks YWay.
- D03 When dominant spread is below TH, it remains None.
- D04 Enabling `EnableAutoDetectXy` allows XYWay classification; when disabled, XYWay must not be produced automatically.
- D05 Manual overrides (None/X/Y/XY) affect only the selection and can be undone.

### E. Notch and Output
- E01 v2.1 output format is stable (fixture comparison).
- E02 legacy output remains usable (compatibility tests must not be deleted).
- E03 The notch TH gate excludes output below the threshold.
- E04 Switching mode (legacy/new) allows result comparison with the same input and must not crash.

### F. Save/Load / Migration
- F01 grid/match/freeform/overrides can be restored after Project save/load.
- F02 embed DXF roundtrip is correct (loading remains possible without the original dxf path).
- F03 migration is append-only: old fields are retained and unknown fields are not lost.
- F04 Settings window submit (Save) and cancel (Cancel) behavior is consistent and should not immediately contaminate the main state.

### F-Plus. Save Project Focus (Protect Before Refactoring)
- F05 Save project: when the user cancels path selection, no file is written, `HasUnsavedChanges` is unchanged, and `_lastSavedPath` is not contaminated.
- F06 Save project: when the dialog handler is not bound, return failure with an explicit status (to avoid silent failure).
- F07 Save project: on IO/serialization exceptions, status and log provide traceability, and existing in-memory state must not be partially overwritten.
- F08 Save project: after success, `HasUnsavedChanges=false`; modifying any persisted setting again must make it dirty again.
- F09 Save project: both yes/no branches of embed DXF require verification (including consistency between warning messages and output file contents).
- F10 Save -> Load roundtrip: hidden CAD / mapping overrides / UI snapshot / notch settings remain consistent.

Current automated baseline (already exists):
- `ProjectPersistenceUseCaseTests`: save/load, embed/no-embed, UI snapshot roundtrip.
- `ProjectStoreTests`: project json access and field roundtrip.
- `FreeformHelperViewModelTests`: `SaveProjectAsync_WhenDialogHandlerMissing_ReturnsFalse`.

### G. UI/UX Guard (Test Structure, Not Artwork)
- G01 Scroll content must not bind `Bounds.Width` (to avoid clipping/overlap).
- G02 Key containers must use the viewport-bounded width pattern.
- G03 Important buttons/input controls must use tokens (hard-coded colors/sizes are prohibited).
- G04 Analysis section Step 1~4 wording and operation order remain consistent.
- G05 Minimum visual snapshot baseline: key UI file hashes must match the baseline (intentional UI changes require a corresponding baseline update).
- G06 Advanced visual snapshot baseline: compare actual headless rendering using aHash + Hamming tolerance (to capture actual layout/color changes).

## 4. Near-Term Test Priorities (Starting This Refactoring Round)
- P0: C02, C04, D01~D04, F01, F03 (protect the mapping/freeform/save-load core first)
- P1: A03, C05, E01, E04 (ensure incremental replacement during refactoring)
- P2: G series (keep adding guards to prevent UI regressions)

## 5. Execution Scripts
```powershell
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj
dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj

# UI baseline 更新（先 dry-run，再 apply）
./scripts/tests/update-ui-baseline.ps1 -Mode DryRun
./scripts/tests/update-ui-baseline.ps1 -Mode Apply
```

## 6. Definition of Done (DoD)
- All P0 items in the test matrix have automated tests.
- The new workflow (overlap-allocation notch) can be compared side by side with legacy results under a flag.
- No new hard-coded style; UI guard tests pass.

