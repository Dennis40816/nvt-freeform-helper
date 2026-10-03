# Notch System Reference

> 最後更新：2026-08-10
> 狀態：目前 Notch pipeline / Simulation / Export 的唯一 canonical reference

本文件的目的不是取代所有歷史文件，而是把「現在系統到底怎麼運作」集中到一個位置。後續若流程、命名、C export contract 有變動，應先更新本檔，再回頭調整 deep-dive 或 legacy example。

## 1. 範圍

本檔覆蓋以下主題：

- Step1 到 Step5 的 Notch 主流程
- `Regular`、`CAD`、`Best Match FW Diff`、`Output` 這幾組 diff identity 的目前語義
- Simulation 的資料來源與限制
- v2.1 / v2.2 export payload
- C export 的直接導入 contract（含多 IC）

不放進本檔的內容：

- 細部數學推導
- 某一版演算法的歷史草案
- 單一案例的驗證 trace

那些內容保留在 `docs/core` / `example`，但都應視為本檔的補充，而不是另一份真值。

## 2. 文件系統規則

Notch 相關文件從現在起依下列規則整理：

- `docs/reference`
  - 放 canonical reference、外部契約、CLI 契約
- `docs/core`
  - 放演算法 deep-dive、流程拆解、設計討論
- `example`
  - 放目前可直接編譯/導入的 `.c` example 與 fixture

若某份文件同時描述「目前行為」與「歷史背景」，必須在開頭明確標示哪一段是 canonical、哪一段是 history。

## 3. 核心資料模型

### 3.1 Regular Pad

- `RegularPad.DiffIndex`
  - 代表 `FW Diff Idx`
  - 這是 regular grid 在 FW / memory 上的真實順序
  - 在同一顆 IC 內應唯一，且一旦 grid 建立後就視為固定真值

### 3.2 CAD Pad

CAD pad 本身不是 FW memory node，但在 workflow 中會衍生出多種 diff identity：

- `Best-Match FW Diff Idx`
  - 來源是幾何比對後選中的 regular seed
  - 用途是幾何分析、trace、anchor seed
  - 不是最終輸出位址

- `CAD Output FW Diff Idx`
  - 來源是 Step4 分配結果
  - 代表該 CAD pad 最後要輸出到哪一個 FW diff / memory address
  - override 應作用在這個值，而不是作用在 best match

- `Visible CAD Diff`
  - 這是 UI projection / display 用的 diff 標示
  - 它可以受到 `See Regular` 或可視投影規則影響
  - 不能直接拿來當作 Step5 source diff 真值

## 4. 目前主流程

### 4.1 Step0 到 Step4

1. 建立 `RegularGrid`
   - 固定 `(IcIndex, FW Diff Idx)` 基準
2. 做 CAD 與 regular 的幾何 match
   - 產生 overlap / best-match seed
3. 決定 `CAD Output FW Diff Idx`
   - override 優先
   - 否則走 best-match / unique assignment 規則
4. 凍結 workflow snapshot
   - Step5 / inspector / runtime query 都應從同一份 snapshot 取資料

### 4.1.1 Active Regular Set 與 SeeRegular Mask

- `Match-scope active regular set`
  - 來源：`_latestPadMatchResult.RegularToCad.Keys`
  - 語意：目前 Step1 / matching 結果中，已經分配到某顆 CAD 的 regular pads
  - 目前用途：
    - v2.2 `ToFull` boundary seed
    - notch compensation 的 active/inactive neighbor 判定

- `Regular Visibility Mask (SeeRegular.csv)`
  - 來源：`SeeRegular.csv` 載入成功，且 `UseRegularVisibilityMask` / `IsRegularVisibilityMaskEnabled` 開啟
  - 語意：額外的 active regular 限制集合
  - 目前用途：
    - Step4 geometry seed / visible assignment
    - Simulation active surface

重點：

- 目前 `boundary` 判定依據的是 `match-scope active regular set`
- 不是 `Regular Visibility Mask (SeeRegular.csv)`
- 兩者可以相同，但系統不應假設它們必然相同

### 4.2 Step5 Notch Table

- `CadAllocation` 是目前主路徑
- `LegacyRegularAnchor` 是相容路徑

在 `CadAllocation` 路徑下：

- row source diff 優先取 `CAD Output FW Diff Idx`
- 若當下缺少 output mapping，才 fallback 到 raw anchor regular diff
- v2.2 target legs 只從 regular `(IC, FW Diff Idx)` 幾何聚合而來
- target 不應再被 visible / CAD projection 改寫
- `ToFull` boundary seed 目前定義為：
  - regular 位於 grid 外圈，或
  - 上下左右任一鄰居不存在 / 不在 `match-scope active regular set`
- boundary regular index set 會在同一輪 generation 預算一次，再在各 candidate 內過濾重用；這是效能優化，不改結果

### 4.3 Simulation

Simulation 的正確定位是：

- 輸入：regular grid + notch table
- 行為：依 notch table 把 regular sensing 量重新分配
- 不應再把 `CadPad` 當作 Simulation 的主輸入真值

換句話說，Simulation 可以參考 CAD 建立出來的 notch table，但執行模擬時應以 regular / FW diff 為核心，而不是再回頭依賴 CAD pad projection。

目前 Simulation 還有兩個固定契約：

- `Active surface`
  - 預設使用整張 regular grid
  - 若 `Regular Visibility Mask (SeeRegular.csv)` 啟用，則使用 `grid ∩ mask`
- `Build path`
  - 開啟 Simulation 時先顯示 host 頁與 build overlay
  - notch table 仍共用 `GenerateCurrentNotchTableAsync(...)`
  - 若 fingerprint 未變，直接重用 cache / prewarm artifact，不重算第二套 simulation 專用演算法

### 4.4 1.3.x shared execution contract：target 與 current debt

硬性 target：

```text
project/filter
  -> immutable version-neutral generation context/evidence
  -> one canonical resolved result
  -> final V2.1 projector -> fixed V2.1 C formatter
  -> final V2.2 projector -> fixed V2.2 C formatter
```

- output version 不得進入 matching、compensation、allocation，或 final projection 前的 candidate／evidence construction；version-specific threshold admission 只能位於 final row projector。Version-neutral resolved audit 與 upstream cache key 亦不得攜帶 output version。
- 同一 workflow revision 的 V2.1-only、V2.2-only、both 必須讀同一 canonical fingerprint/result；第一次且唯一一次 version branch 只能在 final Firmware data projector/formatter。
- `V21_before == V21_after`、`V22_before == V22_after` 是 1.3.x 現在已生效的 hard gate；final-only architecture 是 R13.101～R13.103 的 exit target，不能把兩者混寫成已完成。

`R13.102a-2a`／`R13.102a-2b-2`／`R13.102b-2` 完成後 current state 尚有三個明確邊界：

