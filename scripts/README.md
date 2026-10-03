# Scripts Layout

腳本已依用途分層，避免單一目錄過度擁擠：

- `scripts/verify.ps1`: 本機與 CI 共用的單一驗證入口。`-StructureOnly`（不需 build，數秒）、`-CiLane build`、`-CiLane test -Shard core|ui|viewmodel|snapshots`、`-All`；預設要求 `example/` 資料存在，沒有權限時加 `-AllowMissingExampleData`
- `scripts/ci/`
  - `install-dotnet.ps1`: CI 用；把 `global.json` 指定的 SDK 與專案 target 的 runtime 裝到 repo 內的 `.dotnet/`
- `scripts/build/`
  - `build.ps1`: solution build + optional tests（預設同時更新 dependency graph）
  - `generate-dependency-graph.ps1`: 產生 `docs/generated/project-dependency-graph.md/.json`
  - `publish-exe.ps1`: 發佈核心腳本（`single-file` / `folder`）
  - `publish-exe-single-file.ps1`: 單檔 EXE 包裝入口
  - `publish-exe-folder.ps1`: 資料夾 EXE 包裝入口
- `scripts/perf/`
  - `collect-perf-baseline.ps1`
  - `measure-startup-load.ps1`
  - `measure-startup-load-batch.ps1`
  - `analyze-startup-markers.ps1`
  - `run-3635-regression-baseline.ps1`: 真實 UI/IPC 的 3635 workflow、V21/V22 normalized byte-exact golden 與 performance budget gate
  - `measure-code-size.ps1`: tracked production source primary metric；clean isolated Release DLL size/hash secondary metric
  - `extract-padmatch-telemetry.ps1`
  - `evaluate-padmatch-telemetry.ps1`
  - `audit-fontsize-overrides.ps1`
- `scripts/runtime/`
  - `runtime-quick.ps1`: load-project + run-step + query 快速流程
- `scripts/dev/`
  - `prepare-ui-workspace.ps1`: UI build/test 前置入口；依 Git worktree 清單排除主 checkout 內的其他工作樹，只停止屬於本工作樹的 `FreeformHelper.UI`/spinner/dotnet UI process；Git 查詢失敗時不停止任何程序。並正規化變更檔案為 `CRLF`；`-DryRun` 列出會停止與略過的 process，不停止程序或正規化檔案
  - `clean-build-output.ps1`: 顯示本工作樹 `build/` 可清理目錄及容量；預設 dry run 只選取 `bin/`、`obj/`，`-Apply` 才刪除；`-IncludeEvidence` 才納入證據與交付產物。拒絕 reparse point、含其他已登錄 worktree 或 `.git` 項目的目錄。`-Apply` 在機器上有指定的 .NET 建置／測試／UI 程序或無法讀取程序清單時拒絕刪除，列出阻擋程序名稱與 ID；此規則刻意嚴格，也可能被無關程序阻擋。請關閉建置與 IDE 工作階段、執行 `dotnet build-server shutdown` 後重試；保留規則見 `docs/guides/build-output-and-disk-space.md`。repo 外 test area 仍待 S15.005a 決定
- `scripts/package/`
  - `zip_repo.ps1`: 產生給 OpenAI 上傳的精簡 zip（排除執行檔/常見生成輸出，可限制檔案大小）。`example/` 是 submodule，內容不會被打包
- `scripts/tests/`
  - `check-build-output-selection.ps1`: 以臨時目錄驗證 build 清理範圍、容量與 worktree／`.git` 拒絕規則，另以三個名稱清單案例驗證程序阻擋；由 `verify.ps1 -StructureOnly` 執行
  - `check-ui-process-workspace.ps1`: 驗證 UI process 的工作樹歸屬判斷（由 `verify.ps1 -StructureOnly` 執行）
  - `normalize-crlf.ps1`: 將指定範圍內的文字檔行尾正規化為 `CRLF`（預設只處理變更檔；`-AllFiles` 全量）
  - `run-tests.ps1`: 測試分組入口（含 `notch-core` / `notch-golden`）。`ui-core` 是 `ui-stable`（除 `FreeformHelperViewModelTests` 外的 UI 類別）與 `ui-viewmodel`（只有該類別）的聯集；`uncategorized` 分組會跑所有未被任何分組明列的測試類別，因此「各明列分組 + `uncategorized`」一定涵蓋 `all`；分組清單中若有已不存在的類別名稱會直接報錯。測試失敗時腳本會 throw。
  - `assert-example-data.ps1`: 檢查 `example/` 已抓取、正好在本 repo 釘住的 commit 且沒有未提交變更；兩個 gate、`run-tests.ps1` 與 `verify.ps1` 在跑測試前都會呼叫
  - `run-pre-push-gate.ps1`: pre-push gate（build + dependency graph + targeted tests + lint；`-Milestone` 追加 `lint -AllFiles`）。一律檢查 `example/` 資料（存在、在釘住且已 commit 的指標、資料 commit 已推到資料 repo），加 `-SkipTests` 時也檢查；沒有權限時加 `-AllowMissingExampleData`
  - `run-refactor-gate.ps1`: 重構固定 gate（lint + build + 全部測試分組：notch-core/application/infrastructure/ui-core/ui-snapshots/uncategorized；`-IncludeNotchCore`、`-IncludeInfrastructure`、`-IncludeUiSnapshots` 仍可傳入但已是預設行為）。`example/` submodule 沒抓到或不在釘住的 commit 時，gate 在 lint 之前就失敗；沒有資料權限時加 `-AllowMissingExampleData`
  - `check-startup-budget.ps1`: 啟動預算 gate（`workspace.initial-grid-built`）
  - `check-code-size-baseline.ps1`: code-size line/path semantics、signed reference 與 group/delta consistency gate
  - `lint.ps1`: lint 入口（預設檢查變更檔案；`-AllFiles` 全量；`-AnalyzerScope Solution` 可跑全 solution analyzer；執行前會自動跑 CRLF normalize，可用 `-SkipNormalizeLineEndings` 關閉）
  - `update-ui-baseline.ps1`: UI baseline dry-run/apply

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
# authoritative Release metric 要求 tracked build inputs clean；入口會先執行 workspace preparation
./scripts/perf/measure-code-size.ps1
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi
# 已有 UseAppHost=true 的 UI executable 時才可跳過 build
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -LaunchIsolatedUi
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -LaunchIsolatedUi -EnforceBudget
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -LaunchIsolatedUi -ReverseCExportOrder
# 僅供非 gate 的自訂效能實驗
./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -SkipGoldenCheck
./scripts/perf/extract-padmatch-telemetry.ps1
./scripts/perf/evaluate-padmatch-telemetry.ps1
./scripts/build/publish-exe-single-file.ps1
```
