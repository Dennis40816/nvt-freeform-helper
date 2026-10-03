# Simulation 圓柱/自由滑動輸入模型 Spec（S11.66）
最後更新：2026-04-06  
狀態：Draft v1（可進入 prototype）

## 1. 目標
- 在不破壞現有 `frame slider + playback + CSV` 單一路徑前提下，新增「連續滑動輸入」模型。
- 使用者可以把輸入視為「在圓柱表面連續滾動」，而非只能切離散 frame。
- 所有輸入最終仍收斂成 `SimulationWorkspaceUseCase -> snapshot` 同一條投影路徑。

## 2. 非目標
- 不更動 notch v2.1/v2.2 演算法。
- 不新增第二套 simulation 數值計算器。
- 不在 UI 層自行重算 delta / histogram。

## 3. 核心概念
- `virtual timeline`: 連續時間軸（double），支援小數位置。
- `frame anchor`: 與現有離散 frame 對齊的整數節點。
- `interpolation window`: 連續位置落在兩個 anchor 間時，用同一規則做插值。
- `cylindrical wrap`: 可選擇循環（末幀接首幀）或邊界夾住。

## 4. 資料契約
- 新增 `SimulationInputMode`
  - `DiscreteFrame`（現況）
  - `ContinuousCylinder`
- 新增 `SimulationContinuousInputState`
  - `position`（double）
  - `velocity`（double）
  - `isWrappingEnabled`（bool）
  - `interpolationMode`（`Linear`/`Hold`）
  - `sensitivity`（double）
- 新增 `SimulationInputSample`
  - `source`（mouse/wheel/touch/keyboard）
  - `delta`
  - `timestampUtc`

## 5. UI 行為
- 保留現有 frame slider；在 `ContinuousCylinder` 模式下：
  - slider 顯示連續 position（可小數）。
  - 右側顯示 `anchor low/high` 與 `blend ratio`。
- 新增輸入區：
  - `Input mode` 切換
  - `Wrap EN` 切換
  - `Interpolation` 切換
  - `Sensitivity` 數值
- 所有變更只更新 input state，最後呼叫同一個 snapshot refresh path。

## 6. 投影與效能契約
- 主 thread 只做：
  - 讀取 input state
  - 排程 snapshot 更新
  - 更新可見 UI state
- 插值與快取：
  - 若 position 落在同一區間且來源 frame 未改變，優先重用前次 interpolation cache。
  - cache key: `frameLow/frameHigh/ratio/viewMode/colorMode/areaFilter/revision`。
- 不允許：
  - 因為連續滑動新增第二套 AA 投影器。
  - 在每個 pointer move 全量重建所有非可見 detail panel。

## 7. Prototype 範圍（Beta 0.3）
- 只做 interaction prototype + state contract：
  - mode 切換
  - position 更新
  - wrap/hold/linear 行為
  - snapshot 仍走既有路徑
- 不做：
  - 物理慣性模型
  - 多點觸控手勢庫
  - 新動畫系統

## 8. 驗證標準
- `ContinuousCylinder` 開啟後，AA 值可連續變化，且不跳到錯誤 frame。
- 關閉後可回到離散 frame 模式，數值與現況一致。
- `Wrap EN` 開啟時，超出邊界會循環；關閉時夾住。
- histogram / hotspot / inspector 仍與同一份 snapshot 同步。
- 不得出現第二套 simulation truth source。
