# FreeformHelper Roadmap

This file is the single plan for FreeformHelper. It replaces `TODO.md` and `docs/guides/refactor-roadmap-1.3.x.md`; their history stays in git.

Work runs on three lines:

1. **Fix line**: hotfixes for the latest release.
2. **Product features**: product work on the development line.
3. **Core integration**: adopting NVT Core packages and modules.

Core integration must never block the fix line.

## How to use this file

- Each item states its target version, status and PR or issue link.
- Status is one of: `Not started`, `In progress`, `Blocked: <reason>`, `Waiting for owner`, `Done`.
- Keep the existing IDs (`R13.*`, `S15.*`, `S11.*`). The `13` in `R13` is a historical number, not a version. Commits and PRs refer to them as `Refs: ROADMAP.md <ID>` until the item has an issue; then use `Refs #N`.
- When you find new work, add it here under the right line at once.
- When a release ships, delete its done items. Git history keeps them.
- `pwsh .agents/skills/repo-optimizer-loop/scripts/list_pending_todo.ps1 -TodoPath ROADMAP.md` lists the open items.

Links of the form `Dennis40816/FreeformHelper/issues/N` point to the archived private repository. They stay readable after the archive.

## Release state

| Item | Value |
|---|---|
| Development line | Version line `1.0.x` (`VERSION` = 1.0.0-g.4). The branch is `main` |
| Next customer release | `1.0.0`, after handoff group g4 (owner decision, 2026-10-07) |
| Latest customer release in this repository | None yet |
| Latest release overall | `1.2`, tagged only in the archived private repository. It was never shipped to customers |

### Version rules

These rules follow the owner decisions of 2026-10-07.

- **Customer releases**: only a version shipped to customers gets a release tag and a GitHub Release. The number is set at release time. Customer version numbers are continuous.
- **Minor and patch**: raise the minor version only for a large set of features. Otherwise raise the patch version.
- **Release branch**: to ship a customer release, cut a branch named after the version line, for example `1.0.x`, from `main`. It freezes the content and takes fixes only. `main` keeps moving.
- **Internal names follow the next customer release**: `VERSION` and handoff names match the next customer release. No internal number may be larger than the next customer release. The next customer release is `1.0.0`, so the version line is `1.0.x`. The trunk is always `main`; the version line is not a branch name.
- **Development version format** (owner decision, 2026-10-18): `X.Y.Z` is the next customer release. While it is in development, `VERSION` holds `X.Y.Z-g.N`, for example `1.0.0-g.4`. A customer release removes only the suffix. The next commit after the release starts the next version at `-g.1`. `scripts/verify.ps1` checks the format.
- **Handoff groups and internal tags**: work is grouped in handoff order. When a whole group has merged into the trunk, the GitHub App creates one annotated internal tag `vX.Y.Z-g.N`, for example `v1.0.0-g.4`, and the tag message names the group. The next commit raises `N` by 1 in `VERSION`. Internal tags get no Release. The older format `dev/<line>/g<n>-<name>` is retired. The existing `dev/` tag stays.
- **Skipped versions**: a customer who skips versions must still be able to update. Settings and user data migrate from every customer release directly. The release notes list the accumulated changes.

## 1. Fix line

- A hotfix branch starts from the latest customer release tag.
- The fix ships as a patch release and then merges back into the release branch or `main`.
- This repository has no customer release yet. The fix line starts with `1.0.0`. Until then, ask the owner before you start a hotfix.

Open hotfixes: none.

## 2. Product features

Status of the handoff groups before `1.0.0`. The group order is fixed; a group cannot skip the exit of the one before it. Slice order inside a group may change with a recorded reason. Group exits are owner decisions. The gates and slice specs are in `docs/reference/refactor-contract.md`.

Groups g1 to g7 were named 1.3.0 to 1.3.5 before 2026-10-07. Older commits, PRs and review documents use the old names.

