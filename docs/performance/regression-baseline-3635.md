# 3635 固定回歸基線（重構不改行為）

最後更新：2026-08-08

## 目的
- 固定使用 `example/BOE36.35/project_3635.json` 做重構回歸。
- 一次腳本完成以下項目：
  - 載入專案與 Step 1~4
  - Step 5 匯出（CSV review + C v2.1 + C v2.2）
  - C v2.1/v2.2 與 checked-in golden 的 normalized ordinal exact gate
  - Runtime query 基線（`status` / `selection` / `notch` / `notch-validation`）
  - Selection latency 採樣（優先用 runtime `selection.timings`，必要時才 fallback `Selection updated` log）
  - 固定 budget gate（selection / inspector / export）

歷史比較紀錄（非 current gate）：
- `docs/performance/3635-selection-export-baseline-2026-03-05.md`；該次量測未隔離 UI，已由本文件的 signed/isolated gate 取代。

## 已簽核 production provenance

R13.002 以 `example/BOE36.35/project_3635.json` 作為 Lucid 3635 authoritative input；project SHA-256 為 `A592B926AF61F64870817D5AE8A51E95143360D5504A4C9379160CDA01C88EA3`，embedded DXF 名稱為 `cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf`。

Executable lock 為 `example/BOE36.35/notch_export_golden_manifest.json`；script 與 Application drift test 都先驗證 manifest 中的 project/mask/golden SHA-256、bytes 與 node count，不能同時修改 generator 與 golden 來取得假綠燈。

輸入 contract：

- selected layers：`NC.drawing`、`PGT.drawing`、`PINC.drawing`、`PLN1.drawing`、`Ref1.drawing`～`Ref5.drawing`；raw CAD 9,843 pads 經 production layer/filter path 後為 4,838 pads。
- hidden CAD IDs：`4764`、`4765`、`4751`、`4750`。
- import：`OnlyClosedPolylines=true`、`IncludeBlockPolylines=true`。
- regular mask：`SeeRegular.csv` enabled，SHA-256 `E858600E9467566EA4989EEF408F3D8AC63F7E6CD57F1FBF23D1DAAFB9B970B2`；4,992 regular pads 中 4,898 active。
- Notch：`CadAllocation`、`ConservativeNoGain`、V21 threshold Q7 `0`、V22 threshold percent `0`、linked thresholds、V21+V22 enabled、ToFull rule engine enabled、Release profile；trace disabled。

已簽核輸出：

| Version | Nodes | Bytes | SHA-256 |
|---|---:|---:|---|
| V2.1 | 692 | 130,979 | `8961B8155B0571B193C7C87D8EEA75077B4EF8822828506C4661B50BA2E57488` |
| V2.2 | 548 | 84,023 | `5208068BBD8D82FC0A628693EF6035B31288EC674A25724C0C962CD58757BB47` |

舊 checked-in C 是 raw `_cad` test-only seam 的產物：V2.1 為 640 nodes / 123,178 bytes / `D76296EF8428477A7F61A30FCFE010166B87A8185109974464F8F18E2ADEE08B`；V2.2 為 516 nodes / 79,587 bytes / `A83BC6C6814EBBC65C58D283517EA8734F7D26A05736D38086E37011A01C2BAB`。

### Calibration / approval record

| 欄位 | 簽核值 |
| --- | --- |
| Golden lock commit | `3032121156dec2329e38e971327d55c7887db99f`（tree `b09ec2935a9406433bd56f0f2263f4492e91a462`，2026-08-08） |
| Signed manifest | `example/BOE36.35/notch_export_golden_manifest.json` |
| Gate script | `scripts/perf/run-3635-regression-baseline.ps1`，SHA-256 `97534BCA03B568DB74B8FC9126B9E4B1F95A812BC8EE1D4280F02EB6269FC7D5` |
| Human approval | `Dennis40816`／project owner，於本重構對話在 2026-08-08 明確確認 Lucid 3635 authoritative input，以及 V2.1/V2.2 各自 before/after zero-diff hard contract |
| Approval scope | 只核准由 raw `_cad` test seam 修正為 production layer/filter seam 的一次性 provenance correction；不授權後續 refactor、Q7 correctness 或文件更新改寫 golden |

