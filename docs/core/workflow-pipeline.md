# Workflow Pipeline（非 Notch 架構）
最後更新：2026-08-08

## 目的
- 把 Step 依賴、下游失效清理（invalidation）、以及 Load Project 後的自動重播規則集中。
- 降低 ViewModel 中散落的條件分支，讓後續 AI/人員能以單一規則表追蹤流程。

## 目前單一來源
- `src/FreeformHelper.UI/Services/WorkflowPipelineService.cs`

## 規則一：下游失效（invalidation）
- Step1 變動：清 Step2~Step5
- Step2 變動：清 Step3~Step5
- Step3 變動：清 Step4~Step5
- Step4 變動：清 Step5

## 規則二：右側 Step 展開焦點
- `BuildExpansionState(activeStep)` 決定僅展開目前 step，其他收合。
- VM 不再手寫多組 `IsStepXExpanded = ...`。

## 規則二補充：成功後主流程
- Step1 成功後展開 Step2。
- Step2 成功後展開 Step3，因為 Step3 目前包含會影響 Simulation / Step5 C export 的 compensation guards。
- Step3 成功後展開 Step4。
- Step4 成功後展開 Step5。
- Step4 仍是 optional diagnostics；Step3 不是單純 debug step，不能在主流程中直接跳過。

這裡描述的是 1.3.0 production current behavior，不是最終 operator-flow contract。`WorkflowStepGateService` 已允許 Step5 不依賴 Step4，故 R13.305 的 target 為 Step3 成功後直接展開 Step5；Step4 mapping diagnostics 與 Step6 validation 改由未編號 Diagnostics/Inspector 入口承接。該 UI 搬移必須等 R13.301/R13.302 的 draft/typed invalidation owner 穩定，不能在本文件校準時假裝已實作。

## 規則三：Step 執行/清除單一入口
- VM 內提供統一入口：
  - `RunWorkflowStepAsync(WorkflowStepId)`（目前支援 Step1~Step4）
  - `ClearWorkflowStep(WorkflowStepId)`（支援 Step1~Step5）
- Runtime CLI 經 `RunWorkflowStepAsync` 委派；右側 UI commands 目前仍直接呼叫各 Step handler。兩者共用相同底層 handlers 與 pipeline policy，但尚未共用同一 execution entry，且 `RunWorkflowStepAsync` 仍是 switch。Clear actions 已共用 `ClearWorkflowStep`。
- 好處：
  - 行為一致（狀態更新、step 展開、清除副作用一致）
  - 減少 CLI/UI 行為偏差造成的 debug 成本

Readiness 目前仍由 row/preview count、freeform value或summary text 推測；合法零結果可能被誤認為未執行。R13.302a 會以 revisioned `Completed/Stale` state 取代這種 content-derived gate，本文件在完成前不得把它描述為已解決。

## 規則四：Load Project 自動重播（auto replay）
- 入口：`BuildProjectLoadReplayPlan(autoReplayStep2AfterLoad)`
- 目前計畫：
  - 一定嘗試 Step1 replay
  - `AutoReplayStep2AfterProjectLoad=true` 時再嘗試 Step2 replay
- 每步 replay 都回報：
  - 是否真的重播成功（`replayed`）
  - 耗時毫秒（`elapsed ms`）
  - 跳過原因（例如設定關閉、前置條件不足）

## CLI 可觀測性（load-project）
- `query load-project --path ...` 回傳 `timings.stepReplay`：
  - `step1ReplayElapsedMs`
  - `step2ReplayElapsedMs`
  - `step1Replayed`
  - `step2Replayed`

## 後續擴展建議
- 若新增 Step6/Step7，不要在 VM 直接加 if/else；先擴充 `WorkflowPipelineService`。
- 若新增 replay 條件（例如 Step3 預覽回放），也應透過 replay plan 擴充，而不是在 `LoadProject` 流程硬編碼。