| Group | Old name | Theme | Output policy | Status | Internal tag |
|---|---|---|---|---|---|
| g1 | 1.3.0 | Trusted gates and numeric contracts | Zero difference after the signed R13.002 correction | Done | None. Its commits are only in the archived private repository |
| g2 | 1.3.1 | Notch single-result pipeline | Zero difference | Done. The owner closed it with A1/A2 on 2026-10-04; B1/B2 are accepted exceptions | None, for the same reason |
| g3 | 1.3.2 | Matching and domain state | Zero difference | Done. The owner closed it with the delivered scope on 2026-10-06 | `dev/1.0.x/g3-matching-domain` (the retired format, kept) at the closing merge of [#39](https://github.com/Dennis40816/nvt-freeform-helper/pull/39) |
| g4 | 1.3.3, first part | Core integration (line 3) and the state batch-0 fixes | Zero data difference; the look PR changes appearance with owner approval | In progress | `v1.0.0-g.4` |
| g5 | 1.3.3, second part | Settings and presentation | Zero data difference; the R13.303 opacity change changes appearance with owner approval | Not started. Waits for the g4 exit | Named after 1.0.0 ships: `v<next release>-g.N` |
| g6 | 1.3.4 | Workspace ViewModel split | Zero difference | Not started. Waits for the g5 exit | Named like g5 |
| g7 | 1.3.5 | UI structure and token cleanup | Zero data difference | Not started. Waits for the g6 exit | Named like g5 |
| g8 | - | State refactor ST1 to ST5, after Core 1.0.0 | To be set with the plan | Not started | Named like g5 |

`1.0.0` ships after g4. Work in g5 and later goes into a later customer release.

Order rules:

- NFH, Core and LDRA are all main lines (owner decision, 2026-10-27). The limit that NFH product work was urgent fixes only has ended.
- Do one `R13.*` slice at a time.
- Zero-difference rule: `V21_before == V21_after` and `V22_before == V22_after`. Work that must change Firmware C bytes leaves the refactor plan as a separate behavior item.

### 2.1 Waiting for the owner

The owner closed these parents on 2026-10-07 because their work is delivered: R13.101, R13.102 and R13.103 (parent spec [#27](https://github.com/Dennis40816/nvt-freeform-helper/issues/27)), S15.005 with S15.005a, and S15.009 with S15.009d. V21 and `LegacyRegularAnchor` stay as they are.

- [ ] **R13.104 Converge the Notch display and EMS safety predicate and text** · Target: g2 · Status: Waiting for owner · PR: -
  - R13.104a-1 to a-14 are done. Decide whether the remaining follow-up scope is open work: the `255` cap spread over Step 3 XAML, settings sync, generator, exporter and formatter, and separate Simulation, replay and Dev texts.
- [ ] **P-MASK Behavior for a missing saved mask path and for importing a mask while enabled** · Target: - · Status: Waiting for owner · PR: [#22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) (characterization tests only)
- [ ] **P-V21 Remove V21 and `LegacyRegularAnchor` completely** · Target: after g7 · Status: Waiting for owner (scope) · PR: -
  - No implicit conversion before then. Planning notes: `docs/reviews/v21-*-2026-10.md`.

### 2.2 g5 settings and presentation (after the g4 exit)

- [ ] **R13.301 Settings draft snapshot with per-field Apply, Discard and round-trip tests; save only dirty fields** · Target: g5 · Status: Not started · PR: -
- [ ] **R13.302 One apply plan and side-effect owner for all settings entries; typed Step 2/4/5 invalidation** · Target: g5 · Status: Not started · PR: -
- [ ] **R13.303 Converge the simulation color and opacity scale, brush cache and token fallback** · Target: g5 · Status: Not started · PR: -
  - The owner approved the 0.9 opacity fallback on 2026-10-04. It changes appearance and needs snapshot updates.
- [x] **R13.304 Structured console dedup; one action path for both hosted modes** · Target: g5 · Status: Closed: superseded by C-CONSOLE (owner decision, 2026-10-07) · PR: -
- [ ] **R13.305 Move non-routine parameters out of normal Settings; Step 3 goes straight to Step 5; diagnostics area** · Target: g5 · Status: Not started · PR: -
  - The owner confirmed on 2026-10-03 that Step 4 mapping and Step 6 validation diagnostics move to an unnumbered diagnostics area.

### 2.3 g6 and g7

- [ ] **R13.401 Root project-session shell and child ViewModel compatibility contract** · Target: g6 · Status: Not started · PR: -
- [ ] **R13.402 Move `DxfWorkspaceViewModel` orchestration** · Target: g6 · Status: Not started · PR: -
- [ ] **R13.403 Move `MatchingWorkspaceViewModel` orchestration** · Target: g6 · Status: Not started · PR: -
- [ ] **R13.404 Move `NotchWorkspaceViewModel` orchestration** · Target: g6 · Status: Not started · PR: -
- [ ] **R13.405 Converge the `ProjectSessionViewModel` facade, Save/Load/IPC and cross-workspace tests** · Target: g6 · Status: Not started · PR: -
- [ ] **R13.406 Remaining typed-ID chains (from R13.203) and RegularPad writer transitions (from R13.204)** · Target: g6 · Status: Not started. Waits for the g5 exit · PR: -
  - Convert one API chain at a time and keep the integer adapters. Persistence, export, CSV, CLI and IPC stay numeric.
  - First verify four single-source risks: best-match identity, duplicate (IC, diff) grouping, matched-identity lookup and anchor identity.
- [ ] **R13.501 Split Controls.Core responsibilities; scoped BasedOn and template dedup** · Target: g7 · Status: Not started · PR: -
- [ ] **R13.502 Split the DevView preview sections; keep the action-role laboratory** · Target: g7 · Status: Not started · PR: -
- [ ] **R13.503 Shared layout shell for Simulation and Coordinate** · Target: g7 · Status: Not started · PR: -
- [ ] **R13.504 Converge synonym tokens and legacy aliases** · Target: g7 · Status: Not started · PR: -
  - Banner first, bulk alias second.
- [ ] **R13.505 Explicit NumberScrubber init contract** · Target: g7 · Status: Not started · PR: -
- [ ] **R13.506 Narrow testable seams for the PadCanvas selection and visible-draw engines** · Target: g7 · Status: Not started · PR: -
- [ ] **R13.507 One CadAreaBucketService membership for selection and coloring** · Target: g7 · Status: Not started · PR: -

### 2.4 State refactor after Core 1.0.0

- [x] **H2 One shell navigation selection with computed binding projections** · Target: 1.0.0 (g4 health tasks) · Status: Done · PR: [#70](https://github.com/Dennis40816/nvt-freeform-helper/pull/70) · Issue: [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64)
  - The authorized H2 slice precedes the larger state batches below; it preserves workspace build, source refresh and prewarm ordering.

The owner scheduled these five batches after the NVT Core 1.0.0 release (the target date 2026-10-15 has passed; Core 1.0.0 is not released yet). Each batch is one PR, and each starts with tests that pin the current behavior.

- [ ] **ST1 Compute derived values instead of storing them in `FreeformHelperViewModel`** · Target: after Core 1.0.0 · Status: Not started · PR: -
- [ ] **ST2 Nesting-safe suppression guards, a view-model mode enum and a PadCanvas gesture enum** · Target: after Core 1.0.0 · Status: Not started · PR: -
  - The owner decided that editing is blocked while a project loads.
- [ ] **ST3 Group fields that always change together into records** · Target: after Core 1.0.0 · Status: Not started · PR: -
- [ ] **ST4 Extract five child view models: DxfEditState, NotchValidationPanel, PadInspector, SimulationSafetyOverview and the background operations** · Target: after Core 1.0.0 · Status: Not started · PR: -
- [ ] **ST5 Coordinate planner corners and guides, PadCanvas selection ownership, and view code-behind copies** · Target: after Core 1.0.0 · Status: Not started · PR: -

### 2.5 Repository, CI and release

- [ ] **S15.002 Remove the full-suite timeouts in `FreeformHelperViewModelTests`, then make the `viewmodel` CI shard required again** · Target: - · Status: Blocked: needs a new failure trace for root cause six (headless dispatcher race) · PR: -
  - Root causes 1 to 5, 7 and 8 are fixed. The earlier analysis predates Avalonia 12.1.1 and xUnit v3.
  - On 2026-10-28 three CI test groups (`ui-snapshots`, `notch-core`, `ui-stable`) ended with test host exit 1 and zero results, the cause is unknown, and [#210](https://github.com/Dennis40816/nvt-freeform-helper/pull/210) now prints fixed host-error labels so that the next failure can be traced.
  - The commit that added the note above (`52c6622`) was pushed without its verification in the commit body and without a `lint.ps1` run before it. Lint ran afterwards on the same tree and passed (exit 0). The results are in the checks of [#211](https://github.com/Dennis40816/nvt-freeform-helper/pull/211), and the commit is not rewritten, as `docs/agents/issue-tracker.md` allows.
- [ ] **R13.306-F1 Give the four main-window tests a fake CAD load spinner host** · Target: - · Status: Not started · PR: -
- [ ] **H-LEDGER Lower the code-health ledgers in small slices** · Target: - · Status: In progress: T1 (test findings) is merged, then L1 (`Lock` types) and U1 (unused private members) · PR: [#207](https://github.com/Dennis40816/nvt-freeform-helper/pull/207) · Issue: [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64)
  - Commit-body exceptions on the U1 branch ([#212](https://github.com/Dennis40816/nvt-freeform-helper/pull/212)). Three commits were pushed before their verification was complete, so that the work is not lost. None is rewritten or force-pushed, as `docs/agents/issue-tracker.md` allows. The complete verification is in the body of commit `cab51eed49ac1f0bfe2646372357dda0b6434a57` and in the pull request description.
    - `7040979528523e71ea843cbd0a6641fa3b78b016`: the code change only. Its body says it was not built, linted or tested, and the ledger was not lowered.
    - `1773343662e3f9934c4d88bcae473138be32f183`: removes one unused using. Its body says only the UI build ran. Lint, tests, LowerBaseline and Verify were still to do.
    - `41123340d10c670d673df0cecfbf6c4a4c481d5d`: lowers the ledger and adds this note. Its body says Verify had not run yet. Its body also says the note is under the H-LEDGER row, but the note sat under the H2 row by mistake. Commit `1f4904de6868f3807cc424a4e7ef87b01a8bcfb0` moves it here.
  - Commit-body exceptions on the U2a branch ([#213](https://github.com/Dennis40816/nvt-freeform-helper/pull/213)). Six commits were pushed before their verification was complete. None is rewritten or force-pushed, as `docs/agents/issue-tracker.md` allows. The first complete verification is in the body of commit `8911ee9d0c3e16464281d14e66cd9aed20256b68`. After #212 merged, the complete verification on the merged branch is in the body of `39f394b6b6ed9c45ec34884a754d35a52dc9adcb` and in the pull request description.
    - `fa14b230bf12ae60e744b91b553d8ce13b0991d7`: the code change only. Its body says it was not built, linted or tested, and the ledger was not lowered. Three statements in it are wrong or without source: the two removed comments did not describe only the removed fields (one also described the kept field `_gridBuilder`), `_padOverrideService` is a field and not a constructor parameter, and the reason it gives about power for pushing early has no source.
    - `a4f2ae69da40b8f54b2a6ec1499b06c559be6560`: removes one unused using. Its body says lint, targeted tests, the Architecture tests, LowerBaseline and Verify follow. Lint had not run before the commit.
    - `fa25d4ff2d17b20e799d5fbe9742c5335097191f`: moves eight Architecture keys. Its body says the broader targeted tests, LowerBaseline and Verify follow, and does not state a lint run.
    - `544fc69c26a8e9bf86217a7f949fc6fd056a06fe`: merges `main` after #212 into the branch. Its body says no verification ran on it and that the ledger files take the version of `main`, so the ledger is not lowered for U2a in this commit. Its body also says "The next commit runs LowerBaseline again and lowers it". That is wrong: the next commit is `9cdf96e`, and `39f394b` lowers the ledger.
    - `9cdf96e9f48ccf5931ed8129d3a0bd1e2062b7c7`: this ROADMAP list and three small leftovers (two blank lines, one comment). Its body says build, lint, tests, LowerBaseline and Verify run on the next commits. The verification is in the body of `39f394b6b6ed9c45ec34884a754d35a52dc9adcb`.
    - `504e8af860bb67907d594e638fba031549bf92f6`: adds the two later commits to this list, `ROADMAP.md` only. Its body does not record a pre-commit lint run, which `CONTRIBUTING.md` requires also for a documentation commit. Its body also says "after the rebase", but `544fc69` merged `main` and nothing was rebased. The body of `96374f39d9ac3870a5218ce30ad6eb80e8e1d8a1` says the body of `504e8af` "states no lint run". That overstates it: the body does not record one. The lint run is on the tree of `96374f39`, and its body records the result.
  - Follow-up from [#219](https://github.com/Dennis40816/nvt-freeform-helper/pull/219): remove the unused `padOverrideService` parameter of `ProjectPersistenceUseCase` (9 call sites) in the change that removes the class-level `SuppressMessage(CA1822)`. The parameter stays until then, because dropping it creates a new ledger fingerprint for the kept `_store = ...` statement.
  - Commit-body exceptions on the U2b branch ([#215](https://github.com/Dennis40816/nvt-freeform-helper/pull/215)). Two commits were pushed before `LowerBaseline` and `Verify` ran on them. None is rewritten or force-pushed, as `docs/agents/issue-tracker.md` allows. The complete verification is in the body of commit `9368dfb897ff83c3d64e1da27c497419ca618bef` and in the pull request description.
    - `7dcf26bae66746413cfbf0904552c92fe692f9a4`: the code change. Its body says the ledger is not lowered and that `LowerBaseline` and `Verify` follow "in the next commit". That is wrong: they ran after the next commit, and the results are in `9368dfb`. Its body also says the two removed methods are wrappers "that nothing calls". That means no production caller. The tests of the base called both through reflection.
    - `ace0b9f32c1e70238a018323c3a799b9ab742d6b`: the test fix. Its body says the ledger is not lowered and that `LowerBaseline` and `Verify` follow. Its body also says `HasActiveNotchStageOverlay` stays covered by `NotchPreview_KeepsFreeformHatch_WhenStageOverlayVisible` "through `ShouldDrawFreeformHatch`". That call path does not exist: `ShouldDrawFreeformHatch` (`PadCanvas.Rendering.Regular.cs:67`) checks only `HighlightFreeform` and `pad.Freeform`. The real coverage is `NotchPreview_SuppressesFreeformAccentBorder_WhenStageOverlayVisible` (`PadCanvasCacheInvalidationTests.cs:322`), which reaches it through `ShouldSuppressFreeformAccentBorderForNotchPreview`.
  - Commit-body exceptions on the S1a branch ([#214](https://github.com/Dennis40816/nvt-freeform-helper/pull/214)). Four commits were pushed before their verification was complete. None is rewritten or force-pushed, as `docs/agents/issue-tracker.md` allows. The branch no longer removes the field `_ipcLoopTask` of `CadLoadSpinnerWindow`: that removal changed behavior (the log gains an "Unobserved task exception." line when the pipe cannot be created), so it was reverted. The silent IPC failure is a follow-up in `TODO.md`. The complete verification is in the body of the last ledger commit of the branch and in the pull request description.
    - `ddd91c98364f0dcd5a628325f6b87f20a9421100`: the code change only. Its body says it was not built, linted or tested, and the ledger was not lowered. It also says "ProjectPersistenceUseCase has the same unread parameter". That was wrong: the parameter was read (`ProjectPersistenceUseCase.cs:24` at `832b190`) and only the field `_padOverrideService` was unread. [#219](https://github.com/Dennis40816/nvt-freeform-helper/pull/219) later removed the field and left the parameter unused.
    - `517e3453b924026723435bb2488e86f38f7fe228`: moves the DLL search path attribute to the assembly. Its body says only the UI build ran. It does not record a lint run. The Architecture tests, LowerBaseline and Verify were still to do.
    - `b28d6143e9b87b4ae50cdc8a7a327cf2ab6b672b`: moves one Architecture key. Its body says the remaining targeted tests, LowerBaseline and Verify were still to do.
    - `2b1d83fccbfbd232bb7afa9ea0ed51f3e41d6990`: merges `main` into the branch. Its body says no build, lint or test ran on it, and that both ledger files take the version of `main`, so the ledger is not lowered for S1a in this commit.
    - `c61f087e6f2edda036d7fa4c6869eedb805dc720`: reverts the removal of `_ipcLoopTask` and regenerates the ledger. Its body says one entry of the `FreeformHelperView` size ceilings was removed. That is wrong: the entry stays and two ceilings of it go down, `viewTypeLines` 3543 to 3542 and `stateMembers` 48 to 47. The ledger numbers in that body are correct.
    - Later, `main` took [#113](https://github.com/Dennis40816/nvt-freeform-helper/pull/113), which removed the field `_consoleFocusWithinSubscription` with the in-app console. The branch merged that `main` and no longer removes this field. Its numbers in the bodies above are superseded by the last ledger commit of the branch.
- [ ] **S15.005c Review infrastructure: R0–R3 authority policy, CODEOWNERS, PR and issue templates, review records** · Target: - · Status: Blocked: waits for the shared CI pilot · PR: -
  - The report-only path guard merged in [#21](https://github.com/Dennis40816/nvt-freeform-helper/pull/21) but is not wired into CI. Waiting for owner: the exception and calibration policy, and when it starts to block.
- [ ] **S15.005d Owner-only settings: the `release` environment** · Target: - · Status: Waiting for owner · PR: -
- [ ] **S15.005e Release and rehearsal workflows; tags** · Target: - · Status: Blocked: waits for the first successful manual release · PR: -
  - The first public release tag also starts the fix line (section 1).
- [ ] **SHARED-CI Shared CI pilot: NFH replaces NFU** · Target: - · Status: Not started; NVT Core will contact NFH · PR: -
- [ ] **GOV-REQ Make the report-only governance checks required** · Target: after g3 · Status: Waiting for owner (g3 has ended) · PR: -
- [ ] **S15.009e Issue migration: rewrite the parent-spec links and final status** · Target: - · Status: Waiting for owner · PR: -
- [ ] **S15.009e Archive the private FreeformHelper repository** · Target: - · Status: Waiting for owner; both preconditions are met · PR: -
- [ ] **S15.009f Move the test data into `Dennis40816/nvt-private-assets` under `nfh/`, then archive `FreeformHelper-testdata`** · Target: - · Status: Not started; after C-CONSOLE and C-THEME2 · PR: -
  - A submodule links a whole repository, so decide first how `example/` reads the `nfh/` folder.
  - Update the CI deploy key `TESTDATA_DEPLOY_KEY` and the data checks in `scripts/tests/assert-example-data.ps1`.
  - Never copy the data into this repository, a PR, an issue or a log.
- [x] **DOC-TR Translate the remaining Chinese documents** · Target: - · Status: Done (the screen `HowToUseView.axaml` is tracked separately) · PR: [#30](https://github.com/Dennis40816/nvt-freeform-helper/pull/30) (first batch), [#57](https://github.com/Dennis40816/nvt-freeform-helper/pull/57)

### 2.6 UI text and redesign

- [ ] **UI-TEXT1 Shorten the resident texts of the right panel, Step 4 first** · Target: - · Status: Not started · PR: -
  - The owner approved Step 4 as the first panel on 2026-10-18. Keep the scope small. Add no new structure for the current layout, because UI-REDESIGN replaces it.
  - Each text PR shows English before-and-after images in the PR conversation.
- [ ] **UI-REDESIGN Redesign the whole NFH UI** · Target: - · Status: Waiting for owner (scope and timing) · PR: -
  - Owner, 2026-10-18, on the right-panel text proposal: 「可以，但由於 NFH 是很久之前設計，理論上是整個都要大改」 ("Yes, but NFH was designed long ago, so in principle all of it needs a major redesign").

## 3. Core integration

- [x] **H09 Adopt the pinned Core C# health bundle and enroll NFH** · Target: 1.0.0 · Status: Done. The baseline was enrolled in [#71](https://github.com/Dennis40816/nvt-freeform-helper/pull/71). The `dotnet / build` job runs Verify against the pull request base since [#75](https://github.com/Dennis40816/nvt-freeform-helper/pull/75) · Issue: [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

NFH adopts NVT Core from g4: the non-UI library first, then the UI modules after R13.306. A Core module is done only when NFH uses the Core version and deletes its own copy. Each switch keeps the NFH tests passing and the non-UI output identical. Parent: R13.307, issue [#43](https://github.com/Dennis40816/nvt-freeform-helper/issues/43).

Theme adoption follows two steps (owner decision, 2026-10-07): step 1 pins the Core packages with no visual change; step 2 is one look PR with before-and-after images in Light and Dark, approved by the owner. The tool order for step 2 is NFC, NFU, then NFH.

- [x] **C-PKG1 Core packages 0.1.0 and the Core UI-thread helper; NFH's copy deleted** · Target: g4 · Status: Done · PR: [#44](https://github.com/Dennis40816/nvt-freeform-helper/pull/44)
- [x] **R13.307-Q0 Runtime Query characterization tests before the Core move** · Target: g4 · Status: Done · PR: [#47](https://github.com/Dennis40816/nvt-freeform-helper/pull/47)
- [x] **C-THEME1 Theme step 1: pin Core 0.2.0 with no visual change** · Target: g4 · Status: Done · PR: [#50](https://github.com/Dennis40816/nvt-freeform-helper/pull/50)
- [ ] **R13.306 Upgrade to Avalonia 12 and net10** · Target: g4 · Status: In progress: only the manual desktop checks remain (console editor, rendering, dialogs, input) · PR: [#41](https://github.com/Dennis40816/nvt-freeform-helper/pull/41)
- [x] **R13.307-Q7 Switch to the Core Runtime Query layers; the test list and outcomes stay equal to 847cc453** · Target: g4 · Status: Done · PR: [#55](https://github.com/Dennis40816/nvt-freeform-helper/pull/55)
  - Same-user pipe access with network logons denied, bounded in-flight shutdown, and pipe-creation failure diagnostic timing are owner-approved behavior changes (owner decision, 2026-10-07: 「接受 Core 的行為 (Recommended)」); every other behavior is unchanged.
- [x] **R13.307-M2 Use the Core UiResourceResolver and delete NFH's copy** · Target: g4 · Status: Done · PR: [#58](https://github.com/Dennis40816/nvt-freeform-helper/pull/58)
  - All NFH callers must share one dispatcher registration. See the Core Theme module notes.
- [ ] **C-THEME2 Theme step 2: one look PR with the Core tokens, button roles and scroll styles** · Target: g4 · Status: Not started; waits for the NFC and NFU look PRs and for the Console button roles · PR: -
  - Set only the seven `NfcAccent*` keys for the NFH accent. Delete the local button, scroll and neutral token styles.
  - Map tool-specific button classes to the nearest Core role. Open a Core issue when none fits.
  - This replaces the former steps M3 (scroll styles) and M4 (button roles). It also removes the scroll selectors that match nothing on Avalonia 12 and fixes the neutral text button focus.
- [x] **C-CONSOLE Adopt the redesigned Core Console and delete NFH's console** · Target: g4 (Core 1.0.0) · Status: Done: [#113](https://github.com/Dennis40816/nvt-freeform-helper/pull/113) replaced the in-app console with the Core LogConsole controls on Core 0.9.5 and merged on 2026-10-29 (merge commit `cf8909d`) · PR: [#113](https://github.com/Dennis40816/nvt-freeform-helper/pull/113), design in [Core #82](https://github.com/Dennis40816/nvt_fw_core/pull/82)
  - Historical exceptions of [#113](https://github.com/Dennis40816/nvt-freeform-helper/pull/113), recorded by the rule in `docs/agents/issue-tracker.md`. Pushed history is not rewritten. The bodies of these 21 commits do not record the lint run: `5c73eda`, `e1d8611`, `355393a`, `940506d`, `1ce8868`, `d37d556`, `80f56cc`, `43f78f2`, `4fc1fd7`, `4ef0a12`, `f77a3ab`, `d4e68d4`, `4513f51`, `5c36ebe`, `5af6584`, `77b7952`, `7fe26da`, `2d218b9`, `584663d`, `a77cece`, `82d38b3`. Ten of them also lack the separate `Refs #N` line: `82d38b3`, `a77cece`, `584663d`, `7fe26da`, `77b7952`, `5af6584`, `5c36ebe`, `4513f51`, `d4e68d4`, `f77a3ab`. A missing record does not mean that the command was not run. The verification of the fix tree (head `9c1b824`) is in the description of #113. The body of `940506d` says that the dependency graph stopped listing AvaloniaEdit. That claim is wrong: the commit did not change the graphs, and the graphs list projects, not packages.
  - NFH fixed console review items 01 and 02 in [#49](https://github.com/Dennis40816/nvt-freeform-helper/pull/49). The Core redesign covers items 03 to 21, S15.017 and R13.304.
- [ ] **C-CANVAS-FIT Leave room for the layer toggles in the canvas fit** · Target: g4 · Status: In progress: [#216](https://github.com/Dennis40816/nvt-freeform-helper/pull/216) is a Draft on main and waits for its independent review, then for the owner's review · PR: [#216](https://github.com/Dennis40816/nvt-freeform-helper/pull/216)
  - Owner decision 2026-10-29 01:15, chat with PC-Commander: 「q2 => c」. It is option (c) of the "known visual effect" item in #113.
  - Historical exceptions of [#216](https://github.com/Dennis40816/nvt-freeform-helper/pull/216), recorded by the rule in `docs/agents/issue-tracker.md`. Pushed history is not rewritten. The bodies of these five commits lack a required record. A missing record does not mean that the command was not run. The verification of the final head is in the description of #216 and in one comment on [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
    - `24a00a6`: no verification result and no `Refs #N` line.
    - `be6298d`: no build, lint or test result and no LowerBaseline result.
    - `d81abb6`: postpones build, tests, LowerBaseline, Verify and lint to a later slot.
    - `933b47f`: records the rendered snapshot tests, but not the lint run or the baseline test.
    - `f5d08d2`: no verification result, and the `Refs #64` text is inline instead of a separate line.
- [ ] **R13.307-ICON Material Symbols becomes the shared Core icon system; delete the unused `MaterialIcons-Regular.ttf`** · Target: g4 · Status: In progress: Core font file adoption done; `NvtIcons` migration waits for the Core.Avalonia upgrade · PR: - (pending; [#43](https://github.com/Dennis40816/nvt-freeform-helper/issues/43))
- [x] **R13.307-FONT Adopt the Core font set** · Target: g4 · Status: Done · PR: - (pending; [#43](https://github.com/Dennis40816/nvt-freeform-helper/issues/43))
- [x] **R13.307-LIC Ship the Core license as `licenses/Nvt.Core/LICENSE` and the font licenses (including the Inter OFL text) under `licenses/`** · Target: first NFH release with Core · Status: Done · PR: - (pending; [#43](https://github.com/Dennis40816/nvt-freeform-helper/issues/43))
  - The pinned Fonts 0.1.0 package supplies the Core license, three font license files and Material Symbols NOTICE. It omits a standalone Inter OFL license; the Core packaging follow-up must supply it before customer distribution.
- [x] **C-LSC Use LoadingScopeCoordinator from the Core non-UI library** · Target: g4 · Status: Done; Core side in [Core #44](https://github.com/Dennis40816/nvt_fw_core/pull/44) · PR: [#61](https://github.com/Dennis40816/nvt-freeform-helper/pull/61)
- [x] **C-NONUI Move CoalescedRefresh and UndoService to the Core non-UI library** · Target: g4 · Status: Done · PR: [#60](https://github.com/Dennis40816/nvt-freeform-helper/pull/60)
- [ ] **C-CTRL Move cards, dialogs and input controls to Core** · Target: g4 · Status: Not started · PR: -
- [ ] **R13.307-GUARD Should Core ship test helpers such as the headless session guard?** · Target: - · Status: Waiting for owner · PR: -
- [ ] **R13.307-HL Test whether an exception in test setup stops the headless loop on Avalonia 12.1.1** · Target: - · Status: Not started · PR: -
  - Record the result in the Core Testing module docs.
- Parked: nine more shareable UI candidates stay on the Core candidate list until a second tool needs them.

### UI event failure observation

- [x] H12b — Target 1.0.0; Done. Observe all UI event operations through the canonical Core UiEventRunner, share the save command guard with close handling, and lower both async-void ratchets to zero. [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

## Owner decisions still in force

Decisions about one item are recorded with that item. These apply across the roadmap.

- **Merge approval (2026-10-02)**: only high-risk changes need owner approval on GitHub: `src/**`, `.github/**`, `scripts/**`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props` and `global.json`. After approval, a commit that only merges trunk cleanly needs no new approval if tests pass. Manual conflict resolution needs approval again (2026-10-05). Integration batches awaiting review have no limit.
- **AGENTS.md and CONTRIBUTING.md (2026-10-04)**: changes need the owner's prior confirmation in chat, not a GitHub review.
- **Review**: an agent this project dispatches (a fresh Claude or Codex) does the independent review.
- **License**: all rights reserved; copyright holder Dennis Liu. Core code, including code moved from NFH, ships under the Core proprietary license.
- **Visual changes in 1.x (2026-10-03)**: 1.x restructures only. A visible change outside the forced structural scope needs an owner decision. The Core look PR (C-THEME2) and the R13.303 opacity change are such approved decisions.
- **V21 and `LegacyRegularAnchor` (2026-10-03, 2026-10-04)**: keep both unchanged under the zero-difference gates. Full removal comes after g7 (P-V21).
- **R13.202 exact ties (2026-10-05)**: keep the status quo; no ID tie-break.
- **Core consumption (2026-10-06)**: NFH downloads the versioned Core `.nupkg` files from Core Releases before restore; `core-packages.json` pins each file's SHA-256.
- **Feature classification (2026-10-05)**: after the Core import, each new feature first gets a proposal, with reasons, that classifies it as Core or project-specific. Plain firmware product logic is project-specific.
- **Documents**: English by default. README has an English and a Traditional Chinese version.
- **Diagnostics**: `dotnet-dump` may be installed to investigate S15.002.
