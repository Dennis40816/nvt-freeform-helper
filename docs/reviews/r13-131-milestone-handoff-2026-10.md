# 1.3.1 Milestone Handoff (2026-10)

This handoff follows the six "Milestone handoff" fields in the roadmap. It adds the validation evidence for the final PR-B and PR-C revisions. This document does not check the R13.101 to R13.104 parent items. Whether the 1.3.1 milestone exits is still the owner's decision. (`TODO.md:R13.101`, `R13.102`, `R13.103`, `R13.104`; `docs/guides/refactor-roadmap-1.3.x.md:Milestone handoff`)

Current source note (2026-10-05): the exit-audit document now exists in this worktree; section 7 records the public programme snapshot. Historical source note: The sources are fixed as follows. Implementation status comes from `TODO.md:R13.101` to `R13.104` in this worktree. The final validation maps to PR-B tip `2e3cbd08` and PR-C tip `4d54b35d`. The designated exit-audit file does not exist in this worktree. I referenced `docs/reviews/r13-131-exit-audit-2026-10.md` read-only from local commit `1c195dc0439963c2b1ee15ad9e643edcb15a0590`. In the text below, "audit A1/A2/B1/B2/C1 to C6" refers to the "de-duplicated remaining obligation classes" in that version. The `E:` evidence inside the audit still refers to the implementation commits it records. It is not the final revision re-verification in section 4. (`TODO.md:R13.102a-3b`, `R13.102a-4`, `R13.102a-5`; audit "Sources and limitations")

Current approval (2026-10-04): the owner approved the PR-C firmware corner case in public PR #3 at `9810edf9` and in chat. Historical delivery description: Delivery runs in three PRs, stacked on `origin/1.3.x = 2c1c0c84`. PR-A is on another branch. It uses `NotchDisplayProjector` so that Disabled-target display matches the generator (the owner decided "make the screen agree with the generator"). It changes only the displayed numbers. PR-B covers this tree up to `2e3cbd08`. It contains the R13.104 safety-text centralization, the R13.102 resolved readers (including PadInfo `resolvedDisplayInput`), A1 facts characterization, and duplicate removal. Its output is zero-diff. PR-C is R13.102a-5 at `4d54b35d`. Diagnostics and warm export share the Q7-positive allocation set. It includes the firmware corner case disclosed in section 6, which requires explicit owner approval. The commit SHAs in this document identify commits on the private trunk. The public repo rebuilds these commits from patches, so the SHAs differ but the content is the same. When comparing, match by commit title.

Update (2026-10-04): the owner approved the PR-C firmware corner case in public PR #3 at head `9810edf9` and in chat, as relayed by the Commander session. The owner also decided that 1.3.1 closes with A1/A2 and that C1–C6 do not block exit. These decisions supersede the earlier pending firmware-approval and C-class proposal statements. They do not settle other open questions or change parent checkboxes. The dated public PR state is in section 7.

## 1. Completed R13.* items

| Completed item | Recorded result and basis |
| --- | --- |
| R13.101a, R13.101b | The per-IC candidate pool uses an owned snapshot, which removes repeated copying and scanning. The allocation memo identifies geometry changes under the same ID using immutable polygons and exact geometry comparison. (`TODO.md:R13.101a`, `R13.101b`) |
| R13.101c-1, R13.101c-2a | The generated-table key no longer includes `ExportProfile`. Live guard and cap-only setting changes are reduced to final-projection-only invalidation. (`TODO.md:R13.101c-1`, `R13.101c-2a`) |
| R13.101d-1, R13.101d-2, R13.101e | The normal CadAllocation computation context is frozen. UI and generator use the same Q7-positive pool admission. The full compensation context runs through the single `Compute(context)` path. (`TODO.md:R13.101d-1`, `R13.101d-2`, `R13.101e`) |
| R13.102a-1 (with PadInfo follow-up), R13.102b-1, R13.102b-2 | Preview and deferred Inspector share a revisioned resolved snapshot. Detail and the override-aware Runtime Query projection use the same owner. The PadInfo follow-up captures a single `resolvedDisplayInput`. (`TODO.md:R13.102a-1`, `R13.102b-1`, `R13.102b-2`; audit "old remaining items already excluded by the latest evidence") |
| R13.102a-2a, R13.102a-2b-1 | The normal export and generator reuse an output-request-neutral candidate batch. Generation completion rejects stale results by epoch, final-projection revision, and Simulation source identity. (`TODO.md:R13.102a-2a`, `R13.102a-2b-1`) |
| R13.102a-2b-2a, R13.102a-2b-2b | Concurrent Export and Simulation share a full batch-resolution task. The current single selected sparse result is attached to the batch session. The VM's parallel compensation dictionary is removed, which closes the bounded bridge for 2b-2. (`TODO.md:R13.102a-2b-2a`, `R13.102a-2b-2b`) |
| R13.102a-3b (Facts 2–6; PR-B) | Characterization evidence for anchor IC filtering, source area, Q7-positive admission, and retained ABI projections. See [TODO.md R13.102a-3b/4/5](../../TODO.md) for delivery and owner closeout. |
| R13.102a-4 (PR-B) | Normal reader Facts 2 and 3 now read the owner's ToFull count, the single anchor target area, and the existing Stage D fallback. Duplicate IC filtering, Max, and the non-negative clamp are removed. (`TODO.md:R13.102a-4`; `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate`) |
| R13.102a-5 (PR-C) | The raw allocation builder is removed. Compensation diagnostics, warm export, and the generator share the existing Q7-positive allocation set. A single Q7=0 diagnostic changes. Mixed-allocation review followed by export changes the V21 and V22 rows and the FW apply simulation, so warm now equals cold. Cold generator and golden-input outputs are unchanged. The owner approved the firmware corner case on 2026-10-04 in public PR #3 at `9810edf9` and in chat. (`TODO.md:R13.102a-5`) |
| R13.103a, R13.103b-1, R13.103c-1, R13.103c-2 | Final ABI characterization is locked. Threshold admission moves to final row projection. V22 export and simulation share the Firmware projector. NullValue validation has one shared owner for `0..65535`. (`TODO.md:R13.103a`, `R13.103b-1`, `R13.103c-1`, `R13.103c-2`) |
| R13.103d-1, R13.103d-2 | The Legacy request is frozen before the progress callback. A fixed V21/V22 dispatch replaces the strategy registry. The request-specific table cache is kept. (`TODO.md:R13.103d-1`, `R13.103d-2`) |
| R13.104a-1 to R13.104a-3 | The EMS cap predicate is shared. Combined overflow, effective target count, line, and role read the shared target eligibility and membership projection. (`TODO.md:R13.104a-1`, `R13.104a-2`, `R13.104a-3`) |
| R13.104a-4 to R13.104a-8 | Simulation and replay availability, severity, physical audit warning, and no-cell cap text share one projector. Export review reads the same audit result. (`TODO.md:R13.104a-4`, `R13.104a-5`, `R13.104a-6`, `R13.104a-7`, `R13.104a-8`) |
| R13.104a-9 to R13.104a-14 | Safety guidance, target-cap help, initial cap, guard summary and checklist, export-blocked text, and workspace cap and high-risk labels are consolidated into the shared projector. (`TODO.md:R13.104a-9`, `R13.104a-10`, `R13.104a-11`, `R13.104a-12`, `R13.104a-13`, `R13.104a-14`) |