1. Normal `CadAllocation` 已把phases 1～3 output-neutral resolution與phase 4 final projection拆開；Step5 export cache保存Application-owned opaque batch，即使projection為零列亦可重用。相同CAD/grid/Step3/computation identity與generation epoch的並行Export／Simulation只建立一個resolution task，joiner不重播phases 1～3 progress，每位caller以自己入口凍結的enabled versions、V21/V22 threshold、`NullValue`與target coverage guard/cap執行phase 4。resolved-batch settings fingerprint不含final-only欄位，但completed cache與in-flight join仍做完整computation-settings equality以防32-bit collision。faulted task會移除供下次retry；final-only invalidation保留task/batch並拒絕舊projection發布，完整invalidation推進epoch、detach舊task並拒絕其store/publish。每個generation request另凍結Step5 final-projection revision與Simulation source revision，只有三者仍current才可commit projected row count、last table、progress／flow及caller continuation。Warm projection只執行／回報phase 4。這是full-table consumer task owner的窄進展，不等於repository-wide canonical result。
2. 同一 single-CAD selection/revision 的 Step 3 preview、200 ms deferred CAD Inspector、Notch Detail與Runtime Query `query multi-owner`都投影revisioned sparse `NotchV22ResolvedResult`；VM不再保存平行compensation dictionary。R13.102a-2b-2把目前單一選取CAD的request接入既有full batch session：matching warm result以exact same instance供candidate消費，cold batch則至多攜帶一份由CAD ID + anchor IC/diff識別的resolved result，completion通過currentness檢查後才promotion回同一per-CAD owner。reuse identity鎖exact CAD、immutable grid／CAD-pool signatures、active mask、全部computation settings與anchor；同一mutable grid reference改變也不能誤用舊result。Full batch仍刻意只保存compact candidate evidence，不保留所有CAD的polygons/debug result；因此這是bounded sparse/full bridge，不宣稱所有reader改用一個巨型batch shape。
3. `LegacyRegularAnchor`仍按enabled version／threshold做固定V21／V22 compatibility dispatch，並維持request-specific `NotchTable` cache。Top-level dispatch會在首次同步progress callback前建立owned `LegacyNotchGenerationRequest`，一次固定ordered enabled versions、effective V22／V21 thresholds、`NullValue`與`LenScale`；threshold admission、eligibility與兩版row builders只讀此request及同一明確version switch。Callback仍同步修改caller settings，但只影響下一次generation。R13.103d-2已移除沒有consumer的strategy interface／dictionary／injection seam；V22 builder亦不再保留CadAllocation hybrid compensation branch或all-CAD input。

Owner：R13.101e已讓normal generator／UI compensation以完整、output-version-neutral `NotchV22CompensationContext`收斂到唯一compute path；舊multi-parameter API只作compatibility adapter，明確空allocation evidence不再回退重算。R13.101仍由R13.101c-2繼續完成repository-wide shared-result／final-output cache split；R13.102a-2b-2已讓並行full-table consumers共用revisioned resolution task並完成bounded selected sparse bridge。R13.102／R13.102a／R13.102a-2仍須依各自完整exit criteria稽核所有consumer與Legacy convergence；R13.103完成typed final projectors／formatters與legacy boundary。以上parent均保持open。R13.102b-2只完成單一Runtime Query reader；R13.103d-1凍結Legacy request並移除hybrid branch，R13.103d-2再刪除registry／injection seam，但沒有合併request-specific table cache或整個Legacy boundary。R13.104a-1至a-10依序收斂EMS predicate、resolved display decisions、audit／replay／export status、active Notch cap guidance與target-cap help。`LegacyRegularAnchor` convergence、其他hard-coded cap與其餘Simulation／replay／display文字仍未完成；`LegacyRegularAnchor`只作明確compatibility path，不進normal operator flow。

## 5. Diff Identity 對照

| 名稱 | 所屬層級 | 主要來源 | 主要用途 | 是否可直接當 Step5 source |
| --- | --- | --- | --- | --- |
| `FW Diff Idx` | Regular / FW | `RegularPad.DiffIndex` | 真實 memory / channel 順序 | 是 |
| `Best-Match FW Diff Idx` | CAD geometry | CAD 對 regular 的最佳 seed | 幾何分析 / trace | 否 |
| `CAD Output FW Diff Idx` | CAD workflow | Step4 指派 / override | CAD 最終輸出到哪個 FW diff | 是，CadAllocation 主路徑優先用它 |
| `Visible CAD Diff` | UI display | display projection / See Regular | 畫面標示與檢查 | 否 |

## 6. Export 格式

### 6.1 CSV / C

- CSV review：人讀的 trace / diff review artifact，不是 FW direct-import contract
  - 固定排序：`ic_index -> fw_diff_idx -> version_code -> regular_pad_id -> cad_pad_id`
  - 固定欄位：`row_number, version, version_code, ic_index, ic_number, fw_diff_idx, regular_pad_id, cad_pad_id, payload_width, payload_01..payload_09, comment`
  - `payload_01..payload_09` 對應目前支援的 `v2.1` 9-int row 或 `v2.2` 7-int node；不足欄位留空
- `C v2.1`：輸出僅保留 `stV21` row section，`stV22` 會是空 section
- `C v2.2`：輸出僅保留 `stV22` row section，`stV21` 會是空 section

### 6.2 v2.2 主 payload

`NotchV22Node` 7 欄：

1. `AnchorDiffIndex`
2. `CombinePercent`
3. `TargetDiffIndex1`
4. `TargetRatioPercent1`
5. `TargetDiffIndex2`
6. `TargetRatioPercent2`
7. `Flags`

重要規則：

- `ToRegularRatio = Σ(overlapArea / targetRegularArea)` 仍保留作為診斷值；它描述一顆 CAD 在 regular view 中等價覆蓋多少 regular 面積。
- beta0.9 起，`Current (Gain)` / `Conservative (No Gain)` 的實際 row 不再把整顆 CAD 的 source-wide `R` 先乘到所有 target share。
- per-target group coverage：先對每個 regular 計算 effective area / regular area，再按 `(IC, target diff)` 分組相加：
  - `Current (Gain)`：只有 `IsToFullApplied` regular 使用 `max(sourceArea, stage3EffectiveArea) / targetRegularArea`，其餘仍使用 source coverage。
  - `Conservative (No Gain)`：使用 `source overlap area / targetRegularArea`；ToFull 只作 support/cap/allowance，不直接增加 amount。
  - `Disabled`：`100`
- target group 先 `Math.Round(ratio * 100)`；絕對值超過 100 時拆為 `<=100` chunks。Final legs 只包含 anchor IC、非 source/anchor diff、`RatioPercentRounded != 0`，且通過 strict threshold 或具有 ToFull applied coverage 的 emitted groups；不是所有 overlap targets 都必然進 C。
- `CombinePercent` 是 retained anchor group 加上實際 emitted 的正向 target legs，採 per-group rounding 後相加；不得用 CAD-level `CombinedRatio` 取代。
- `NotchV22TargetCoverageProjection` 是上述emitted target list、pre-guard raw combined percent與`>255%` risk的Application owner；generator與revisioned resolved result共讀它。Normal anchored display的effective count、compact target line與card role只消費`EmittedTargets`的`(IcIndex, DiffIndex)` membership；Inspector、Pad Info、Notch Detail與Runtime Query不得再sum targets或自行重建`strict || ToFull`門檻。
- 若缺少anchor/source，或不是target-regular coverage mode，projection的`RawCombinedPercent`為null；為保留public compatibility，display ratio仍由同一Application policy沿用既有all-target positive sum（無targets時回退CAD-level combined diagnostic），target summary／line／role則維持既有strict-only顯示。全部target diagnostics、ordering與strict／ToFull evidence仍保留。
- beta0.9 起 `Current (Gain)` 另有兩個可開關 guard：
  - `Boundary virtual-area cap`：ToFull 虛擬外延面積進入 Stage3 gain/allocation 前，先限制為 `inside overlap × cap ratio`。
  - `Target coverage guard`：canonical rows 選定後，統計每個 target FW diff 的 retained/incoming coverage；超過 cap（預設 `120%`）時按比例縮小該 target 的 retained/leg 百分比，避免 uniform 400 超過 EMS 480。
