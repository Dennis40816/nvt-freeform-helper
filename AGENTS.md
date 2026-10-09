# FreeformHelper Agent Rules

## Mission and Basis

These rules apply to all refactors and UI changes in this repository. FreeformHelper is a single context repository. Before exploring code, read the dependency graph, the applicable reference contract, and the current 1.0.x roadmap as described in `docs/agents/domain.md`. At the start of each task, you must check `docs/generated/project-dependency-graph.md` first. If it is missing or stale, rebuild it with `scripts/build/generate-dependency-graph.ps1`, then do deeper file searches.

Specifications and executable work are tracked in GitHub Issues. Commits and PRs must link the related issue, as described in `docs/agents/issue-tracker.md`. Lifecycle labels are `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See `docs/agents/triage-labels.md` for details.

## Task Scope and Autonomous Execution

- Reply in Chinese by default, unless the user asks for something else.
- Use low-usage execution: handle one TODO slice at a time. Do not scan the whole repository or run the full test suite unless a milestone or failure investigation requires it.
- Do not suppress warnings with `SuppressMessage`, `#pragma warning disable`, editorconfig severity downgrades, or equivalent methods, unless the user explicitly asks for it.
- Do not relax existing lint standards. Do not merge while lint failures remain.

## Delegation and Handoff

For multi-person work or handoff across sessions, see `docs/handoff/README.md` for the handoff record format and the bug ledger entry point. For refactor handoff and implementation steps, see `docs/guides/refactor-playbook.md`.

## Standard Commands and Workspace Preparation

- For each logical milestone, run `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`, then push the branch.
- Before each UI build, test, or lint cycle, run `./scripts/dev/prepare-ui-workspace.ps1` once. It stops stale `FreeformHelper.UI`, spinner, and dotnet UI processes, and normalizes modified text files to CRLF. Do not repeatedly use ad hoc PowerShell commands to do the same workspace cleanup. During the 1.2 UI phase, process locks cost at least two build/test rounds, and fixing line endings cost time several times.
- After script edits or batch rewrites, and before build or lint, run `./scripts/dev/prepare-ui-workspace.ps1 -SkipStopApp`. This restores touched text files to CRLF, so you do not depend on formatting fixes later. All tracked text files must follow the CRLF rules in `.editorconfig` and `.gitattributes`.
- If a tool reports that a file is locked by `FreeformHelper.UI`, run `./scripts/dev/prepare-ui-workspace.ps1 -SkipNormalizeLineEndings` once, then retry. Do not rerun the same build or test command directly.
- Before each commit, run `./scripts/tests/lint.ps1 -UseNoAppHost`. Before merging into `main`, run `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`. (The original rule said `master` before merging.)
- The repo verifier runs its structure, build, test, and all lanes through `./scripts/verify.ps1`. Choose the lane that matches the actual work. See `CONTRIBUTING.md` for details.
- `example/` is a private git submodule. For a new clone or worktree, run `git submodule update --init example` before any gate. If it is missing, example-data tests may be skipped, and `run-refactor-gate.ps1` will fail. Do not bring its contents into this repo, PRs, issues, or logs. When switching to a branch from before the submodule was set up, run `git submodule deinit -f example` first.

## Core Architecture and Existing Behavior Contracts

- Prefer a single entry point for user-visible actions (commands, property-change side effects, UI events).
- User-visible computed results must keep one source-of-truth calculation path and one result model. Multiple UI entry points may remain only if they converge to the same state and the same final predicate or calculation path.
- UI, RuntimeQuery, export, inspector, pad info, and views must not each re-derive the same result from partial data. When several places show the same result, project from a shared model.
- ViewModels handle only UI state and command binding. Workflows go through dedicated UseCases or services. Views, Controls, and ViewModels must not duplicate business logic. Move shared logic into Application or UI services.
- Explicitly list side effects such as selection clear, rebuild triggers, and fit/zoom reset, and keep their policy in one place.
- Pure logic and algorithms belong in `src/FreeformHelper.Application`. UI workflow orchestration belongs in `src/FreeformHelper.UI/Services` or dedicated UseCases directories. UI components and styles belong in `src/FreeformHelper.UI/Views`, `src/FreeformHelper.UI/Controls`, and `src/FreeformHelper.UI/Styles`.

