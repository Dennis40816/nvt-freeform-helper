# Notch V22 演算法細節

> Canonical reference：[`docs/reference/notch-system-reference.md`](../reference/notch-system-reference.md)
> 本檔保留 v2.2 演算法 deep-dive；流程真值與 export 契約以 canonical reference 為準。

最後更新：2026-08-08

`V22` 在 repo 裡有兩條關聯但不同層級的實作：

1. 主線：
- `NotchV22CompensationService`
- `NotchV22TargetAllocationService`
- `NotchTableGenerator.Generation.V22`
- 這是 Step3 / Step5 真正的 `v2.2` 主路徑

2. 相容：
- `V22LegacyRowStrategy`
- 這是 legacy 9 欄相容 row，不是 Step5 主輸出

本文件先講主線，再講 legacy 相容支線。

## 1. 主線入口

- `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
- `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs`
- `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs`
- `src/FreeformHelper.Domain/Notch/NotchV22Node.cs`

## 2. 主線的三個核心比例

### 2.1 `ToRegular`
- 來源：CAD 與所有 overlapped regular 的面積關係
- 比率：`sum(overlapArea / regularArea)`
- 注意：這是診斷用的 CAD-level 面積覆蓋總量，不應再被視為 source-wide gain 乘到所有 target share。
- 實際 v2.2 row 使用 per-target regular coverage：
  - No Gain：`overlapAreaOnTarget / targetRegularArea`
  - Gain：`stage3EffectiveAreaOnTarget / targetRegularArea`

### 2.2 `ToFull`
- 來源：Stage3 補償後最終面積 / CAD 原始面積
- 必須先通過 rule gate，才會真的展開

### 2.3 `Combine`
- beta0.9 後的主線公式：

```text
CombinePercent = retainedCoveragePercent + Σ(targetLegCoveragePercent)
```

- `Current (Gain)`：coverage 來自 `stage3EffectiveAreaOnTarget / targetRegularArea`
- `Conservative (No Gain)`：coverage 來自 `overlapAreaOnTarget / targetRegularArea`
- `R` / `F` 仍保留在 diagnostics/comment 中，但不再先合成 source-wide gain 後二次分配。

例子：3635 的 `CAD4818` 覆蓋 `REG384/FW1472` 約 `98.6%`，覆蓋 `REG385/FW1473` 約 `58.8%`，因此 diagnostic `R≈157.4%`。v2.2 row 不會把 `R` 乘回 target share；No Gain row 應輸出 `diff1472 leg≈99%`、`diff1473 retained≈59%`、`C≈158%`。目前 `example/BOE36.35/notch_export_v22_current.c` 對應 row 為 `{ 1473, 158, 1472, 99, ... }`，`NODE KEEP=59% MOVE=99%`。

## 3. `ToFull` 的 gate

每顆 overlapped regular 都會被評估：
- boundary candidate？
- source area 足夠？
- multi-owner？
- blocker？
- really has expansion？

常見 gate code：
- `GATE_TOFULL_DISABLED`
- `GATE_NOT_BOUNDARY`
- `GATE_SOURCE_EMPTY`
- `GATE_MULTI_OWNER`
- `NO_EXPANSION_NEEDED`
- `EXPAND_CLEAR_PATH`

規則引擎：
- `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs`

## 4. Stage1 / Stage2 / Stage3

### 4.1 Stage1
- seed overlap
- CAD 真正與 regular 重疊的區域

### 4.2 Stage2
- candidate regular boundary
- 可考慮擴張的 regular 候選

### 4.3 Stage3
- final to-full result
- 真正套用後的 union / outline

UI、RuntimeQuery、Inspector 都必須讀同一份 resolved result，不得自己再推。

## 5. Target allocation

`V22` 主輸出不是只看 anchor 自己，還會算 target legs。

target allocation 規則：
- 以 `(IC, Diff)` 分桶
- 排除 anchor diff
- 過 strict threshold
- 形成 `target diff + ratio`
- `Current (Gain)`：target leg 直接使用 Stage3 target coverage，不再使用 `CombinePercent * share`。
- `Conservative (No Gain)`：target leg 直接使用 source-overlap target coverage，ToFull 只作 support/cap/allowance。
- 若單一 target coverage 超過 `100%`，必要時拆成多個 <=100% chunk。

主服務：
- `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs`

## 6. `NotchV22Node` 7 欄

主輸出 payload：

1. `AnchorDiffIndex`
2. `CombinePercent`
3. `TargetDiffIndex1`
4. `TargetRatioPercent1`
5. `TargetDiffIndex2`
6. `TargetRatioPercent2`
7. `Flags`

### 6.1 continuation row

若一個 anchor 有超過兩個 target legs：
- 第一列保留真實 `CombinePercent`
- 續列固定 `CombinePercent = 100`
- `Flags` 打 continuation bit

## 7. `V22LegacyRowStrategy` 相容支線

檔案：
- `src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs`

這支的用途是：
- 提供 legacy 9 欄相容表示
- 不是 Step5 主匯出 payload

一般 production flow 中，公開 `Generate` 只有 `LegacyRegularAnchor` 會呼叫它的 `Build`，所以正常 row
使用 legacy 9 欄幾何估算；`CadAllocation` canonical 路徑直接走共同 compensation／allocation pipeline。
但 `Generate` 在 mode dispatch 後會同步呼叫 caller 提供的 `IProgress.Report`，callback 可修改同一份
mutable settings，讓 `Build` 重新讀到 CadAllocation。R13.004f 因此保留 compensation branch、comment
與 `allCadPads`，不把「正常流程不用」誤判成 public API 下的不可達。

此 strategy 的 `CanHandle` 仍被 CadAllocation eligibility 查詢共用，但 eligibility 不會呼叫 `Build`；
這個跨模式 seam 與 generation input immutability 留給 R13.101～R13.103 收斂，不應被誤認為 canonical
V2.2 ownership。

## 8. 修改時要守的契約

1. `Combine` 一律從 per-target regular coverage sum 得出
- UI / export / simulation 不能各算各的。
- `ToRegular * ToFull` 只保留為 CAD-level 診斷概念，不可再反推成 row payload。

2. Stage overlay 只是顯示投影
- 不得反向成為 business truth。

3. `NotchV22Node` 是 Step5 主輸出單一來源
- legacy 9 欄只作相容，不是主路徑。

更細的 Stage / UI 契約：
- `docs/core/notch-2.2-spec.md`
