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
| Development line | `1.0.x` (`VERSION` = 1.0.0) |
| Next customer release | `1.0.0`, after handoff group g4 (owner decision, 2026-10-07) |
| Latest customer release in this repository | None yet |
| Latest release overall | `1.2`, tagged only in the archived private repository. It was never shipped to customers |

### Version rules

These rules follow the owner decisions of 2026-10-07.

- **Customer releases**: only a version shipped to customers gets a release tag and a GitHub Release. The number is set at release time. Customer version numbers are continuous.
- **Minor and patch**: raise the minor version only for a large set of features. Otherwise raise the patch version.
- **Release branch**: to ship a customer release, cut a branch with the same name from the trunk. It freezes the content and takes fixes only. The trunk keeps moving.
- **Internal names follow the next customer release**: the trunk name, `VERSION` and handoff names match the next customer release. No internal number may be larger than the next customer release. The next customer release is `1.0.0`, so the trunk is `1.0.x`.
- **Handoff groups and internal tags**: work is grouped in handoff order. When a whole group has merged into the trunk, the GitHub App creates one internal tag `dev/1.0.x/g<n>-<name>`. Internal tags get no Release.
- **Skipped versions**: a customer who skips versions must still be able to update. Settings and user data migrate from every customer release directly. The release notes list the accumulated changes.

## 1. Fix line

- A hotfix branch starts from the latest customer release tag.
- The fix ships as a patch release and then merges back into the release branch or `1.0.x`.
- This repository has no customer release yet. The fix line starts with `1.0.0`. Until then, ask the owner before you start a hotfix.

Open hotfixes: none.

## 2. Product features

Status of the handoff groups before `1.0.0`. The group order is fixed; a group cannot skip the exit of the one before it. Slice order inside a group may change with a recorded reason. Group exits are owner decisions. The gates and slice specs are in `docs/reference/refactor-contract.md`.

Groups g1 to g7 were named 1.3.0 to 1.3.5 before 2026-10-07. Older commits, PRs and review documents use the old names.