Facts 2 to 6 of A1 FACTS are characterized. `R13.102a-3b` is still unchecked. On 2026-10-03 the owner ruled 「診斷跟 generator 一致，Q7=0 不顯示」 ("diagnostics match the generator; Q7=0 is not shown"). PR-C implements this as R13.102a-5. The diagnostics decision was separate from the mixed-allocation firmware approval. The owner granted that approval on 2026-10-04 in public PR #3 at `9810edf9` and in chat. (`TODO.md:R13.102a-3b`, `R13.102a-4`, `R13.102a-5`)

## 2. Single-entry and single-result changes

- PR-A's Disabled-target display is projected by `NotchDisplayProjector`, so the numbers match the generator. This PR is on another branch. In delivery order it comes before PR-B and PR-C on this tree.

- The normal generator and UI first build the full compensation context, then go through the single `Compute(context)`. The old public multi-parameter entry only fills in the context and delegates thinly. An explicit empty allocations set is authoritative empty. (`TODO.md:R13.101d-1`, `R13.101e`)
- For the same selection and revision, preview, Inspector, Detail, and Runtime read the revisioned `NotchV22ResolvedResult`. On the normal PadInfo snapshot path, a single `resolvedDisplayInput` is projected. An override under Runtime `multi-owner` builds only one corresponding result. Metadata uses the lightweight Inspector snapshot. (`TODO.md:R13.102a-1`, `R13.102b-1`, `R13.102b-2`, `R13.102a-2b-2b`)
- The normal Export and Simulation full consumers share one resolution task. Each projects from the final request frozen at its entry. A full batch reuses at most the current single selected sparse result. This does not mean that all candidates and consumers share one result across the repository. (`TODO.md:R13.102a-2a`, `R13.102a-2b-2a`, `R13.102a-2b-2b`, `R13.102a-2`)
- PR-B characterizes Facts 2 to 6 under R13.102a-3b. R13.102a-4 makes the normal generator's Facts 2 and 3 read the resolved allocation and compensation owner directly. The output is zero-diff. The ABI clamp, chunk, and ordering projections for Facts 4 and 6 are kept on purpose. (`TODO.md:R13.102a-3b`, `R13.102a-4`; `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate`)
- PR-C's R13.102a-5 removes the `BuildCompensationAllocations` raw builder. `CreateContext` and the compatible `Compute` share the existing `q7 > 0` predicate in `NotchAllocationService.BuildAllocations`. Diagnostics and the resolved target use the same allocation set as the generator. Real export reuses the reviewed warm resolved result. As a result, warm rows from mixed Q7=0 and Q7-positive allocations change to cold values, and the FW apply simulation also consumes this set of rows. The owner gave explicit approval for this output change on 2026-10-04. It remains outside PR-B's zero-diff scope. (`TODO.md:R13.102a-5`)
- Final threshold admission produces a shared admitted view. The V22 C formatter and the simulation both read the projected nodes from `NotchV22FirmwareProjector`. It is an existing contract that the simulation aggregates Actions after integer physics. This must not be treated as recalculating compensation. Release no-op filtering is still the exporter's emission policy. (`TODO.md:R13.103b-1`, `R13.103c-1`; audit "old remaining items already excluded by the latest evidence")
- Target coverage and overflow are owned by the Application policy. Display only formats the supplied ratio and risk. EMS predicate, Simulation and replay severity, and safety text are projected by shared owners. Completing this scope does not mean the `R13.104` parent has exited. (`TODO.md:R13.104a-1` to `R13.104a-14`, `R13.104`)

## 3. Whether side effects changed

Some local policies changed, and some side-effect contracts are explicitly kept. Items the sources do not record one by one are not presumed to be globally unchanged. (`TODO.md:R13.101c-2a`, `R13.102a-2b-1`, `R13.103d-1`, `R13.104a-12`)

