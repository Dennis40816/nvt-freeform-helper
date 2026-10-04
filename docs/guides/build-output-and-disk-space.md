# Build 輸出與磁碟空間管理

`Directory.Build.props` 在未設定 `ArtifactsPath` 時，將各專案的 `bin` 與 `obj` 放在**目前 checkout** 的 `build/`。Git worktree 通常各有自己的工作目錄與 `build/`；工具只選取腳本所在 checkout 的 `build/` 子目錄，若選取目錄內有已登錄的其他 worktree，會拒絕刪除。設定 `ArtifactsPath` 的隔離量測工作另依其腳本設定輸出位置。

The external test area decided by the owner on 2026-10-04 (S15.005a) is implemented in PR #14 and awaits integration: `scripts/tests/run-tests.ps1` defaults to `D:\FreeformHelper-TestArea`, accepts the `FREEFORMHELPER_TEST_AREA` override, and sets `TEMP` / `TMP` / `TMPDIR` only for tests, with seven-day retention and manual cleanup. This is not yet available on trunk; see [handoff section 7](../reviews/r13-131-milestone-handoff-2026-10.md#7-handoff-for-2026-10-06) for the dated PR status. The build cleanup tool still targets only the `build/` directory of each checkout and does not delete the test area.

## `build/` 的內容與保留規則

| 路徑 | 來源與用途 | 清理規則 |
| --- | --- | --- |
| `bin/`、`obj/` | `Directory.Build.props` 指定的編譯輸出、中介檔與還原產物 | 可重建；預設清理清單只包含這兩項。刪除後下次 build 可能需重新還原套件。 |
| `perf/` | `scripts/perf/` 的啟動、載入、PadMatch 與 3635 回歸量測；可能含逐次紀錄、CSV、C 輸出與 summary | 屬於量測證據；需要比較或審核時先複製到適當的保存位置。 |
| `test-gate/` | `run-refactor-gate.ps1`、`run-pre-push-gate.ps1`、workspace 準備與結構驗證的 JSON 摘要；code-size contract 檢查也會在此放量測檔 | 屬於 gate 證據；先確認紀錄已保存。 |
| `code-size/` | `measure-code-size.ps1` 的 JSON manifest、隔離的 Release DLL 大小／雜湊產物，以及暫存建置樹 | manifest 與 artifact 可供重現性審核；整個目錄預設保留，清理前先保存所需證據。 |
| `logs/` | UI 日誌；多個效能腳本以 `build/logs/app.log` 為輸入 | 可能是診斷或量測原始證據；先保存需要的紀錄。 |
| `publish/`、`packages/` | 發佈腳本的輸出與 `zip_repo.ps1` 產生的封裝 | 可能是待交付的產物；先確認已交付或另存。 |
| 其他目錄 | 非清理工具明列的內容 | 一律保留，需人工確認來源。 |

以上證據與交付產物即使可由腳本再次產生，也可能無法重現當時的輸入、版本或量測環境。清理工具的 `-IncludeEvidence` 會把表中的 `perf/`、`test-gate/`、`code-size/`、`logs/`、`publish/`、`packages/` 一併列入；使用前先複製要留存的檔案。`build/` 根目錄本身與未知項目永不列入。

## 操作方式

從要清理的 worktree 執行：

```powershell
./scripts/dev/clean-build-output.ps1
./scripts/dev/clean-build-output.ps1 -Apply
```

第一行是預設 dry run，逐項顯示預計刪除的目錄及位元組／GiB 合計，沒有刪除動作。確認計畫後才使用 `-Apply`。只有已保存證據並確定不再需要上述證據與交付產物時，才使用 `-IncludeEvidence -Apply`。工具只依**腳本所在的 repository root** 選擇 `build/` 子目錄，不接受外部目標路徑；遇到 reparse point 或選取目錄任一深度的 `.git` 檔案／目錄會拒絕處理。刪除前還會比對 `git worktree list --porcelain`：其他已登錄 worktree 的根目錄若等於或位於選取目錄內，就拒絕刪除。

`-Apply` 採用刻意嚴格的程序規則：只要機器上存在任何 `dotnet.exe`、`testhost.exe`、`testhost`、`MSBuild.exe`、`VBCSCompiler.exe` 或 `FreeformHelper.UI.exe`，就會列出程序名稱與 ID 並拒絕刪除；無法讀取程序清單時也拒絕刪除。即使是無關的 .NET 程序，也可能阻擋清理。請先關閉建置、測試、UI 與 IDE 工作階段，再執行 `dotnet build-server shutdown`，最後重新執行清理命令；建置伺服器在 build 結束後仍可能留在背景。

切換或刪除 worktree 前，先在該 worktree 執行 dry run，保存需要的證據，再清理或移除該 worktree。不要從某棵 worktree 指向另一棵的 `build/` 清理。主 checkout 約 37 GB 的 `build/` 由 owner 自行決定；此任務不清理它。此腳本進入主 checkout 後，owner 可在**主 checkout** 執行 `./scripts/dev/clean-build-output.ps1` 檢視計畫，再執行 `./scripts/dev/clean-build-output.ps1 -Apply` 清理預設項目。
