# Scripts Layout

Scripts are grouped by purpose to keep any single directory from becoming crowded:

- `scripts/verify.ps1`: The single validation entry point shared by local runs and CI. Options: `-StructureOnly` (no build needed, takes seconds), `-CiLane build`, `-CiLane test -Shard core|ui|viewmodel|snapshots`, and `-All`. By default it requires `example/` data to exist. If you lack permission, add `-AllowMissingExampleData`.
- `scripts/ci/`
  - `install-dotnet.ps1`: For CI. Installs the SDK specified in `global.json` and the runtime for the project target into `.dotnet/` inside the repo.
- `scripts/build/`
  - `build.ps1`: Solution build plus optional tests. Also updates the dependency graph by default.
  - `generate-dependency-graph.ps1`: Generates `docs/generated/project-dependency-graph.md/.json`.
  - `publish-exe.ps1`: Core publish script (`single-file` / `folder`).
  - `publish-exe-single-file.ps1`: Entry point for single-file EXE packaging.
  - `publish-exe-folder.ps1`: Entry point for folder EXE packaging.
- `scripts/perf/`
  - `collect-perf-baseline.ps1`
  - `measure-startup-load.ps1`
  - `measure-startup-load-batch.ps1`
  - `analyze-startup-markers.ps1`
  - `run-3635-regression-baseline.ps1`: 3635 workflow using real UI/IPC, V21/V22 normalized byte-exact golden files, and a performance budget gate.
  - `measure-code-size.ps1`: Primary metric for tracked production source. Secondary metric: size/hash of a clean, isolated Release DLL.
  - `extract-padmatch-telemetry.ps1`
  - `evaluate-padmatch-telemetry.ps1`
  - `audit-fontsize-overrides.ps1`
- `scripts/runtime/`
  - `runtime-quick.ps1`: Quick flow of load-project, run-step, and query.
- `scripts/dev/`
  - `prepare-ui-workspace.ps1`: Entry point to prepare for UI build/test. Uses the Git worktree list to exclude other worktrees inside the main checkout. It only stops `FreeformHelper.UI`, spinner, and dotnet UI processes that belong to this worktree. If a Git query fails, it stops no processes. It also normalizes changed files to `CRLF`. With `-DryRun`, it lists the processes it would stop and skip, without stopping any processes or normalizing any files.
  - `clean-build-output.ps1`: Shows the `build/` directories in this worktree that can be cleaned, along with their sizes. By default it is a dry run that selects only `bin/` and `obj/`. `-Apply` is required to delete. `-IncludeEvidence` is required to include evidence and deliverable artifacts. It refuses reparse points and directories that contain other registered worktrees or `.git` entries. With `-Apply`, it refuses to delete if the machine has any specified .NET build, test, or UI processes running, or if the process list cannot be read. It lists the names and IDs of the blocking processes. This rule is intentionally strict and may be blocked by unrelated processes. To proceed, close build and IDE sessions, run `dotnet build-server shutdown`, and retry. For retention rules, see `docs/guides/build-output-and-disk-space.md`. Cleanup of test areas outside the repo is still pending the decision in S15.005a.
- `scripts/package/`
  - `zip_repo.ps1`: Creates a slimmed-down zip for upload to OpenAI. Excludes executables and common generated output, and can limit file size. `example/` is a submodule, so its contents are not packaged.