| Scope | Change or retention, with basis |
| --- | --- |
| Guard and cap invalidation | Direct Step3 controls and Settings Save share `InvalidateNotchFinalProjection`. They keep the Step3 revision, per-CAD result identity, and export batch. They clear only the Step5 summary, last table, and validation, and notify Simulation of the source revision. Only a full Step5 clear resets operation progress and busy state and evicts the batch. (`TODO.md:R13.101c-2a`) |
| Completion and busy lifetime | Only the current completion may commit the row count, table, progress, and continuation. Final-only invalidation keeps the batch but resets the projected row count to zero. It rejects old projections and sessions. Full invalidation rejects the old store. Export and Simulation share a ref-counted busy scope, so finishing one item does not end the other's busy state early. (`TODO.md:R13.102a-2b-1`, `R13.102a-2b-2a`) |
| Deferred Inspector and PadInfo | Debounce, pending state, cancellation, stale rejection by selection and revision, and UI-thread apply are kept. A full-key recheck before applying the cold Detail result prevents overwriting the current identity. The PadInfo follow-up adds no selection clear, rebuild, fit or zoom, cache invalidation, or status side effect. (`TODO.md:R13.102a-1`, `R13.102b-1`) |
| Callback freeze | Normal generation keeps the timing that freezes after the phase-2 initial callback returns. Legacy now freezes the request before the first progress callback. Callback changes still return to the caller, but they affect only the next generation. (`TODO.md:R13.101d-1`, `R13.103d-1`) |
| Validation failure | An out-of-range NullValue fails fast before Firmware formatting and evaluation. The Named Pipe boundary returns the original message in the existing `IPC_ERROR` envelope. Empty table and unsupported simulation keep their fast paths. (`TODO.md:R13.103c-2`) |
| Audit warning and notification | A physical-only audit shows `audit warning` in status, overview, and export review. The export safety clean flag is false, but export is not blocked. A current cap change notifies the three guidance properties. The workspace overview's `HasRisk` still means only EMS danger. `NeedsAttention` includes physical risk and stale state. (`TODO.md:R13.104a-7`, `R13.104a-8`, `R13.104a-9`) |
| Side effects of text consolidation | Guard summary and checklist do not change selection clear, rebuild, fit or zoom, undo, notifications, or export eligibility. The blocked text and workspace labels do not change row selection, notifications, rebuild, or layout. (`TODO.md:R13.104a-12`, `R13.104a-13`, `R13.104a-14`) |

Intended visible changes are listed separately. Cross-IC UI computation changes `ToRegular / ToFull / Combined` from `0.5 / 2 / 1` to the generator's `0.5 / 1 / 0.5`. Normal Detail now projects the anchor IC. The difference between the unanchored `200%` in characterization and the authoritative `100%` is recorded. Explicit empty allocations no longer get `ToRegular=0.5` from geometry fallback. They produce a zero result. (`TODO.md:R13.101d-2`, `R13.102b-1`, `R13.101e`)

## 4. Actual G0 to G6 execution results

The following are validation evidence for the final revisions. They were run by the 1.3.x session outside the sandbox: Windows, Debug, outside the sandbox, confidential example data present, and the submodule pinned at `8c84e4d6`. This document update did not re-run these gates. The refactor gate for PR-B `2e3cbd08` took about 3 minutes. The same command for PR-C `4d54b35d` also passed. In both runs, all listed test groups show `0 failed / 0 skipped`.

| Gate | PR-B tip `2e3cbd08` | PR-C tip `4d54b35d` |
| --- | --- | --- |
| G0 Build/Lint | The final gate's lint `-AllFiles` completed, and build-ui was OK. | The final gate's lint `-AllFiles` completed, and build-ui was OK. |
| G1 Targeted | When each leaf landed, the direct contract tests ran. R13.102a-3b added characterization `18/18`. R13.102a-4's `A1Fact*` had `18 passed`, and PadInfo had `6/6`. The final revision instead ran all groups through the gate. (`TODO.md:R13.102a-3b`, `R13.102a-4`) | When the leaf landed, these ran: the Fact 5 and Q7=1 positive control, warm/cold V21/V22, and the partial-overlap case for R13.102a-5. A1-F5b added three cases, and `A1Fact*` had `21` passing. The three new cases fail under the old builder. The final revision instead ran all groups through the gate. (`TODO.md:R13.102a-5`) |
| G2 Notch Core | Final gate: `209 passed / 0 failed / 0 skipped`. | The final gate and the explicit `-Group notch-core` call both gave `214 passed / 0 failed / 0 skipped`. |
| G3 Golden | The notch-core group in the final gate includes the golden tests. Both versions use the same group list, with no failures or skips. This revision did not provide a separate `-Group notch-golden` run record. (Group membership: `TODO.md:R13.102a-3b`, `R13.102a-4`) | Explicit `-Group notch-golden`: `6 passed / 0 failed / 0 skipped`, including the C export drift, golden baseline, and TM8.1 acceptance matrix. |
| G4 Runtime CLI | Baseline `-LaunchIsolatedUi -EnforceBudget` passed forward. It also passed with `-ReverseCExportOrder` in reverse. All `passesStrictThreshold` values are true. | The same baseline passed both forward and in reverse. All `passesStrictThreshold` values are true. |
| G5 UI | Final gate: ui-core `283 passed`, ui-snapshots `22 passed`. Both show `0 failed / 0 skipped`. | Final gate: ui-core `283 passed`, ui-snapshots `22 passed`. Both show `0 failed / 0 skipped`. |
| G6 Merge | The workspace prepare for the three roadmap lines is run by the caller of the gate scripts before each cycle. The gate itself runs lint `-AllFiles`, the build, and all test groups. Beyond the groups above, application `239`, infrastructure `11`, and uncategorized `382` passed, each with `0 failed / 0 skipped`. | Likewise prepared by the caller before each cycle. The gate itself runs lint `-AllFiles`, the build, and all test groups. Beyond the groups above, application `244`, infrastructure `11`, and uncategorized `382` passed, each with `0 failed / 0 skipped`. |