### Current Runtime Mechanisms

The following four runtime mechanisms must not be changed without an explicit task.

- App settings persistence: after `Load Project`, app-level general settings enter deferred mode. App-level writes are flushed only after the next successful `Save Project`.
- App settings tests must not write to the user's real settings. `tests/FreeformHelper.Tests/TestInfrastructure/TestAppSettingsIsolation.cs` redirects `FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH`.
- `Ctrl+S` calls `SaveProjectAsync()` when not in a text input context. `MainWindow` shows the save result as a top toast.
- Runtime query IPC commands must follow `docs/reference/runtime-cli-plan.md`. A new query command must go through the single entry point in `RuntimeQueryUseCase`.

## UI Style and Theme Rules

- Views and Controls must not add inline colors or sizes. Use design tokens from `src/FreeformHelper.UI/Styles/Tokens.axaml`. Code-behind must not hardcode colors, sizes, or brushes. Resolve them from tokens or resources.
- Icons follow shared size and brush conventions. Do not use temporary glyph sizes or custom stroke colors.
- Preview a new control style or icon change on the Dev page before applying it widely. UI/UX changes must confirm the Dev page preview and token use.
- Advanced edit areas must use flat accordion or header-body handling. Do not restore the old dropdown appearance or the nested card plus default expander combination.
- New UI resources must support themes through `DynamicResource` and tokens. If a token is missing, add it to `Tokens.axaml` first. Do not hardcode values in XAML.

## Risk and Verification Gates

- Runtime-only style risks must have a guard test before commit. Tooltip style changes need both the static style guard in `UiLayoutGuardTests` and a headless tooltip-open smoke test.
- For details on data paths, golden files, and the 1.0.x gate, see `docs/reference/refactor-contract.md`.

## Branch and Review Boundaries

- The default branch and the trunk is `main`. Work uses `<type>/<scope>/<topic>` branches targeting `main`. The type is one of `feature`, `fix`, `refactor`, `docs`, `test`, `build`, `ci` or `chore`. The scope is a component name, such as `console` or `notch`, and never a version number. The topic is a short kebab-case name. A release branch `X.Y.x` is cut from `main` only when a customer release ships. Do not keep `backup/` or `codex/` branches; use tags. Delete branches only from a list the owner approved.
- Changes to `AGENTS.md` or `CONTRIBUTING.md` require owner confirmation in chat before editing. The owner said on 2026-10-04: 「讓我在聊天中確認即可」 ("Confirming in the chat is enough"). A GitHub review is not required for this confirmation. The owner-approval rules for high-risk PRs in `CONTRIBUTING.md`, including `src/**`, `scripts/**`, and `.github/**`, still apply.

- Keep each commit small and clear. Each commit must have a title and a body. Do not put unrelated fixes in the same commit. Prefer committing them one at a time.
- For branch, version, and release governance, see `docs/governance/branch-version-and-release-governance.md`. For PR and merge rules and the contributor execution order, see `CONTRIBUTING.md`.

## Skills, Scans, and Completion Criteria

- When a trigger condition is met, follow the workflows in the automatically discovered `.agents/skills/freeform-refactor/SKILL.md`, `.agents/skills/ui-consistency/SKILL.md`, and `.agents/skills/repo-optimizer-loop/SKILL.md`.
- A full-repository refactor scan must include at least: file size and line count hotspots, a summary of analyzer warnings, and checks for stale documentation or behavior in `docs/reference/behavior-inventory.md` and `docs/guides/settings-entry-matrix.md`.
- A full-repository scan must explicitly audit "multiple derivation paths for the same feature or result." Record separately the acceptable multi-entry single-path cases and the unacceptable multi-path re-derivations.
- Immediately write newly found optimization or refactor items into the matching track in `ROADMAP.md` (fix track, product features, or Core integration). Include the target version, status, and PR or issue link.

## C# conventions

New code follows the [C# conventions](https://github.com/Dennis40816/nvt_fw_core/blob/main/docs/core/conventions.md). The ratchet tests in `tests/FreeformHelper.Tests/Architecture` fail when a counted metric gets worse or its baseline is not lowered after a cleanup.
