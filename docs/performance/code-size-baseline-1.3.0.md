# FreeformHelper 1.3.0 Code-size Baseline

- 簽核日期：2026-08-08
- 量測入口：`scripts/perf/measure-code-size.ps1`
- 契約檢查：`scripts/tests/check-code-size-baseline.ps1`
- 1.3.x signed-start commit：`3032121156dec2329e38e971327d55c7887db99f`
- 首個獨立 cutdown before commit：`207e29d5115c3d3318ee3249ed3af197f2d18180`

## 1. 量測契約

Production source 是 primary metric，只計 git tracked 且符合下列互斥群組的檔案：

- `src/FreeformHelper.Domain/**/*.cs`
- `src/FreeformHelper.Application/**/*.cs`
- `src/FreeformHelper.Infrastructure/**/*.cs`
- `src/FreeformHelper.UI/**/*.cs`
- `src/FreeformHelper.UI/**/*.axaml`

`UI ViewModels` 與 `UI Services` 是 UI C# 的 subsets；計算 logic-first 時加入，但不會再加進 production total 一次。tests、docs、scripts、Assets、Goldens、Generated、bin、obj、build 與 generated suffix (`*.g.cs`、`*.g.i.cs`、`*.generated.cs` 等) 均排除。工具會先跑 line/path classifier self-test，再量測真實 tracked tree。

行數定義：

- physical：CRLF、LF、lone CR 都是分隔符；EOF newline 不額外產生 sentinel line。
- empty eligible file：檔案數仍為 1，physical/nonblank 都是 0。
- nonblank：對每個 physical line 套用 `String.IsNullOrWhiteSpace`，結果為 false 才計數。

Source metric 可讀取 tracked working tree，並同時記錄 base commit/source-tree OID 與 `productionSourceDirty`；簽核數字一律來自 immutable commit archive。Release metric 只接受 clean tracked build inputs，且只是 secondary metric。

## 2. 三個不可混用的 anchor

| Anchor | Total files | Physical | Nonblank | Logic files | Logic physical | Logic nonblank |
|---|---:|---:|---:|---:|---:|---:|
| GitHub #4 建票前 signed-worktree observation | 586 | 98,946 | 88,311 | 381 | 64,705 | 57,366 |
| 1.3.x signed-start `3032121` | 586 | 98,950 | 88,315 | 381 | 64,709 | 57,370 |
| R13.006b independent before `207e29d` | 589 | 98,617 | 88,021 | 384 | 64,377 | 57,077 |

原始 observation 沒有 immutable tree OID，因此不能作往後自動 gate。`3032121` 是完成 R13.002 production 3635 gate 後的第一個 immutable anchor；相對 observation，total 與 logic-first 都是 `+0 files / +4 physical / +4 nonblank`。Roadmap 的 `165f076` 仍是規劃文件基準，不是 code-size signed start。

`207e29d` 的 `src` tree 是 `c5048b4ecfd0c8a89a8a4719a48afbf11b980f68`。它位於 R13.004 完成後，所以 R13.006b 必須以此作獨立 before；若改用 `3032121`，會把 #6 已完成的刪減重複算入 #7。

## 3. Source breakdown

| Group | `3032121` files / physical / nonblank | `207e29d` files / physical / nonblank | Delta |
|---|---:|---:|---:|
| Domain | 18 / 1,087 / 994 | 18 / 1,087 / 994 | 0 / 0 / 0 |
| Application | 92 / 17,606 / 15,632 | 95 / 17,475 / 15,517 | +3 / -131 / -115 |
| Infrastructure | 14 / 2,130 / 1,873 | 14 / 2,130 / 1,873 | 0 / 0 / 0 |
| UI C# | 393 / 61,930 / 54,514 | 393 / 61,730 / 54,337 | 0 / -200 / -177 |
| UI AXAML | 69 / 16,197 / 15,302 | 69 / 16,195 / 15,300 | 0 / -2 / -2 |
| **Total** | **586 / 98,950 / 88,315** | **589 / 98,617 / 88,021** | **+3 / -333 / -294** |
| UI ViewModels subset | 163 / 31,076 / 27,493 | 163 / 30,889 / 27,324 | 0 / -187 / -169 |
| UI Services subset | 94 / 12,810 / 11,378 | 94 / 12,796 / 11,369 | 0 / -14 / -9 |
| **Logic-first** | **381 / 64,709 / 57,370** | **384 / 64,377 / 57,077** | **+3 / -332 / -293** |

Total 的 `3032121 -> 207e29d` 可完整對帳：R13.003 為 `+320 physical / +288 nonblank`，R13.004 為 `-653 / -582`，合計即 `-333 / -294`。R13.006a 本身不得修改 `src`，完成 commit 的 source-tree OID 必須仍等於 `207e29d`。

### R13.006b 首個獨立 cutdown

