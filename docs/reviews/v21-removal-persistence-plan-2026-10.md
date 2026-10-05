# Complete V21／Legacy Removal: Persistence Compatibility Plan

This document only inventories the persistence impact and policy options for the TODO item "Complete V21／Legacy Removal". The owner has confirmed that 「2.1」 means V21 and still chooses to retain V21 and `LegacyRegularAnchor`; this document does not authorize removal, propose a new product or file format version, or schedule the order or date of removal.

The basis is the code and test declarations on the current branch `feature/queue/v21-removal-persistence-plan`, the [dependency graph](../generated/project-dependency-graph.md), the [Notch contract](../reference/notch-system-reference.md), the [settings entry matrix](../guides/settings-entry-matrix.md), and the [owner decisions in the 1.3.x roadmap](../guides/refactor-roadmap-1.3.x.md). The following describes the current state and recommendations that have not been adopted; no private example data was read, and no golden or baseline was inspected or changed.

## 1. File Format and Enum Contracts

| Carrier／value | File: symbol | Confirmed format and current state |
|---|---|---|
| Project JSON | `src/FreeformHelper.Infrastructure/Project/ProjectSchema.cs:ProjectSchema.CurrentVersion`; `ProjectFile.cs:ProjectFile.SchemaVersion`; `JsonProjectStore.cs:JsonProjectStore.Options` | The current `schemaVersion` is the string `"2"`; JSON fields use camelCase, and enums use numeric values, with no string-enum converter. All project fields in the next section can be carried by format `"2"`. |
| App whitelist JSON | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:AppGeneralSettingsDocument.CurrentSchemaVersion/Save` | The current `SchemaVersion` is the integer `2`; fields retain PascalCase. It contains only `View`, `Import`, and `Behavior`, with no `Settings.Notch` or `UiSnapshot.Notch`. |
| Old app JSON | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:LegacyAppGeneralSettingsDocument/TryLoad/LooksLikeWhitelistShape` | `AppGeneralSettingsStoreTests.TryLoad_LegacyDocument_MapsToWhitelistAndDefaultsBehavior` explicitly lists the old shape with integer `SchemaVersion = 1`. The reader selects the shape based on the presence of top-level `View`／`Import`／`Behavior`, not the version number; the old shape maps only `UiSnapshot.View` and `UiSnapshot.Import`. |
| V21 | `src/FreeformHelper.Domain/Notch/NotchAlgorithmVersion.cs:NotchAlgorithmVersion.V21` | Numeric value `21`; project `settings.notch.enabledVersions` can contain this value. |
| V22 | Same file: `NotchAlgorithmVersion.V22` | Numeric value **`30`**, not `22`; removing V21 does not permit renumbering this persisted value. |
| Legacy mode | `src/FreeformHelper.Application/Settings/NotchComputationMode.cs:NotchComputationMode.LegacyRegularAnchor` | Numeric value `0`; project `settings.notch.computationMode = 0` is an explicit selection of the old mode. |
| CAD mode | Same file: `NotchComputationMode.CadAllocation` | Numeric value `1`; this is also the initialization default for `NotchSettings.ComputationMode`, so a missing field is not equivalent to Legacy `0`. |
| V21 C export option | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Models.cs:FreeformHelperViewModel.NotchExportFileType.Cv21` | Implicit enum value `1` (`Csv = 0`, `Cv22 = 2`); the project／app actually writes the string `"Cv21"`, not this enum's numeric value. |

The version and fields of format `"2"` are supported by code／test evidence; the available data is insufficient to determine the historical format in which each field first appeared. `ProjectFileMigrator.MigrateInPlace` only fills in data such as blank versions and null collections; it does not dispatch by `"1"`／`"2"` or migrate V21／Legacy. Other nonblank version markers currently have no corresponding rejection check. Old files must therefore be handled by inspecting their content, not just their version number. Files with no version or other version markers may be accepted by the same reader; this does not establish a verified historical format contract for them.

## 2. Complete Inventory of Selection Entry Points and Persisted Fields

### 2.1 Settings and Entry Points That Directly Select V21／Legacy

| Setting／entry point | File: symbol | Persisted result／format |
|---|---|---|
| Algorithm set and defaults／normalization | `src/FreeformHelper.Application/Settings/NotchSettings.cs:NotchSettings.EnabledVersions/ReplaceEnabledVersions/DefaultEnabledVersions/NormalizeEnabledVersions` | Project `"2"`: `settings.notch.enabledVersions`, for example `[21]` or `[21,30]`. The default includes both V21／V22; a missing field, null, an empty list, or a list containing only unsupported values falls back to the two-version default. Currently, `[30,31]` retains only `30`. |
| Computation mode | Same file: `NotchSettings.ComputationMode`; `src/FreeformHelper.Application/Settings/ProjectSettings.cs:ProjectSettings.ValidateOrThrow` | Project `"2"`: `settings.notch.computationMode`; `0` selects Legacy, and `1` selects CAD. Validation requires a defined enum value. V21／V22 and computation mode are independent dimensions; a V22-only file may still select Legacy. |
| Live V21 toggle | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.Notch.cs:FreeformHelperViewModel.EnableV21/EnableV22`; `FreeformHelperViewModel.Settings.Sync.cs:LoadSettingsToUi/ApplyUiToSettings` | Projects／writes back the version set above; there is no separate `enableV21` JSON field or app algorithm toggle field. |
| Settings draft V21 toggle | `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:SettingsWindowViewModel.EnableV21/EnableV22`; `FreeformHelperViewModel.SettingsWindow.cs:ApplySettingsWindowDraft`; `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml:EnableV21 binding` | Draft Save applies to the live toggles, which are then persisted through the version set in project `"2"`; Cancel does not create another set of persisted settings. |
| C v2.1 file type and pinned version | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:FreeformHelperViewModel constructor`; `FreeformHelperViewModel.Models.cs:NotchExportFileTypeOption.PinnedVersion`; `NotchExportFileTypeMetadata.cs:TryGetPinnedVersion` | The `Cv21` option has `PinnedVersion = V21`; the file type preference is stored as the string in the next table, while `PinnedVersion` itself has no JSON field. The file type can request a V21 export, so clearing only the version set while retaining this option is insufficient. |
| Version／type selection in Export Notch Rows | `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs:SelectedVersionOption/SelectedExportTypeOption/BuildVersionOptions`; `FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection/ApplyNotchExportTypeSelection` | The window can select V21, and C types pin the version; applying the selection updates the live toggles and file type preference, which are persisted when the project is saved. There is no other project version field; `All versions` retains the original project set. |

Legacy mode is loaded and retained from project settings, with no corresponding mode-switch editor for normal operators; `ApplyUiToSettings` does not reset it to the CAD default. The existing `LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields` explicitly protects a "Legacy + V22" roundtrip. Other matching／To Full settings whose names contain Legacy are not equivalent to `LegacyRegularAnchor` and are excluded from this removal plan.

### 2.2 Project JSON Fields

Where the full prefix is not repeated for the symbols below, `ProjectUiSnapshot.cs` is in `src/FreeformHelper.Infrastructure/Project/`, and `NotchSettings.cs` is in `src/FreeformHelper.Application/Settings/`.

| JSON path | File: symbol | Format／purpose and old values |
|---|---|---|
| `settings.notch.enabledVersions` | `NotchSettings.cs:NotchSettings.EnabledVersions` | Project `"2"`; numeric `21` is V21, and `30` is V22; the sole authoritative algorithm set. |
| `settings.notch.computationMode` | `NotchSettings.cs:NotchSettings.ComputationMode` | Project `"2"`; `0` is Legacy, independent of the version set. |
| `uiSnapshot.notch.enabledVersions` | `ProjectUiSnapshot.cs:UiNotchSnapshot.EnabledVersions` | Project `"2"` can carry old string lists, for example `["V21","V22"]`. Marked `LegacyUiSnapshotField`; currently, `BuildUiSnapshot` does not write it, and UI loading does not use it to restore versions. However, a direct `JsonProjectStore.Save` can write a non-null list again, so not every save can be said to exclude this field. |
| `uiSnapshot.view.notchExportFileType` | `ProjectUiSnapshot.cs:UiViewSnapshot.NotchExportFileType` | Project `"2"`; `"Cv21"` selects C v2.1, with default `"Csv"`. Written by `FreeformHelperViewModel.UiSnapshot.cs:BuildUiSnapshot`; `FreeformHelperViewModel.Operations.cs:ApplyViewSnapshot` parses it and selects an existing option. |
| `settings.notch.thresholdQ7`, `settings.notch.linkVersionThresholds` | `NotchSettings.cs:NotchSettings.ThresholdQ7/LinkVersionThresholds` | Project `"2"`; does not select a version, but persists the V21 Q7 threshold and cross-version linking (default true). When linked, V22 also uses `ThresholdQ7 * 100 / 128`. |
| `settings.notch.thresholdPercentV22` | `NotchSettings.cs:NotchSettings.ThresholdPercentV22` | Project `"2"`; this independent V22 threshold applies only when unlinked. Retaining this field alone is insufficient to preserve the results of linked projects. |
| `settings.notch.lenScale` | `NotchSettings.cs:NotchSettings.LenScale` | Project `"2"`; the V22 legacy 9-column length scale for LegacyRegularAnchor (`V22LegacyRowStrategy.cs:51`, `:76`–`:77`), default `10`; unused by V21 and CadAllocation; not a V21／Legacy selector. |
| `uiSnapshot.notch.thresholdQ7`, `uiSnapshot.notch.linkThresholds`, `uiSnapshot.notch.thresholdPercentV22`, `uiSnapshot.notch.lenScale` | `ProjectUiSnapshot.cs:UiNotchSnapshot.ThresholdQ7/LinkThresholds/ThresholdPercentV22/LenScale` | Project `"2"`; the snapshot currently still writes these copies, but settings loading uses `Settings.Notch` as authoritative and does not rederive thresholds or versions from the copies. |

The last four rows are companion fields that must not be overlooked during removal, not additional algorithm toggles. `NullValue`, `ExportProfile`, and compensation settings are shared parameters and do not become ignorable data when V21 is removed. Projects persist inputs and settings, not generated Notch tables or C bytes; the runtime row's `Version` is not another project field either.

### 2.3 App Settings Fields and Old Shape

| JSON path | File: symbol | Format／current state |
|---|---|---|
| `View.NotchExportFileType` | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:AppGeneralSettingsDocument.View` → `src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:UiViewSnapshot.NotchExportFileType` | App `2`; can persist `"Cv21"`. `FreeformHelperViewModel.AppSettings.cs:CloneViewSnapshot/PersistAppGeneralSettingsNow` copies and saves it, and it is restored at startup. |
| `UiSnapshot.View.NotchExportFileType` | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:LegacyAppGeneralSettingsDocument.UiSnapshot/TryLoad` → the same `UiViewSnapshot.NotchExportFileType` | Old app `1` shape; maps to the current `View`, so `"Cv21"` does not disappear automatically. |
| `UiSnapshot.Notch.EnabledVersions` | Same file: `LegacyAppGeneralSettingsDocument.UiSnapshot/TryLoad` → `ProjectUiSnapshot.cs:UiNotchSnapshot.EnabledVersions` | The old app `1` shape can carry V21 strings; after deserialization, they are not mapped into the whitelist and do not select an algorithm. |
| `UiSnapshot.Notch.ThresholdQ7/LinkThresholds/ThresholdPercentV22/LenScale` | The same old document → corresponding properties of `ProjectUiSnapshot.cs:UiNotchSnapshot` | Companion copies in the old app `1` shape; likewise not mapped into the whitelist. `UiNotchSnapshot.ComputationMode` does not exist. |
| `Settings.Notch.EnabledVersions/ComputationMode` and companion settings | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:LegacyAppGeneralSettingsDocument/TryLoad` | In old app `1`, the full `Settings` shape can carry numeric V21／Legacy values; the current compatibility DTO **has no `Settings` property**, so these are unknown fields that are skipped directly, not currently effective app settings. The old fixture proves the top-level `Settings` shape, not the historical point at which each Notch field was first written. |
| `Behavior.ApplyVisualPreferencesOnProjectLoad` | Same file: `AppGeneralBehaviorSettings.ApplyVisualPreferencesOnProjectLoad`; `FreeformHelperViewModel.AppSettings.cs:TryApplyAppGeneralVisualPreferencesAfterProjectLoad` | App `2`; not a selector, but when true, app `View` overrides the project view after project loading, including the C type preference. |