- 超過兩個 target legs 時拆 continuation rows
- 第一列保留真實 `CombinePercent`
- 續列固定 `CombinePercent = 100`
- `NotchV22FirmwareProjector` 是 typed／legacy V22 table row 到 final Firmware node 的唯一正規化入口：typed `V22Node` 優先，`Values.Length >= 7` 沿用 raw compatibility decode，`< 7` 保留既有 legacy combine fallback 與 exact overflow exception。
- projection 同時提供保留原始 row／action ordinal 的 `SourceRows`，以及保留既有 C per-IC ordering 的 `NodesByIc`；兩個 view 共用同一 projected row。Exporter 只格式化 projected nodes，simulation 執行同一批 nodes 後再聚合 Actions／diagnostics。
- Release no-op filtering 只屬 C emission policy；它不改 projector 或 simulation 所見的 row 集合。Release 仍須先排除 no-op，再依既有不穩定排序契約排列其餘 nodes，才能保持 frozen C bytes。

### 6.3 v2.1 legacy payload

- 保留 legacy `9-int row`
- 主要目的為相容舊格式或比對歷史資料
- 不應再把它視為新的主契約來源
- leg ratio 是 `UINT8` Q7 magnitude `0..255`，`128=100%`、`255≈199%`；sign 只由 `ADD/SUB` type 承載。
- encode 使用 AwayFromZero；final Firmware projector 對 magnitude 做 UINT8 saturation。
- apply 直接執行 `(INT16 source * magnitudeQ7) >> 7`，不可先 decode 成 percent/double。
- `ThresholdQ7` 是另一份 `0..128` admission gate，不得與 payload range 合併。
- `NotchV21FirmwareProjector` 是 CadAllocation／LegacyRegularAnchor 到 final v2.1 ABI node 的唯一投影；exporter 只格式化，simulation 以同一 node 執行。
- CadAllocation terms 依 `(IC, destination diff)` 分組，再由 final projector 依 source diff 建立 deterministic order；每個 ABI node 最多兩個 terms，第三個 term 起建立下一個 destination node，不得截斷或塞入非 ABI 欄位。
- evaluator 與生成的 C 都以 `INT16` firmware carrier 執行 modular narrowing。Characterization fixture 將三個 `32767 × Q7 128` terms 分成兩個 nodes後，destination 的 observable final value固定為 `32765`；不得改成 saturation或不受限的浮點／整數加總。
- missing destination、missing source與missing anchor是三個不同的診斷契約；必須保留 IC／diff identity及 evaluator→source-row action的穩定順序。

## 7. C Export Contract

目前 `NotchTableExporter.ExportAsCInitializer(...)` 產出的 C 檔遵守 codebase `v2.0.0` 的 `func_notch.c` 風格。

### 7.1 C 檔保證

- 1.3.x frozen Lucid 3635 Release baseline：V2.1 為 692 nodes / 130,979 bytes / SHA-256 `8961B8155B0571B193C7C87D8EEA75077B4EF8822828506C4661B50BA2E57488`；V2.2 為 548 nodes / 84,023 bytes / SHA-256 `5208068BBD8D82FC0A628693EF6035B31288EC674A25724C0C962CD58757BB47`。完整輸入 provenance 見 `docs/performance/regression-baseline-3635.md`。
- 1.3.x 只允許 `V21_before == V21_after`、`V22_before == V22_after`；Q7 correctness 或 simulation parity 都不是更新 C golden 的例外。
- 重驗必跑 `run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget` 的 V21→V22 與 `-ReverseCExportOrder` 的 V22→V21；Windows/.NET/MSBuild/GCC、script hash、完整 layers/filter/settings 與 artifact 路徑統一由 `docs/performance/regression-baseline-3635.md` 維護，避免本檔再建立第二份 calibration owner。
- 輸出檔以 `#include "notch.h"` 開頭，並包在 `#if (USER_SWITCH_NOTCH_COMPENSATION == FUNC_ENABLE)` 內。
- 不直接 `#include <stdint.h>`；輸出檔假設 FW platform 已提供 `UINT8`、`UINT16`、`INT8`、`INT16`。
- 對外保留 `FUNC_NHC_DiffCompensation(void)` 入口。
- 多 IC 只增加最小 helper：`FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)`。
- `FUNC_NHC_DiffCompensation(void)` 預設呼叫 IC1，維持舊單 IC 呼叫方式。
- `castNHC_TABLE` 保留為 legacy alias，指向 `castNHC_TABLE_IC1`。
- Release profile 不輸出 FW simulation base mask；Debug profile 才額外輸出 `FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)`，讓 FW bring-up 時可重建與 FreeformHelper Simulation 相同的 active regular baseline。
- 不輸出 `ST_PRI_NHC_TABLE_EXPORT` / `PriNhc_GetExport(...)` 這類新 root ABI。
- 在提供 `notch.h` / platform typedef / `S_2D_DIFFAFTER` 的 FW 環境中，可被 `gcc -std=c11 -Wall -Wextra -Werror` 編譯。

### 7.2 多 IC 表示法

- 每顆 IC 輸出獨立 table，例如：
  - `castNHC_TABLE_IC1`
  - `castNHC_TABLE_IC2`
  - `castNHC_TABLE_IC3`
- `castNHC_TABLE_BY_IC[USER_NHC_IC_NUM]` 儲存每顆 IC 的 table pointer 與 row count。
- 若某顆 IC 沒有 row，該 IC dispatch entry 會是 null pointer + `0` count，不輸出非法空陣列。

### 7.3 主要型別

- `ST_PRI_NHC_TABLE_NODE_INFO`
  - v2.1 / v2.2 各自 C 檔都使用這個名稱，符合 codebase v2.0.0 命名習慣。
  - v2.1 欄位保留 legacy `NHC_IDX / NHC_REGULAR / NHC_REGU_TO_FULL / NHC_1ST_* / NHC_2ND_*`。
  - v2.2 欄位改為 source-oriented payload：`NHC_IDX / NHC_COMBINE / NHC_1ST_IDX / NHC_1ST_RATIO / NHC_2ND_IDX / NHC_2ND_RATIO / NHC_FLAGS`。
- `ST_PRI_NHC_IC_TABLE_INFO`
  - codebase v2.0.0 style 的 per-IC table dispatch metadata。
- `ST_PRI_NHC_FW_BASE_MASK_INFO`
  - 僅 Debug profile 輸出的 FW simulation 專用 mask dispatch metadata。
  - `pu16ActiveDiffs` 指向該 IC active regular FW diff list。
  - `u16ActiveDiffNum` 是 active diff 數量。
  - `u16DiffSpan` 是該 IC 需要先清 0 的 diff span。

### 7.4 v2.2 node 註解語意