Both versions use the same group list. The differences notch-core `209→214` and application `239→244` come only from the five tests added by the A1 facts and PR-C. (`TODO.md:R13.102a-5`)

The command list keeps the G2 and G3 groupings and the three roadmap lines for G6. G4 lists the forward and reverse budget calls for the final evidence. The G2 and G3 groupings come from roadmap R13.001 and "Common commands" in `scripts/README.md`. The three G6 lines are quoted verbatim from section 12 of the roadmap, "Additional commands before merge." The final refactor gate commands actually run for both versions are as follows:

```powershell
./scripts/tests/run-refactor-gate.ps1 -IncludeUiSnapshots -LintAllFiles -UseNoAppHost
```

G2:

```powershell
./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost
```

G3:

```powershell
./scripts/tests/run-tests.ps1 -Group notch-golden -UseNoAppHost
```

G4:

```powershell
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget -ReverseCExportOrder
```

G6 (roadmap original):

```powershell
./scripts/dev/prepare-ui-workspace.ps1
./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -IncludeUiSnapshots
```

## 5. Whether golden was updated, by whom, and sign-off

PR-B `2e3cbd08`: golden, baseline, and snapshot data were not updated. No one updated them, and nothing changed, so no golden-update sign-off is needed. The output is zero-diff. Evidence comes from the final gate in section 4 and the forward and reverse Runtime CLI baselines.

PR-C `4d54b35d`: golden, baseline, and snapshot data were also not updated. No one updated them, and nothing changed, so no golden-update sign-off is needed. The explicit notch-golden run shows `6 passed / 0 skipped`. The forward and reverse Runtime CLI baselines passed again. Cold generator output and V21/V22 output for each golden input remain unchanged. Tests that failed before R13.102a-5 and passed afterward capture the mixed-allocation warm-export difference. The owner approved that firmware change on 2026-10-04 in public PR #3 at `9810edf9` and in chat. Unchanged golden data does not substitute for that approval. (`TODO.md:R13.102a-5`)

Evidence from `git diff origin/1.3.x --name-only` for the delivered code and test revisions, relative to `origin/1.3.x`, lists only source, test, and `TODO.md` files. It shows no differences in golden, baseline, or snapshot data. The only name matching `snapshots/` is the static guard test `UiLayoutGuardTests.cs`. No one updated golden data, so no golden-update sign-off was required. (`docs/guides/refactor-roadmap-1.3.x.md:3.1 Golden update rules`)

## 6. Remaining risks, owner exceptions, and the first slice of the next version