- `scripts/tests/`
  - `check-build-output-selection.ps1`: Uses a temporary directory to verify the build cleanup scope, size reporting, and the worktree and `.git` refusal rules. It also verifies process blocking with three name-list cases. Run by `verify.ps1 -StructureOnly`.
  - `check-ui-process-workspace.ps1`: Verifies how UI processes are attributed to a worktree. Run by `verify.ps1 -StructureOnly`.
  - `normalize-crlf.ps1`: Normalizes line endings of text files in the specified scope to `CRLF`. By default it only handles changed files. `-AllFiles` processes all files.
  - `run-tests.ps1`: Entry point for test groups, including `notch-core` and `notch-golden`. `ui-core` is the union of `ui-stable` (UI classes other than `FreeformHelperViewModelTests`) and `ui-viewmodel` (only that class). The `uncategorized` group runs every test class not listed in any group, so "each listed group plus `uncategorized`" always covers `all`. If a group list names a class that no longer exists, the script raises an error. The script throws when tests fail.
  - `assert-example-data.ps1`: Checks that `example/` is fetched, is at exactly the commit pinned by this repo, and has no uncommitted changes. Both gates, `run-tests.ps1`, and `verify.ps1` call it before running tests.
  - `run-pre-push-gate.ps1`: Pre-push gate (build, dependency graph, targeted tests, and lint). `-Milestone` adds `lint -AllFiles`. It always checks `example/` data (exists, at the pinned and committed pointer, and the data commit has been pushed to the data repo), and it also does so with `-SkipTests`. If you lack permission, add `-AllowMissingExampleData`.
  - `run-refactor-gate.ps1`: Fixed gate for refactoring (lint, build, and all test groups: notch-core, application, infrastructure, ui-core, ui-snapshots, uncategorized). `-IncludeNotchCore`, `-IncludeInfrastructure`, and `-IncludeUiSnapshots` can still be passed, but they are already the default behavior. If the `example/` submodule is not fetched or is not at the pinned commit, the gate fails before lint. If you lack data permission, add `-AllowMissingExampleData`.
  - `check-startup-budget.ps1`: Startup budget gate (`workspace.initial-grid-built`).
  - `check-code-size-baseline.ps1`: Gate for code-size line/path semantics, signed references, and group/delta consistency.
  - `lint.ps1`: Lint entry point. By default it checks changed files. `-AllFiles` checks everything. `-AnalyzerScope Solution` runs analyzers across the full solution. It runs CRLF normalization automatically before linting. Use `-SkipNormalizeLineEndings` to turn this off.
  - `update-ui-baseline.ps1`: UI baseline dry run and apply.

## Quick Commands

```powershell
./scripts/build/build.ps1
./scripts/build/generate-dependency-graph.ps1
./scripts/dev/prepare-ui-workspace.ps1
./scripts/dev/prepare-ui-workspace.ps1 -SkipStopApp
./scripts/dev/prepare-ui-workspace.ps1 -DryRun
./scripts/dev/clean-build-output.ps1
./scripts/dev/clean-build-output.ps1 -Apply
./scripts/tests/run-pre-push-gate.ps1 -UseNoAppHost
./scripts/tests/run-pre-push-gate.ps1 -UseNoAppHost -TestGroups notch-core
./scripts/tests/run-pre-push-gate.ps1 -UseNoAppHost -Milestone
./scripts/tests/normalize-crlf.ps1
./scripts/tests/normalize-crlf.ps1 -AllFiles
./scripts/tests/lint.ps1
./scripts/tests/lint.ps1 -AllFiles
./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost
./scripts/tests/lint.ps1 -AllFiles -AnalyzerScope Solution
./scripts/tests/run-refactor-gate.ps1
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -IncludeNotchCore
./scripts/tests/run-refactor-gate.ps1 -LintAllFiles -LintAnalyzerScope Solution
./scripts/tests/run-refactor-gate.ps1 -LintAllFiles -LintAnalyzerScope Solution -UseNoAppHost
./scripts/tests/run-refactor-gate.ps1 -IncludeStartupBudget -InitialGridBudgetMs 1000
./scripts/tests/check-startup-budget.ps1 -SkipBuild -InitialGridBudgetMs 1000
./scripts/tests/check-code-size-baseline.ps1
./scripts/tests/run-tests.ps1 -Group smoke
./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost
./scripts/tests/run-tests.ps1 -Group notch-golden -UseNoAppHost
./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost
./scripts/tests/run-tests.ps1 -Group uncategorized -UseNoAppHost
./scripts/runtime/runtime-quick.ps1 -Project example/BOE36.35/project_3635.json
./scripts/package/zip_repo.ps1
./scripts/package/zip_repo.ps1 -MaxFileSizeMB 20
./scripts/package/zip_repo.ps1 -DryRun
./scripts/perf/collect-perf-baseline.ps1
./scripts/perf/measure-code-size.ps1 -SkipReleaseBuild
# authoritative Release metric requires tracked build inputs to be clean; the entry point runs workspace preparation first
./scripts/perf/measure-code-size.ps1
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi
# Only skip the build when a UI executable with UseAppHost=true already exists
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -LaunchIsolatedUi
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -LaunchIsolatedUi -EnforceBudget
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -LaunchIsolatedUi -ReverseCExportOrder
# For custom performance experiments only, not for gates
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -SkipGoldenCheck
./scripts/perf/extract-padmatch-telemetry.ps1
./scripts/perf/evaluate-padmatch-telemetry.ps1
./scripts/build/publish-exe-single-file.ps1
```