簽核／重驗環境（2026-08-08）：Windows `10.0.26200` x64、RID `win-x64`、PowerShell `7.6.3`、culture `zh-TW`、timezone `Taipei Standard Time`、.NET SDK `10.0.302`、MSBuild `18.6.11.33009`、target `net8.0`、GCC `15.1.0`。Repository 沒有 `global.json`，因此此紀錄只主張相同 source、command 與已記錄環境可重現，不主張跨 SDK/OS byte reproducibility。

正式重驗命令必須同時跑兩種 export order：

```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -LaunchIsolatedUi -EnforceBudget `
  -OutDir build/perf/3635-regression-r13005-forward

./scripts/perf/run-3635-regression-baseline.ps1 `
  -LaunchIsolatedUi -SkipBuild -ReverseCExportOrder -EnforceBudget `
  -OutDir build/perf/3635-regression-r13005-reverse
```

兩份 2026-08-08 artifact 的 golden checks、raw signed hashes、export-state invariant 與 performance budget 均為 PASS；forward 為 V21→V22，reverse 為 V22→V21。Artifact schema 目前不嵌 source revision，因此它們只作 operational measurement；可獨立驗證的 signed provenance 仍由 lock commit、manifest、tracked input hashes 與重跑 command 共同構成。最新 production source tree `b35b9a50cb646be14db5c15cdd5533ab64d73867` 保持兩份 C exact。

後續 1.3.x 只允許各版本 before/after exact，不得再以 refactor 或 Q7 correctness 為由更新這兩份 golden。

## 最新 profile 與 hotspot 分類

2026-08-08 R13.005 forward measurement：selection total p50/p95 `4/6 ms`、inspector p95 `1 ms`、notch preview p95 `3 ms`；CSV `404 ms / 107,806 bytes`、C V2.1 `90 ms / 130,979 bytes`、C V2.2 `83 ms / 84,023 bytes`。Reverse measurement：selection total p50/p95 `4/11 ms`、inspector p95 `1 ms`、notch preview p95 `3 ms`；CSV `519 ms`、C V2.1 `123 ms`、C V2.2 `85 ms`。這些數字是 cache-aware Runtime Query end-to-end elapsed，不是 method-level CPU attribution。

| Candidate | Current size | 3635 runtime evidence | R13 decision |
| --- | ---: | --- | --- |
| `NotchFirmwareCExporter.cs` | 993 physical / 906 nonblank | 位於被量測的 C export path，但 gate 只量到整段 83～123 ms，未隔離 formatter CPU | 保留為 structural/measured-path hotspot；R13.103 在建立 final projector boundary 後再拆 projection/format，不宣稱目前已證實 CPU dominant |
| `CoordinatePlannerComputationService.cs` | 925 physical / 839 nonblank | 3635 regression script完全不呼叫 Coordinate Planner | 從 3635 runtime-hotspot 清單移除；保留為 R13.206 structural candidate，須另做 Coordinate snapshot/profile 才能提出 performance claim |

因此 R13.005 不以 LOC 猜 runtime bottleneck，也不把 `FinalizeCanonicalExportsMs` 誤當 C formatter timing。`docs/performance/3635-selection-export-baseline-2026-03-05.md` 保留為歷史紀錄；最新 signed measurement 以本節與上述 artifact 為準。

## 前置條件
- Exact gate 請先關閉既有 FreeformHelper UI；腳本會啟動自己的 isolated hidden instance。
- 第一次或剛跑過 `-UseNoAppHost` gate 時勿加 `-SkipBuild`；exact gate build 會明確產生 apphost executable。

## 執行
```powershell
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi
```

常用參數：
```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CadId 4767 `
  -RegularId 4978 `
  -SelectionCadIds "4767,4808,7107" `
  -SelectionCycles 8 `
  -OutDir build/perf/3635-regression-latest `
  -SkipNotchValidation `
  -LaunchIsolatedUi `
  -SkipBuild
```

預設 golden：

- `example/BOE36.35/notch_export_v21_current.c`
- `example/BOE36.35/notch_export_v22_current.c`
- `example/BOE36.35/notch_export_golden_manifest.json`