- **A1 Fact 5 is implemented in PR-C. The owner approved the firmware corner case on 2026-10-04 in public PR #3 at `9810edf9` and in chat.** On 2026-10-03 the owner ruled 「診斷跟 generator 一致，Q7=0 不顯示」 ("diagnostics match the generator; Q7=0 is not shown"). R13.102a-5 shares the Q7-positive allocation set. A single regular `10×10` lies entirely inside a `2570×10` CAD. Raw ratio=`1/257`, Q7=`0`. For all three models: allocation `1→0`, overlap `100→0`, overlapped regular area `100→0`, regular/debug count `1→0`, and resolved target `1 entry 100%→empty set`. For CurrentGain and ConservativeNoGain, ToRegular and Combined go from `100%→0%`. Disabled stays `100%`. For all three models, ToFull is still `100% (disabled)`, Stage3 area is still `25700`, and the generator still produces `0 candidates／0 V22 rows`. However, the CAD contains both Q7=0 and Q7-positive allocations. When the CAD is reviewed before export, the warm resolved result's V21 and V22 firmware rows and the FW apply simulation do change. Warm now equals cold. It cannot be claimed that firmware C output is unchanged in all cases. The review example uses CAD `(0,0)–(2570,10)`, a 1×2 grid with `xEdges=[0,10,2570]` and `yEdges=[0,10]`, and IC0. diff0 has overlap=`100` and Q7=`0`. diff1 has overlap=`25600` and Q7=`128`. diff1 is `XWay`, with `MatchedCadPadId`=CAD, `MatchScore=1`, and CAD output diff=`1`. With ConservativeNoGain, strict overlap=`0.001`, and threshold=`0`, CombinePercent goes from `200→100`. V21 removes the `ADD Q7=128` term between destination diff0 and ref diff1. The V22 Release row becomes a no-op and is no longer output. The FW apply simulation changes along with the rows. The full warm/cold row snapshot tests failed under the old builder and pass after the fix. This synthetic case did not separately run the C formatter or an actual UI export. A separate partial-overlap case uses CAD `(5,0)–(2575,10)` and regular `(0,0)–(10,10)`, with overlap=`50` and Q7=`0`. It uses strict=`0.001`, ToFull enabled, boundary virtual-area cap enabled, and ratio=`1`. Stage3Area goes from `25750→25700`, and `IsToFullEnabled=true→false`. Stage polygons, Notch canvas preview and `notch-stage` counts also change, and the R label may change. The core test locks the area and flag for this case. It did not separately operate the canvas or R label, or query `notch-stage`. Historical approval requirement: PR-C had to be delivered independently, with the owner's explicit approval of the firmware corner case. The existing diagnostic ruling does not mean the firmware change was approved. (`TODO.md:R13.102a-3b` Fact 5, `R13.102a-5`) Update (2026-10-04): the owner approved this firmware corner case in public PR #3 at head `9810edf9` and in chat, as relayed by the Commander session.
- **A1 normal single-owner evidence.** Fact 2/3 deduplication, the Fact 5 Q7-positive correction, and the retained Fact 4/6 ABI projections are recorded in [TODO.md R13.102a-3b/4/5](../../TODO.md). The owner selected A1/A2 as the closeout scope. Use [TODO.md R13.101/R13.102](../../TODO.md) for status. The bounded sparse/full bridge does not claim repository-wide convergence.
- **The final-revision validation evidence for A2 is added in sections 4 and 5.** PR-B and PR-C passed full lint, the refactor gate, UI snapshots, and the forward and reverse Runtime CLI baselines at the recorded revisions. The owner approved PR-C's warm firmware corner case on 2026-10-04. Zero diff on existing golden inputs does not prove zero diff for every input. Parent checkboxes and milestone exit remain the owner's decision. (audit A2, "1.3.1 minimal closeout list and Milestone handoff"; `TODO.md:R13.102a-5`) Update (2026-10-04): the firmware corner case in PR-C has been approved by the owner. See the previous item.
- **B1/B2 remain accepted exceptions (2026-10-03). The 2026-10-04 owner decision places V21/Legacy removal after 1.3.x. The specific version and scope will be decided later, with no implicit conversion beforehand.** B1 exempts the Legacy request-specific table identity and cache, and the generator's merge into the normal batch or task. B2 exempts the extra V21/Legacy projector and formatter consolidation, the broader adapter, and new typed/legacy equivalence investment. V21 and Legacy keep their existing behavior and zero-diff gates. The normal V22 projector does not regress. The exceptions cannot close A1 along with them. Historical timing note: the version and scope for fully removing V21 are not yet set. (audit B1, B2, "how the latest owner answer affects classification"; `TODO.md:R13.101c-2`, `R13.102a-2`, `R13.103`, `R13.103c-1`)
- **Owner decision (2026-10-04): 1.3.1 closes with A1/A2. C1–C6 do not block exit, and B1/B2 remain accepted exceptions.** Historical proposal: Keeping the C class out of the exit table was still a proposal, pending owner confirmation. It was not an accepted exemption. C1 is an extra repository-wide final-output cache. C2 is the PadInfo public callbacks and partial-input fallback without an Inspector snapshot. C3 is the exporter metadata target-cap `255` clamp. C4 is the V22 short-row payload `255` ceiling. C5 is the UI error formatter's `255` parsing and prompt. C6 is the DevView fixed `EMS OK` display sample and the static guard allowlist. If the normal flow truly recomputes the same result, it still belongs to A1. The three `255` cases are not merged into one policy just because the literal is the same. (audit C1–C6, "1.3.1 minimal closeout list and Milestone handoff"; `TODO.md:R13.101c-2`, `R13.102a-1`, `R13.103c-1`, `R13.104a-14`) Update (2026-10-04): The owner decided to close with A1 and A2, and C1–C6 do not block closeout. This proposal was superseded (as relayed by Commander).
- **The static guard's scope is broader than the EMS-cap rule.** `UiLayoutGuardTests.ViewsAndViewModels_DoNotIntroduceLiteralEmsCapOrRiskWording` uses the unconditional pattern `\b480\b`. Unrelated literal `480` values in Views and ViewModels are also flagged. This milestone adds the whole guard (the first commit of `R13.104`). It narrows one allowlist exception in each of the next two commits. The unconditional `480` pattern was not narrowed.
- **The debug-entry order in compensation diagnostics has changed.** Allocations are now sorted by Ratio descending. The row and column order of the old raw builder is no longer preserved. In the review example, the debug order goes from `[col0,col1]→[col1,col0]`. No firmware tie-break regression was proven, and no test was added for this sort case. (`TODO.md:R13.102a-5`)
- **PR-A is on another branch and delivered as a separate PR.** The Disabled-target display fix changes only the displayed numbers. The PR-B and PR-C revision evidence in section 4 is not a validation record for PR-A. The delivery order remains PR-A → PR-B → PR-C.
- **Mask product questions.** The owner must decide how a missing saved mask path should behave, and how importing a mask while enabled should affect assignment refresh. Use [PR #22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) for characterization evidence and [TODO.md](../../TODO.md) for the decision status. Characterization does not authorize a behavior change.
- **1.3.2 sequence.** Consult [TODO.md R13.201–R13.206](../../TODO.md) for delivery, review, and remaining work. On 2026-10-05 the owner decided to end 1.3.2 with the delivered scope. TODO.md R13.306 and R13.307 come first in 1.3.3. Section 7 provides IDs and links.

## 7. Handoff for 2026-10-06

Last verified: 2026-10-05 19:44 (+08:00), local full test run with the example data on the tree of trunk 1.3.x fd078295 (PR #35 merged at 19:49): 1230 passed, 0 failed, 0 skipped.

This line records the integrating session's local full run on the tree of the merges listed below. Re-check the table, current PR head, approvals, and required checks before acting.

**Status source and repository context.** [TODO.md](../../TODO.md) is the single status table until Issues replace it after the issue migration. This handoff references IDs and purposes; consult the corresponding TODO.md rows for execution status. The integrator verifies merge results and writes the table back once per batch. Main development uses the public [Dennis40816/nvt-freeform-helper](https://github.com/Dennis40816/nvt-freeform-helper) repository. The default branch is `1.3.x` (trunk); `main` currently equals the initial import commit and will hold released versions only. Work uses `feature/<version>/<topic>` branches targeting `1.3.x`. The original public import came from private `FreeformHelper` `1.3.x` at `2c1c0c84`; private archive and issue-migration actions are tracked by S15.009e.

**Pull request references: IDs, links, and purposes.**

| PR | TODO.md reference | Purpose |
| --- | --- | --- |
| [#1](https://github.com/Dennis40816/nvt-freeform-helper/pull/1) | [R13.102](../../TODO.md) | PR-A: align Disabled-target display with the generator. |
| [#2](https://github.com/Dennis40816/nvt-freeform-helper/pull/2) | [R13.102a-3b/4; R13.104](../../TODO.md) | PR-B: characterize and deduplicate normal readers; centralize safety text. |
| [#3](https://github.com/Dennis40816/nvt-freeform-helper/pull/3) | [R13.102a-5](../../TODO.md) | PR-C: use Q7-positive allocations in diagnostics and warm export; see section 6 for the firmware scope. |
| [#4](https://github.com/Dennis40816/nvt-freeform-helper/pull/4) | [R13.101–R13.104](../../TODO.md) | R13.131 milestone handoff. |
| [#5](https://github.com/Dennis40816/nvt-freeform-helper/pull/5) | [S14.011](../../TODO.md) | Single-pass DXF opening. |
| [#6](https://github.com/Dennis40816/nvt-freeform-helper/pull/6) | [S15.018](../../TODO.md) | Shell log unsubscription. |
| [#7](https://github.com/Dennis40816/nvt-freeform-helper/pull/7) | [S15.017](../../TODO.md) | Console writer inventory. |
| [#8](https://github.com/Dennis40816/nvt-freeform-helper/pull/8) | [S15.020](../../TODO.md) | Terminal auto-follow test waits. |
| [#9](https://github.com/Dennis40816/nvt-freeform-helper/pull/9) | [S15.005b](../../TODO.md) | 2026-10-03 documentation batch. |
| [#10](https://github.com/Dennis40816/nvt-freeform-helper/pull/10) | [S15.009e](../../TODO.md) | Public-repository transfer documentation. |
| [#11](https://github.com/Dennis40816/nvt-freeform-helper/pull/11) | [Owner decisions](../../TODO.md) | 2026-10-04 owner-decision record. |
| [#12](https://github.com/Dennis40816/nvt-freeform-helper/pull/12) | [S15.005a](../../TODO.md) | Dependabot: weekly NuGet and GitHub Actions updates, limit 5. |
| [#13](https://github.com/Dennis40816/nvt-freeform-helper/pull/13) | [S15.005a](../../TODO.md) | VERSION 1.3.0 and its structure check. |
| [#14](https://github.com/Dennis40816/nvt-freeform-helper/pull/14) | [S15.005a; S15.006 external test area](../../TODO.md) | Repository-external test area with seven-day retention and manual cleanup. |
| [#15](https://github.com/Dennis40816/nvt-freeform-helper/pull/15) | [R13.201](../../TODO.md) | Overlap evidence API without the unused MatchingSettings parameter. |
| [#16](https://github.com/Dennis40816/nvt-freeform-helper/pull/16) | [R13.204](../../TODO.md) | RegularPad.AssignMapping for matched-pair assignment. |
| [#17](https://github.com/Dennis40816/nvt-freeform-helper/pull/17) | [R13.205](../../TODO.md) | Audit-phase characterization tests. |
| [#18](https://github.com/Dennis40816/nvt-freeform-helper/pull/18) | [R13.206](../../TODO.md) | One private coordinate transform. |
| [#19](https://github.com/Dennis40816/nvt-freeform-helper/pull/19) | [S15.005a; R13.002](../../TODO.md) | ExampleProjectFixture temporary project copies with a local saved-mask path. |
| [#20](https://github.com/Dennis40816/nvt-freeform-helper/pull/20) | [S15.005a](../../TODO.md) | Dependabot actions/checkout 7.0.1. |
| [#21](https://github.com/Dennis40816/nvt-freeform-helper/pull/21) | [S15.005c](../../TODO.md) | Report-only scripts/tests/path-guard.ps1 prototype. |
| [#22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) | [Mask product questions](../../TODO.md) | Characterize a missing saved mask path and importing a mask while enabled. |
| [#23](https://github.com/Dennis40816/nvt-freeform-helper/pull/23) | [R13.202](../../TODO.md) | Characterize CadBest and hover best-match rules. |
| [#24](https://github.com/Dennis40816/nvt-freeform-helper/pull/24) | [Owner decisions](../../TODO.md) | Documentation state synchronization. |
| [#25](https://github.com/Dennis40816/nvt-freeform-helper/pull/25) | [R13.202](../../TODO.md) | Characterize allocation-anchor and freeform-classification selection. |
| [#28](https://github.com/Dennis40816/nvt-freeform-helper/pull/28) | [R13.203](../../TODO.md) | Typed CadPadId/RegularPadId for the DXF manual override chain. |
| [#29](https://github.com/Dennis40816/nvt-freeform-helper/pull/29) | [Owner decisions; S15.009e](../../TODO.md) | Status refresh, 2026-10-05 decisions, README in two languages, AGENTS.md/CONTRIBUTING.md corrections. |
| [#30](https://github.com/Dennis40816/nvt-freeform-helper/pull/30) | [Owner decisions](../../TODO.md) | First translation batch: 35 documents to English. |
| [#31](https://github.com/Dennis40816/nvt-freeform-helper/pull/31) | [R13.205](../../TODO.md) | Explicit seed and segment stages of the DXF regular mask audit. |
| [#32](https://github.com/Dennis40816/nvt-freeform-helper/pull/32) | [R13.201](../../TODO.md) | Contract guards for the three matching bounded contexts. |
| [#33](https://github.com/Dennis40816/nvt-freeform-helper/pull/33) | [Owner decisions](../../TODO.md) | Status-table write-back of the 2026-10-05 merges. |
| [#34](https://github.com/Dennis40816/nvt-freeform-helper/pull/34) | [R13.205](../../TODO.md) | Explicit local-repair and passive-compensation inputs and results. |
| [#35](https://github.com/Dennis40816/nvt-freeform-helper/pull/35) | [R13.203](../../TODO.md) | Typed IcIndex/DiffIndex behind the mapping adapters. |

Repository CI checks out the PR head, not the merge ref. A trunk fix reaches a PR when trunk is merged into its branch; closing and reopening the PR does not bring in the fix. PR #19 documents the tests-only CI fixture repair. The signed golden manifest hashes the original project JSON; editing that data requires re-signing.

**Owner decisions and dates.**

- **2026-10-04 — Transfer:** main development and new work move to the public repository; the queue uses the public clone and new work is public-only from this date. Private `1.3.x` stays frozen at `2c1c0c84`; the owner archives private FreeformHelper read-only after public CI, including the deploy key, is green and private PRs are closed.
- **2026-10-04 — 1.3.1:** close with A1/A2; C1–C6 do not block, and B1/B2 remain accepted exceptions from 2026-10-03. Parent and milestone checkboxes remain the owner's decision. The owner approved PR-C's firmware corner case in public PR #3 at `9810edf9` and in chat; section 6 preserves its warm-output scope and verification limits.
- **2026-10-04 — S15.017:** incremental console links are not merged. Console-system work comes later, first in FreeformHelper, then in NVT Core as the shared console; other modules are abstracted into NVT Core over time. The 2026-10-05 model decision returns model selection to the default.
- **2026-10-04 — VERSION, test area, and Dependabot:** keep the name and version 1.3.0; use `D:\FreeformHelper-TestArea` by default, overridable with FREEFORMHELPER_TEST_AREA, with TEMP/TMP/TMPDIR set only for tests, seven-day retention, and manual cleanup. Dependabot follows the NVT Core template: weekly NuGet and GitHub Actions updates, limit 5. Reference: S15.005a.
- **2026-10-04 — Agent and contributor documents:** the owner said 「讓我在聊天中確認即可」 ("Confirming in the chat is enough"). Changes to AGENTS.md or CONTRIBUTING.md require prior owner confirmation in chat; a GitHub review is not required for this confirmation. On 2026-10-05 the owner selected 「同意，改好開 PR 給我看 (Recommended)」 ("Agreed; make the corrections and open a PR for me to inspect (Recommended)") for exactly the current branch model and this confirmation flow.
- **2026-10-04 — PR language; 2026-10-05 — document language:** new PR titles and bodies default to English; already-open PRs are not rewritten, and conflicting AGENTS.md/CONTRIBUTING.md wording must first be raised with the owner. The owner said 「後續文件，除了 README 預設要有中文版本，其他都預設用英文，除非我指定新增中文版本」 ("Future documents default to English, except that README should have a Chinese version by default, unless I request an additional Chinese version"). Existing Chinese documents are translated in low-priority Codex batches, preserving Chinese owner quotes with English translations; only English is maintained afterwards, except the bilingual README. The owner selected 「可以 (Recommended)」 ("Allowed (Recommended)") for [README.md](../../README.md) in English and [README.zh-TW.md](../../README.zh-TW.md) in Traditional Chinese.
- **2026-10-04 — Old private issues; 2026-10-05 decision:** move only publishable issues. On 2026-10-05 the owner chose 「同意，只搬 #1 改寫版 (Recommended)」 ("Agreed, move only the rewritten issue 1"): the old parent spec was rewritten and created as public [#27](https://github.com/Dennis40816/nvt-freeform-helper/issues/27) (old 1 -> new 27); the other 42 open issues are delivered child tickets and are not migrated. Old PRs are not migrated and their links are not rewritten. The owner also kept the 1.3.1 parent unticked.
- **2026-10-04 — R13.303:** implement simulation opacity fallback 0.9 in 1.3.3 and update snapshots for this visual change.
- **2026-10-04 — V21/Legacy removal:** removal is outside 1.3.x; the specific version and scope are decided later, with no implicit conversion beforehand. On 2026-10-05 the owner scheduled Avalonia 12 as R13.306.
- **2026-10-04 — R13.305b:** the owner said 「禁止關掉最後一個版本」 ("Do not allow the last version to be disabled"). Neither V21 nor V22 may be switched off when it is the last enabled version; implement the guard in 1.3.3 after the 1.3.2 exit.
- **2026-10-05 — R13.202:** preserve distinct CadBest and hover rules and share only parts proven identical. For exactly equal scores, the owner selected 「維持現狀 (Recommended)」 ("Keep the status quo (Recommended)"): add no ID tie-break to PadMatcher sorting or R13.202 selection. Any later change is a separate behavior-change item; the ID tie-break question is resolved.
- **2026-10-05 — Approval after trunk integration:** the owner selected 「可以，但手動解衝突要重批 (Recommended)」 ("Allowed, but manual conflict resolution requires approval again (Recommended)"). After owner approval, a new commit that only merges trunk cleanly needs no new approval if tests pass; any manual conflict resolution requires owner approval again.
- **2026-10-05 — Integration batches:** the owner selected 「不設上限」 ("No limit") for batches awaiting review.
- **2026-10-05 — Status table:** the owner said 「可以先用各自的，但路徑未來要統一」 ("Each project may use its own table for now, but the path will be unified later"). TODO.md is NFH's single status table until Issues replace it after migration. Roadmap, handoff, and WIP files reference IDs without restating execution status; the integrator verifies merge results and writes the table back once per batch.
- **2026-10-05 — Shared CI pilot:** the owner selected 「改由 NFH 試點，1.3.2 結束後開始 (Recommended)」 ("Use NFH as the pilot, starting after 1.3.2 completion (Recommended)"). NFH replaces NFU as NVT Core's shared CI pilot after the 1.3.2 exit. CI path-check evaluation is tracked under S15.005c for later NVT Core introduction.
- **2026-10-05 — Models and usage:** models return to the default; Codex usage remains economical after its reset.
- **2026-10-04 22:07 — Ruleset:** the owner created Protect 1.3.x and main, requiring policy / structure and dotnet / build-test, with strict mode off and admin bypass.
- **2026-10-05 — 1.3.2 exit, NVT Core and Avalonia 12:** [TODO.md](../../TODO.md) is the status source.
  - The owner closed R13.201–R13.206 with the delivered scope. 1.3.2 ends on 2026-10-06.
  - The remaining R13.203 and R13.204 work moves to R13.406, after 2026-10-18.
  - PR #38 set VERSION to 1.3.2. Tags and the release workflow remain under S15.005e.
  - The Avalonia 12 upgrade starts on 2026-10-07 as R13.306.
  - All NVT Core content is due by 2026-10-18. Until then Core comes first, and non-Core NFH work is limited to 1.3.2 and urgent fixes.
  - NFH adopts Core from 1.3.3 as R13.307. The non-UI library on net8 comes first. The UI modules follow the Avalonia 12 upgrade.
  - Core code is all rights reserved, with the owner as copyright holder. This includes code moved from NFH.
  - Governance checks run report-only now and become required after 1.3.2.
  - After the Core import, each new feature first gets a classification proposal with reasons, and the owner decides. Plain firmware product logic is project-specific and is only recorded.
- **2026-10-06 — Avalonia 12 upgrade and Core use:** TODO.md R13.306 and the owner decisions record the owner's words.
  - Fonts follow the Core font set: Inter, Cascadia Mono, Noto Sans TC and Material Symbols Outlined.
  - Font sizes are caption 11, body 13, heading 16 and title 24. All fonts are embedded with pinned versions.
  - NFH's current Segoe UI Variable Text and Consolas are outside the set. NFH moves to the set when it adopts Core.
  - NFH UI snapshots may change without owner approval. The pull request attaches before-and-after images.
  - Non-UI outputs must stay identical.
  - All repositories use package lock files, and CI restores in locked mode.
  - The all-rights-reserved wording becomes a proprietary license. The license explicitly allows integration, shipping with the tools and execution by users.
  - NFH consumes Core as versioned .nupkg files from a local feed. Core has its own SemVer. NFH uses only the non-UI library until the Avalonia 12 upgrade.
  - NFH may gradually move shareable UI to Core. Each candidate goes to the owner with reasons.

**Owner actions — consult TODO.md for status before acting.**

- Archive the private repository read-only: [FreeformHelper settings](https://github.com/Dennis40816/FreeformHelper/settings), with prerequisites tracked by S15.009e.
- Import the shared CI ruleset when NVT Core provides it. The checks become required after 1.3.2: [repository rulesets](https://github.com/Dennis40816/nvt-freeform-helper/settings/rules). Status: [S15.005c/S15.005d](../../TODO.md).
- Decide the exception and calibration policy of the merged report-only path check and when it starts to block; later NVT Core introduction: [#21](https://github.com/Dennis40816/nvt-freeform-helper/pull/21), [S15.005c](../../TODO.md).
- Decide how a missing saved mask path should behave and how importing a mask while enabled should affect assignment refresh: [#22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22), [TODO.md Mask product questions](../../TODO.md).

**Queue references.** These names identify work, not execution status; consult TODO.md.

- S15.002: `s15-002-console-ring-tail-isolation`.
- R13.202: best-match, allocation-anchor, and freeform-classification characterization in PR #23/#25.
- R13.203: DXF manual override chain in PR #28 and the mapping chain in PR #35 (`r13203-mapping-ids`).
- R13.205: audit-phase characterization in PR #17, seed and segment stages in PR #31, and the local-repair/passive-compensation stage in PR #34 (`r13205-repair-passive`).
- S15.005c: path-check evaluation; the report-only prototype is PR #21.

**Release gates.** Consult [TODO.md R13.201–R13.206](../../TODO.md) for 1.3.2 work. R13.202 shares only parts proven identical; R13.203 covers its typed-ID boundary; R13.205 covers the production pipeline. On 2026-10-05 the owner decided to end 1.3.2 with the delivered scope. TODO.md R13.306 and R13.307 come first in 1.3.3. R13.303 opacity 0.9 with updated snapshots and R13.305b's last-version guard retain their 1.3.3 gates. No parent, milestone, or exit checkbox is closed by this handoff.

**Open product questions.** The missing saved mask path and importing a mask while enabled require owner behavior decisions; consult [PR #22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) for characterization and [TODO.md](../../TODO.md) for status. PadMatcher sorting and R13.202 have no open ID tie-break question: exact ties preserve the current behavior under the 2026-10-05 decision.
