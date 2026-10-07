# Development Workflow

Status: The current development workflow for this repository. Use the root `AGENTS.md` as the entry point for task authorization and technical rules; see [branch governance](branch-version-and-release-governance.md) for versions and branches. The actual gates are defined by the repository scripts and the 1.0.x roadmap.

## Before Starting (Preflight)

1. Check the current branch and working tree. Read `docs/generated/project-dependency-graph.md`, then follow `docs/agents/domain.md` to read `ROADMAP.md`, `docs/reference/refactor-contract.md`, and applicable reference contracts.
2. Confirm the outcome, affected layers, side effects, acceptance criteria, targeted test, and human/golden gates for one roadmap slice/issue. See `ROADMAP.md S15.002` for the `S15.*` issue-link exception.
3. Run `./scripts/dev/prepare-ui-workspace.ps1` before UI build/test/lint; initialize the private `example/` submodule in a new worktree before running gates. Do not copy its contents into records or public files.

## Work Admission (Admission)

When refactoring, first list the entry, reader, writer, and side effects of user actions, and confirm whether every consumer of the same result shares a single computation path and a single result model. Multiple entry points that converge on a single path may be retained; follow the playbook when multiple places recompute the same result from partial data. Place pure logic, UI orchestration, ViewModels, and Views/Controls according to `AGENTS.md`.

PRs must record the associated issue/TODO ID, outcome and scope, behavior/contract impact, actual verification, and golden evidence (if applicable). `S15.005c` has not yet established the template authority classifier, CODEOWNERS, and review record, so this document does not claim they are in effect.

## Verification Selection (Narrow test selection)

| Change | Primary verification |
| --- | --- |
| Documentation and governance structure | Check the diff, links, and CRLF; run `./scripts/verify.ps1 -StructureOnly`. |
| UI/styles | Dev page preview, token checks, `UiLayoutGuardTests`, and applicable headless smoke; tooltips require both a static style guard and tooltip-open smoke. |
| 1.0.x code slice | UI build, targeted tests, and lint, selected according to roadmap G2～G5 risks. |
| Merging a 1.0.x milestone | All-file lint, refactor gate, and UI snapshots (refactor contract G6). |

The current repo verifier lanes in `./scripts/verify.ps1` are `-StructureOnly`, `-CiLane build`, `-CiLane test -Shard core|ui|viewmodel|snapshots`, and `-All`. The `viewmodel` shard temporarily does not block CI merges; restore it according to the closure criteria in `ROADMAP.md S15.002`. Skips caused by a missing `example/` are not evidence that the private-data gate passed.

## Review and checkpoint

For each logical slice, first complete the directly relevant tests, build, and lint, inspect the diff to be committed, then record the outcome in a commit with a single scope. Push the branch at each milestone; merge requirements follow the ["Merge boundaries" in the contribution guide](../../CONTRIBUTING.md#合併邊界owner-決定2026-10-02). Gates that have not passed are not treated as passed.

## Specification Consistency and Retries

Compare the affected reference contracts, current behavior, and tests; if a conflict involves golden data or product behavior, list the evidence and human gate without rewriting the expected values yourself. On failure, first determine whether it comes from this change, an existing issue, or the environment; repeated blockers should be recorded and the investigation narrowed. See `AGENTS.md` for the fixed inventory and TODO synchronization rules for repository-wide scans.

## Handoff

Completed documentation work records the reason for the change, actual verification, and limitations. For work across sessions or multiple people, follow the [handoff protocol](../handoff/README.md) to record the scope, branch/head, checks, and remaining gates; do not copy private data into handoff records.
