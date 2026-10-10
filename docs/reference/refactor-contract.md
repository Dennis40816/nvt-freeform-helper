# FreeformHelper refactor contract

- Scope: the handoff groups `g1` to `g7` before `1.0.0`. They were named `1.3.0` to `1.3.5` before 2026-10-07; older commits and reviews use the old names.
- This document keeps the rules that every 1.0.x refactor slice must follow: the zero-difference policy, the Firmware C and Q7 contract, the gates G0 to G6, the golden update rules, the targeted suites, and the specs of the open slices.
- Status, target versions and order live in `ROADMAP.md`. This document does not track progress.
- It was split out of `docs/guides/refactor-roadmap-1.3.x.md` on 2026-10-07. The finished g1 slice specs, the 2026-07-20 gate-gap history (old 1.2), the release-train table (old 4) and the reviewer traceability tables (old 11.1 to 11.3) were removed; git history keeps them.
- The Notch algorithm specification remains `docs/reference/notch-system-reference.md`.
- Planning base: `codex/code-size-cutdown`, commit `165f076`. Code-size signed reference: R13.002 commit `3032121`; see `docs/performance/code-size-baseline-1.3.0.md`.
- GitHub parent spec: [#27](https://github.com/Dennis40816/nvt-freeform-helper/issues/27).


---

## 0. Executive conclusions

1. 1.0.x uses a fixed order: first build a trustworthy gate, then move the core data path, and finally split the UI.
2. Every 1.0.x refactor (including `R13.003`) must keep the following:
   - The example panel's V21/V22 Firmware C export is byte-exact.
   - The TM8.1 acceptance matrix and the Notch golden snapshot match exactly.
   - No new second-pass derivation may be added to Runtime Query, UI, inspector, simulation or export. Existing multi-path derivations must be listed and removed step by step under R13.102/104. This clause does not mean they are already done.
   - Existing UI appearance, action roles, spacing and DevView previews remain unchanged, except for the owner-approved R13.303 opacity fallback of 0.9 in g5, which requires updated snapshots.
   - `V21_before == V21_after` and `V22_before == V22_after`. It does not require the V21 and V22 files to be identical to each other.
   - The architecture exit target for R13.101 to R13.103 is: V21 and V22 share version-neutral input, matching evidence, allocation, compensation, audit and resolved result. They may only diverge at the final version-specific data projection/formatting boundary. The current g1 generator still has early threshold, cache and legacy dispatch debt; see the R13.005 canonical docs.
   - If a Q7 correctness fix requires changing any C byte, that work leaves the 1.0.x zero-diff refactor. It must be handled by a separate product-behavior change issue, and the golden for this plan must not be updated.
3. Run only one `R13.*` slice at a time. Each slice must be built, targeted-tested, linted, committed and pushed separately.
4. Do not force-merge different bounded contexts just to remove duplication. Many-to-many Pad overlap, DXF one-to-one audit and Canvas hit-test produce different results. Share only truly identical geometry evidence or tie-break policy.
5. Do not delete single-use tokens in batches, and do not remove DevView. Merge only tokens with identical meaning. DevView is the formal preview and guard surface of the UI contract.
6. Reviewer suggestions may not be treated as "covered" based only on the release theme. Each item must map to an `R13.*`, to completed evidence, to an explicit reason for not executing, or to a discovery gate with an exit condition.
7. `R13.007` is cross-version traceability governance. A tracker, labels and spec may be created first, and it must not be mixed into the same commit as a production slice. Production code still runs only one `R13.*` at a time, on the dependency frontier. The execution tracker is [#2](https://github.com/Dennis40816/FreeformHelper/issues/2).

**Owner decisions (2026-10-03; reply to the product questions in `docs/reviews/ui-feature-inventory-2026-10.md`)**

- V21 firmware C output: the reply was "not sure, keep it for now". Accordingly, the final projection/formatter separation of `R13.103` (including legacy convergence) is postponed. Do not start removing or changing V21 output, or the read path for old projects that depend on it.
- `LegacyRegularAnchor` (re-export of already delivered projects): the reply was "not sure, keep it for now". Accordingly, `R13.101c-2` and the legacy-only parts of `R13.102` are postponed. The existing zero-diff gates continue to protect it.
- Follow-up reply (2026-10-03; the question was whether `R13.101c-2`/`R13.103` continue by keeping equivalence-tested Legacy compatibility adapters, or stay postponed): the owner's words were "keep it for now, but the later target will be complete removal of 2.1".
- Confirmation (the owner replied "V21" on screen on 2026-10-03): "2.1" refers to V2.1, i.e. V21 (this repository's roadmap/TODO uses "final V2.1 projector"), not release version 2.1.
- The follow-up reply above replaces the earlier "postponed" consequence for `R13.101c-2`/`R13.103` and for the pure legacy part of `R13.102`. V21 and Legacy stay as they are, protected by the existing zero-diff gates. No extra convergence or equivalence work is invested. Complete removal is a later goal with no version assigned. On 2026-10-04, the owner (relayed by the Commander) placed it after g7. Its scope will be decided later. No implicit conversion happens before then. This entry only records the decision and does not change task status.
- The g2 exit audit keeps B1/B2 as accepted exceptions (owner decision, 2026-10-03). The owner decided on 2026-10-04, relayed by the Commander session, that g2 closes with A1/A2 and C1–C6 do not block closure. Parent and milestone exits remain the owner's decision.
- Step4 mapping and Step6 validation diagnostics: the reply was "move to the unnumbered diagnostics area". Accordingly, the owner confirmed the direction already written in `R13.305a`.
- Visual redesign: the reply was "do not change visuals for now". Accordingly, 1.x only reorganizes structure. It does not adopt a new visual language or reset the token set. Beyond the scope forced by structural changes, any change that alters the screen needs a separate owner decision.

---

## 1. Current baseline and verified facts

### 1.1 Test baseline of 2026-07-20

Before planning, the extended Notch/Firmware core set was run:

```powershell
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj `
  -c Debug -p:UseAppHost=false --nologo --no-restore `
  --filter "FullyQualifiedName~NotchExampleCExportDriftTests|FullyQualifiedName~NotchGoldenBaselineTests|FullyQualifiedName~Tm81NotchAcceptanceMatrixTests|FullyQualifiedName~NotchTableExporterTests|FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~NotchV22CompensationServiceTests|FullyQualifiedName~NotchV22TargetAllocationServiceTests|FullyQualifiedName~NotchV22ResolvedResultServiceTests|FullyQualifiedName~NotchV22FinalOutlineServiceTests|FullyQualifiedName~NotchDiffIdentityPipelineTests|FullyQualifiedName~NotchCadOutputFwDiffProjectionServiceTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~NotchApplySimulationReviewUseCaseTests|FullyQualifiedName~NotchSettingsTests|FullyQualifiedName~RuntimeQueryUseCaseTests"
```

Result: `103 passed / 0 failed / 0 skipped`.

On 2026-07-28, R13.002 further showed that this number only proves that the existing test seams are all green. It cannot alone prove that the checked-in C golden equals production export:

- The old `NotchExampleCExportDriftTests` took raw `_cad` directly (9,843 pads). It skipped the production `BuildFilteredCadPadSet` and the layer selections saved by the project.
- The real UI/IPC path and the corrected production-equivalent test both use 4,838 selected-layer pads. The SHA-256 of both paths' output is identical.
- Production V21: 692 nodes / 130,979 bytes / `8961B8155B0571B193C7C87D8EEA75077B4EF8822828506C4661B50BA2E57488`.
- Production V22: 548 nodes / 84,023 bytes / `5208068BBD8D82FC0A628693EF6035B31288EC674A25724C0C962CD58757BB47`.

On 2026-08-08, the team confirmed that `example/BOE36.35/project_3635.json` and the production layer/filter state saved by the project are the authoritative Lucid 3635 input. They agreed to make a one-time provenance correction using the two production hashes above. This is a baseline repair before the refactor. It is not authorization for later golden updates.

### 1.3 R13.002 cross-path investigation results

The R13.002 version of `scripts/perf/run-3635-regression-baseline.ps1` can already produce `c-v21` and `c-v22` through an isolated hidden UI/IPC path. It runs the exact gate with a normalized ordinal compare. The current evidence shows:

- Runtime Query still delegates to the existing ViewModel Step 5 export command. There is no second C row generation path.
- The real UI/IPC path and the corrected production-equivalent Application test are each byte-identical for V21 and V22.
- The CLI `--format` only switches the export file type temporarily and restores it after export. It does not modify `EnableV21` or `EnableV22`. Consecutive V21/V22 exports do not contaminate each other's project version state.
- The gate only stops the UI process it started itself and whose executable path was verified. It isolates app-general-settings.

Project-saved layer selections remain the production Step1/Step5 input. The checked-in C golden follows the signed provenance correction in section 3.1. After `R13.002` is complete, later 1.0.x slices must not update these two C golden files.

---

## 2. Firmware C and Q7 contract

### 2.1 V21 Firmware C

V21 Firmware C still uses Q7:

- `NHC_1ST_RATIO` / `NHC_2ND_RATIO` are `UINT8`.
- The legal magnitude range is `0..255`. The value represents `ratio * 128`, so 100% equals 128, and 255 is about 199%.
- The sign is carried only by `NHC_TYPE_ADD` / `NHC_TYPE_SUB`. The Q7 byte itself is not a signed value.
- Percent/fraction encoding uses `MidpointRounding.AwayFromZero`. When entering the Firmware payload, it saturates to `UINT8`.
- Firmware apply uses the INT16 integer path directly: `(source * magnitudeQ7) >> 7`. It must not first convert to percent/double and approximate.
- The V21 threshold uses an independent `ThresholdQ7` contract with a legal range of `0..128`. It is not the `0..255` payload magnitude.

Therefore Q7 is the public compatibility contract of V21 Firmware C. 1.0.x must not silently change it to percent.

### 2.2 V22 Firmware C

V22 Firmware C does not use Q7:

- `NHC_COMBINE` is a `UINT8` percent.
- `NHC_1ST_RATIO` / `NHC_2ND_RATIO` are `INT8` signed percent.
- Firmware apply uses an `INT16` carrier. Percent scaling is `(INT16)((source * percent) / 100)`. Integer division truncates toward zero. Writeback keeps firmware modular narrowing.
- The V22 threshold is natively a percent. If `LinkVersionThresholds` is enabled, UI/Application may convert from the V21 Q7 threshold. Q7 is never written into a V22 Firmware C node.

### 2.3 V22 internally still has a legacy Q7 projection

The V22 canonical row uses the signed percent of `NotchV22Node`. However, to stay compatible with the existing 9-column `NotchTableRow.Values` reader, `ProjectV22RowsToV21Rows` still converts the V22 percent into Q7 and places it in the legacy slot.

This is an internal compatibility projection. It is not the V22 Firmware C format. 1.0.x must distinguish:

- The V21 Firmware Q7 codec.
- The V22 canonical percent model.
- The V22-to-legacy-row Q7 compatibility projection.

The exporter, simulation and validation must not each implement different clamp/rounding logic.

### 2.4 Result of R13.003 convergence

- `NotchV21Q7Codec` is the only numeric contract for V21 payload encode/decode/scale.
- `NotchThresholdQ7Contract` independently handles `0..128` threshold/allocation rounding. It does not let the output-version codec leak into shared upstream geometry.
- `NotchV21FirmwareProjector` completes, in one step, the projection from a CadAllocation source row or a LegacyRegularAnchor row to the final ABI node. The C formatter only serializes the final node.
- `NotchV21FirmwareEvaluator` consumes the same final node and executes the three stages: MUL, snapshot Q7 offsets, and writeback with INT16 narrowing.
- The V21 baseline of C# simulation is explicitly quantized to INT16 (finite decimals truncate toward zero, out-of-range values saturate, non-finite values become 0). Cells, Actions and audit all read the same firmware baseline, so quantization is not misreported as a compensation delta.
- CadAllocation provides source-oriented Actions. LegacyRegularAnchor only provides exact Cells/EMS and does not fabricate source-flow Actions in the opposite direction.

The full byte boundary, CadAllocation, LegacyRegularAnchor and GCC exact parity are locked by tests. The V21/V22 Lucid 3635 C golden was not updated.

---

## 3. 1.0.x acceptance levels

| Gate | Purpose | When to run |
|---|---|---|
| G0 Build/Lint | Ensure compilation, analyzers, formatting and CRLF | Every slice |
| G1 Targeted | Lock the direct contract of this slice | Every slice |
| G2 Notch Core | Generator/export/simulation/projection/CLI contract (118 cases when R13.004b completed) | Any Application/Domain/Settings/RuntimeQuery/VM Notch slice |
| G3 Golden | 3635 V21/V22 byte-exact + 3635/TM8.1 snapshot + TM8.1 matrix | Any slice that may affect the data path |
| G4 Runtime CLI | The V21/V22 export from real UI/IPC equals the checked-in golden | Required after g1 is built; required for VM/RuntimeQuery/export slices |
| G5 UI | DevView + UiLayoutGuard + headless smoke; rendered snapshot dry-run when needed | View/Style/Control slices |
| G6 Merge | Full lint + refactor gate + UI snapshots | Before merging the 1.0.x milestone |

### 3.1 Golden update rules

1. By default, refactoring does not allow golden changes.
2. When a test fails, save the actual diff first. Never overwrite expected with actual to get a green build.
3. 1.0.x allows only one signed provenance correction, which is `R13.002`. It must prove that the old golden came from a non-production test seam. Any later correctness fix that changes C must leave this zero-diff refactor plan.
4. A provenance correction must first prove that two independent production-equivalent seams produce the same output. The production behavior itself must not be changed to match the actual output.
5. An update must record: version, slice, project, CAD/diff/fields, input selection/filter, old value, new value, reason, human sign-off and verification commands.
6. After updating, run G2, G3 and G4 in full to prove there is no undeclared difference.

---

---

## 4. Targeted suites per version

| Version | Required tests beyond G2/G3 |
|---|---|
| g2 | `NotchDisplayProjectorTests`, `NotchValidationUseCaseTests`, `NotchValidationTraceServiceTests`, `SimulationSafetyOverviewProjectorTests`, `SimulationSafetyAuditServiceTests`, `PadInfoViewModelTests` |
| g3 | `PadMatcherTests`, `DxfRegularMappingAnalyzerTests`, `DxfRegularMaskAuditServiceTests`, `FreeformDetectorTests`, `CoordinatePlannerComputationServiceTests`, `PadCanvasHitTestTests` |
| g5 | New `SettingsDraftContractTests`, existing Settings persistence cases, `SimulationWorkspaceUseCaseTests`, `SimulationWorkspaceViewModelTests`, `SimulationColorScaleResolverTests`, `ShellViewModelConsoleTests` |
| g6 | `FreeformHelperViewModelTests`, `WorkflowPipelineServiceTests`, `RuntimeQueryIpcTests`, `HeadlessUiSmokeTests` |
| g7 | `UiLayoutGuardTests`, `UiRenderedVisualSnapshotTests`, `UiVisualSnapshotTests`, `HeadlessUiSmokeTests`, `CadAreaBucketServiceTests`, `PadCanvasHitTestTests`, `PadCanvasCacheInvalidationTests`, `check-xaml-action-roles.ps1` |

---

## 6. g2 — Notch single-result pipeline

### R13.101 Explicit NotchGenerationContext

- Replace the optional precomputed-parameter combination of `NotchV22CompensationService.Compute`.
- The context must carry consistent allocations, boundary indices, active regular mask, strict ratio and boundary query context.
- Callers must not pass only some caches, which would create implicit stage ordering.
- The shared computation context must not carry `EnabledVersions`, and must not select threshold or strategy by V21/V22. The version belongs only to the final projection request.
- `R13.101a` was completed by [#10](https://github.com/Dennis40816/FreeformHelper/issues/10). The cost of `BuildV22CadCandidate` repeatedly copying or scanning the same pool for each CAD/IC candidate has been removed. The per-IC pool inside the generator is an owned, fixed-size snapshot. When the shared boundary context exists, it is the authoritative pool, and the redundant fallback is not read. When the context is absent, null/empty/same-ID normalization and missing-current append are still kept by CAD identity. The caller-ownership contract of the public context factory is not rewritten in this slice. The full `NotchGenerationContext`, memo key and cache split remain the responsibility of the R13.101 parent, 101b and 101c.
- A public seam uses a throw-on-read fallback to lock zero reads on the shared path. Missing-current and same-ID mutations lock that the fallback is not empty. The existing generator deterministic test locks the ordered rows of parallel candidate generation. For Lucid 3635, the `9,831` candidates are unchanged. The p50 canonical candidate build went `207 -> 128 ms`, and compensation `1,596 -> 798 ms`. Wall time went `3,568 -> 3,593 ms` (`+0.7%`), so the evidence only claims deterministic duplicate-work removal. Production net change is `-2 physical / -2 nonblank`. Targeted 51, notch-core 122, golden 6, GCC exporter/parity 15 tests, and the positive and negative hidden UI/IPC gates all pass. UI build passes with 0 warnings and 0 errors from lint.
- `R13.101b` was completed by [#12](https://github.com/Dennis40816/FreeformHelper/issues/12). The allocation memo still partitions by CAD ID and keeps grid-reference invalidation and the 8,192-entry capacity. However, each entry now holds an immutable `Polygon2`. The same instance hits directly in O(1). When the same ID has a different polygon instance, the comparison goes through the existing `CadPadGeometrySignature` owner, which verifies the invariant round trip with exact coordinates. The public 6-decimal/AwayFromZero signature is kept for DXF duplicate/audit tolerance. No parallel signature class, cache framework, version/output state or public API was added.
- The public `Generate` seam uses concave polygons with the same area and bounds and the same rounded signature, but with inner X values `5.0000004` and `4.9999996`. This locks the flip of left/right overlap and the DIFF anchor. A to B and B to A both equal a fresh generator exactly, and both lock the V21/V22 ordered rows. If the memo mutation is returned to the public 6-decimal signature, the result regresses from DIFF20 to DIFF10 and turns RED. Cyclic-start and reversed-winding cases, fixed rounded literals and existing DXF consumers also have guards. Compared with production at `4a05fc9`, the net change is `+8 physical / +5 nonblank`. Two fresh isolated deterministic Release builds are fully identical. The total DLL goes `15,315,456 -> 15,314,944 bytes` (`-512`). Application goes `691,200 / 2679A0FE…C093A -> 690,688 / 3B2BA783…F2FA`. Domain `39,936 / 0EAD9AC9…66D5`, Infrastructure `60,928 / CAFA1813…6F29`, and UI `14,523,392 / 3A0CBCAC…72B6` are unchanged. Lucid 3635 candidates remain `9,831`. The p50 wall time goes `5,914 -> 4,726 ms`, generation `5,861 -> 4,695 ms`, and BuildProfiles `5,672 -> 4,530 ms`. These are only no-regression evidence. Targeted 36, notch-core 124, golden 6, GCC exporter/parity 15, RuntimeQuery 13 tests, and the positive and negative hidden UI/IPC gates all pass. Both C golden files, state and budget are unchanged. The full `NotchGenerationContext` and cache split remain the responsibility of R13.101 parent and 101c.
- `R13.101c` exit target: the version-neutral result fingerprint does not include `ExportProfile` or `EnabledVersions`. The final projector/formatter cache may still carry the output contract. The old persistence roundtrip does not change.
- `R13.101c-1` was completed by [#14](https://github.com/Dennis40816/FreeformHelper/issues/14). It removes `ExportProfile` only from the current generated-table settings fingerprint. The cached table is still serialized with the currently requested profile. No formatter cache, key abstraction or public API was added. The public headless export seam locks that switching Release to Debug and Debug to Release each performs only one miss/store, then hits the same table. Warm and fresh requested-profile raw C are exact, while profile metadata and the Debug-only FW mask are still output according to the current profile. Adding the profile back in a mutation turns both cases RED. Switching V21/V22 and changing the V21 Q7 threshold still miss.
- Compared with production at `87cf59a`, the net change is `-1 physical / -1 nonblank`. Two deterministic Release builds total `15,314,944 bytes`. Only the UI hash changes, to `B1AC7878…1881`, while its bytes remain `14,523,392`. Targeted 10, notch-core 130, ui-core 237, golden 6, GCC exporter/parity 15, RuntimeQuery 13 tests and the positive and negative hidden UI/IPC gates all pass. V21/V22 golden, export state and budget are unchanged.
- Historical `R13.101c-2` implementation evidence: R13.102a-2a removed enabled versions, threshold, `NullValue` and target guard/cap from the normal `CadAllocation` resolved-batch settings fingerprint. A matching export can reuse the same candidate batch and re-project the current request. R13.101c-2a [#64](https://github.com/Dennis40816/FreeformHelper/issues/64) then made the live UI guard/cap settings final-projection-only. It keeps the Step3 revision, per-CAD sparse identity and export batch. It only invalidates Step5 projected table and validation, and notifies Simulation. R13.102a-2b-1 [#66](https://github.com/Dennis40816/FreeformHelper/issues/66) rejects in-flight stale completions at the existing generation boundary, using the cache epoch, final-projection revision and source revision. R13.102a-2b-2 [#68](https://github.com/Dennis40816/FreeformHelper/issues/68) and [#76](https://github.com/Dennis40816/FreeformHelper/issues/76) then completed the parallel full task and a bounded bridge to the currently selected single sparse result. The current exit scope follows the owner decisions of 2026-10-03/04: normal A1/A2 closure requires evidence. Legacy request-specific table identity and further V21/Legacy projector convergence remain accepted B1/B2 exceptions. The repository-wide final-output cache is C1. C1 to C6 do not block g2. Parent and milestone exits remain the owner's decision.
- Owner, 2026-10-03: whether `LegacyRegularAnchor` still needs re-export of already delivered projects has not been decided. It is kept for now. `R13.101c-2` is postponed, and the existing zero-diff gates continue to protect it.
- A later owner reply (2026-10-03) replaces the "postponed" consequence above. V21 and Legacy stay as they are, protected by the existing zero-diff gates. No extra convergence or equivalence work is invested. Complete removal is a later goal with no version assigned. On 2026-10-04, the owner (relayed by the Commander) placed it after g7. Its scope will be decided later, and no implicit conversion happens before then. For the original wording and the owner's confirmation that "2.1" means V21, see the owner decisions in section 0.
- R13.101c-2a uses public direct UI and Settings Save fixtures to lock that a guard/cap-only change does not recompute Step3. Retained batch hits and fresh projection are byte-exact. Three behavior mutations and a public enum ABI contract each turn RED. Final-projection invalidation is carried by a named internal flag in the settings plan. The public policy surface and enum values are unchanged. Compared with `74301a3`, both production and logic-first are `0 files / +22 physical / +19 nonblank`, with 0 new dependency, service, cache or session. Two fresh isolated Release builds are fully identical. The total DLL increases by `512 bytes`, and only UI changes. Focused 5, notch-core 182, Application 218, ui-core 264, smoke 25, the UI build, lint/analyzer and the positive and negative hidden UI/IPC gates all pass. The signed C remains V21 `692 / 130979 / 8961B815…E57488` and V22 `548 / 84023 / 5208068B…57BB47`. Selection p95 is `27/15/15` and `28/16/15 ms`. Standards, spec and simplification findings were all fixed. No performance improvement is claimed.
- `R13.101d-1` was completed by [#20](https://github.com/Dennis40816/FreeformHelper/issues/20). It freezes the computation input of the normal `CadAllocation`. A private `CadAllocationGenerationContext` owns, in one place, profiles/allocations, per-IC CAD pools, boundary query/index evidence, owned active-regular and CAD-output snapshots, grid/CAD references, strict ratio, and the resolved compensation, switch, rule, boundary and allocation policy. The three candidate helpers receive only this context. They no longer receive or reread the mutable `ProjectSettings` or `NotchSettings`. Enabled outputs, threshold, `NullValue` and target guard/cap still belong only to the separate final projection request. This is not the R13.102 resolved batch/task, and it does not change the `LegacyRegularAnchor` compatibility dispatch.
- The freeze boundary exactly preserves the existing callback timing. The caller's set and map are owned only after BuildProfiles completes. The pool, strict and boundary prerequisites are created then. The remaining computation policy and projection request are frozen once, after the phase-2 initial progress callback returns. A public synchronous progress seam locks phase-2 `ProcessedCount=0/1`, the cap-50 V2.1/V2.2 exact rows, the snapshot of enabled outputs and `NullValue`, and caller collection ownership. A reflection architecture guard locks that the three candidate helpers no longer depend on settings types.
- Compared with `0e03662`, the necessary context boundary of R13.101d-1 is `+12 physical / +10 nonblank` in production. The carrier is a private sealed class with readonly fields. It does not use a positional record, so it has no unused equality, deconstruction or `ToString`. Two fresh isolated deterministic Release builds are fully identical. The total DLL goes `15,312,384 -> 15,312,896 bytes` (`+512`). Only Application changes, `688,640 / CC0C1F71…8E86 -> 689,152 / 28F3BBF2…4E8F`. The other three DLLs keep their bytes and hashes. For Lucid 3635, candidates are fixed at `9,831`. On the same environment, p50 wall goes `3,819 -> 3,971 ms`, generation `3,798 -> 3,942 ms`, candidate `121 -> 131 ms`, and compensation `906 -> 960 ms`. These are only no-regression observations. Targeted 77, notch-core 140, golden 6, GCC exporter/parity 15, RuntimeQuery 13, ui-core 243 tests, and the hidden UI/IPC positive and negative gates all pass. Both signed C files, export state and budget are unchanged. The UI build and lint, plus the two-axis review, report 0 findings. The full `NotchGenerationContext`, version-neutral resolved batch, cache split and typed final projectors remain the responsibility of the R13.101 parent, R13.102 and R13.103.
- `R13.101d-2` was completed by [#26](https://github.com/Dennis40816/FreeformHelper/issues/26). It unifies the per-IC CAD pool admission of single-CAD UI and the generator. The membership query of `NotchAllocationService` and `BuildAllocations` share a Q7-positive predicate. They check the IC first and then do the intersection, returning at the first hit. The UI no longer groups pools only by each CAD's primary IC. The existing empty-pool all-visible fallback and the selected-target inclusion fallback are kept.
- The public two-IC fixture A `[0,5]` and B `[5,15]` records an intentional correction. The old UI gave `ToRegular / ToFull / Combined = 0.5 / 2 / 1`. The generator gives `0.5 / 1 / 0.5`. After completion, the selection preview path, deferred Inspector, Notch Detail and the generator all give `0.5 / 1 / 0.5`. They share the owners, blocker, reason/trace, target allocation and resolved identity. The pool cache also changes from count-only to a fingerprint of canonical ordered CAD IDs (including count), so a same-count B-to-C member swap invalidates it. The firmware API/schema, UI visuals and signed C are unchanged.
- Compared with `e18a4f3`, the production net change is `+62 physical / +57 nonblank`. Two fresh isolated deterministic Release builds are fully identical. The total DLL goes `15,313,408 -> 15,314,432 bytes` (`+1,024`). Application bytes are unchanged but the hash is updated. UI increases by `1,024 bytes`. Domain and Infrastructure bytes and hashes are unchanged. The candidate count in the fresh current-source benchmark is still `9,831`. Forward and reverse selection `total / notchPreview / Inspector` p95 goes from `11 / 3 / 8` and `5 / 3 / 1 ms` to `38 / 22 / 20` and `45 / 24 / 23 ms`. This is a correctness cost, not a speedup. The official `600 ms` total and `400 ms` Inspector budgets still PASS. Focused 71, notch-core 143, ui-core 247, notch-golden 6, GCC exporter/runtime 16, RuntimeQuery 13 tests, the hidden UI/IPC positive and negative gates, the UI build and lint/analyzer all pass. At this historical checkpoint, the R13.101/101c-2 context/cache split, the R13.102/102a-2 batch/task and the R13.103 projector/formatter targets were still open. The current closure follows A1/A2 and the accepted B1/B2 exceptions above.

- `R13.101e` was completed by [#78](https://github.com/Dennis40816/FreeformHelper/issues/78). It converges the optional precomputed combination to a canonical form. The normal generator and the UI first build one `NotchV22CompensationContext`. It carries, in one object, the allocation evidence, boundary indices, active mask, strict/query context and all computation policy. Then the single `Compute(context)` runs. The old public multi-parameter `Compute` keeps source compatibility, but it only fills in a complete context. An explicitly empty allocations set is no longer treated as missing, so it no longer falls back to geometry. Stage A and B no longer contain nullable evidence or implicit stage-order fallbacks. The context does not contain `EnabledVersions`, threshold, `NullValue` or target guard/cap.
- Public TDD first locks, with a half-covered CAD plus explicit empty allocations, the old `ToRegular=0.5` RED. After completion, ratio, combined, overlap and count are all `0`, with no debug rows. A raw factory, the canonical context and a compatibility adapter separately lock the same normal diagnostics. Compared with `659f214`, production and logic-first are both `0 files / +108 physical / +103 nonblank`. The Stage A/B fallback removes 45 physical lines. It adds 0 new service, cache, session, dependency or generic executor. The largest related file has 497 lines. Two deterministic Release builds are both `15,362,560 bytes`, which is `+2,048` over the baseline. Focused 76, notch-core 190, Application 221, ui-core 274, smoke 25, golden 6, Runtime Query/IPC 23, GCC 8 and the hidden positive and negative gates all pass. The signed C, export state, budget, Runtime schema and callback/ordered-row contract are unchanged. This historical checkpoint predates the owner's 2026-10-03/04 exit decisions. The current A1/A2 obligations and accepted B1/B2 exceptions are listed above.

### R13.102 Unified Notch resolved result

- Build one source-of-truth result. It contains compensation, Stage1/2/3, target allocation, coverage audit and reason/trace.
- The generator, inspector, PadInfo, RuntimeQuery, simulation and overlay only read or project this result.
- Readers must not recompute Stage3Area, ToFull enabled or target legs from partial data.
- For the same input, under V21-only, V22-only and V21+V22 output requests, the shared resolved result must be identical. The output request must not change candidate evidence.
- Synchronous preview, deferred inspector and export for the same selection and workflow revision must wait for, or project from, the same resolved task/snapshot. They must not recompute compensation.
- `R13.102a-1` was completed by [#16](https://github.com/Dennis40816/FreeformHelper/issues/16). The Step 3 preview and the 200 ms deferred CAD Inspector for the same single-CAD selection/revision share the existing revisioned per-CAD `NotchV22ResolvedResult` identity. The warm deferred path only projects the existing immutable result. A cold miss is built once in the background and then stored. Output-only changes to profile, file type or version do not change the Step 3 revision or result identity. Real computation-input changes still invalidate. The existing stale, cancellation, pending and UI-thread rules are unchanged.
- Compared with `f600dfe`, production net change is `-16 physical / -11 nonblank`. Two deterministic Release builds both total `15,314,432 bytes`, which is `512 bytes` less than the previous slice. Only the UI DLL changes, from `14,523,392 / B1AC7878…1881` to `14,522,880 / B3B399D9…400F`. The other three DLL bytes and hashes are unchanged. Targeted 6, notch-core 130, ui-core 243, golden 6, GCC exporter/parity 15, RuntimeQuery 13 tests and the hidden UI/IPC positive and negative gates all pass. Both C golden files, export state and budget are unchanged.
- `R13.102b-1` was completed by [#22](https://github.com/Dennis40816/FreeformHelper/issues/22). It converges the next UI reader. `ShowNotchDetailCommand` first gets the `NotchV22ResolvedResult` of the current selection/revision through the existing full-key owner. Detail only projects that instance and no longer runs compensation or resolved build again. Warm, cold, output-only reuse, real Step3 invalidation and full detail fields are locked by a public headless workflow. The public `NotchDetailUseCase.Build` signature and compatibility behavior are preserved.
- The cross-IC correction is recorded as intended. In the old compatibility `Build`, `anchorIcIndex:null` added up all IC targets. The normal command now matches the preview and Inspector, using the workflow anchor-IC allocation. The smallest real fixture locks the unanchored `Combined ratio: 200.00 %` and the authoritative `100.00 %`. The underlying compensation ratio is unchanged. The change does not use another unanchored allocation recomputation to keep a Detail-only second derivation. The deferred Inspector cold completion also re-queries the full key before overwriting. This prevents a late background job from removing the current identity that Detail just built.
- Compared with `2973229`, production net change is `+16 physical / +14 nonblank`. It contains only the compatibility-preserving projection seam and the deferred cold race guard. Two fresh isolated deterministic Release builds are both `15,313,408 bytes`, which is `512 bytes` more than the baseline. Only the UI DLL changes, from `14,522,880 / B3B399D9…5400F` to `14,523,392 / 984487EE…BE67`. The other three DLL bytes and hashes are unchanged. Focused 4, ui-core 246, notch-core 140, golden 6, GCC exporter/parity 15, RuntimeQuery 13 tests, the UI build and lint/analyzer all pass. For Lucid 3635, the hidden UI/IPC positive and negative order both give V21 `692 / 130979 / 8961B815…E57488` and V22 `548 / 84023 / 5208068B…57BB47`. Golden, export state and budget all PASS. Selection p95 is `6 ms` in each case. The Standards, Spec and simplification reviews have no blocker.
- `R13.102b-2` was completed by [#36](https://github.com/Dennis40816/FreeformHelper/issues/36). It converges the Runtime Query `multi-owner` reader. After the visible CAD guard, it obtains only one current-revision `NotchV22ResolvedResult`, resolved from the current setting or the `--overlap-percent` override. The lightweight Inspector snapshot is used only for the existing CAD response metadata. Rows, summary counts and rule trace are all projected from `resolved.Compensation.RegularDebugInfos`. A cold override reduces from two compensation misses to one. A re-query with the same CAD/revision/override does not add a miss. The payload schema, ordering, limit/truncation, threshold metadata and errors are unchanged.
- This slice only completes R13.102b-2. It does not build a revisioned task/session shared by UI and export. It also does not change the export candidate batch, `LegacyRegularAnchor` or the combined-overflow derivation of `NotchDisplayProjector`. R13.102, R13.102a-2, R13.101c-2, R13.103 and R13.104 remain open.
- Completion evidence: compared with `f866e6a`, production and logic-first are both `+14 physical / +12 nonblank`. Two fresh isolated deterministic Release builds are fully identical. The total DLL goes `15,322,624 -> 15,323,136 bytes` (`+512`). Only UI changes, from `14,528,512 / 9B017D15…09DBC` to `14,529,024 / 20280245…86A7`. Focused 2, RuntimeQueryUseCase 18, RuntimeQueryIpc 5, notch-core 170, ui-core 250, notch-golden 6, GCC 8, the UI build, lint/analyzer and the Standards, Spec and simplification reviews all pass. The signed C, golden, export state and budget of the hidden UI/IPC positive and negative order are unchanged. Selection total p95 is `47 / 96 ms`. The evidence SHA-256 values are `359F4F0B…B75F4` and `854254B0…D2D5B`. No performance improvement is claimed.
- `R13.102a-2a` was completed by [#28](https://github.com/Dennis40816/FreeformHelper/issues/28) as a narrow, export/generator-only slice. The normal `CadAllocation` cold path builds one Application-owned opaque compact candidate batch and the first final projection. A matching warm request reuses the same batch and re-projects it with the current version, threshold, `NullValue` and target coverage guard/cap. A first projection with zero rows still stores the batch. `LegacyRegularAnchor` keeps its request-specific `NotchTable` cache.
- Warm batch projection does not replay the profiles, candidate or merge work. It reports only phase 4 of the current run. The Runtime Query schema is unchanged. `exportGenerationCache.entryRowCount` is the row count of the latest projected table. It is not the candidate count or the batch size.
- Mutation evidence: each of these changes fails a focused characterization RED test: adding the version/threshold key back, removing the strong compatibility guard, refusing to store a zero-row batch, rerunning the candidates on a hit, or returning the first final table. Combined-request admission keeps the V22-controlled shared set that R13.103b-1 already locked.
- Completion evidence: compared with `1deb591`, production and logic-first are both `+405 physical / +377 nonblank`. Two fresh isolated deterministic Release builds are fully identical. The total DLL is `15,314,432 -> 15,322,112 bytes` (`+7,680`). Application is `689,152 / 3FE87F11…CCBB5 -> 693,248 / 141A8972…730A`, and UI is `14,524,416 / AF1EC4E4…9D67 -> 14,528,000 / B00AAB25…E2D0`. The bytes and hashes of Domain and Infrastructure are unchanged. In current-source Lucid 3635, all three cold runs give `9,831` candidates and `1,148` rows. The cold p50 is `1,328.20 ms`. The warm projection p50 / p95 on the same batch is `0.5884 / 1.0393 ms`. The estimated retained size is `331,509 bytes/batch`. The ignored evidence SHA-256 is `56198980…A031205`. It serves only as single-machine no-regression and capacity evidence. Focused 54, notch-core 146, ui-core 250, golden 6, GCC 16, RuntimeQuery 13 and the hidden UI/IPC positive and negative order all PASS. The signed C is still V21 `692 / 130979 / 8961B815…E57488` and V22 `548 / 84023 / 5208068B…57BB47`.
- `R13.102a-2b-1` was completed by [#66](https://github.com/Dennis40816/FreeformHelper/issues/66). It converges the existing generation completion boundary. The request entry fixes the cache generation epoch, the Step5 final-projection revision and the Simulation source revision. Only a completion for which all three are still current may commit the projected row count, the last generated table, progress/flow and the caller continuation. The accepted Simulation session stores the source revision captured for that run.
- A final-only guard/cap invalidation on `CadAllocation` rejects the table, session and progress publication of an old completion. It keeps the resolved batch and keeps the projected row count at `0`, so the next current request can hit the batch and re-project it. A full Step5 invalidation advances the generation epoch even if the cache is empty, so an old request cannot store a batch or table afterwards. Export and Simulation share a reference-counted busy scope. When one of two overlapping operations finishes, the busy state of the other is not released early. The compatibility behavior of `LegacyRegularAnchor` is unchanged.
- This slice does not complete R13.102 or R13.102a-2. R13.102a-2b-2 must still make the single-CAD UI sparse result and the full export batch share one revisioned task/session. The parents R13.101c-2, R13.102 and R13.103 also remain open.
- `R13.102a-2b-2` is split into two consumer-backed leaves, according to code-size and lifetime risk. `R13.102a-2b-2a` [#68](https://github.com/Dennis40816/FreeformHelper/issues/68) first makes concurrent Export and Simulation share one full batch-resolution task under the same output-neutral identity and generation epoch. It explicitly separates the resolution of phases 1 to 3 from the phase 4 projection of each caller. The next leaf connects the single-CAD sparse resolved result to the same session. 2b-2a must not add a generic executor, a second cache service, a per-candidate task or an eager full preview. It must not claim to complete the 2b-2 parent.
- `R13.102a-2b-2a` is complete. After the cold owner succeeds, it stores once, and only if the epoch and entry are still current. Joiners read the same task, but each projects with the final request frozen at its own entry. A final-only invalidation keeps the task and batch and rejects old projections. A full invalidation detaches the old task and forbids storing afterwards. A faulted entry is removed so that it can be retried. The completed cache, the in-flight join and the projection all keep the full computation-settings equality, to prevent a 32-bit fingerprint collision. The old UI service facade delegates thinly to the same path, to keep source compatibility.
- Completion evidence: compared with `03faae6`, production and logic-first are both `0 files / +182 physical / +169 nonblank`, and the ViewModel net reduction is `-2 / -4`. There is no new production file, service or dependency. Two fresh isolated deterministic Release builds are fully identical. The total DLL is `15,332,864 -> 15,342,080 bytes` (`+9,216`). Application is `696,320 / 9899BC42…B952EBF -> 697,856 / EA953FCE…33E3B4`, and UI is `14,535,680 / 258E5AB4…723CE6 -> 14,543,360 / EDB83455…4D0D4B`. Domain and Infrastructure are unchanged. Facade 1, notch-core 187, Application 218, ui-core 274, smoke 25, notch-golden 6, Runtime Query 23 and GCC 8 tests and the UI build and lint are all green. The hidden UI/IPC positive and negative order give signed C, golden, state and budget all PASS. The selection p95 is `31/17/15` and `29/15/15 ms`, and the evidence SHA-256 values are `5B5C5272…D5DB4E` and `C3EC88C3…D2961`. The generator is now `974 / 899` and the cache service is `634 / 575` physical / nonblank. The next leaf takes an extract/delete-first approach. 2b-2 and all parents remain open.
- `R13.102a-2b-2b` [#76](https://github.com/Dennis40816/FreeformHelper/issues/76) is complete and closes 2b-2. The two caches of the single-CAD UI, compensation and resolved, are converged into one resolved owner. A cold or warm full batch carries and reuses at most one resolved result of the currently selected single CAD. The identity locks the CAD ID, the anchor IC/diff, the exact CAD, the immutable grid state and CAD pool signatures, the active mask, and all computation settings. A final-only request is not part of the identity. A change of the same mutable grid reference also cannot reuse an old result by mistake.
- 2b-2b does not keep the polygons and debug evidence of all candidates. It also adds no per-CAD task registry, no second cache/session service, no generic executor and no dependency. After the generator carrier is extracted, the main file drops from `974` to `841` lines, and the ViewModel net reduction is `109 / 100` physical / nonblank. Compared with `7b06722`, production is `+2 files / +127 physical / +118 nonblank`. This is less than the `128` physical lines that deleting the duplicate cache path directly would save. Two fresh isolated deterministic Release builds are identical. The total DLL is `15,342,080 -> 15,360,512 bytes` (`+18,432`). Focused 8, notch-core 189, Application 220, ui-core 274, smoke 25, golden 6, Runtime Query/IPC 23, GCC 8, the hidden UI/IPC positive and negative order, build and lint, and the three-axis review all PASS. The signed C is still V21 `692 / 130979 / 8961B815…E57488` and V22 `548 / 84023 / 5208068B…57BB47`. R13.101 / 101c-2 / 102 / 102a / 102a-2 / 103 and Legacy convergence remain open under their own exit conditions.

- Owner-approved firmware corner case (2026-10-04): the owner approved public PR #3 at head `9810edf9` and the corner case in chat. With mixed Q7-zero/Q7-positive allocations after CAD inspection, warm V21/V22 firmware rows and FW apply simulation now match cold results; cold generator and golden-input outputs remain unchanged. Synthetic warm/cold full-row snapshots cover this case, but it was not separately verified through the C formatter or actual UI export. See [milestone handoff section 6](../reviews/r13-131-milestone-handoff-2026-10.md) for the exact scope and remaining presentation/interaction validation limits.

### R13.103 Export projection and formatter separation

- owner 2026-10-03: The V21 firmware C output is not yet decided, so it is kept for now. The final projection/formatter separation for this item, including legacy convergence, is postponed. Do not start removing or changing the V21 output, or the old project read paths that depend on it.
- Later owner reply (2026-10-03), which replaces the postponement above: V21/Legacy stay as they are, and the existing zero-diff gates continue to protect them. No further convergence or equivalence work is planned. Full removal is a future goal with no assigned version. On 2026-10-04, the owner decided (relayed by Commander) to schedule this after g7. The scope will be decided later. No implicit conversion happens before then. The owner's original words, and the owner's confirmation that "2.1" refers to V21, are in the Owner decision in section 0.
- `NotchFirmwareCExporter` is already in the Application layer. This item is not "move it to Application."
- Converge the version-specific final Firmware node projection (V21 destination-oriented, V22 source-oriented), the version threshold, and the null, continuation, and ordering rules into the only allowed version-branching boundary and a testable result model.
- The exporter should only handle C text formatting and fixed contract emission.
- Typed and legacy projections must have equivalence tests.
- `R13.103a` was completed by [#24](https://github.com/Dennis40816/FreeformHelper/issues/24). It added final V2.1 projector/evaluator characterization. The public exporter fixture sorts three terms with the same destination in a stable order by source identity, then splits them into two ABI nodes. It locks the exact C lines, the node count, and the generated `INT16` carrier. The C# evaluator and the GCC `15.1.0` runtime both give a final destination of `32765` for three `32767 × Q7 128` terms. A separate public simulation fixture locks the exact diagnostics, and their order, for missing destination, missing source, and missing anchor.
- Mutation evidence: changing the projector step to 3, changing the evaluator to a saturating clamp, and suppressing the missing-source diagnostic. Each of these three cases turns RED in the new characterization. After restoring the code, the targeted exporter/simulation tests pass (27), along with notch-core (142) and notch-golden (6). This slice has no production source difference. Two fresh isolated deterministic Release builds both stayed at `15,313,408 bytes`, and the bytes and SHA of the four own-output DLLs matched. The Lucid 3635 hidden UI/IPC forward and reverse results remain exact: V21 `692 / 130979 / 8961B815…E57488` and V22 `548 / 84023 / 5208068B…57BB47`. Golden, export state, and budget all PASS. The UI build and lint have no warnings.
- Remove the behavior where canonical candidate build chooses the threshold early based on `exportsV22`. First build a version-neutral superset and evidence. Then create one compatibility admitted view in the final row projection. V21-only uses the Q7 predicate. Whenever the request includes V22 (including both), use the V22 effective-percent predicate to control the same shared set, and then project V21 from that set. Changing "both" to two independent per-version filters would change existing non-golden output. That needs a separate behavior-change ticket and cannot be mixed into the zero-diff refactor.
- `R13.103b-1` ([#18](https://github.com/Dennis40816/FreeformHelper/issues/18)) implements this narrow boundary. The compatibility request is frozen after BuildProfiles and the CAD pool, strict, and boundary prerequisites are complete. It is frozen after the phase-2 initial progress callback returns and before parallel candidate compute starts. Candidate build and merge no longer receive the enabled version or the version-selected threshold. After final admission, the same dictionary is used in order for primary selection, the target coverage guard, rows, the coverage audit, V22 append, and V21 projection.
- The candidate phase now intentionally measures the full superset. `CandidateCount`, candidate timings, and the merge-phase `GeneratedRowCount` are candidate and bucket telemetry, so they can be larger than the final rows. Phase 4 still reports the actual output row count. Payload overflow evidence is also saved with the candidate first. It is checked only for admitted candidates, using the original exception type and message. This means threshold-rejected data does not add new exceptions. These are explicitly listed telemetry boundaries for building output-neutral evidence. They do not mean R13.102 batch/result is complete.
- R13.103b-1 completion evidence: Compared with `7490477`, production has a net reduction of `-8 physical / -10 nonblank`. Two fresh isolated deterministic Release builds both produced `15,312,384 bytes`, which is `2,048 bytes` less than the base. Only the Application DLL changed, from `690,688 / 3B2BA783…F2FA` to `688,640 / CC0C1F71…8E86`. Lucid 3635 candidates are fixed at `9,831`. p50 wall time went `4,726 -> 3,819 ms`, and generation `4,695 -> 3,798 ms`. This is recorded only as a no-regression observation. Targeted 30, notch-core 132, golden 6, GCC 15, and ui-core 243 tests pass. The hidden UI/IPC forward and reverse order checks, UI build, lint, and dual-axis review also pass. The V21/V22 signed C, export state, and budget are unchanged.
- `R13.103c-1` ([#32](https://github.com/Dennis40816/FreeformHelper/issues/32)) converges the final V22 projector. `NotchV22FirmwareProjector` normalizes, in one pass, the typed payload, `Values.Length >= 7`, and the short-row compatibility payload. The same projected row forms both the input-order `SourceRows` and the existing C-order `NodesByIc`. The exporter only formats the projection. The Release no-op is still removed at the sorting and emission boundary, in the existing order. Simulation runs each node under FW `INT16` semantics. It then aggregates Actions and diagnostics from the computed evaluation. It no longer decodes on its own, and it no longer recalculates physics from merged continuation legs.
- Intentional correction: For the MAIN `C=120` fixture with legs `60/20` and a CONT leg `10`, the V22 simulation source is changed from `30` to `40`, matching the frozen C. The full result is `[40,60,20,10]`. For the same fixture, the frozen V21 ABI remains `[30,60,20,10]`. Source `1` and target `50%` are also corrected from the floating value `0.5` to the FW integer `0`. Typed and untyped clamp, short-row exact fallback and overflow, the Release no-op, invalid IC, the INT16 baseline, and public GCC parity all have characterization. C nodes, ordering, bytes, and signed golden are unchanged.
- Completion evidence: Compared with `f0d16c8`, production and logic-first are both `+1 file / +19 physical / +20 nonblank`. The two fresh isolated deterministic Release builds were identical. The total DLL stays at `15,322,624 bytes`. Application bytes remain `693,248`, but the SHA changes from `D88E9F7F…E0CBD` to `45431F85…1068EF`. Focused 35, notch-core 156, notch-golden 6, GCC 8, Runtime Query 15, and ui-core 250 tests pass, and the UI build and lint pass. Both production mutations turn RED in the public parity fixture. The hidden UI/IPC forward and reverse V21/V22 signed C, golden, export state, and budget are all unchanged. Selection total p95 is `41 / 47 ms`. No performance improvement is claimed.
- `R13.103c-2` ([#34](https://github.com/Dennis40816/FreeformHelper/issues/34)) completes the null-sentinel domain contract. `NotchSettings.ValidateNullValueOrThrow` uses `0..UINT16.MaxValue` as the only Application validation owner. `ProjectSettings.ValidateOrThrow`, the V21 final projector, and the V22 final projector all share it. Step 5 and Runtime Query notch validation also go through the same settings validation entry first. The VM and direct UseCase fail fast before the report is built. The outermost Named Pipe layer returns the original message in the existing `IPC_ERROR` failure envelope. This prevents an exception from dropping the connection and turning into `EMPTY_RESPONSE`. Public exporter and simulation paths that actually project Firmware nodes throw the same exact exception for out-of-range values, before the formatter or evaluator runs. They do not silently clamp, migrate, or change the persistence schema or UI range. The empty C table and the unsupported simulation fast path are unchanged.
- Public `65536` and target `65535` fixture: Before the change, the C and GCC output for `[50,0]` and the simulation output for `[50,50]` showed invalid-input drift. After the fix, both entry points fail fast consistently. Runtime Query `notch-validation` changed from silent clamping to a direct exception and the exact `IPC_ERROR` message over IPC. The public pipe fixture first locked the old `EMPTY_RESPONSE` and then turned GREEN. A single C export snapshots the sentinel before it reads the caller-owned collections. When active-set enumeration is also changed from `65534` to `65535`, the old live-read implementation makes both the V21 and V22 fixtures RED. After the fix, the macro, projector, and formatter all still share `65534`. The `0／65535` settings boundary, and the legal boundary where the V22 target and V21 Legacy reference diff are `65535` with sentinel `65534`, all have C and GCC parity characterization. The old `NHC_DIFF_NONE` literal for the V21 custom-adjacent sentinel is intentionally changed to the numeric `65535`. The default-sentinel V21/V22 C bytes match signed golden exactly. Golden was not updated.
- Completion evidence: Compared with `493d5d6`, production and logic-first are both `+0 files / +16 physical / +16 nonblank`. The two fresh isolated deterministic Release builds were identical. The total DLL stays at `15,322,624 bytes`. Application stays at `693,248 bytes`, but its SHA changes from `45431F85…1068EF` to `827EF8A0…1EE5D8`. UI goes from `14,528,512 bytes` with SHA `D0BABA97…3431B` to `9B017D15…09DBC`. Focused 13, notch-core 168, notch-golden 6, GCC 8, RuntimeQueryUseCase 16, RuntimeQueryIpc 5, and ui-core 250 tests pass. The UI build and lint pass. The projector, settings, silent-clamp, IPC `EMPTY_RESPONSE`, and export live-read mutations are each caught RED by a public fixture. The hidden UI/IPC forward and reverse V21/V22 signed C, golden, export state, and budget are all unchanged. Forward and reverse selection total p95 is `53 / 40 ms`. The evidence is at `build/perf/r13103c2-forward-snapshot-final/regression-baseline-summary.json` (SHA-256 `31B3D8F4…AF85D`) and `build/perf/r13103c2-reverse-snapshot-final/regression-baseline-summary.json` (SHA-256 `ED200E05…5FB15`). No performance improvement is claimed.
- `R13.103d-1` ([#54](https://github.com/Dennis40816/FreeformHelper/issues/54)) converges Legacy request consistency. `LegacyNotchGenerationRequest` owns a copy of the enabled versions before the first progress callback. It freezes the effective V22 and V21 thresholds, `NullValue`, and `LenScale`. The legacy threshold admission and the V21 and V22 strategies read only this request. The callback still runs synchronously, and the caller's settings are visible immediately. However, a change only affects the next generation. The V22 strategy's CadAllocation compensation branch, which was reachable through re-entrant live mode, and its `allCadPads` parameter are removed.
- A public triangle RED test locks this behavior. When the callback changes the entry Legacy mode to CadAllocation, the old row's ToFull value changes from `100` to the hybrid value `200`. After the fix, the result is still the exact Legacy 9-int row. The 1x2 and caller-set fixtures further lock that a mid-run change to the linked or unlinked threshold, `NullValue`, `LenScale`, or the enabled versions does not affect rows that have not been built yet. The progress tuple and the V21 to V22 order stay the same. The new values are used only on the next call. The live-set and live-threshold mutations, and the original live-mode RED, were all run and then restored.
- Completion evidence: Compared with `36d7bb8`, production and logic-first are both `0 files / -3 physical / -4 nonblank`. The two fresh isolated deterministic Release builds were identical. The total DLL goes `15,330,816 -> 15,332,864 bytes` (`+2,048`). Only Application changes, from `697,856 / 699FF83D…744649` to `699,904 / 33FB9E60…FA6170`. Legacy targeted 6, generator 44, Application 212, notch-core 179, ui-core 258, smoke 24, UI snapshots 21, golden 2, exporter and GCC 30, and Runtime Query 18 tests all pass. The UI build and lint/analyzer have 0 warnings and 0 errors. The hidden UI/IPC forward and reverse signed C, golden, export state, and budget are unchanged. Selection total, Inspector, and preview p95 are `38/23/14` and `27/15/15 ms`. The evidence SHA-256 values are `57685C9D…395309F` and `82BF6DF4…FD47D5`. No performance improvement is claimed.
- `R13.103d-2` ([#60](https://github.com/Dennis40816/FreeformHelper/issues/60)) removes a fixed two-version Legacy strategy registry that had no injected consumer. Generation and eligibility now share an explicit V21/V22 switch. The two stateless compatibility algorithms become static owners. `INotchAlgorithmStrategy`, the dictionary, the test-only injection constructor, and the missing-strategy fake branch are all removed. Owned requests, thresholds, 9-int rows, comments, ordering, and progress stay exact.
- The public XWay, YWay, and XYWay matrix locks that a reversed configured set still outputs V21 then V22 in canonical order. Eligibility, row count, and anchor stay consistent. Two mutations, one that loosens V22 admission and one that swaps the two version builders, both turn RED. Compared with `f244c47`, production and logic-first are both `0 files / -61 physical / -53 nonblank`. The two fresh isolated deterministic Release builds were identical. The total DLL goes `15,329,792 -> 15,329,280 bytes` (`-512`). Only Application changes, from `696,832 / 06F8CF03…C5C0A4E` to `696,320 / 9899BC42…B952EBF`. The other three DLLs keep the same bytes and SHA. Application 218, notch-core 182, notch-golden 6, ui-core 258, smoke 24, Runtime Query, exporter, and GCC 48 tests, and the UI build and lint pass. The hidden forward and reverse signed C, golden, export state, and budget are unchanged. Selection total, Inspector, and preview p95 are `29/16/15` and `32/19/14 ms`. The evidence SHA-256 values are `F1F09E5A…B79A89` and `0581FE62…F1D12`.
- Historical status after R13.103a, R13.103b-1, R13.103c-1, R13.103c-2, R13.103d-1, and R13.103d-2 left the R13.103 parent open. Under the owner decisions of 2026-10-03/04, Legacy request-specific table/cache and compatibility convergence are accepted B1/B2 exceptions. They are not remaining g2 blockers. Normal A1/A2 evidence and owner-controlled parent and milestone exits still apply.
- The fixed V21/V22 compatibility dispatch in `LegacyRegularAnchor` must not become a second actual path in the normal flow.

### R13.104 Notch display and safety projection convergence

- `NotchDisplayProjector` only formats the resolved result.
- Remove the duplicated `>255.0` threshold derivation from `NotchDisplayProjector`. The overflow and EMS risk predicates are computed only once, in the shared policy and result.
- `SimulationSafetyOverviewProjector` and the workspace must not each build the same conclusion string.
- Add an EMS safety constant and predicate guard. This prevents View or ViewModel code from reintroducing a hard-coded cap or risk wording.
- `R13.104a-1` was completed by [#30](https://github.com/Dennis40816/FreeformHelper/issues/30). It narrowly converges the EMS after-cap decision. The existing `DefaultEmsAfterCap = 480` is unchanged. `SimulationSafetyAuditService.IsEmsAfterCapViolation` uses `afterValue > afterCap + 1e-9` as the only check. It is shared by audit violations, net-flow and target-coverage EMS classification, the Runtime Query regular snapshot, cell EMS status, and the workspace and overview high-risk projection. `cap` and `cap + 0.5e-9` are safe. `cap + 2e-9` is a violation.
- Public headless Simulation and Runtime Query workflows, plus two raw `>` mutations, lock in consistency. Compared with `0c46d69`, production is `+21 physical / +20 nonblank`. Two fresh isolated Release builds were both `15,322,624 bytes`, and the bytes and SHA of each of the four DLLs match exactly. Affected 59, ui-core 250, notch-core 148, golden 6, GCC 6, and Runtime Query 15 tests pass. The hidden UI/IPC forward and reverse gate, UI build, and lint all pass. The existing V21/V22 signed C matches exactly.
- `R13.104a-2` was completed by [#38](https://github.com/Dennis40816/FreeformHelper/issues/38). It narrowly converges the combined-overflow decision. `NotchV22TargetAllocationPolicy` produces `NotchV22TargetCoverageProjection`, which is used by both the generator and the revisioned resolved readers. For normal anchored target coverage, it counts only the anchor (max once) and the per-group rounded percent of the groups actually emitted (strict or ToFull). `NotchDisplayProjector` only formats the supplied ratio and risk. Unanchored public compatibility and non-target-coverage modes still use the Application owner's old all-target display fallback. Below-gate diagnostics, the global target-coverage guard, and the raw CAD-level `CombinedRatio` are unchanged.
- Public headless safe and true-overflow cases, the ToFull bypass, rounding, and `255/256` fixtures, plus three production mutations, lock in consistency. Compared with `f98c2e3`, production and logic-first are both `+73 physical / +67 nonblank`. Two fresh isolated Release builds were both `15,326,720 bytes`, and the bytes and SHA of all four DLLs match. Focused 16, notch-core 175, ui-core 252, golden 6, Runtime Query 18, and GCC 8 tests pass. The hidden UI/IPC forward and reverse gate, UI build, and lint all pass. The signed V21/V22 C matches exactly.
- `R13.104a-3` was completed by [#40](https://github.com/Dennis40816/FreeformHelper/issues/40). It narrowly converges target display membership. For normal anchored target coverage, the effective count, compact lines, and card role are projected only from the membership of `(IcIndex, DiffIndex)` in the supplied `EmittedTargets`. Rounded-zero values and other non-emitted diagnostics are still fully displayed as `Below gate`. The existing strict-only display is kept for `RawCombinedPercent == null` compatibility.
- Public Pad Info and compatibility fixtures, plus strict-only and UI `strict || ToFull` mutations, lock in consistency. A paired order-race test was first `3/3` RED. After it awaited the existing grid-rebuild idle, it became `3/3` GREEN. Only the test harness was stabilized. Production did not change. Compared with `0fe6416d`, production and logic-first are both `+22 physical / +21 nonblank`. The two fresh isolated Release builds were identical. The total DLL goes `15,326,720 -> 15,327,744 bytes` (`+1,024`). Only UI changes, `14,529,536 / D18E2F57…33467 -> 14,530,560 / 5BCB3018…AE304F`. The other three DLLs keep the same bytes and SHA. Focused public reader 41, notch-core 175, ui-core 253, golden 6, Runtime Query 18, and GCC 8 tests (GCC `15.1.0`) pass. The hidden UI/IPC forward and reverse gate, UI build, and lint all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `31/16/16` and `33/18/15 ms`. The evidence SHA-256 values are `DA135200…707C14` and `C33465AB…FA1034`.
- `R13.104a-4` was completed by [#42](https://github.com/Dennis40816/FreeformHelper/issues/42). It narrowly converges the base status of the Simulation audit. `SimulationSafetyTextProjector.BuildStatusText` projects `null/no cells -> Simulation not run`, `cells + violations -> EMS risk` and `cells + safe -> EMS OK` from the supplied audit. The workspace status and overview both read this. The stale suffix and build-failure unavailable state stay at the overview boundary. A public headless empty-regular-grid fixture and two mutations (binary, and ignoring HasCells) lock in availability consistency. The normal operator workspace builder's rejection contract for missing workflow inputs is unchanged.
- Compared with `ef408f6`, production and logic-first are both `+11 physical / +9 nonblank`. The two fresh isolated Release builds were identical. The total DLL stays at `15,327,744 bytes`, and the first three DLLs keep their bytes and SHA. UI bytes stay at `14,530,560`, but the SHA is updated to `2A133931…AD043A`. Focused 7, notch-core 175, Application 208, ui-core 253, smoke 24, golden 6, Runtime Query 18, and GCC 8 tests (GCC `15.1.0`) pass. The hidden UI/IPC forward and reverse gate, UI build, and lint all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `35/21/17` and `58/26/32 ms`. The evidence SHA-256 values are `17B745E4…F04EC41C` and `A2E1D970…DACA098`.
- `R13.104a-5` was completed by [#44](https://github.com/Dennis40816/FreeformHelper/issues/44). It narrowly converges the priority of Copper replay status. The existing positional constructor for the step is unchanged. Support state and the global-flow residual are stored as non-positional facts marked `[JsonIgnore]`, along the original result path. The step physical risk covers global-flow, net-flow, and target-coverage. The result then projects the any-unsupported, any-EMS, and any-physical facts. `SimulationSafetyTextProjector.BuildReplayStatusText` fixes the priority as `unsupported > EMS risk > audit warning > EMS OK`. The step status and the VM aggregate summary both read it. The artifact row copies the JSON-ignored global-flow fact. This keeps the existing structured risk consistent with the status, without adding any CSV, JSON, or clipboard field. The EMS count suffix is also unchanged.
- A public headless two-point replay fixture fixes the following: step 0 `Max After 200` with a global-flow-only `audit warning`, step 1 `Max After 399`, and the aggregate. The aggregate is corrected from a wrong `EMS OK` to the exact `audit warning`. The shared priority, structured facts, an artifact schema guard, and three production mutations lock in consistency. Compared with `bdab625`, production and logic-first are both `0 files / +32 physical / +26 nonblank`. The two fresh isolated Release builds were identical. The total DLL goes `15,327,744 -> 15,329,792 bytes` (`+2,048`). The bytes and SHA of the four DLLs are reproducible. Focused 17, Application 208, notch-core 175, ui-core 253, smoke 24, golden 6, Runtime Query 18, and GCC 8 tests pass. The hidden UI/IPC forward and reverse gate, UI build, and lint all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `44/18/21` and `40/18/26 ms`. The evidence SHA-256 values are `619E0484…89875B` and `0A4DD778…85C48`. No performance improvement or normal operator UI coverage is claimed.
- `R13.104a-6` was completed by [#46](https://github.com/Dennis40816/FreeformHelper/issues/46). It narrowly converges the provenance of the EMS cap when there are no cells. `SimulationSafetyTextProjector.FormatEmsAfterCap` uses the `AfterCap` from the supplied audit. Only a null audit falls back to `DefaultEmsAfterCap = 480`. The overview's no-cells cap and the export no-cells handoff prompt both read this projection. Public empty audit theories with `512` and null with `480`, plus three mutations (the overview constant, the export literal, and null-zero), lock in consistency. The availability status, schema, predicate, and layout are unchanged.
- Compared with `dd559a0`, production and logic-first are both `0 files / +6 physical / +5 nonblank`. The two fresh isolated Release builds were identical. The total DLL stays at `15,329,792 bytes`, and the first three DLLs keep their bytes and SHA. UI stays at `14,531,072 bytes`, but the SHA is updated to `CD7779FD…34E37`. Focused 12, notch-core 175, ui-core 253, smoke 24, golden 6, Runtime Query 18, and GCC 8 tests, plus the hidden UI/IPC forward and reverse gate, UI build, and lint, all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `40/19/20` and `34/18/16 ms`. The evidence SHA-256 values are `F874E56C…CE447A` and `4CD7EBB7…5EA40`.
- `R13.104a-7` was completed by [#48](https://github.com/Dennis40816/FreeformHelper/issues/48). It narrowly converges the status of physical audits. For an audit that has cells, `SimulationSafetyTextProjector.BuildStatusText` uses one shared priority: `EMS risk > audit warning > EMS OK`. Physical risk covers the existing global-flow, net-flow, and target-coverage facts. Null or no-cells still shows `Simulation not run`. The workspace and overview both read this base status. The overview `HasRisk` still means only EMS danger. `NeedsAttention` includes physical risk and stale, and for a non-stale summary it appends the existing physical evidence. A public global-flow-only fixture, plus the EMS priority and three status, attention, and summary mutations, lock in consistency. The export-selection warning chip, its predicate, its counts, the schema, and the layout are unchanged.
- Compared with `e1c92d0`, production and logic-first are both `0 files / +6 physical / +6 nonblank`. The two fresh isolated Release builds were identical. The total DLL stays at `15,329,792 bytes`, and the first three DLLs keep their bytes and SHA. UI stays at `14,531,072 bytes`, but the SHA is updated to `08878462…F95AB`. Focused 14, Application 208, notch-core 175, ui-core 253, smoke 24, golden 6, Runtime Query 18, and GCC 8 tests, plus the hidden UI/IPC forward and reverse gate, UI build, and lint, all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `27/15/14` and `31/18/15 ms`. The evidence SHA-256 values are `3BFF980B…04D199` and `577A9BB1…0A229`. No performance improvement is claimed.
- `R13.104a-8` was completed by [#50](https://github.com/Dennis40816/FreeformHelper/issues/50). It narrowly converges the Notch export review reader for physical warnings. The badge reads the shared `BuildStatusText` priority. The physical-only summary reuses `BuildPhysicalAuditSummaryText`. A physical-only audit is no longer labeled clean, but it still does not block export. A new VM visibility fact only drives the existing `chipStatus warning` presentation. No new style or token is added. The clean, EMS block, and availability wording stay exact.
- A public fixture with `Before=400`, `After=450`, and a global residual of `+50`, the clean, EMS-priority, and availability compatibility checks, a static XAML guard, and three production mutations lock in consistency. Compared with `cb73c8c`, production is `+17 physical / +16 nonblank`, and logic-first is `+10 / +9`. The two fresh isolated Release builds were identical. The total DLL goes `15,329,792 -> 15,330,304 bytes` (`+512`). Only the UI bytes and SHA are updated. The final UI is `14,531,584 / 956A3FA5…06AFC9`. Focused 65, Application 208, notch-core 175, ui-core 256, UI snapshots 20, smoke 24, golden 6, Runtime Query 18, and GCC 8 tests, plus the hidden UI/IPC forward and reverse gate, UI build, and lint, all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `40/25/17` and `35/20/16 ms`. The evidence SHA-256 values are `981C85CA…4B32D8` and `979B4E22…DD664F`. No performance improvement is claimed.
- `R13.104a-9` was completed by [#52](https://github.com/Dennis40816/FreeformHelper/issues/52). It narrowly converges the active Notch safety guidance. `SimulationSafetyTextProjector` owns three templates: short, Simulation policy, and export handoff. The main VM supplies the current overview cap and notifies the dependent properties. Settings uses the same owner, with the default `480`. The Step 5 overview tooltip reads the existing dynamic guidance property. No new layout, style, or token is added.
- A public one-cell fixture with `AfterCap=512`, default and null `480` compatibility, three dependent notifications, an active-tooltip source guard, and three mutations lock in the fix. The same screen no longer shows two EMS policies at once. This slice does not change the default cap, the predicate, the audit, the target-coverage calibration, Runtime Query, persistence, or the firmware contract.
- Compared with `e1b0b9c`, production and logic-first are both `0 files / +21 physical / +16 nonblank`. The two fresh isolated Release builds were identical. The total DLL goes `15,330,304 -> 15,330,816 bytes` (`+512`). Only UI changes, to `14,532,096 / 67C40172…D7AF33`. Focused 32, Application 208, notch-core 175, ui-core 258, UI snapshots 21, smoke 24, golden 6, Runtime Query 18, and GCC 8 tests, plus the hidden UI/IPC forward and reverse gate, UI build, and lint, all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `37/23/16` and `34/17/19 ms`. The evidence SHA-256 values are `97152F5A…41A1EA` and `DD69E750…7DA0EB`. No performance improvement is claimed.
- `R13.104a-10` was completed by [#62](https://github.com/Dennis40816/FreeformHelper/issues/62). It narrowly converges the provenance of the target-cap help text. `SimulationSafetyTextProjector.BuildNotchTargetCoverageCapHelpText` formats the current target cap, the uniform `400` mapped to After, and the EMS cap used for comparison. Active Step 3 supplies the current overview cap. Settings keeps the documented default cap. The two existing tooltips read the same template. No new control, layout, style, or token is added, and the target guard and EMS computation are not changed.
- Public headless Step 3 tooltip, VM, and Settings fixtures lock in the following. With a target cap of `128%`, the active view shows exactly `After 512 / EMS cap 512`, and Settings shows exactly `After 512 / EMS cap 480`. Static XAML, the default active cap, and the dependent-notification three mutations all turn RED, and then are restored. Compared with `32f4469`, production and logic-first are both `0 files / +23 physical / +20 nonblank`. The two fresh isolated Release builds were identical. The total DLL goes `15,329,280 -> 15,329,792 bytes` (`+512`). Only UI changes, to `14,532,608 / 44805041…29B52`. Focused 19, ui-core 260, smoke 25, notch-core 182, golden 6, Runtime Query, exporter, and GCC 48, the hidden UI/IPC forward and reverse gate, UI build, and lint all pass. The signed V21/V22 C matches exactly. Selection total, Inspector, and preview p95 are `40/20/19` and `39/23/21 ms`. The evidence SHA-256 values are `D4BE8E99…A08D95D` and `36915EDF…2A3EE3`. No performance improvement is claimed.
- Remaining hard-coded cap and Simulation/replay/display text convergence stays recorded as follow-up scope. The owner decision of 2026-10-04 closes g2 with normal A1/A2 evidence; C1–C6 do not block closure, and B1/B2 remain accepted exceptions. The earlier Legacy-convergence blocker is historical; parent and milestone exits remain the owner's decision.

Exit criteria: close normal A1/A2 with the single-owner result and final-projection evidence plus G2/G3/G4 validation. B1/B2 remain accepted exceptions and C1–C6 are nonblocking under the owner's 2026-10-04 decision; parent and milestone exits remain the owner's decision.

---

## 7. g3 — Matching and Domain state

### R13.201 Define matching bounded contexts

- Execution status and delivery evidence: the R13.201 entries of the former `TODO.md` (git history).

Keep three distinct contracts:

1. `PadMatcher`: many-to-many overlap evidence between CAD and Regular.
2. `DxfRegularMappingAnalyzer`: configurable one-to-one audit/suggestion that a person can override.
3. `PadCanvas` selection/hit-test: pure UI geometry interaction.

Do not merge the three into one large `PadMatchingService`.
`R13.004c-2` only removes the live UI owner of the obsolete setting. The `MatchingSettings` compatibility parameter of `PadMatcher`/`PadMatchService` stays until this item removes it together with the overlap evidence API boundary. It is not changed in g1, to avoid silently altering the public seam.

### R13.202 Preserve distinct best-match rules

- Owner decision (2026-10-05): keep the status quo. `CadBestMatchSeedService` and PadCanvas hover retain their own rules; share only parts proven identical, with no user-visible change. R13.202 may close as status quo or a reduced scope.
- The 3635 measurement covers 4,838 CADs: 582 touch two or more regulars, the two rules select different regulars for 6 CADs, and there are 0 exact ties. The owner selected 「維持現狀 (Recommended)」 ("Keep the status quo (Recommended)") on 2026-10-05: when scores are exactly equal, add no ID tie-break to PadMatcher sorting or R13.202 selection. Preserve current behavior; any later change is a separate behavior-change item. The ID tie-break question is resolved. See [INV2, section 2](../reviews/r13-slice-inventories-2026-10.md) for the rule inventory and the R13.202 entries of the former `TODO.md` (git history) for execution status and characterization PRs.
- DXF audit may share overlap evidence, but keeps its own score, one-to-one allocation, and override policy.
- `SelectCadAllocationAnchor` and `GetFreeformType` first build parity and difference cases from the same inputs. The former is the allocation anchor, and the latter is classification. They cannot be merged just because both pick the highest score. If they are proven to share the same ordering and evidence, extract a shared anchor projection. Both consumers still decide their own result semantics.

### R13.203 Typed ID incremental introduction

- The typed-ID boundary work and review are tracked by the R13.203 entries of the former `TODO.md` (git history); R13.201 is its API dependency.

- Introduce `CadPadId`, `RegularPadId`, `IcIndex`, and `DiffIndex` in batches, one boundary at a time.
- Convert one API chain at a time and keep adapters. Do not rewrite the whole repo at once.
- Equality, ordering, and JSON/CSV/CLI parsing must have legacy equivalence tests.

### R13.204 RegularPad state transition API

- Matched-pair assignment and the broader writer-transition scope are tracked separately by the R13.204 entries of the former `TODO.md` (git history).

- Inventory the writers of `IcIndex`, `DiffIndex`, `MatchedCadPadId`, `MatchScore`, and `Freeform`.
- Centralize invariants and invalidation behind explicit assign/replace/apply APIs.
- The first phase only encapsulates existing semantics and adds no new rejection rules. New lifecycle rules go into a separate correctness slice.

### R13.205 Explicit DxfRegularMaskAudit pipeline

- Audit-phase characterization and the production pipeline scope below are tracked separately by the R13.205 entries of the former `TODO.md` (git history).

- Make the ordering dependencies between segment offset, local repair, and passive compensation explicit through context and step results.
- Keep the existing distinct decision sources and reason codes.
- The TM8.1 matrix is a mandatory gate.

### R13.206 CoordinatePlanner transform builder

- Transform delivery evidence and execution status: the R13.206 entries of the former `TODO.md` (git history).

- Consolidate the repeated machine, normalized, pixel, world, and safe coordinate projections for point, line, and rectangle into a single parameterized transform/builder.
- The builder only unifies coordinate conversion. It does not mix in guide, BIST, or custom array/path feature policy.
- Add before/after snapshot equivalence using the existing `CoordinatePlannerComputationServiceTests`, and treat G2/G3 as low-cost Application-layer insurance.

Completion criteria: all planner artifacts use the same transform contract. Existing keys, ordering, raw/safe coordinates, and output snapshots show zero difference.

Exit criteria: preserve the distinct CadBest and hover rules and all user-visible results; share only evidence or calculations proven identical. Consult the R13.201–R13.206 entries of the former `TODO.md` (git history) for execution status. The g3 exit remains the owner's decision.

---

## 8. g5 — Settings and Presentation orchestration

### R13.301 Settings draft completeness

- Replace the manual diff/copy drift across roughly 50 fields with an explicit draft snapshot/binding map.
- The draft must record the original plus a dirty-field change set. After a non-modal Settings window opens, live changes from Canvas or Header must not be overwritten by stale snapshots of unedited fields.
- Dedicated tests must verify each field individually for: Open, Apply, Discard, Reopen, project roundtrip, and app-general deferred write.
- The 3635 golden tests can serve only as downstream insurance. They cannot replace SettingsWindow operation tests.

### R13.302 Single path for Settings side effects

- All entry points converge on the existing settings apply policy/orchestration.
- Explicitly lock down selection clear, rebuild, downstream invalidation, focus/step, undo, status, and fit/zoom.
- WorkspaceHeader, SettingsWindow, and RightWorkflowPanel may have multiple entry points, but they must share the same apply plan.
- Workflow readiness and completion must come from a revisioned execution result state, not from row count, preview item, or summary string. A legitimate zero-result outcome is still Completed. After an input change, the state must become explicitly Stale.
- Step2, Step4 diagnostics, and Step5 export presentation must each have typed invalidation. After Apply, old results must not continue to be marked as current.

### R13.303 Simulation color and brush policy

- Share the simulation intensity/color scale, but only move numeric mappings that are truly identical.
- `NotchApplySimulationAaView` auto-scale and color mode remain presentation policy. Share the existing `SimulationColorScaleResolver`. Do not move them into Domain or Application business results unless a non-visual consumer appears.
- Owner decision (2026-10-04): in g5, after the g3 exit, use 0.9 as the opacity fallback for AaView and HeatmapView. This approved visual change requires updated snapshots. Do not expand it into a global table of defaults.
- Share the 4 duplicated `GetBrush(Color)` cache primitives. Do not absorb View-specific resource lookup or cache lifetime.
- When removing code-behind magic fallbacks, first add tokens and runtime guard tests. If the scope goes beyond the known color/opacity views, open a follow-up slice rather than expanding R13.303.

### R13.304 Console behavior consolidation

- Deduplication compares structured log identity, not formatted lines that contain timestamps.
- Merge the use cases shared by shell-hosted and event-hosted modes. The View only forwards actions.
- Preserve the contracts for copy, jump, font, filter, and auto-follow.

### R13.305 Normal-flow settings surface reduction

- Owner, 2026-10-03: confirmed the direction of `R13.305a`. Step4 mapping and Step6 validation diagnostics move out of the numbered flow into an unnumbered Diagnostics area.
- Settings and workflow show only parameters that users must decide during normal operation. Fields that are not routine manual adjustments must not be placed in the Step flow.
- Coordinate pixel X/Y, Step4 mapping weights, candidate count, and confidence/ambiguous thresholds leave the normal Settings and workflow. If calibration support is still needed, it may only live on a Dev or diagnostic surface.
- `ProjectSettings + ProjectUiSnapshot`, defaults, and old project JSON roundtrip are fully preserved. The first phase only adjusts entry points and visibility. It does not delete the persistence schema.
- Display-only, derived, automatic-policy, and compatibility fields must each have a single owner. Hiding them from the UI must not create a second hidden mutable state.
- Move the Layer category batch action out of draft settings. The DXF workspace's immediate-operation owner takes it over, so Cancel cannot leave an unrolled-back mutation.
- The numbered normal flow is fixed as Step3 directly to Step5. Step4 mapping and Step6 validation remain in the unnumbered Diagnostics/Inspector, and normal users are not required to run them manually.
- Remove non-operable derived toggles and placeholders. For R13.305b, the owner decided on 2026-10-04: "Do not turn off the last remaining version." Neither V21 nor V22 may be switched off when it is the last enabled version. Implement this bidirectional guard in g5, after the g3 exit.

Completion criteria: non-manual parameters no longer interfere with the normal flow. Required workflow actions, old project roundtrip, 3635 V21/V22 C, and the main UI operating experience remain unchanged.

Exit criteria: Settings and presentation side effects each have a single owner, and dedicated tests can verify them independently.

---

## 9. g6 — Workspace ViewModel decomposition

### R13.401 Establish the root shell boundary

- `FreeformHelperViewModel` retains project session, workspace navigation, and the command compatibility facade.
- Establish the child VM contracts first. Do not move state yet.
- List all commands, property callbacks, View events, RuntimeQuery readers, and side effects.

### R13.402 DxfWorkspaceViewModel

- Move DXF load, edit, layer, and indexing orchestration.
- Project session updates go through explicit result/event flows. They must not directly mutate private root fields.

### R13.403 MatchingWorkspaceViewModel

- Move Step1 and Step2 UI state and command binding.
- Computation remains delegated to the Application/UI use cases. Do not rewrite matching inside the child VM.

### R13.404 NotchWorkspaceViewModel

- Move Notch generation, preview, validation, and export selection UI state.
- Read only the resolved result model from g2.

### R13.405 ProjectSessionViewModel facade consolidation

- Save/Load, Ctrl+S, RuntimeQuery, and project settings persistence keep their existing public contracts.
- Remove migrated root state and add cross-workspace integration tests.

Exit criteria: the root VM is noticeably thinner. UI, IPC, keyboard shortcuts, and project roundtrip behavior are unchanged. G2 through G5 are all green.

---

## 10. g7 — UI structure and token consolidation

### R13.501 Styles responsibility split

- Split the workspace, DXF, and validation responsibilities of `Controls.Core.axaml`.
- Keep include and selector precedence. Do not change hover, disabled, or checked behavior because of file moves.
- Introduce `BasedOn` only for action roles with identical semantics. Merge duplicate `ControlTemplate`s only after structural comparison confirms equivalence one by one. Do not create cross-role inheritance chains.
- Verify using DevView and UI guards.

### R13.502 DevView preview extraction

- Split the large preview area into section controls.
- Keep the action role laboratory and the token probe, plus the disabled/checked/icon/chip/status matrix.
- DevView is not moved out of the official source tree and is not deleted.

### R13.503 Shared workbench shell

- Extract only the layout shell that is truly identical between Simulation and Coordinate.
- Inventory the repeated skeletons of section cards, overview cards, and settings tiles. Extract a shared Control/style only when structure, density, spacing owner, and interaction contract are all identical. Otherwise keep role-specific views.
- Follow the single spacing owner, `panelFormField`, and the two-column token contract.
- Do not force different workflow actions into a generic control.

### R13.504 Token and naming cleanup

- Merge only synonymous tokens or incorrect legacy aliases.
- Keep single-use tokens that have a clear meaning.
- The `WorkspaceHeader` popup minimum and inset boundaries (currently `260`/`200`/`30`) are provided by theme-aware tokens or resources. Code-behind must not keep layout magic literals.
- Before renaming `notchExport*` or `workspace*`, scan for `Classes.Contains` and code-behind selector consumers. Handle the orphaned banner first, then the `Controls.Core` bulk aliases, while keeping them in the same semantic slice and preserving selector precedence.

### R13.505 Control initialization contracts

- Change the `NumberScrubber` initialization order into an explicit contract, and add a Settings UI regression test.
- Exclude `BalancedWrapPanel` from the public mutable state fix. The scan hit is the private nested `Row.Children`, not externally accessible control state.

### R13.506 PadCanvas engine seams

- Replace the implicit dependencies that `PadCanvasSelectionEngine` and `PadCanvasVisibleDrawListBuilder` have on their entire owner with narrow interfaces or explicit state-and-result seams. This makes hit-test, viewport query, decimation, and selection side effects independently testable.
- Do not move Avalonia pointer handling, zoom/pan, or draw-list caching into Application. This item is a UI testability and dependency-direction fix, not the addition of a global DI container registration.

### R13.507 PadCanvas view-only geometry ownership audit

- Area-bucket membership is not a pure brush detail. Currently `PadCanvas.Caches` computes buckets separately, while `CadAreaBucketService` is already used for selection. Project colors from a shared service/result instead, eliminating the second derivation between selection and coloring.
- Freeform hatch `Rect` clipping, screen-to-world spacing, and Avalonia `DrawingContext` belong to the renderer and explicitly stay in the UI. Extract a pure geometry helper only when a non-Avalonia consumer appears, or when the same geometry result gets a second reader.
- R13.303 owns AaView color and auto-scale. This item does not move them again.

Completion criteria: under the same tolerance, area-bucket selection and coloring read the same membership. The hatch ownership decision has test or document evidence, and no speculative move is made for layer purity.

Exit criteria: no-visual-change evidence is complete. DevView, UiLayoutGuard, headless smoke, rendered snapshot dry-run, and lint are all green.

---

## 11. Old proposals that will not be executed or must be rewritten

| Old proposal | Disposition for 1.0.x |
|---|---|
| Golden baseline is currently broken | Disproven. As of 2026-07-20, 103/103 green |
| Encapsulate the `BalancedWrapPanel` public list | Not executed. False positive for a private nested helper |
| Switch `LoadingSpinner` to `LoadingSpinnerDesignSize` | Completed. No longer listed as work |
| Fix 5 CRLF files | Current `git ls-files --eol` shows no `w/lf` or `w/mixed`. Not listed as work |
| Merge all matcher/audit/hover logic | R13.201/R13.202 preserve distinct rules. Share only parts proven identical under the 2026-10-05 owner decision |
| Move exporter to Application | Already in Application. Replaced by the R13.103 projection/format split |
| Delete a large number of single-consumer tokens | Not executed. Only semantic aliases and consolidation are done |
| Remove or move out DevView | Not executed. Replaced by R13.502 preserving the preview contract |
| Verify Settings draft only through golden tests | Not accepted. R13.301 adds dedicated Apply/Discard/roundtrip tests |
| Split the entire root ViewModel in one pass | Not accepted. R13.401–R13.405 migrate one workspace at a time |


---

## 12. Fixed execution template for each slice

1. Read this contract, `ROADMAP.md`, and the dependency graph.
2. Inventory: list entries, readers, writers, and side effects.
3. Write or update targeted regression tests.
4. Implement a single `R13.*` slice.
5. Run:

   ```powershell
   ./scripts/dev/prepare-ui-workspace.ps1
   dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -p:UseAppHost=false --nologo
   # targeted tests
   ./scripts/tests/lint.ps1 -UseNoAppHost
   ```

6. Depending on risk, also run G2, G3, G4, or G5.
7. Update `ROADMAP.md` and any necessary canonical docs.
8. One slice per commit. The commit body records the single entry, single result, side effects, and verification results.
9. Push the branch before starting the next slice.

Additional commands to run before merge:

```powershell
./scripts/dev/prepare-ui-workspace.ps1
./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -IncludeUiSnapshots
```

---

## 13. Progress and change control

- This contract defines scope and gates. `ROADMAP.md` is NFH's single status table. It holds version order, target versions, and status. Handoff and WIP documents reference IDs without restating execution status. The integrator verifies merges and writes `ROADMAP.md` back once per batch.
- Approval after trunk integration (owner, 2026-10-05): 「可以，但手動解衝突要重批 (Recommended)」 ("Allowed, but manual conflict resolution requires approval again (Recommended)"). After owner approval, a new commit that only merges trunk cleanly needs no new approval if tests pass. Any manual conflict resolution requires owner approval again.
- Integration batches awaiting review have no limit (owner, 2026-10-05: 「不設上限」 ("No limit")).
- A version may not skip the exit criteria of the previous version. Slice order may be adjusted within the same version, but the dependency rationale must be recorded.
- Newly discovered refactoring items are written into `ROADMAP.md` first, then assigned to a version. They do not directly expand the current slice.
- If the same blocking condition recurs consecutively, narrow the slice or add a guard first. Do not bypass it by updating golden files, relaxing lint, or suppressing warnings.
- Any change to computed output is a correctness change and must not be mixed into a pure refactor or UI commit.

### Milestone handoff

When each 1.0.x milestone is completed, record:

1. The completed `R13.*` items.
2. Changes to single-entry and single-result.
3. Whether side effects changed.
4. The actual results of G0 through G6.
5. Whether golden files were updated. If so, attach the sign-off record.
6. Remaining risks and the first slice of the next version.
