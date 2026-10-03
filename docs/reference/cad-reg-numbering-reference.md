# CAD / REG Numbering Reference
最後更新：2026-06-16

## 目的
這份文件整理 FreeformHelper 目前使用者會看到的 CAD / REG / Diff 編號語意，避免把穩定 key、UI 顯示序號、FW diff identity 混在一起。

## 快速對照

| 類型 | 起點 / 方向 | 是否受 `ScanOrder` 影響 | 主要用途 | 使用者可見層級 |
| --- | --- | --- | --- | --- |
| `REG id` / `RegularPadId` | 左下開始，往右編，下一排往上 | 否 | REG 穩定識別 key | 一般可見 |
| 內部 `row` | 下排 = 0，往上增加 | 否 | 程式內部 grid 座標 | 通常隱藏 |
| UI 顯示 / 輸入 `Row` | 上排 = 0，往下增加 | 否 | Header rows range、Pad info 顯示 | 可見 |
| `REG FW Diff idx` / `DiffIndex` | 依 `ScanOrder` 決定 | 是 | FW / Notch / Simulation 的 diff identity | 一般可見 |
| `CAD id` | DXF 讀入 / 展開順序，從 0 遞增 | 否 | CAD 穩定 key、project/edit/export reference | 弱化顯示或診斷可見 |
| `CAD #display` | UI 顯示用，左上到右下，1-based | 固定用顯示排序 | 人眼檢視 CAD 順序 | 一般可見 |
| `CAD Output FW Diff idx` | CAD pad 對接到 FW diff 空間的輸出 diff | 間接受 REG diff / Step1 match 影響 | Step4 / Export / Simulation 的 CAD source diff | 診斷 / handoff 可見 |

## REG id

一般 generated grid 建立 REG pad 時，`RegularPadId` 就是 `Index`，計算方式固定：

```text
RegularPadId = row * cols + col
```

內部 `row = 0` 是幾何上的 bottom row，`col = 0` 是左側。因此 `REG id` 是從左下開始 row-major 編號。

以 `cols = 4`、`rows = 3` 為例：

| 視覺位置 | REG id |
| --- | --- |
| 上排左到右 | 8, 9, 10, 11 |
| 中排左到右 | 4, 5, 6, 7 |
| 下排左到右 | 0, 1, 2, 3 |

相關程式入口：

- `src/FreeformHelper.Application/Services/RegularGridBuilder.cs`
- `src/FreeformHelper.Domain/Pads/RegularPad.cs`

## UI Row

UI 給使用者看的 row 是 top-origin：

```text
UI row 0 = AA 視覺上方第一排
```

因此 UI row 和內部 row 會互轉：

```text
displayRow = (totalRows - 1) - actualRow
actualRow = (totalRows - 1) - displayRow
```

相關程式入口：

- `src/FreeformHelper.UI/Services/ManualSizingService.Parsing.cs`

## REG FW Diff idx

`DiffIndex` / `RegularFwDiffIndex` 是 FW / Notch / Simulation 使用的 diff identity，不等於 `REG id`。

Grid 建好後會依目前 `ScanOrder` 與 IC 分欄計算：

```text
DiffIndex = scanRow * icCols + scanColLocal
```

目前支援的 scan order：

| `ScanOrder` | Row 方向 | Col 方向 |
| --- | --- | --- |
| `LeftToRight_TopToBottom` | 上到下 | 左到右 |
| `RightToLeft_TopToBottom` | 上到下 | 右到左 |
| `LeftToRight_BottomToTop` | 下到上 | 左到右 |
| `RightToLeft_BottomToTop` | 下到上 | 右到左 |

相關程式入口：

- `src/FreeformHelper.Application/Services/AfeMapper.cs`
- `src/FreeformHelper.Application/Settings/ScanOrder.cs`

## CAD id

`CAD id` 是 DXF import 時給 CAD pad 的穩定 key。它從 `0` 開始，依 DXF entity 讀入與 block insert 展開順序遞增。

它不是空間排序號，也不應拿來表示使用者視覺上的第幾顆 CAD。

相關程式入口：

- `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.cs`
- `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.PolylineCommit.cs`
- `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.InsertExpansion.cs`

## CAD #display

`CAD #display` 是 UI 顯示用序號，讓使用者依視覺順序找 CAD。它固定使用左上到右下排序，並且是 1-based。

目前 inspector 會優先顯示：

```text
CAD #123 (id 4823)
```

其中：

- `#123` 是 display index，適合人工檢視。
- `id 4823` 是穩定 key，適合 project state、edit、diagnostics、runtime query。

相關程式入口：

- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorSummary.cs`

## CAD Output FW Diff idx

`CAD Output FW Diff idx` 是 CAD pad 接到 FW diff 空間的輸出端編號。它和 `REG FW Diff idx` 是連接關係，但不是同一個欄位，也不是 CAD display index。

一般情況：

```text
CAD pad
  -> Step1 geometry best-match REG
  -> matched REG 的 FW Diff idx
  -> CAD Output FW Diff idx
```

進入 Notch / Simulation 時可理解為：

```text
source = CAD Output FW Diff idx
target = REG FW Diff idx
```

可能改變 CAD Output FW Diff 指派的情況：

| 情況 | 結果 |
| --- | --- |
| 一般幾何 match | 使用 matched REG 的 `FW Diff idx` |
| `SeeRegular.csv` active mask / repair | 可能改接到 active REG 的 `FW Diff idx` |
| 手動 override | 使用使用者指定的 `CAD Output FW Diff idx` |
| `StrictUnique` auto mode | 同 IC 內重複 diff 會保持 unresolved，等待修正 |

相關程式入口：

- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.LayerFiltering.cs`
- `src/FreeformHelper.Application/Services/CadBestMatchSeedService.cs`
- `src/FreeformHelper.Application/Services/CadOutputFwDiffIndexAssignmentService.cs`
- `src/FreeformHelper.Application/Settings/CadOutputFwDiffAutoMode.cs`

## UI 顯示原則

一般操作應該優先顯示：

```text
CAD #display / REG id / FW Diff idx
```

診斷、匯出、runtime query、project/edit trace 才補充顯示：

```text
CAD id / CAD Output FW Diff idx / internal row-col
```

不要把 `CAD id` 當成空間順序，也不要把 `REG id` 當成 FW diff identity。