Unversioned old app files are also read by shape; `SchemaVersion` cannot establish which old fields are applied. The current whitelist has no app `EnableV21`, `NotchAlgorithmVersion`, or `ComputationMode` field. Old app algorithm／mode fields should not be promoted to a new project source-of-truth.

## 3. Post-removal Read Policy Options and Recommendations

This section only addresses compatibility choices "if the owner separately agrees to complete removal"; it does not change current loading behavior. V21 → V22 and Legacy → CAD are two different behavioral changes; neither can be claimed to be equivalent or byte-exact.

### 3.1 User Consequences of the Three Options

| Loading strategy | Consequences for old projects | Consequences for old app settings |
|---|---|---|
| Migrate to V22／CadAllocation | Change `21` to `30` and Legacy `0` to CAD `1`; the project can open, but rows, compensation, export ABI／C bytes may change, and saving loses the original selections. Thresholds must also be handled according to their existing effective values. Explicitly state "Converted; the original V21／Legacy output cannot be reproduced", instead of showing only a normal loading success message. | The `Cv21` preference can change to `Cv22`; the user will next see the C v2.2 option and must know that its firmware contract differs. Old algorithm／mode fields are already ignored, so they need not be migrated into effective app algorithm defaults. |
| Reject clearly | Preserve the original file; the user must open it with an older version that still supports V21／Legacy and cannot re-export the same C in the removal version. The message must identify the path／field／old value and reason, rather than reporting only an invalid enum. | Rejecting the entire file would also lose the user's other visual and import preferences. The current `TryLoad` only logs a warning and returns null, which is not equivalent to providing a clear user message. |
| Continue reading and ignore | Suitable for obsolete UI copies. Ignoring the actual version set／mode and substituting V22／CAD defaults is effectively an unannounced conversion; the old file appears to open successfully, but its output cannot be reproduced. | Suitable for old algorithm／mode／Notch copies already excluded by the whitelist; retain other preferences. If a failed `Cv21` parse only retains the current option, the user may get CSV or the previous preference, making the result depend on session state. |