Exact gate 必須加 `-LaunchIsolatedUi`，並先關閉既有 UI instance。腳本會拒絕任何既有 UI/dotnet UI process，以隔離的 app-general-settings path 啟動 hidden UI，並要求 `status.processId` 等於 managed PID，避免固定 IPC pipe 連到其他 instance；結束時只停止自己啟動且執行檔路徑已驗證的 process。只有非 gate 的自訂效能實驗可加 `-SkipGoldenCheck`。若要改用其他唯讀 baseline，使用 `-GoldenV21Path` / `-GoldenV22Path`；所有 output/golden path 在 export 前必須互斥，腳本不提供更新 golden 的選項。

版本 state/invalidation 相關 slice 除了預設 V21→V22，還要再跑一次 `-ReverseCExportOrder` 的 V22→V21；兩種順序都必須 exact，以證明前一份 export 不會污染後一份。

`-SkipBuild` 只可在 `build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe` 已存在時使用；若先前 build 使用 `UseAppHost=false`，請移除 `-SkipBuild` 讓腳本重建 apphost。

## 主要輸出
- `build/perf/3635-regression-latest/regression-baseline-summary.md`
- `build/perf/3635-regression-latest/regression-baseline-summary.json`
- `build/perf/3635-regression-latest/notch_table.csv`
- `build/perf/3635-regression-latest/notch_v2.1.c`
- `build/perf/3635-regression-latest/notch_v2.2.c`
- `build/perf/3635-regression-latest/runtime-export-c-v21.json`
- `build/perf/3635-regression-latest/runtime-export-c-v22.json`
- `build/perf/3635-regression-latest/runtime-status.json`
- `build/perf/3635-regression-latest/runtime-selection.json`
- `build/perf/3635-regression-latest/runtime-notch.json`
- `build/perf/3635-regression-latest/runtime-notch-validation.json`
- `build/perf/3635-regression-latest/selection-latency.json`
- `docs/performance/regression-baseline-3635.budget.json`

## 驗收重點
1. Step 5 三個匯出檔存在且 size > 0。
2. `regression-baseline-summary.json` 的兩個 `goldenChecks` 都是 `pass=true`；比較只把 CRLF、LF、lone CR 統一為 LF，並容許最多一個 optional EOF newline，其他 whitespace、comment、ordering 都使用 ordinal exact compare。
3. `runtime-status.json` 中 `workflow.hasStep1Result~hasStep4Result` 皆為 `true`。
4. `runtime-notch.json` 與 `runtime-notch-validation.json` 回傳 `ok=true`（若 `RegularId=-1` 且 notch 無候選，會標記 `SKIPPED`）。
5. `selection-latency.json` 中 `sampleCount > 0` 且有 `totalMs/inspectorMs/notchPreviewMs` 統計。
6. `regression-baseline-summary.(md|json)` 中 `gate.pass=true`；若要同時強制 performance budget，執行腳本時加 `-EnforceBudget`。

## 注意事項
- `selection.timings` 會優先提供 runtime selection/inspector/notchPreview 耗時；只有舊版 payload 或異常情況才會 fallback 解析 log。
- 若這輪只想驗 selection / inspector / export budget，可加 `-SkipNotchValidation` 避開 Step5 validation query。
- Exact gate 若偵測到既有 UI instance 會直接失敗並要求先關閉，避免連到未隔離的個人設定；`-SkipGoldenCheck` 實驗模式若重用 UI，IPC 回傳 `INSTANCE_NOT_RUNNING` 時請確認 instance 已啟動且未卡在 loading。
- 腳本會覆寫 `OutDir` 下同名基線檔案。
- golden mismatch 會保留 actual C，並在錯誤訊息列出 actual/golden path 與第一個 ordinal diff 的 line/column、expected/actual code point；不會寫入或覆蓋 checked-in golden。
- `export-notch` 仍經 `RuntimeQueryUseCase` 委派既有 ViewModel Step 5 export command；這個 gate 只驗證真實 UI/IPC 結果，沒有新增第二條 C 生成路徑。