| Group | Old name | Theme | Output policy | Status | Internal tag |
|---|---|---|---|---|---|
| g1 | 1.3.0 | Trusted gates and numeric contracts | Zero difference after the signed R13.002 correction | Done | None. Its commits are only in the archived private repository |
| g2 | 1.3.1 | Notch single-result pipeline | Zero difference | Done. The owner closed it with A1/A2 on 2026-10-04; B1/B2 are accepted exceptions | None, for the same reason |
| g3 | 1.3.2 | Matching and domain state | Zero difference | Done. The owner closed it with the delivered scope on 2026-10-06 | `dev/1.0.x/g3-matching-domain` at the closing merge of [#39](https://github.com/Dennis40816/nvt-freeform-helper/pull/39) |
| g4 | 1.3.3, first part | Core integration (line 3) and the state batch-0 fixes | Zero data difference; the look PR changes appearance with owner approval | In progress | `dev/1.0.x/g4-core-adoption` |
| g5 | 1.3.3, second part | Settings and presentation | Zero data difference; the R13.303 opacity change changes appearance with owner approval | Not started (after 2026-10-18) | `dev/1.0.x/g5-settings-presentation` |
| g6 | 1.3.4 | Workspace ViewModel split | Zero difference | Not started. Waits for the g5 exit | `dev/1.0.x/g6-workspace-viewmodels` |
| g7 | 1.3.5 | UI structure and token cleanup | Zero data difference | Not started. Waits for the g6 exit | `dev/1.0.x/g7-ui-structure` |
| g8 | - | State refactor ST1 to ST5, after Core 1.0.0 | To be set with the plan | Not started | `dev/1.0.x/g8-state-refactor` |

`1.0.0` ships after g4. Work in g5 and later goes into a later customer release.

Order rules:

- Core comes first until 2026-10-18. Until then, NFH product work is limited to urgent fixes.
- Do one `R13.*` slice at a time.
- Zero-difference rule: `V21_before == V21_after` and `V22_before == V22_after`. Work that must change Firmware C bytes leaves the refactor plan as a separate behavior item.

### 2.1 Waiting for the owner

The owner closed these parents on 2026-10-07 because their work is delivered: R13.101, R13.102 and R13.103 (parent spec [#27](https://github.com/Dennis40816/nvt-freeform-helper/issues/27)), S15.005 with S15.005a, and S15.009 with S15.009d. V21 and `LegacyRegularAnchor` stay as they are.

- [ ] **R13.104 Converge the Notch display and EMS safety predicate and text** · Target: g2 · Status: Waiting for owner · PR: -
  - R13.104a-1 to a-14 are done. Decide whether the remaining follow-up scope is open work: the `255` cap spread over Step 3 XAML, settings sync, generator, exporter and formatter, and separate Simulation, replay and Dev texts.
- [ ] **P-MASK Behavior for a missing saved mask path and for importing a mask while enabled** · Target: - · Status: Waiting for owner · PR: [#22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) (characterization tests only)
- [ ] **P-V21 Remove V21 and `LegacyRegularAnchor` completely** · Target: after g7 · Status: Waiting for owner (scope) · PR: -
  - No implicit conversion before then. Planning notes: `docs/reviews/v21-*-2026-10.md`.

### 2.2 g5 settings and presentation (after 2026-10-18)

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
- [ ] **R13.406 Remaining typed-ID chains (from R13.203) and RegularPad writer transitions (from R13.204)** · Target: g6 · Status: Not started (after 2026-10-18) · PR: -
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

The owner scheduled these five batches after the NVT Core 1.0.0 release (target 2026-10-15). Each batch is one PR, and each starts with tests that pin the current behavior.

- [ ] **ST1 Compute derived values instead of storing them in `FreeformHelperViewModel`** · Target: after Core 1.0.0 · Status: Not started · PR: -
- [ ] **ST2 Nesting-safe suppression guards, a view-model mode enum and a PadCanvas gesture enum** · Target: after Core 1.0.0 · Status: Not started · PR: -
  - The owner decided that editing is blocked while a project loads.
- [ ] **ST3 Group fields that always change together into records** · Target: after Core 1.0.0 · Status: Not started · PR: -
- [ ] **ST4 Extract five child view models: DxfEditState, NotchValidationPanel, PadInspector, SimulationSafetyOverview and the background operations** · Target: after Core 1.0.0 · Status: Not started · PR: -
- [ ] **ST5 Coordinate planner corners and guides, PadCanvas selection ownership, and view code-behind copies** · Target: after Core 1.0.0 · Status: Not started · PR: -

### 2.5 Repository, CI and release

- [ ] **S15.002 Remove the full-suite timeouts in `FreeformHelperViewModelTests`, then make the `viewmodel` CI shard required again** · Target: - · Status: Blocked: needs a new failure trace for root cause six (headless dispatcher race) · PR: -
  - Root causes 1 to 5, 7 and 8 are fixed. The earlier analysis predates Avalonia 12.1.1 and xUnit v3.
- [ ] **R13.306-F1 Give the four main-window tests a fake CAD load spinner host** · Target: - · Status: Not started · PR: -
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

## 3. Core integration

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
- [ ] **C-CONSOLE Adopt the redesigned Core Console and delete NFH's console** · Target: g4 (Core 1.0.0) · Status: Not started; Core implements it first · PR: [Core #82](https://github.com/Dennis40816/nvt_fw_core/pull/82) (approved design)
  - NFH fixed console review items 01 and 02 in [#49](https://github.com/Dennis40816/nvt-freeform-helper/pull/49). The Core redesign covers items 03 to 21, S15.017 and R13.304.
- [ ] **R13.307-ICON Material Symbols becomes the shared Core icon system; delete the unused `MaterialIcons-Regular.ttf`** · Target: g4 · Status: Not started · PR: -
- [ ] **R13.307-FONT Adopt the Core font set** · Target: g4 · Status: Not started · PR: -
- [ ] **R13.307-LIC Ship the Core license as `licenses/Nvt.Core/LICENSE`** · Target: first NFH release with Core · Status: Not started · PR: -
- [ ] **C-LSC Use LoadingScopeCoordinator from the Core non-UI library** · Target: g4 · Status: Not started; Core side in [Core #44](https://github.com/Dennis40816/nvt_fw_core/pull/44) · PR: -
- [x] **C-NONUI Move CoalescedRefresh and UndoService to the Core non-UI library** · Target: g4 · Status: Done · PR: [#60](https://github.com/Dennis40816/nvt-freeform-helper/pull/60)
- [ ] **C-CTRL Move cards, dialogs and input controls to Core** · Target: g4 · Status: Not started · PR: -
- [ ] **R13.307-GUARD Should Core ship test helpers such as the headless session guard?** · Target: - · Status: Waiting for owner · PR: -
- [ ] **R13.307-HL Test whether an exception in test setup stops the headless loop on Avalonia 12.1.1** · Target: - · Status: Not started · PR: -
  - Record the result in the Core Testing module docs.
- Parked: nine more shareable UI candidates stay on the Core candidate list until a second tool needs them.

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