### 3.2 Per-field Recommendations

| Old data condition | Recommendation | User-visible result |
|---|---|---|
| `settings.notch.enabledVersions` contains `21`, including `[21,30]` | **Reject clearly**; do not remove `21` unilaterally or convert versions automatically. | The user knows that this project requires V21 and that re-exporting the same C requires an older version that retains compatibility; the original file is not rewritten. Even if `[21,30]` contains V22, the owner cannot be assumed to have agreed to abandon V21 delivery. |
| `settings.notch.computationMode = 0`, even with only `[30]` | **Reject clearly**. | The user knows that this project requires Legacy; clearing V21 alone is still insufficient for safe opening. |
| An old file has missing／null／empty／entirely unsupported `enabledVersions`, or the whole `Settings.Notch` is missing | **Clearly reject this ambiguity**; do not silently treat the old V21＋V22 default as V22. | Explicitly state that the old file depends on a historical default containing V21 and that the available data cannot establish the original output version; verify with an older version before deciding whether to accept conversion. |
| Explicit `[30]`, with mode `1` or missing | **Retain V22／CadAllocation**; a missing mode keeps the current CAD initialization semantics. | An old format marker or redundant UI V21 strings do not cause incorrect rejection of a project already using V22／CAD. |
| `uiSnapshot.notch.enabledVersions`, old app `UiSnapshot.Notch`, and unknown `Settings.Notch` | **Continue reading and ignore**; assess the project only by authoritative settings, without deriving algorithms from copies. | Outdated strings do not switch it back to V21／Legacy, and other usable data is not discarded. This does not mean project `settings.notch` can be ignored. |
| Project `uiSnapshot.view.notchExportFileType = "Cv21"` or the same value in app `View`／old `UiSnapshot.View` | If the remaining data is readable, **explicitly migrate to the `Cv22` preference**, without automatically exporting. | Display "The original C v2.1 preference has changed to C v2.2; the original C cannot be reproduced. Please confirm firmware support." The app preference itself should neither prevent opening a usable V22／CAD project nor override rejection of actual V21／Legacy settings. |
| V22／CAD projects: Q7／linking fields | If these effective fields are completely removed, **migrate the effective V22 threshold**; retain `ThresholdPercentV22` when unlinked and use the existing `ThresholdQ7 * 100.0 / 128.0` when linked, without switching to snapshot copies. | The effective V22 threshold retains its original value; cross-version linking is no longer displayed. The two decimal places used for UI display must not be treated as algorithm precision. |
| `LenScale` and Q7／linking copies in the Notch UI | **Retain compatible reading and ignore copies no longer used**; their presence does not select V21. | A project already using V22／CAD does not fail to load because of obsolete display fields. This recommendation does not delete shared `NullValue`, profile, or compensation values. |