- `R=...%` 表示 To Regular 診斷倍率：`Σ(overlapArea / targetRegularArea)`。它不再作為 source-wide gain 乘到所有 target share。
- `F=...%` 表示 To Full 診斷倍率；在 `Conservative (No Gain)` 中 To Full 主要是 boundary support/cap/allowance，所以非邊界擴展情境常見 `F=100%`。在 `Current (Gain)` 中，To Full 後的 Stage3 effective area 只影響對應 target 的 coverage。
- `C=...%` 表示 node 的實際 coverage sum，也就是此 source row 的 self retained + target legs 總量。
- `NODE KEEP=...% MOVE=...%` 是 C export 額外推導出的實際 node flow；例如 `C=100%` 且 target leg 為 `44` 時，代表 source 保留 `56%`、移動 `44%`。

目前 model 對 `C` 的規則如下：

- `Current (Gain)`：`C = Σ(stage3EffectiveAreaOnTarget / targetRegularArea)`，不是 `round(R * F / 100)` 的 source-wide gain。
- `Conservative (No Gain)`：`C = Σ(overlapAreaOnTarget / targetRegularArea)`，ToFull 不放大 source combine。
- `Disabled`：`C = 100`。

target allocation 對不同 model 的規則如下：

- `Current (Gain)`：target legs 使用 per-target Stage3 coverage；若單腿超過 `100%`，會拆成多個 continuation/chunk legs。
- `Conservative (No Gain)`：target legs 使用 per-target source overlap coverage；ToFull 仍只作 support/cap/allowance。
- `Disabled`：保留 baseline source-area projection。

### 7.4.1 No Gain / Gain 完整流程與 CAD4818 例子

共同前處理：

1. 對每顆 CAD pad，計算它與所有 overlapped regular pad 的交集面積。
2. 對每個 target regular 記錄：
   - `regularArea`
   - `overlapArea`
   - `target FW Diff`
   - 是否被 `ToFull` boundary expansion 影響
   - `stage3EffectiveArea`，也就是 ToFull 後該 target 的有效面積
3. 計算 CAD-level diagnostic：

```text
ToRegularRatio / R = Σ(overlapAreaOnTarget / targetRegularArea)
```

重要限制：`R` 只描述這顆 CAD 在 regular view 中等價覆蓋多少 regular 面積，不再直接寫進 row payload，也不再當成 source-wide gain 乘到每個 target share。

`Conservative (No Gain)` 流程：

```text
targetCoverage = overlapAreaOnTarget / targetRegularArea
CombinePercent = self retained coverage + Σ(target leg coverage)
```

- `ToRegular` 仍開啟，用於還原 NF 對面積差異的壓平。
- `ToFull` 仍跑 rule engine，但只決定 support / cap / boundary allowance。
- 實際 target leg 只使用原始 overlap coverage。
- 因此 No Gain 是「沒有 ToFull amount gain」，不是完全沒有補償。

`Current (Gain)` 流程：

```text
targetCoverage = stage3EffectiveAreaOnTarget / targetRegularArea
CombinePercent = self retained coverage + Σ(target leg coverage)
```

- `ToFull` 後的 Stage3 effective area 會進入分配量。
- 但 gain 是 per-target 的：只有被 ToFull 影響的 target 會被放大。
- 不允許先把整顆 CAD 合成 `R * F`，再依 CAD source share 二次分配。

歷史解釋例：`CAD 4818`

下列 `98.6%/58.8%` 是早期幾何分析的近似中間值，current signed artifact 沒有保存其 derivation command，不能單獨作 acceptance gate。可執行的 current evidence 是 checked-in V2.2 C row 與其整檔 signed hash；若要重新簽核原始 coverage，需以 `-CadId 4818` 另產生並保存 hashable diagnostic artifact。

此案例位於 3635 右側邊緣，`CAD 4818` 的 source 是 `CAD Output FW Diff 1473`，幾何上覆蓋兩個 target regular：

| target regular | target FW diff | CAD 面積 share | target regular coverage |
| --- | ---: | ---: | ---: |
| `REG384` | `1472` | 約 `47.0%` | 約 `98.6%` |
| `REG385` | `1473` | 約 `53.0%` | 約 `58.8%` |

所以 diagnostic：

```text
R = 98.6% + 58.8% = 157.4%
```

新的 No Gain row 應接近：

```text
source diff 1473
target leg to diff1472 = 99%
retained on diff1473   = 59%
CombinePercent         = 99% + 59% = 158%
```

目前重新匯出的 v2.2 C example 已符合這個語意：

```c
{ 1473, 158, 1472, 99, NHC_DIFF_NONE, 0, NHC_FLAG_NONE }, // CAD=4818 ... NODE KEEP=59% MOVE=99%
```

舊算法的錯誤是把 `R=157.4%` 當成 source-wide gain，再用 CAD 面積 share 分配：

```text
old retained on diff1473 ≈ CAD share 53% * R 157.4% ≈ 83%
```

但 `REG385` 的真實 target coverage 只有 `58.8%`。若旁邊 CAD 再流入約 `41%`，舊算法會得到：

```text
old diff1473 total ≈ 83% + 41% = 124%
400 * 124% ≈ 496
```

這就是先前兩側邊緣出現 500 左右異常值的根因。新算法讓 `CAD4818` 對 `diff1473` 只保留約 `59%`，旁邊流入約 `41%` 時會回到接近 `100%`。

### 7.4.2 C runtime 與 FreeformHelper Simulation 一致性

`NotchFirmwareCExporter` 與 `NotchApplySimulationService` 共同消費 `NotchV22FirmwareProjector` 產生的 final nodes；simulation 不再自行 decode raw row，或先把 continuation legs 合併後重算 physics。

1. active baseline 先量化為 FW `INT16`：有限小數向零截斷、超界飽和，NaN／Infinity 為 0。
2. 套用任何 node 前，逐 source diff 備份其原始值；相同 source 的 main／continuation row 都讀同一份原始 source。
3. 對非 continuation row，計算：

```text
retainedPercent = max(0, CombinePercent - TargetRatioPercent1 - TargetRatioPercent2)
sourceDelta     = retainedPercent - 100
```

4. source 與 target scaling 都使用 `(INT16)((source * percent) / 100)`；整數除法向零截斷，累加維持 firmware modular narrowing。
5. continuation row 只補自己的 target legs，不再次調整 source retained。
6. physics 完成後才按 `SourceRows` 聚合 Actions；action grouping 不得反向影響 firmware 計算。

測試契約：

- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22ContinuationRows_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV22Int16Truncation_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccAndCSharpSimulationMatchV21Q7BoundaryContract_WhenGccIsAvailable`
- `NotchTableExporterTests.ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21LegacyRegularAnchor_WhenGccIsAvailable`

這些測試會把 exporter 產出的 C code 用 `gcc` 編譯並執行，再和 C# `NotchApplySimulationService` 做逐點 exact equality；不再接受原本的 `±1` tolerance。boundary fixture 明確覆蓋 `0/1/127/128/129/255 × ADD/SUB`，Legacy fixture 另鎖 ABI saturation。C export algorithm 因此不是只靠靜態格式檢查，而是有 runtime parity 測試保護。

R13.103c-1 有兩個 intentional correction fixture：

- MAIN／CONT continuation fixture 的 V22 source 由錯誤的 `30` 修正為 `40`，完整結果為 `[40,60,20,10]`；frozen V21 ABI 仍為 `[30,60,20,10]`，不要求跨版本相等。
- source `1` 的 `50%` target scaling 由浮點 `0.5` 修正為 FW `INT16` 結果 `0`。

兩案都只修正 C# simulation；generated V22 C node、ordering、raw bytes 與 signed golden 不變。合法 `< 7` legacy row 也由 exporter-only compatibility 擴充為 exporter／simulation 共用；export bytes 不變，simulation 不再拒絕該 row。

R13.103c-2 後，`NullValue` 的可執行 Firmware domain 固定為 `0..UINT16.MaxValue`。`NotchSettings.ValidateNullValueOrThrow` 是 Application owner；`ProjectSettings.ValidateOrThrow` 與 V21／V22 final projectors共用此規則，Step 5／Runtime Query notch validation也必須由同一settings entry驗證後投影。單次C export必須在讀取caller-owned collections前snapshot sentinel，projector、macro與formatter全程使用同一值；後續settings mutation只影響下一次export。越界 sentinel 會在 node formatting／evaluation或validation report建立前以 `InvalidOperationException("NullValue must be in [0,65535].")` fail-fast，不會被 silent clamp或遷移；direct UseCase保留該exception，Named Pipe server則在最外層轉為既有`IPC_ERROR` failure envelope並保留exact message，不能讓連線中斷退化為`EMPTY_RESPONSE`。Empty C table、unsupported simulation、public `int`／persistence shape／UI range維持不變。V21 custom-adjacent sentinel `65534`下的真實ref diff `65535`由舊錯誤的`NHC_DIFF_NONE` literal修正為numeric `65535`；default sentinel與Lucid signed C bytes不變。

V21／V22 simulation 的輸入 domain 是 FW INT16：有限小數向零截斷，超過 INT16 範圍時飽和，NaN／Infinity 量化為 0。`Cells.BeforeValue`、Actions 與 safety audit 都使用這份量化後 baseline，避免把輸入量化誤顯示成 notch compensation。V21 CadAllocation 另提供 source-oriented Actions；V21 LegacyRegularAnchor 的 row 是 destination-oriented，因此只提供 exact Cells/EMS，不產生方向錯誤的 source-flow Actions。

1.3.x 若發現 parity 缺口，只能在不改 C bytes 的前提下調整 simulation／decoder／compatibility adapter；需要改 encoder、row ordering 或 C emission 的 correctness change 必須另立 post-1.3.x product issue。

### 7.4.3 C export generation metadata

C export 會輸出純註解 metadata 區域，不新增 C 結構、不新增 root ABI。metadata 註解會記錄：

- `ComputationMode`
- `CompensationModel`
- `SourceCombineRule`
- `TargetAllocationRule`
- effective `ToRegular / ToFull`
- `ToFullRuleEngine / Trace`
- `BoundaryVirtualAreaCap / BoundaryVirtualAreaCapRatio`
- `TargetCoverageGuard / TargetCoverageCapPercent`
- export profile
- v2.1 / v2.2 是否實際有 row
- FW simulation base mask 的 active diff 數量與清 0 span（僅 Debug profile）

### 7.5 FW simulation base mask

C export 的 Debug profile 會額外輸出一組 FW simulation base mask，目的是讓 FW 端不用另外手寫 `SeeRegular` 類似 mask，也能建立與 FreeformHelper Simulation 一致的 baseline。

Release profile 會省略整段 simulation base mask，不輸出相關 define、struct、`cau16NHC_FW_BASE_MASK_ICx[]`、dispatch table，或 `FUNC_NHC_SimulationLoadFwBaseMask*` helper，以避免正式 `.c` 檔攜帶額外常數資料。

- mask 來源：
  - export 時若有 active regular set，使用該 set。
  - 若沒有 active regular set，fallback 為目前 regular grid 的全部 pad。
- mask 內容：
  - 每 IC 一份 `cau16NHC_FW_BASE_MASK_ICx[]`，內含 active regular 對應的 FW diff index。
  - 每 IC 一份 dispatch entry：`castNHC_FW_BASE_MASK_BY_IC[u8Ic]`。
- runtime helper：
  - `FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)`
    - 先把該 IC `0..u16DiffSpan-1` 全部設為 `0`。
    - 再把 mask 內的 active FW diff 設為 `s16BaseValue`。
  - `FUNC_NHC_SimulationLoadFwBaseMask(void)`
    - legacy 單 IC helper，預設載入 IC1，base value 為 `USER_NHC_SIM_BASE_VALUE`，目前預設 `400`。
- 注意：
  - 這是 simulation / bring-up helper，不改變 `FUNC_NHC_DiffCompensationByIc(...)` 的正式補償流程。
  - 正式 FW 補償不需要這段資料；只有需要在 FW 內重建 FreeformHelper Simulation baseline 時才切 Debug profile 匯出。
  - 多 IC FW 端呼叫前仍需切到對應 IC 的 `S_2D_DIFFAFTER` buffer/context。

### 7.6 對外符號

- `castNHC_TABLE_ICx`
  - 每顆 IC 的實際 table。
- `castNHC_TABLE_BY_IC`
  - 多 IC dispatch table。
- `castNHC_TABLE`
  - legacy single-IC alias，等同 `castNHC_TABLE_IC1`。
- `castNHC_FW_BASE_MASK_BY_IC`
  - 僅 Debug profile 輸出，每 IC 的 FW simulation base mask dispatch table。
- `FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)`
  - 僅 Debug profile 輸出，FW simulation baseline 載入 helper。
- `FUNC_NHC_SimulationLoadFwBaseMask(void)`
  - 僅 Debug profile 輸出，legacy 單 IC baseline 載入 helper。
- `FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)`
  - 多 IC 呼叫入口；呼叫前 FW 端應切到對應 IC 的 `S_2D_DIFFAFTER` buffer/context。
- `FUNC_NHC_DiffCompensation(void)`
  - 舊 codebase 入口；預設套用 IC1。

### 7.7 v2.1 / v2.2 差異

- `C v2.1`
  - 仍使用 codebase v2.0.0 的 legacy compensation 公式：先做 `NHC_REGU_TO_FULL` 乘法，再用 `NHC_TYPE_ADD/SUB + Q7 ratio` 建 offset，最後回加到 `NHC_IDX`。
  - 在 `CadAllocation` canonical 路徑下，共用 final projector 會先把 source-oriented v2.1 payload 轉成 legacy FW function 可正確執行的 destination-oriented rows；exporter 與 simulation 都只消費這份結果。
  - 這是為了讓舊 v2.1 runtime 公式仍可承接目前 v2.2 canonical 語義。
- `C v2.2`
  - 檔案骨架與入口仍是 codebase v2.0.0 style。
  - table payload 改成 source-oriented：source diff 先備份，再依 `combine / legs / continuation` 重新分配到 targets；leg 為 `INT8 -100..100` signed percent，不經 V21 Q7 codec。
  - v2.2 不再透過 unified export root 導入。

因此 v2.1 和 v2.2 目前都是「基於 codebase v2.0.0，只替換 table 內容與 compensation algorithm」。

## 8. 目前與文件最常混淆的地方

### 8.1 `Visible` 不等於 `Output`

- `Visible CAD Diff` 是畫面投影結果
- `CAD Output FW Diff Idx` 才是 workflow 真值

### 8.2 `Best Match FW Diff` 不等於 `Final Output`

- best match 是幾何 seed
- final output 是 Step4 assignment 結果

### 8.3 Simulation 不應回頭以 CAD 當真值

- CAD 參與的是 notch table 生成
- 一旦進入 Simulation，就應以 regular + FW diff + notch table 為主

## 9. Domain Golden Rules（Sensor / FW / Notch 物理前提）

這一節記錄目前 Freeform / Notch 設計時應遵守的 domain golden rules。若未來演算法與這些規則衝突，應先以這裡作為檢查基準，再決定是修正演算法還是明確修正文檔。

### 9.1 錯位幾何前提

- sensor team 為了排列異形屏，局部一定會出現錯位排列。
- golden rule 是：一次只會有單一主軸的嚴重錯位，也就是嚴重錯位應以 `X` 或 `Y` 單一方向為主，不應同時在兩軸都大幅錯位。
- `CAD pad area / Regular pad area` 應大致落在 `75% ~ 125%` 區間，避免 pad 面積差過大造成 Open Test 或實際感應量異常。

### 9.2 FW / Memory 觀點

- tool 與 IC 內的記憶體只看長方形排列的感應量。
- FW 最終消費的是 regular view / rectangular memory view，不會直接看到 sensor 上的錯位幾何。
- 因此 `(IC, FW Diff Idx)` 代表的是最終 memory / channel 真值，而不是 CAD 幾何真值。

### 9.3 Notch Table 的根本目的

- freeform / 錯位區的實際感應量分布，與 FW 用長方形 grid 計算座標時假設的分布並不一致。
- notch table 的目的就是把「sensor 實際收到的感應量」重新分配回 regular view，讓 FW 在 regular / rectangular 座標系下仍能算出接近真實觸點的位置。

### 9.4 NF（Normal Force）前提

- 現行 sensor pad 都有 `NF`，會用接地銅柱刷整面，讓整面大致都落在 `400` 左右的感應量區間。
- NF 原始目的是補償：
  - 遠近端差異
  - 位置差異
  - 面內 / 面外（邊緣）差異
- 但 NF 也會順帶壓平一部分由 pad 面積不同造成的感應量差異，亦即：
  - 面積較大的 pad，其感應量會被部分壓低
  - 面積較小的 pad，其感應量差異會被部分抹平

### 9.5 To Regular 的物理語意

- `ToRegular` 的目的不是單純做 gain，而是先把被 NF 壓平過的面積差異還原回去。
- 也就是說，`ToRegular` 應被視為「將感應量拉回較接近面積正比的狀態」，用來抵銷 NF 對面積差異的部分補償。
- 若這個前提成立，`ToRegular` 在物理語意上是合理的，而且比單純直接保留 NF 後的值更接近真實 sensor 面積分布。

### 9.6 To Full 的物理語意

- `ToFull` 的目的不是為了任意放大感應量，而是為了支援 boundary 推邊。
- 背景原因是：Boundary 演算法會根據邊緣外一條感應量推估位置，若 freeform 區在 notch 校正前沒有有效感應量輸出，邊緣就無法正確往外推。
- 因此 `ToFull` 的合理用途應是：
  - 允許邊緣 regular 取得可用的邊界感應量支撐
  - 幫助邊緣報點，而不是改寫整體 interior 的主分配規則

### 9.7 對 To Full 的 canonical 建議

- `ToFull` 是否應該直接把擴展後的 full regular area 當成 target allocation 權重，這件事目前不應視為已定案。
- 目前較符合物理直覺的建議是：
  - `exact overlap area` 應作為主要分配權重
  - `ToFull` 應作為 `support mask / reachability cap / boundary allowance`
  - 只有在「可不可以分到那裡」與「邊界是否允許補償」這層使用 `ToFull`
  - 不應讓 tiny overlap 因為 full-area expansion 直接反客為主，變成主要 target ratio
- 例外：`Current (Gain)` 是研究/校準用模型。beta0.9 起它不再把 CAD-level `R*F` 當 source-wide gain，而是直接用 per-target Stage3 coverage 產生 retained / target legs，避免小邊緣 regular 的 coverage 外溢。

### 9.8 Simulation 的定位

- Simulation 的目的，是在本地端模擬 FW 套用 notch table 後的行為。
- 它不是第二套真值來源，而是：
  - 驗證 Step5 / notch table 是否合理
  - 用 `global 400` 之類 uniform input 作診斷輸入，暴露局部 net-flow imbalance 或不合理累積
  - 檢查 shared EMS after-cap predicate 判定為 true、可能觸發 EMS 保護機制的風險
- `After` 接近 400 不是物理正確性的充分條件；真正的主約束仍是幾何面積比例與 FW memory view 的重新分配是否自洽。

### 9.8.1 Simulation safety / physical audit gate

- `SimulationSafetyAuditService.Analyze(NotchApplySimulationResult)` 是 Simulation / export 前的正式 audit 入口。
- `SimulationSafetyAuditService.IsEmsAfterCapViolation(afterValue, afterCap)` 是 EMS after-cap decision 的 Application owner；預設 cap 仍為 `DefaultEmsAfterCap = 480`。唯一判斷契約是 `afterValue > afterCap + 1e-9`：`cap` 與 `cap + 0.5e-9` 安全，`cap + 2e-9` 為 violation。
- `SimulationSafetyTextProjector.BuildStatusText(SimulationSafetyAuditResult?)` 是 workspace status與overview base availability／severity text的UI owner：`null`或`HasCells == false`為`Simulation not run`；有cells時依`EMS risk > audit warning > EMS OK`排序，physical warning直接讀audit既有global-flow／net-flow／target-coverage facts。Overview的`HasRisk`仍只代表EMS danger，`NeedsAttention`則涵蓋physical risk與stale；非stale summary在既有EMS句後附加`BuildPhysicalAuditSummaryText` evidence。Stale suffix與build-failure `Simulation unavailable`仍由overview boundary裝飾；cell／high-risk文字及EMS cap/predicate不屬於此mapping。Copper replay severity另由下節的`BuildReplayStatusText`投影。
- `SimulationSafetyTextProjector.FormatEmsAfterCap` 是overview／export no-cells cap文字的UI owner：supplied audit即使`HasCells == false`仍保留其`AfterCap`；只有null audit使用`DefaultEmsAfterCap = 480`。此fallback不改`HasAudit`、availability status、summary或EMS violation predicate。
- `SimulationSafetyTextProjector`亦擁有Notch `EMS cap ...` short text、Simulation safety policy、export handoff policy與target-cap help templates。主`FreeformHelperViewModel`只把current `SimulationSafetyOverviewEmsCapText`與current target cap供給此owner，cap變更時通知對應dependent properties；Settings以同一owner、current draft target cap與default EMS cap保留documented context。Active Step 3及Settings Step 3的既有target-cap tooltips綁定`NotchTargetCoverageCapHelpText`：例如target cap `128%`對uniform `400`投影`After 512`，active端比較current EMS cap、Settings比較default cap。此文字投影不另算risk，也不改target coverage guard、EMS after-cap predicate或其輸入。
- Notch export review的badge對supported audit共讀`BuildStatusText` severity；EMS case保留既有violation count，physical-only case顯示`audit warning · Max After ...`且不阻擋export。Physical-only export summary保留既有safe EMS-cap／location句，再附加同一`BuildPhysicalAuditSummaryText` evidence；同時有EMS／physical risk時既有block dialog全文不變。VM只提供physical-only visibility fact，XAML重用既有`chipStatus warning`／token；不得解析status字串或重算global-flow／net-flow／coverage。Null／no-cells仍為not run，只有EMS violation可觸發既有block dialog。
- audit 使用同一份 simulation result，不從 UI partial state 重新推導：
  - `Cells` 提供 `Before / After / Delta`、Max After 與 EMS cap violation。
  - `Actions` 提供 source retained、target legs 與 per-diff net-flow。
- 預設 hard gate：
  - EMS after-cap：預設 cap 為 `480`；只在 `After > 480 + 1e-9` 時違規
  - target coverage cap：同一 `(IC, FW Diff)` 的 retained + incoming coverage 預設不可超過 `120%`
- audit 會輸出：
  - global action-flow residual：`Result.DeltaTotal - Σ(action source/target delta)`
  - per-diff net-flow residual：`cell.Delta - (sourceDelta + targetDelta)`
  - target coverage risk：定位 retained + incoming coverage 超過 cap 的 `(IC, FW Diff)`
  - high-risk diff：依 `After` 排序，保留 cap margin 與 worst REG / IC / FW Diff
- audit violation/count、net-flow／target-coverage `EmsRisk` 分類、Runtime Query `regular.isEmsSafetyRisk`、cell `EMS OK`／`EMS risk` 及 workspace／overview high-risk 狀態皆投影同一 predicate。`MarginToCap = afterCap - afterValue` 仍是原始數值 evidence，不能再只依正負重判；容差內的微小負 margin 仍安全，presentation 顯示為零 margin。
- 原因分類：
  - `GeometryExpected`：符合幾何 coverage cap，偏差可由面積/邊界語意解釋
  - `NetFlowSuspicious`：cell delta 與 action net-flow 不一致
  - `EmsRisk`：shared EMS after-cap predicate 對該 cell／max After 判為 true
- `global 400` 仍只作 diagnostic input。audit 不把「越接近 400」視為正確性目標；真正 gate 是 EMS、net-flow 自洽與 geometry coverage cap。

### 9.8.2 Copper path replay

- Copper path replay 透過 `SimulationWorkspaceUseCase.ReplayCopperPath(...)` 執行。
- 每個 path point 都走同一條正式路徑：
  - `BuildCopperDataset`
  - `BuildSnapshot`
  - `SimulationSafetyAuditService.Analyze(...)`
- replay step 會記錄：
  - copper center
  - Max After
  - EMS violation count
  - worst REG / IC / FW Diff
  - net-flow residual count
  - target coverage risk count
- replay step以不改既有positional constructor的non-positional、`[JsonIgnore]` facts保存simulation support state與global action-flow residual；artifact row複製同一JSON-ignored global-flow fact，使既有structured physical-risk與status一致，但不新增artifact serialized field。
- `CopperPillarPathReplayStep.HasPhysicalAuditRisks`涵蓋global-flow residual、net-flow residual count與target-coverage risk count；`CopperPillarPathReplayResult`再從steps投影`HasUnsupportedSteps`與`HasPhysicalAuditRisks`，consumer不得解析`StatusText`還原state。
- `SimulationSafetyTextProjector.BuildReplayStatusText(isSupported, hasEmsViolations, hasPhysicalAuditRisks)`是replay severity wording的唯一owner，優先序固定為`unsupported`、`EMS risk`、`audit warning`、`EMS OK`。UseCase step status與VM aggregate summary共讀此owner；aggregate只在EMS case保留既有total violation count suffix。
- 既有artifact CSV／JSON／clipboard fields、header、ordering、public positional constructors與replay physics／sampling不變。Public headless characterization只驗證result／ViewModel seam，不代表normal operator workflow必然建立同一replay。
- 目的不是取代互動式 mouse preview，而是把邊緣與 notch 區的 sweep 驗證自動化，避免靠手動觀察判斷 release risk。

### 9.9 實際應用判定

依照目前 sensor / FW / NF 的實際應用場景，`ToRegular` 與 `ToFull` 應被視為兩個不同目的的處理：

- `ToRegular`
  - 結論：合理，且應保留。
  - 原因：NF 會把不同 pad 面積造成的感應量差異壓平；`ToRegular` 的合理語意是先將錯位區感應量還原到較接近面積正比。
  - 使用限制：不應把它理解成任意 gain，而是 `Undo NF area flattening`。
- `ToFull`
  - 結論：合理，但不應直接用 expanded/full area 決定 target ratio。
  - 原因：`ToFull` 的真實目的，是讓 boundary / 推邊演算法有足夠邊緣支撐，而不是讓 tiny overlap 被 full expansion 放大成主分配腿。
  - 建議語意：`ToFull` 決定可達範圍、支援資格、上限或邊界補貼；實際分配比例仍應優先由 `exact/source overlap` 決定。
- `with gain`
  - 結論：不適合作為預設主路徑。
  - 原因：目前已修正為 per-target Stage3 coverage，但在 target allocation 尚未經實測證明 uniform-field 穩定前，gain 仍可能放大局部 net-flow imbalance。
- `without gain`
  - 結論：較適合作為預設主路徑。
  - 原因：它保留 `ToRegular` 的面積還原，但不把 `ToFull` 當成 source-side 放大倍率，較不容易製造 EMS safety risk 或額外的局部累積。

## 10. 目前四種組合的取捨與推薦

### 10.1 直接結論

- 若目標是「最符合上述物理前提，且仍維持可接受資源成本」，目前最推薦的正式設計真值是：
  - `v2.2 + without gain (ConservativeNoGain)`
- 若 FW 或外部導入端仍只能吃 `v2.1`：
  - 仍應以 `v2.2 canonical` 為設計真值
  - `v2.1` 只作 compatibility projection / export format
- `with gain` 目前不應作為預設主路徑；雖然 target legs 已改為 Stage3 effective allocation，仍需要實測確認 ToFull gain 是否符合物理量測。

### 10.2 四種組合比較

| 組合 | 物理貼近度 | Tool / Simulation 成本 | Export / FW 成本 | 結論 |
| --- | --- | --- | --- | --- |
| `v2.2 + without gain` | 最高（以目前 shipping 選項） | 中 | 中到低 | 最推薦；作為 canonical truth |
| `v2.2 + with gain` | 中 | 中 | 中到低 | 可保留做校準/研究，但不宜當預設 |
| `v2.1 + without gain` | 中低 | 低到中 | 低 | 僅適合作 compatibility export，不宜當設計真值 |
| `v2.1 + with gain` | 最低 | 低到中 | 低 | 最不推薦；格式能力與 gain 放大問題同時存在 |

補充：

- 目前 normal `CadAllocation` export cache 已保存 compact output-request-neutral candidate batch；同identity／epoch的並行full-table consumers共用phases 1～3 resolution task，再各自對 enabled versions、threshold、`NullValue` 與 target guard/cap 做phase 4 final projection。Generation completion以入口凍結的cache epoch、final-projection revision與source revision拒絕stale store／publish；final-only invalidation保留task/batch但維持row count `0`，完整invalidation則推進epoch、detach task並禁止舊completion補存。R13.102a-2b-2再讓batch至多攜帶並reuse／promotion目前單一選取CAD的一份identity-bound sparse resolved result；它不保存其餘candidate的完整polygons/debug evidence。R13.101e已讓normal generator／UI先建立完整compensation context，allocation／boundary／active／strict/query／policy evidence不再由caller以nullable組合控制stage順序；context仍不含任何final-output state。R13.103c-1 已讓 V22 C exporter 與 simulation 共用同一 final Firmware projector，R13.103c-2 則固定 V21／V22 final projection 共用的 `UINT16` sentinel fail-fast。`LegacyRegularAnchor`雖已移除registry並改用固定V21／V22明確派送，仍保留request-specific table cache與compatibility boundary；R13.101c-2／R13.102／R13.102a-2／R13.103 parents依各自exit criteria保持open。
- 若只看 row payload，`v2.2 node` 是 `7-int`，`v2.1 legacy row` 是 `9-int`；所以在目前 contract 下，`v2.2` 不一定比 `v2.1` 更耗記憶體。
- 真正的成本差異主要在語義能力與下游相容性，而不是「v2.2 一定更重」。

### 10.3 為什麼 `without gain` 比較適合作為預設

- beta0.9 起，`with gain` 不再把 `ToRegularPercent * ToFullPercent` 乘回每個 target share。
- `with gain` 的 v2.2 row 使用 per-target Stage3 coverage：`stage3EffectiveAreaOnTarget / targetRegularArea`。
- `without gain` 的 v2.2 row 使用 per-target source overlap coverage：`overlapAreaOnTarget / targetRegularArea`；ToFull 只作 support/cap/allowance。
- `ToRegularRatio = Σ(overlapArea / targetRegularArea)` 仍是重要診斷值，但不再作為整顆 CAD 的 source-wide gain。
- `Target coverage guard` 仍保留作 EMS safety net。它不是把所有值強制拉回 400，而是在 row 選定後限制單一 target FW diff 的總 coverage，降低多個 source 在邊界堆疊後被 shared EMS after-cap predicate 判為 risk 的機會。
- 歷史 non-signed diagnostic 曾記錄 `max=424 / EMS violations=0`；current 3635 signed gate 未保存該 uniform-400 metric，因此不可把這組數字當作目前驗收事實。若要重新使用，必須產生含 input/command/environment/hash 的 simulation artifact。
- 設計判斷仍是：避免把 CAD-level `R` 當 source-wide gain後再依 source share 二次分配；但 EMS 結論應由 current `SimulationSafetyAuditResult` 證據決定，不由這組歷史裸數字推定。
- 若使用 `global 400` 作為診斷輸入，應先檢查：
  - 是否存在 shared EMS after-cap predicate 判定的 risk
  - 高偏差 diff 是否能被幾何面積比例、boundary 支撐或 net-flow residual 解釋
  - `with gain` 只適合在已經證明分配矩陣合理，且另有物理量測支持 gain 校準時再開

### 10.4 為什麼 `v2.2` 比 `v2.1` 更適合作為主線

- `v2.2` 可以明確表達：
  - `source diff`
  - `combine percent`
  - `target diff legs`
  - continuation rows
- `v2.1` 在現在的系統中已經屬於 compatibility payload，不應再承擔新的主語義設計。
- 若要處理 freeform / boundary / SeeRegular / per-diff redistribution，`v2.2` 的表達能力明顯較合適。

### 10.5 Post-1.3.x 演算法研究方向（不屬於 zero-diff refactor）

1. `ToRegular` 應保留
   - 因為它符合「先把被 NF 壓平的面積差異還原回去」這個物理前提。
2. `ToFull` 應改成 support/cap，而不是直接作 target weight
   - beta0.7 baseline 的 v2.2 allocation 會在 `ToFull` 命中時，把 `ReachableArea` 當成 `EffectiveArea` 直接進 target ratio。
   - beta0.8 prototype 已先改成以 `SourceArea` 作主權重，讓 `ToFull` 不再直接主導比例。
   - beta0.9 起 `Current (Gain)` 例外回到 Stage3 effective allocation，因為 gain mode 若不以 ToFull 後面積分配，會產生邊緣 retained signal 異常。
   - beta0.9 已補第一版 cap/guard：ToFull 虛擬外延面積可被 inside overlap ratio 限制，且 CurrentGain 可用 target coverage guard 避免 boundary net-flow 堆疊超過 EMS。
   - 這仍需 AutoCAD 實測確認：如果 target coverage >120% 來自真實 CAD 幾何覆蓋，guard 只能作 safety policy；如果它是演算法堆疊，guard 才應作 CurrentGain 預設。
3. target allocation 應增加 area-preserving + EMS-guarded 約束
   - 主目標不是把每個 `After` 強制拉近 `400`，而是在保留 source/target 面積比例的前提下避免不合理累積。
   - `global 400` 是診斷 case：若 interior diff 動輒偏差 `100+`，必須能解釋為合理 geometry / boundary effect，否則列為 net-flow suspicious。
   - shared EMS after-cap predicate 判定為 true 時視為 EMS safety violation，需要在 Simulation / export 前提示或阻擋；target coverage guard 是第一版自動壓制策略。
   - 2026-04-30 起，正式 audit 會同時檢查 global action-flow residual、per-diff net-flow residual 與 target coverage cap，讓高偏差 diff 可被歸類為 `GeometryExpected`、`NetFlowSuspicious` 或 `EmsRisk`。
4. `single-axis` 與 `CAD/Regular area ratio` 先保留為 domain 前提
   - 這兩者描述 sensor 設計輸入的合理範圍。
   - beta0.8 不把它們放進主開發範圍，因為 tool 端目前無法控制 sensor team 的輸出。

因此，最符合你描述的 future state 應該是：

- `v2.2 canonical`
- `without gain` 作為預設
- `ToRegular` 保留
- `ToFull` 改為 `support / cap / boundary allowance`
- target allocation 再加入 `area-preserving + EMS-guarded` 的安全校正與 audit；beta0.9 的 `Boundary virtual-area cap` / `Target coverage guard` 是目前第一版實作

## 11. 相關 deep-dive / example

- [Notch v2.1 / v2.2 流程定義與流程圖](../core/notch-v21-v22-flow.md)
  - Step5 flow、bucket、continuation 與 export path 拆解
- [Notch / Simulation Mermaid 圖庫](../diagrams/notch-simulation/zh-TW/README.md)
  - 雙語 overview、Notch table 模組地圖、Simulation 模組地圖與各模組細節子圖
- [Notch V21 演算法細節](../core/notch-v21-algorithm.md)
  - v2.1 legacy 9 欄 row 的幾何語義與 Q7 contract
- [Notch V22 演算法細節](../core/notch-v22-algorithm.md)
  - v2.2 主線與 legacy 相容支線的演算法拆解
- [Current v2.1 C export example](../../example/BOE36.35/notch_export_v21_current.c)
  - 目前 exporter 產生的 v2.1 `.c` example
- [Current v2.2 C export example](../../example/BOE36.35/notch_export_v22_current.c)
  - 目前 exporter 產生的 v2.2 `.c` example

## 12. 維護規則

後續若改到以下任一項，必須先更新本檔：

- diff identity 命名
- Step5 source / target 定義
- Simulation 的輸入真值
- C export ABI / symbol / multi-IC 格式

若 deep-dive 文件與本檔矛盾，以本檔為準，deep-dive 文件需補上差異註記或回收。
