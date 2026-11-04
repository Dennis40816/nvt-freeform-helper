# Contributing Guide

This guide summarizes the current branch, commit, verification, and merge rules for this repository. For implementation details, see [Development Workflow](docs/governance/development-workflow.md). For version and branch boundaries, see [Branch, Version, and Release Governance](docs/governance/branch-version-and-release-governance.md).

## Branch Model

- The default branch and the trunk is `main`. A release branch `X.Y.x`, for example `1.0.x`, is cut from `main` only when a customer release ships. It freezes the content and takes fixes only.
- Work uses `<type>/<scope>/<topic>` branches targeting `main`. The type is one of `feature`, `fix`, `refactor`, `docs`, `test`, `build`, `ci` or `chore`. The scope is a component name, such as `console` or `notch`, and never a version number. The topic is a short kebab-case name. An example is `feature/console/level-icons`.
- Do not keep `backup/` or `codex/` branches; use tags. Delete branches only from a list the owner approved.
- The version order, target versions, and status for 1.0.x are defined in `ROADMAP.md`. Slice specifications, gates, and exit criteria are defined in `docs/reference/refactor-contract.md`. The release workflow is still part of `S15.005e`. Do not treat the template's release procedure as a gate already implemented in this repository.

## Changes, Commits, and PRs

1. Define a verifiable scope from `ROADMAP.md` or an existing issue. When scope or features change, update `ROADMAP.md` immediately. Before starting, read the dependency graph and applicable contracts as described in `AGENTS.md`.
2. Complete one logical slice at a time. Verify program and UI changes against `docs/guides/refactor-playbook.md` and the gates in the 1.0.x roadmap.
3. Keep each commit within a single logical scope. Use a title and a body that explains the reason, the result, and the verification. Use `Refs #N` as described in `docs/agents/issue-tracker.md`. For existing `S15.*` exceptions, use a separate line `Refs: ROADMAP.md S15.xxx` until a corresponding issue can be created.
4. The PR description lists related issue or TODO IDs, behavior or contract impact, the verification commands actually run and their results, and any required golden evidence.

## Verification Entry Point

`./scripts/verify.ps1` is the single entry point for structure, build, and test. The actual contents of lanes and shards are determined by the script. The CI workflow only names them.

| Command | Purpose |
| --- | --- |
| `./scripts/verify.ps1 -StructureOnly` | Required files, submodule links, private paths, SDK pin, action pin, test grouping, CRLF, XAML action role. |
| `./scripts/verify.ps1 -CiLane build` | Full-file lint, analyzers, and UI build, with warnings treated as errors. |
| `./scripts/verify.ps1 -CiLane test -Shard core` | Core test shard. `ui`, `viewmodel`, and `snapshots` can replace `core`. |
| `./scripts/verify.ps1 -All` | Structure, build, and all test shards. |

Each work milestone still runs the UI build as required by `AGENTS.md`. Run `./scripts/tests/lint.ps1 -UseNoAppHost` before each commit. Run `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost` before merging into `main` (the original rule referred to merging into `master`). The 1.0.x G0 to G6 gates and targeted suites are in `docs/reference/refactor-contract.md`. The current required CI checks are `policy / structure` and `dotnet / build-test`. The `viewmodel` shard is reported but does not block merges, as a temporary decision under `ROADMAP.md S15.002`. Once that item is closed, it becomes required again.

`example/` is a private git submodule. A new clone or worktree must run `git submodule update --init example` before any gate. Without it, example-data tests may be skipped, and `run-refactor-gate.ps1` will fail. Do not bring its contents into this repo, a PR, an issue, or a log. To switch to a branch from before the submodule was set up, first run `git submodule deinit -f example`.

## Merge Boundaries (Owner Decision, 2026-10-02)

Every pull request has a tier. A machine check of the changed paths decides the tier. The author's own claim does not count.

Tier A needs approval from the owner. A pull request is tier A when it changes any of these:

- `.github/**`, and `scripts/**` that define a gate
- `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`
- release files and agent permission settings
- tests that define a check (for example the architecture, guard, baseline, ratchet or contract tests)
- new documents
- `src/**`, unless the change only deletes code (see tier B)
- golden or snapshot files, and `.axaml` view files

Tier B needs no approval from the owner. A pull request is tier B when it only changes docs and tests, debt baselines that only go down, or `src/**` `.cs` files by deleting code only (no added line, at most 200 deleted lines in all, tests only shrink, no new test file). The bot merges a tier B pull request after an independent review concludes `accept` on the final head, with no P0 or P1 finding, required checks green and no open thread.

A tier that the machine cannot prove is tier A.

The bot is the repository's existing GitHub App, inherited from the template project. Changes to `AGENTS.md` or `CONTRIBUTING.md` require owner confirmation in chat before editing. The owner said on 2026-10-04: 「讓我在聊天中確認即可」 ("Confirming in the chat is enough"). A GitHub review is not required for this confirmation. The GitHub owner-approval rules for `src/**`, `scripts/**`, `.github/**`, and the other high-risk paths above continue to apply.

Before merging, reconfirm the PR's current head, the applicable independent review and owner approval, and the required checks. Use `gh pr merge <n> --merge --match-head-commit <head>`, treating the head confirmed at that moment as the merge boundary.

The execution status for the S15.005c authority policy/review record and the S15.005d ruleset is maintained in ROADMAP.md. Do not describe unimplemented automatic checks as current gates.

## C# conventions

Follow the [C# conventions](https://github.com/Dennis40816/nvt_fw_core/blob/main/docs/core/conventions.md); the ratchet tests in `tests/FreeformHelper.Tests/Architecture` require cleanup changes to lower the matching count and update its offender list in `debt-baseline.json`.