The proposed rejection message is: "This project's `settings.notch.enabledVersions` contains V21 (21)／`settings.notch.computationMode` is LegacyRegularAnchor (0). This version cannot reproduce the original output; the original file was neither loaded nor rewritten. Please open it with an older version that still supports that mode." This is proposed content, not a UI text change in this task.

The policy should use the existing `JsonProjectStore.Load` → `ProjectFileMigrator.MigrateInPlace` → `ProjectSettings.ValidateOrThrow` and `AppGeneralSettingsStore.TryLoad` mechanisms, without creating another persistence layer. Removing members from the current numeric enums does not guarantee immediate rejection during deserialization: the version set first passes through `NormalizeEnabledVersions`, which may drop `21` and then apply defaults; mode `0` may fail only during enum validation. Therefore, rejection must still be able to identify the original old values, without letting normalization erase the evidence first. The existing error result／status in `LoadProjectFromPathAsync` can carry a clear message; do not duplicate migration decisions in the UI.

Loading should not automatically save the original file. The rules for app settings entering deferred mode after a successful project load and flushing only on the next successful Save Project still apply; rejection, cancellation, or save failure must not write app preferences early. Only top-level `ProjectFile.ExtensionData` preserves unknown fields, which does not guarantee lossless roundtrips for nested Notch fields after removal. Reading compatibility fields does not mean retaining the old computation path.