R13.006b 只刪除 `BuildHeatmap` 對已 materialize 且按 row／col 排序之 `cells` list 的第二次相同排序，不重算 R13.004。公開 characterization 使用亂序 pad input，鎖定 `Result.Cells` 與 `Heatmap.Cells` 的 row／col 順序；完成 source-tree OID 為 `b35b9a50cb646be14db5c15cdd5533ab64d73867`。

| Scope | Before `207e29d` files / physical / nonblank | R13.006b files / physical / nonblank | Delta |
|---|---:|---:|---:|
| Application | 95 / 17,475 / 15,517 | 95 / 17,473 / 15,515 | 0 / -2 / -2 |
| **Total** | **589 / 98,617 / 88,021** | **589 / 98,615 / 88,019** | **0 / -2 / -2** |
| **Logic-first** | **384 / 64,377 / 57,077** | **384 / 64,375 / 57,075** | **0 / -2 / -2** |

## 4. Secondary Release artifacts

量測 profile：同一 clean checkout、同一 SDK/OS/RID/TFM，對兩個全新且彼此隔離的 `ArtifactsPath` 執行 Release build。每個 root 都獨立承載 bin/obj；可共用 immutable global NuGet cache。四個 primary DLL 的 bytes 與 SHA-256 必須兩次完全一致。

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `FreeformHelper.Domain.dll` | 39,936 | `0EAD9AC98CB828F244555F7FB910C5F9EC6E469FC6F246C88B31CF4B564766D5` |
| `FreeformHelper.Application.dll` | 691,200 | `49887D9A72BF581283F35B681CE0D8F344EDCFFF62FDD2C53812CECD8731A4A4` |
| `FreeformHelper.Infrastructure.dll` | 60,928 | `CAFA18138E4847422C698657F3EED87565D3CFAB5633260D0537F8BCA3A86F29` |
| `FreeformHelper.UI.dll` | 14,523,392 | `3A0CBCAC6E054A9AB762A8EC9C7C3EBF5872C2B459DC470E1C9D60838FC072B6` |
| **Total** | **15,315,456** | — |

R13.006b clean candidate 的兩次 isolated build 同樣 exact repeatable。四個 artifact 的 bytes 皆未改變，總量 delta 為 0；這是 PE alignment 下的誠實結果，不取代上節 production source 的 `-2 / -2` primary evidence。只有包含刪除實作的 Application DLL hash 更新：

| Artifact | R13.006a bytes / SHA-256 | R13.006b bytes / SHA-256 | Byte delta |
|---|---|---|---:|
| `FreeformHelper.Domain.dll` | 39,936 / `0EAD9AC9…6766D5` | 39,936 / `0EAD9AC9…6766D5` | 0 |
| `FreeformHelper.Application.dll` | 691,200 / `49887D9A…31A4A4` | 691,200 / `B7B07B17…62A9B` | 0 |
| `FreeformHelper.Infrastructure.dll` | 60,928 / `CAFA1813…A86F29` | 60,928 / `CAFA1813…A86F29` | 0 |
| `FreeformHelper.UI.dll` | 14,523,392 / `3A0CBCAC…FC072B6` | 14,523,392 / `3A0CBCAC…FC072B6` | 0 |
| **Total** | **15,315,456** | **15,315,456** | **0** |

這是明確的 no-PDB measurement profile：`Release`、`net8.0`、`UseAppHost=false`、`ContinuousIntegrationBuild=true`、`Deterministic=true`、`IncludeSourceRevisionInInformationalVersion=false`、`DebugType=None`、`DebugSymbols=false`，並把各自 artifact root 與 repo root PathMap 到固定虛擬路徑。Source revision metadata 被排除，避免只改 docs/commit SHA 就改變 structural size hash；不可把這個 total 與預設 portable-PDB Release 大小混比。

簽核環境：.NET SDK `10.0.302`、MSBuild `18.6.11.33009`、Microsoft Windows `10.0.26200`、OS/process `X64`、RID `win-x64`。Repo 沒有 `global.json` 或 `packages.lock.json`，因此此 hash 只宣稱在已記錄的 checkout 與相同 toolchain/environment 下可重現，不是跨 SDK/OS 的 binary reproducibility 保證。

## 5. 重跑方式

只檢查 source contract（可用於未提交 slice）：

```powershell
./scripts/tests/check-code-size-baseline.ps1
./scripts/perf/measure-code-size.ps1 -SkipReleaseBuild
```

產生 authoritative source + Release manifest（tracked build inputs 必須 clean；入口會先執行 workspace preparation）：

```powershell
./scripts/perf/measure-code-size.ps1 `
  -OutJsonPath build/code-size/code-size-baseline.json
```

JSON 與 assembly outputs 位於 ignored `build/code-size/`，不提交動態 machine output。Review evidence 應引用 command、commit/source-tree OID、summary values 與 manifest path。
