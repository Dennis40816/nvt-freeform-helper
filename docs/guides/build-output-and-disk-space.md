# Build Output and Disk Space Management

When `ArtifactsPath` is not set, `Directory.Build.props` places each project's `bin` and `obj` under the `build/` of the **current checkout**. Git worktrees usually each have their own working directory and `build/`. The tool selects only the `build/` subdirectory of the checkout where the script is located. If the selected directory contains another registered worktree, the tool refuses to delete. Isolated measurement work that sets `ArtifactsPath` sets its output location according to its own script.

The out-of-repo test area is managed under the decision in the next section, S15.005a. It is not included in the build cleanup tool's target scope for this guide.

## Out-of-Repo Test Scratch Area (S15.005a)

The owner decided on 2026-10-04 that the default location is `D:\FreeformHelper-TestArea`. While `scripts/tests/run-tests.ps1` runs `dotnet test`, it creates `<test scratch area>\temp` and sets `TEMP`, `TMP`, and `TMPDIR` to point to this directory. The environment of the calling shell and other tools is not affected. `FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH` isolation and `-ValidateOnly` behavior remain unchanged.

The environment variable `FREEFORMHELPER_TEST_AREA` can override the root directory of the test scratch area. The three scratch variables still point to its `temp` subdirectory. If the area is not overridden and the default `D:` drive does not exist, the script outputs one line of explanation, keeps the original scratch environment, and falls back to the system temp area.

Test artifacts are kept for 7 days and cleaned manually. Only artifacts from finished work that have been kept for at least 7 days may be deleted. Before cleanup, confirm that the corresponding test work and its child processes have ended. Artifacts that are still running or whose ownership cannot be confirmed are kept. Diagnostic evidence that must be retained is saved separately first. Do not decide deletability based on the timestamp of the whole `temp` directory.

## Contents and Retention Rules for `build/`

| Path | Source and purpose | Cleanup rule |
| --- | --- | --- |
| `bin/`, `obj/` | Compile output, intermediate files, and restore output specified by `Directory.Build.props` | Rebuildable. By default, the cleanup list contains only these two items. After deletion, the next build may need to restore packages again. |
| `perf/` | Startup, load, PadMatch, and 3635 regression measurements from `scripts/perf/`; may contain per-run records, CSV, C output, and summaries | Measurement evidence. When comparison or audit is needed, copy it to an appropriate retention location first. |
| `test-gate/` | JSON summaries from `run-refactor-gate.ps1`, `run-pre-push-gate.ps1`, workspace preparation, and structure validation; the code-size contract check also places measurement files here | Gate evidence. Confirm the records are saved first. |
| `code-size/` | JSON manifests from `measure-code-size.ps1`, isolated Release DLL size and hash artifacts, and staging build trees | Manifests and artifacts support reproducibility audits. The whole directory is kept by default. Save required evidence before cleanup. |
| `logs/` | UI logs; several performance scripts use `build/logs/app.log` as input | May be raw diagnostic or measurement evidence. Save the records you need first. |
| `publish/`, `packages/` | Output of publish scripts and packages produced by `zip_repo.ps1` | May be deliverable artifacts. Confirm they have been delivered or save them separately first. |
| Other directories | Content not explicitly listed by the cleanup tool | Always keep. Source must be confirmed manually. |

Even if the scripts can regenerate the above evidence and deliverable artifacts, they may not be able to reproduce the original inputs, versions, or measurement environment. The cleanup tool's `-IncludeEvidence` adds `perf/`, `test-gate/`, `code-size/`, `logs/`, `publish/`, and `packages/` from the table to the list. Before using it, copy the files you need to keep. The `build/` root directory itself and unknown items are never included.

## How to Operate

Run from the worktree you want to clean:

```powershell
./scripts/dev/clean-build-output.ps1
./scripts/dev/clean-build-output.ps1 -Apply
```

The first line is the default dry run. It shows each directory planned for deletion and the total in bytes and GiB, with no deletion action. Use `-Apply` only after confirming the plan. Use `-IncludeEvidence -Apply` only after the evidence has been saved and you are sure the above evidence and deliverable artifacts are no longer needed. The tool selects the `build/` subdirectory only from the **script's repository root**. It does not accept an external target path. If it encounters a reparse point, or a `.git` file or directory at any depth of the selected directory, it refuses to process it. Before deletion, it also compares against `git worktree list --porcelain`. If the root directory of another registered worktree equals or is inside the selected directory, it refuses to delete.

`-Apply` uses deliberately strict process rules. If any `dotnet.exe`, `testhost.exe`, `testhost`, `MSBuild.exe`, `VBCSCompiler.exe`, or `FreeformHelper.UI.exe` exists on the machine, it lists the process names and IDs and refuses to delete. It also refuses to delete if the process list cannot be read. Even unrelated .NET processes may block cleanup. Close build, test, UI, and IDE sessions first, run `dotnet build-server shutdown`, and then run the cleanup command again. The build server may still remain in the background after a build ends.

Before switching or deleting a worktree, run a dry run in that worktree, save the evidence you need, and then clean or remove that worktree. Do not clean the `build/` of one worktree from another worktree. The owner decides on the `build/` of the main checkout, which is about 37 GB. This task does not clean it. After this script enters the main checkout, the owner can run `./scripts/dev/clean-build-output.ps1` in the **main checkout** to review the plan, and then run `./scripts/dev/clean-build-output.ps1 -Apply` to clean the default items.