## 4. Test Name Inventory

Only names are listed below; this task does not modify tests, fixtures, or expected values.

### 4.1 Existing Expectations That Must Change

- `ProjectFileMigrationTests.Load_MigratesNullCollections`
- `FreeformHelperViewModelTests.LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields`
- `FreeformHelperViewModelTests.SaveThenLoadProject_PersistsStep5NotchVersionSelection`

### 4.2 Existing Roundtrip／Contract Tests Affected by Whether Compatibility Fields Are Retained

- `ProjectStoreTests.SaveAndLoad_RoundTripsMatchingAndIndexMappingSettings`
- `UiSnapshotPersistenceContractTests.SectionContract_MatchesAllSnapshotSettableProperties`

### 4.3 Existing Load／Save／Roundtrip Tests Whose Coverage Should Be Extended

- `ProjectFileMigrationTests.Load_DropsUnsupportedLegacyNotchVersions`
- `ProjectFileMigrationTests.LoadAndSave_PreservesUnknownRootProperties`
- `ProjectStoreTests.SaveAndLoad_RoundTripsSettings`
- `ProjectPersistenceUseCaseTests.SaveAsync_WritesMatchingAndMappingSettingsAndUiSnapshot`
- `ProjectPersistenceUseCaseTests.SaveThenLoadAsync_RoundTrip_PreservesCriticalProjectData`
- `AppGeneralSettingsStoreTests.SaveThenLoad_WhitelistShape_RoundTrips`
- `AppGeneralSettingsStoreTests.TryLoad_LegacyDocument_MapsToWhitelistAndDefaultsBehavior`
- `FreeformHelperViewModelTests.SaveThenLoadProject_RestoresStepAndExportSettingsFromProjectSnapshot`
- `FreeformHelperViewModelTests.ApplyViewSnapshot_Cv22ExportType_SelectsCv22`
- `FreeformHelperViewModelTests.SettingsLayerPrecedence_ProjectOverridesAppGeneralAndAppGeneralOverridesDefault`
- `FreeformHelperViewModelTests.LoadProject_WhenApplyAppVisualPreferencesEnabled_ReappliesAppVisualLayer`
- `FreeformHelperViewModelTests.AppGeneralSettings_AfterLoadProject_DefersUntilNextProjectSave`
- `FreeformHelperViewModelTests.SaveProjectAsync_WhenUserCancels_KeepsUnsavedState`
- `FreeformHelperViewModelTests.SaveProjectAsync_WhenIoError_ReturnsFalseAndKeepsUnsavedState`

### 4.4 New Cases Needed If the Recommendations Above Are Adopted

