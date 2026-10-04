# 重構測試分層執行策略

最後更新：2026-07-20

## 目標
- 每次重構都跑固定順序與最小回歸集，降低漏測風險。
- 避免平行測試造成 DLL lock（先前在 Windows 上曾出現 `CS2012` / testhost lock）。

## 固定順序（最小集）
1. `notch-golden`
2. `application`
3. `ui-core`
4. `smoke`

對應命令：
```powershell
./scripts/tests/run-tests.ps1 -Group notch-golden
./scripts/tests/run-tests.ps1 -Group application
./scripts/tests/run-tests.ps1 -Group ui-core
./scripts/tests/run-tests.ps1 -Group smoke
```

## 建議 gate（重構里程碑）
```powershell
./scripts/tests/run-refactor-gate.ps1
```

預設流程（2026-10-02 起固定跑全部測試分組，見 `TODO.md` S15.003）：
1. 檢查 `example/` 資料（已抓取、在釘住的 commit、沒有未提交變更）
2. `lint`
3. `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
4. `notch-core`（已包含全部 golden 類別）
5. `application`
6. `infrastructure`
7. `ui-core`
8. `ui-snapshots`
9. `uncategorized`（所有未被明列的測試類別）

`-IncludeNotchCore`、`-IncludeInfrastructure`、`-IncludeUiSnapshots` 仍可傳入，但這些分組已是預設行為。沒有 `example/` 資料權限時加 `-AllowMissingExampleData`。

輸出：
- `build/test-gate/refactor-gate-summary.json`

## 何時加跑額外群組
固定 gate 已包含下列全部分組；這一節是只用 `run-tests.ps1` 跑單一分組時的選擇指引。

- `infrastructure`：當變更包含 logging / console parser / runtime IPC 文本協定。
- `notch-core`：當變更可能影響 Notch generator/export/simulation/projection/settings/Runtime Query/VM 資料路徑。
- `ui-snapshots`：當 UI 視覺結構、樣式 token、布局契約有改動。
- 全量 `dotnet test`：僅在跨層改動很大或 release 前做。

## 執行原則

Run `pwsh -NoProfile -File ./scripts/tests/path-guard.ps1` to check tracked test code, JSON/CSV, project/settings files and text goldens, including an initialized private `example/` submodule (missing example coverage is reported); `-SelfTest` runs only synthetic samples. JSON strings are decoded before checking local path forms, the expanded `USERPROFILE`, and whole username path segments. Findings report only file, line or JSON path, and rule, never values; exit codes are 1 for findings or invalid configuration and 0 otherwise. The default `.github/path-guard.json` has an empty `exceptions` array; each exception requires an exact repository-relative `file`, `rule`, `reason`, `owner`, ISO `expiry` date (inclusive, UTC), and a `field` JSON path (for example, `$['mask']`) or `fingerprint` (SHA-256 of the UTF-8 decoded JSON string or text line without its newline); when both are supplied, both must match. This is a report-only prototype with no CI or `verify.ps1` integration.

- 優先序列執行（不要同時開多個 `dotnet test`）。
- 任何 group 失敗就停下來修，不要先跑完全部再回頭。
- commit 前至少保證最小集（application/ui-core/smoke）全綠。
