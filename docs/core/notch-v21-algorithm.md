# Notch V21 演算法細節
最後更新：2026-03-23

本文件描述目前 repo 內 `V21` 的實作契約。這裡講的是「現在程式怎麼算」，不是歷史外部答案檔。

主要實作：
- `src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs`
- `src/FreeformHelper.Application/Services/NotchAlgorithms/NotchAlgorithmHelpers.cs`

## 1. 適用條件

`V21` 只處理：
- `reg.Freeform != None`

也就是：
- `XWay`
- `YWay`
- `XYWay` 仍會通過 `CanHandle`，但 legacy v2.1 軸向 helper 會將非 `YWay` 視為 X 軸

判斷入口：
- `V21NotchAlgorithm.CanHandle(...)`

## 2. 輸出格式

`V21` 輸出是 9 欄 legacy row：

1. `IDX`
2. `REGULAR_PERCENT`
3. `REGU_TO_FULL_PERCENT`
4. `FIRST_DIFF`
5. `FIRST_TYPE`
6. `FIRST_RATIO_Q7`
7. `SECOND_DIFF`
8. `SECOND_TYPE`
9. `SECOND_RATIO_Q7`

## 3. 前三欄怎麼算

### 3.1 `IDX`
- 直接取 `reg.DiffIndex`

### 3.2 `REGULAR_PERCENT`
- `cad.Area / reg.Area * 100`
- 四捨五入到整數

### 3.3 `REGU_TO_FULL_PERCENT`
- `cad.Bounds.Width * cad.Bounds.Height / reg.Area * 100`
- 四捨五入到整數

注意：
- 這兩欄是兩個不同觀點的幾何比例。
- 不會在 `V21` row 生成時彼此相乘。

## 4. 軸向判定

`V21` 先把 freeform 類型收斂成軸：
- `XWay -> X`
- `YWay -> Y`
- `XYWay -> X`（legacy fallback；正式 Step5 主線仍以 v2.2 canonical flow 為準）

輔助入口：
- `NotchAlgorithmHelpers.ResolveAxisKind(...)`

## 5. 鄰居與幾何上下文

### 5.1 X 軸
- 左鄰居：`(row, col - 1)`
- 右鄰居：`(row, col + 1)`

### 5.2 Y 軸
- 上鄰居：`(row + 1, col)`
- 下鄰居：`(row - 1, col)`

這裡用的是內部 grid row 契約，不是 UI 顯示的 top-down row label。

建立入口：
- `CreateAxisGeometryContext(...)`
- `CreateAxisNeighborContext(...)`

## 6. `type` 定義

`V21` 的 leg type 只有三種：

- `0 = none`
- `1 = add`
- `2 = sub`

白話：
- `add`：把量加到鄰居 diff
- `sub`：代表在自己這顆 diff 內扣回去

## 7. `Q7` ratio 是什麼

`V21` 的第 6 / 9 欄是 firmware `UINT8` Q7 magnitude：

- `1.0 = 128`
- `0.5 = 64`
- `0.0 = 0`
- 合法 payload 範圍是 `0..255`；`255` 約為 `199%`
- 正負不放在 magnitude，而只由 `NHC_TYPE_ADD / NHC_TYPE_SUB` 承載

也就是：

```text
ratioQ7 = roundAwayFromZero(fraction * 128)
```

legacy geometry row 可以暫存大於 `255` 的未飽和值以維持歷史 row/CSV 語意；唯一 final firmware projector 會在 ABI 邊界飽和到 `0..255`。Firmware apply 直接執行 `(INT16 source * magnitudeQ7) >> 7`，不可先轉成整數 percent 或 double 再套用。

fraction 的來源是：
- 幾何延伸長度 / 目前 cell 長度
- 或幾何延伸長度 / 鄰居 cell 長度

對應入口：
- legacy row：`NotchV21Q7Codec.EncodeLegacyRowFractionRaw(...)`
- final payload／display／apply：`NotchV21Q7Codec`

`ThresholdQ7` 是另一份 `0..128` admission contract，由 `NotchThresholdQ7Contract` 管理；它不是 leg payload，不能與 `0..255` 的 magnitude 上限合併。

## 8. `add` / `sub` 怎麼決定

對 X / Y 軸都一樣：

1. 先算 leading / trailing 的幾何 delta
2. 判斷 CAD 是否真的往那一側跨出 cell
3. 若跨出：
- `type = add`
- `diff = neighbor diff`
4. 若沒跨出但有回縮：
- `type = sub`
- `diff = current reg diff`

實作入口：
- `geometryContext.ExpandsLeading(...)`
- `geometryContext.ExpandsTrailing(...)`
- `geometryContext.GetLeadingDelta(...)`
- `geometryContext.GetTrailingDelta(...)`

## 9. `V21` 真正表達的是什麼

`V21` 這列可以白話成：

- 這顆 CAD 在自己的 regular 內大概占多少
- 若看 bounding box，會到多少
- 在第一個方向，對哪個 diff 有 `add/sub`
- 在第二個方向，對哪個 diff 有 `add/sub`
- 每個方向的量有多大（用 `Q7`）

它是幾何導出的 legacy row，不是 firmware 真值表。

## 10. 修改時要守的契約

1. `Q7` 是目前唯一 V21 leg ratio 契約
- 不要再把 UI 可調的 `LenScale` 帶回使用者層。
- V22 leg 維持 `INT8 -100..100` signed percent，不走 V21 Q7 codec。

2. `V21` 的 `[1]/[2]` 是幾何比例
- 不應該被 UI / exporter / simulation 再重解成別的單位。

3. `V21` 的 `type / diff / ratio` 必須一起看
- 不可只看 ratio 而忽略 `add/sub` 語意。
- exporter 與 simulation 必須消費同一份 `NotchV21FirmwareProjector` final node；formatter 不再重做 clamp 或 projection。