- `ProjectFileMigrationTests.Load_V21Only_RejectsWithCompatibilityMessage`
- `ProjectFileMigrationTests.Load_V21AndV22_RejectsWithoutDroppingV21Evidence`
- `ProjectFileMigrationTests.Load_V22WithLegacyRegularAnchor_RejectsWithCompatibilityMessage`
- `ProjectFileMigrationTests.Load_MissingNullEmptyOrUnsupportedVersions_RejectsLegacyDefaultAmbiguity`
- `ProjectFileMigrationTests.Load_V22WithMissingComputationMode_KeepsCadAllocation`
- `ProjectFileMigrationTests.Load_OldOrMissingSchemaVersion_UsesSameCompatibilityPolicy`
- `ProjectFileMigrationTests.Load_V22CadAllocation_IgnoresConflictingLegacyUiVersionStrings`
- `ProjectFileMigrationTests.Load_LinkedQ7Threshold_PreservesExactEffectiveV22Percent`
- `ProjectFileMigrationTests.Load_UnlinkedThreshold_PreservesIndependentV22Percent`
- `ProjectFileMigrationTests.Load_RemovedNumericEnumValues_DoesNotReinterpretAsSurvivingValues`
- `ProjectStoreTests.SaveThenLoad_V22CadAllocation_DoesNotReintroduceV21OrLegacySelections`
- `ProjectStoreTests.SaveThenLoad_CompatibilityFields_FollowsDeclaredReadAndWritePolicy`
- `AppGeneralSettingsStoreTests.TryLoad_LegacyNotchSettings_AreIgnoredWithoutDroppingOtherPreferences`
- `AppGeneralSettingsStoreTests.TryLoad_LegacyUiNotchVersionStrings_AreIgnored`
- `AppGeneralSettingsStoreTests.TryLoad_OldAndWhitelistCv21Preference_MapsToCv22`
- `FreeformHelperViewModelTests.LoadProject_UnsupportedNotchSettings_ShowsMessageAndPreservesCurrentProject`
- `FreeformHelperViewModelTests.LoadProject_Cv21Preference_ShowsConversionNoticeWithoutExporting`
- `FreeformHelperViewModelTests.LoadProject_AppCv21Overlay_DoesNotRestoreRemovedExportType`
- `FreeformHelperViewModelTests.LoadRejectedProject_DoesNotWriteProjectOrAppSettings`
- `FreeformHelperViewModelTests.LoadConvertedPreferences_CancelledOrFailedSave_DoesNotFlushAppSettings`

### 4.5 Alternative Cases Needed Only If Algorithm／Mode Conversion Is Allowed Instead

- `ProjectFileMigrationTests.Load_V21Selection_MigratesToV22AndReportsChangedOutputContract`
- `ProjectFileMigrationTests.Load_LegacyRegularAnchor_MigratesToCadAllocationAndReportsChangedComputation`
- `ProjectStoreTests.SaveThenLoad_MigratedNotchSettings_PreservesConvertedValues`
- `FreeformHelperViewModelTests.LoadMigratedProject_ShowsNonReproducibilityNoticeAndMarksUnsaved`

## 5. Risks and Open Decisions

- **Reproducibility of delivered C**: A saved project may have exported C with V21, but its JSON contains no C artifact, export history, or byte-exact evidence. The current `Cv22` preference also cannot prove that V21 was never delivered. V21 Q7 and V22 signed-percent firmware contracts differ; changing the version or computation mode cannot preserve the same C. If deliveries must be reproducible, a version with complete removal cannot provide that re-export capability; a usable older version and the original inputs／settings／existing delivered files are still required.
- **Retaining only V22 may still change results**: Legacy is independent of the version set; linked Q7 thresholds also still affect V22. Removing only the V21 enum／UI toggle misses two different risks. Application uses the full converted value, while the UI displays two decimal places; mistakenly using the displayed value may change rows at threshold boundaries.
- **Data loss and readback**: Removing enum members／properties may cause silent fallback, generic errors, or fields lost on the next save. The current schema is append-only, and unknown root-level data can be retained, but nested fields have no equivalent guarantee. The policy must explicitly decide which old fields are read-only, which are no longer written, and whether newly saved files can still be used by older versions; this document does not specify a new schema.
- **Cross-project preference contamination**: The app `View` overlay may replace a project's Cv22 selection with an old Cv21 preference. If removal relies only on parse failure to retain the current value, the result depends on startup／loading order. Both app conversion and project rejection must follow the existing deferred-write contract and test settings isolation.
- **Compatible reading mistaken for compatible computation**: Retaining the ability to read old strings／fields may avoid JSON failures, but does not mean V21 export or Legacy simulation remains available. Messages and roundtrip expectations must explain this capability difference; current zero-diff protection remains effective, and this plan does not permit baseline updates.
- **Pending owner decisions**: Whether to accept that delivered projects cannot be re-exported in the removal version; whether to adopt this document's rejection strategy or explicitly accept a non-equivalent V22／CAD conversion; whether to accept converting app／UI Cv21 preferences to Cv22, and the read／write retention policy for compatibility fields. These choices have not been adopted and do not change the 「目前先保留」 (retain for now) decision.

This delivery is limited to this persistence plan; it does not change production code, tests, TODO, roadmap, golden, or baseline, create conversion tools／configuration options, or propose removal implementation arrangements.
