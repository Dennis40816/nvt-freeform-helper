# 行為盤點
最後更新：2026-08-10

目前文件基線：FreeformHelper 1.3.0 current-state audit，production evidence commit `ef08945`。歷史 stale-check 保留決策脈絡；R13.005 校準段落才是 current owner/result/reader 狀態。

## S10.10 stale-check（2026-03-08）
- DXF edit 行為已擴充為完整 project state：
  - `Save Project` 會保存 DXF edit 的 `Hidden / Combined / Layer move` 狀態。
  - `Load Project` 會回放同一組 DXF edit 狀態，不再只還原 hidden。
- 左側 `DXF edits` 契約：
  - `Hidden / Combined / Moved` summary badge 為可點擊入口，會打開 `DXF edit details` modal。
  - `Reset all DXF edits` 會重置 hidden、combined、layer move 三類變更，不再只還原 hidden。
  - `Remove combined` 只清除目前選中的 combined group，不會全清。
- `DXF edit details` modal 契約：
  - 依 `Hidden / Combined / Moved` 類型分頁顯示目前被修改過的 CAD pad。
  - 每列可 `Focus` 到 AA/canvas，也可做單筆還原：
    - `Hidden` → restore hidden
    - `Combined` → clear combine group
    - `Moved` → restore original layer
- `Layers On / Off` 契約：
  - 這是 aggregate 狀態顯示 + batch action，不是單一 layer toggle。
  - `On` 只有在所有 layer 都可見時視為 active。
  - `Off` 只有在所有 layer 都隱藏時視為 active。
- `Export Notch Rows` hide / restore 契約：
  - 面板可暫時 `Hide panel (inspect AA)`，主畫面繼續操作 AA。
  - 恢復方式固定為：
    - workspace 右上角 `Restore`
    - `Ctrl+Shift+E`
  - 隱藏當下會顯示 top toast 提示 restore 方式。

## S11.18 stale-check（2026-03-10）
- `DXF edit details` 的具體 UI host 已固定為 `DxfEditChangeListWindow`：
  - 使用者入口只有左側 `DXF edits > Hidden / Combined / Moved` summary badge。
  - window 本身只承接現有 DXF edit project state 的投影與 restore/focus 動作，不應自帶另一套 DXF edit 邏輯。
- `CAD load overlay` 契約已固定為單一路徑：
  - 使用者可見入口只有 `Open DXF` 與 `Load Project`。
  - 兩條 workflow 都必須包在 `RunWithCadLoadCanvasOverlayAsync(...)` scope 內，先讓 UI 進一幀 render，再做 DXF load/apply/rebuild。
  - 顯示狀態只看 `IsCadLoadCanvasOverlayVisible`；nested scope 必須維持 overlay 直到最外層結束。
  - 呈現容器固定為 `CadLoadSpinnerProcessHost` 啟動的透明 top-level `CadLoadSpinnerWindow`（獨立 helper process），主視窗只負責透過 `IsCadLoadCanvasOverlayVisible` 發送 show/hide。
  - spinner 可視性 guard 需覆蓋「overlay 可見 + spinner dash offset 持續變化 + scope 結束後視窗隱藏」。

## S11.72 stale-check（2026-03-22）
- `SeeRegular.csv` regular visibility mask 契約已改為 `Step 1 · Geometry match` 顯式匯入：
  - 入口固定為：
    - `Import Regular Visibility Mask (SeeRegular.csv)`
    - `Use Regular Visibility Mask`
    - `Clear mask`
  - 不再於 `Load Project` 後自動從同資料夾尋找或載入 `SeeRegular.csv`；只能還原 project snapshot 中明確保存的 mask path / embedded mask。
- regular visibility mask 狀態屬 project-restored workflow state：
  - `Load Project` 可還原 project snapshot 中的 mask path / toggle；embed project 會優先使用 embedded SeeRegular mask，path 失效時仍可還原。
  - `Open DXF` 成功後會清掉先前手動選取的 mask 路徑與已載入結果。
  - `Step 4` 直接讀取目前 workflow snapshot mask；Notch generation 消費 mask 影響後的 CAD Output FW Diff assignment。
  - `Simulation` 同時使用共同生成的 notch table，並把 regular grid IDs 與 active mask 取交集形成 active surface；目前不能寫成「Simulation 只看 table」。
- `RegularVisibilityMaskSummary` 契約：
  - `not loaded`：尚未手動匯入
  - `loaded but disabled`：已匯入但 toggle 關閉
  - `enabled`：已匯入且正在約束 `Step 4 / Simulation`
  - `incompatible / failed`：CSV 尺寸或解析失敗，僅作診斷，不應默默 fallback 為已啟用

## S11 draw pipeline rewrite（2026-03-08）
- `PadCanvas` 單幀資料模型已收斂為三層：
  - `PadCanvasViewFrameSnapshot`
    - 單一來源：zoom / pan / bounds size / world viewport / world-to-screen transform。
  - `visible draw cache`
    - query cache：只受 viewport 與 CAD/Regular data revision 影響。
    - draw-list cache：只受 query revision、selection/highlight/resource、decimation 條件影響。
  - `PadCanvasRenderFrame`
    - 每幀 render 開頭解析出 `showRegular / showCad / lowDetail / secondaryVisualsDeferred / opacity / outline width`。
- invalidation / refresh 契約：
  - `RequestViewRefresh(...)` 與 `RequestVisualRefresh(...)` 共用 coalesced queue。
  - `ViewChanged` 只在需要的路徑 raise；visual-only 變更不應同步觸發 `ViewChanged`。
  - selection/highlight/resource 變更只會失效 draw-list cache，不會重查 viewport candidates。
  - CAD/Regular data 或 viewport 變更才會失效 query cache。
- navigation hot path 契約：
  - zoom / pan / fit / focus 期間允許短暫 `transient low-detail`。
  - `diff index overlay`、`notch preview overlays`、`match/notch ratio labels`、`axis labels`、`hover debug overlay`
    - 在 active navigation window 期間可延後繪製。
    - navigation 結束後會由 coalesced refresh 補畫，最終結果需一致。
  - `selection overlay`、box-selection rectangle、核心 CAD/Regular geometry 仍屬主繪製熱路徑。

## S11.8 Notch 2.2 per-CAD result path（R13.005 校準）
- `NotchV22CompensationResult` 是目前 UI/RuntimeQuery per-CAD 數值 kernel 的主要結果：
  - `ToRegularRatio`
  - `ToFullRatio`
  - `CombinedRatio`
  - `IsToFullEnabled`
  - `Stage3Area`
- `IsToFullEnabled` 契約：
  - 只看 `RegularDebugInfos.Any(info => info.IsToFullApplied)`。
  - 不可再用 `IsToFullBoundaryCandidate` 代替。
- `Stage3Area` / `ToFullRatio` 契約：
  - `Stage3Area = cadArea + Σ(max(0, ReachableArea - OverlapArea))`，僅統計 `IsToFullApplied` regular。
  - `ToFullRatio = max(1.0, Stage3Area / cadArea)`。
  - `CombinedRatio = ToRegularRatio * ToFullRatio` 是 CAD-level diagnostic；v2.2 row payload 不可直接用它當 source-wide gain。
- `Target allocation` 契約：
  - `NotchV22TargetAllocationService` 必須讀 `compensation.Stage3Area`，不得自行用 `cadArea * ToFullRatio` 以外的路徑重算。
  - `Current (Gain)` 必須使用 per-target Stage3 coverage：`stage3EffectiveAreaOnTarget / targetRegularArea`。
  - `Conservative (No Gain)` 必須使用 per-target source overlap coverage：`overlapAreaOnTarget / targetRegularArea`，ToFull 不直接放大 target amount。
  - `ToRegularRatio = Σ(overlapArea / targetRegularArea)` 只保留為 CAD-level diagnostic，不可再先合成整顆 CAD 的 `R` 後乘到每個 target share。
- `Per-IC CAD allocation pool` 契約：
  - ordinary admission 必須使用 `NotchAllocationService` 與 `BuildAllocations` 共用的 Q7-positive predicate；raw overlap 若量化為 Q7=0，不構成該 IC 的 ordinary membership。
  - IC 不符須在 geometry intersection 前排除，membership query 可於第一個正 Q7 allocation short-circuit，不建立完整 allocation／candidate。
  - cross-IC CAD 可進入每個實際具有正 Q7 allocation 的 IC pool，不得只依 primary IC assignment 排除。既有 empty-pool all-visible 與 selected-target inclusion fallback 仍保留，不能把 ordinary predicate 誤寫成無例外的 `iff`。
  - 實際 pool 依 CAD ID 排序；cache identity 的 pool component 使用 ordered IDs 並包含 count，same-count member swap 必須失效。
  - A `[0,5]`、B `[5,15]` fixture 的 UI `ToRegular / ToFull / Combined` 由舊 primary-IC-only 路徑的 `0.5 / 2 / 1` 刻意修正為與 generator 相同的 `0.5 / 1 / 0.5`。
- `Resolved result` 契約：
  - `NotchV22ResolvedResultService` 是 UI/query 的 per-CAD 投影入口；不是 repository-wide export source-of-truth。
  - `PadInspector`、`Notch preview`、`Notch detail` 與 `RuntimeQuery query notch` 只讀 resolved result，不得各自重建 `enabled / stage2 / stage3 / target allocation`。Detail 的 raw overlap allocation／Q7／freeform仍是既有 view projection，不屬 target-allocation resolved model。
  - current boundary：normal `CadAllocation` export已有獨立的Application-owned output-request-neutral candidate batch；matching export不會因版本、threshold、`NullValue`或target guard/cap而重建candidate。Phases 1～3 resolution與phase 4 final projection已分離；相同CAD/grid/Step3/computation identity與generation epoch的並行Export／Simulation會共用一個in-flight resolution task，每位caller仍以自己入口凍結的final request投影。fingerprint命中仍做完整computation-settings equality，fault會移除task供重試；final-only變更保留task/batch，完整失效則推進epoch、detach舊task並拒絕其store/publish。Live UI的guard/cap-only變更亦保留Step3 revision、per-CAD sparse identity與此batch，只清目前Step5 table／validation並通知Simulation source revision；完整Step5 clear才evict batch與重設operation state。Generation request會凍結cache generation epoch、Step5 final-projection revision與Simulation source revision；completion只有在三者仍current時才可commit row count、last table、progress／flow及caller continuation，Simulation session亦保存該次accepted source revision。V22 C exporter與simulation另共用`NotchV22FirmwareProjector`的final node normalization；batch request可reuse目前單一選取CAD的exact resolved instance，或在cold resolution後至多回傳一份identity-bound result並經currentness驗證promotion回既有per-CAD owner。batch仍只保存compact candidate evidence，不保存全部CAD的polygons/debug result。R13.101e讓normal generator／UI compensation先建立完整`NotchV22CompensationContext`，allocation／boundary／active／strict-query／policy evidence不再由caller以partial nullable組合控制stage順序；舊public API只作adapter，明確空allocations不回退geometry，final-output state仍不進context。`LegacyRegularAnchor`已以owned request固定同次generation的versions／threshold／`NullValue`／`LenScale`，並以同一明確V21／V22 switch供generation與eligibility使用；未使用的strategy registry／injection seam已刪除，但request-specific table cache仍在。R13.101c-2／R13.102／102a-2與R13.103保持open。Public compatibility `NotchDetailUseCase.Build`仍可建立unanchored result，但normal command不再使用它。

## S11.9 Notch 2.2 shared display projection（2026-03-09）
- `NotchDisplayProjector` 是 Notch 2.2 顯示字串的單一路徑：
  - normal anchored target-coverage的combined ratio／overflow只格式化`NotchV22TargetCoverageProjection`；target summary／compact line／card role只消費`EmittedTargets`的`(IcIndex, DiffIndex)` membership，target admission、逐group rounding與risk不得在UI重算。
  - unanchored compatibility與非target-coverage modes的舊all-target ratio fallback由`NotchV22TargetAllocationPolicy`擁有；`RawCombinedPercent == null`時target summary／line／role維持既有strict-only顯示，diagnostic target rows仍顯示全部targets。
  - `ToRegularRatioText`
  - `ToFullRatioText`
  - `CombinedRatioText`
  - `ToRegularValueText`
  - `ToFullValueText`
  - `CombinedValueText`
  - `To Full reason`（short / full）
  - `Stage3AreaText`
  - `TargetAllocationSummaryText`
  - `TargetAllocationLines`
  - `OwnerSummaryText`
  - `formatted diagnostics`
- `PadInspector` 契約：
  - `Reason / To Regular / Combined` 必須讀 shared display projection，不可再自行 parse `diagnostics` 或重組 ratio text。
- `PadInfo` 契約：
  - `CadPadInfoViewModel` 的 notch 區塊（ratio / reason / stage3 / target summary / owner summary）必須讀 shared display projection。
- `Notch detail` 契約：
  - `NotchDetailUseCase` 需先建 shared display projection，再交由 `NotchDetailViewModel` 呈現。
  - `NotchDetailViewModel` 不應自行重算 ratio text。
- `RuntimeQuery` 契約：
  - `query pad` / `query notch` 可保留 raw numeric payload。
  - 若輸出 display-oriented text，必須透過 shared display projection 產出，不可各自組字串。

## R13.005 result / reader matrix

目前存在兩個 source-of-truth result scope，另有一個由 generated table 派生的 Simulation review projection；它們尚未合成一個 repository-wide model：

| Result scope | Current owner / entry | Readers | Current second-pass debt |
| --- | --- | --- | --- |
| Per-CAD diagnostics | `NotchV22ResolvedResultService`；root VM revision/cache；combined-overflow與normal target effective membership由`NotchV22TargetAllocationPolicy`投影 | Step3 preview/Canvas、deferred Inspector、CAD PadInfo、Notch detail、RuntimeQuery `pad/notch/notch-stage/multi-owner` | normal UI readers與`query multi-owner`已消費 revisioned per-CAD result；public compatibility `NotchDetailUseCase.Build`仍保留 unanchored standalone行為；`GetCrossIcOwnerSharesForRegularPad`仍從 geometry推導，且display projector仍會 parse diagnostics（R13.102/104） |
| Generated firmware artifact | `GenerateCurrentNotchTableAsync` -> `NotchExportGenerationCacheService` -> `NotchTableGenerator`；`NotchV22FirmwareProjector`擁有final V22 node normalization | UI Step5 export、Runtime export adapter、Simulation workspace、Step6/Runtime validation | normal `CadAllocation` cache保存compact output-request-neutral candidate batch；phases 1～3由identity/epoch-bound task解析一次，並行full-table consumers共用task後各自執行phase 4 final projection。final-only變更不detach task，完整失效detach並拒絕舊store；fault可重試，fingerprint collision仍以完整settings identity拒絕錯誤join。Generation completion另以captured cache epoch、final-projection revision與source revision做current check。V22 exporter與simulation已共用`SourceRows`／`NodesByIc` projection；selected sparse bridge讓batch warm reuse或cold回傳至多一份CAD + anchor-keyed resolved result，驗證current後promotion回同一per-CAD owner，且不保留其餘candidate的完整diagnostics。R13.101e已把normal generator／UI compensation的完整evidence與policy收斂為單一context／compute path；compatibility adapter與explicit-empty修正不改final request、row projection或schema。V21／V22 final projectors與Step6/Runtime validation共用 Application-owned `0..65535` null-sentinel guard；invalid sentinel在VM／direct UseCase fail-fast，actual IPC以既有`IPC_ERROR` envelope傳回exact message，default-sentinel C不變。Legacy同次generation已共用owned request，callback mutation只影響下次；generation／eligibility共用固定V21／V22明確派送，registry／injection seam已移除。`LegacyRegularAnchor`仍保存request-specific `NotchTable`與compatibility boundary。R13.101c-2／R13.102／R13.102a-2／R13.103保持open |
| Simulation review | `NotchApplySimulationReviewUseCase` -> `NotchApplySimulationResult` / `SimulationSafetyAuditResult`；EMS after-cap decision由`SimulationSafetyAuditService.IsEmsAfterCapViolation`擁有，workspace／overview／export base severity、no-cell cap文字、Copper replay severity及Notch cap／target-cap guidance分別由`SimulationSafetyTextProjector.BuildStatusText`／`FormatEmsAfterCap`／`BuildReplayStatusText`／guidance builders擁有；overview與physical-only export summary共用`BuildPhysicalAuditSummaryText` evidence | `SimulationWorkspaceUseCase`、Simulation VM/overlay/validation、`CopperPillarPathReplayArtifactService`、export selection safety badge／summary、Step 3／5 Notch guidance、Runtime Query simulation regular snapshot、cell／overview／high-risk projections | 此scope是generated table的合法projection；active-surface mask是額外explicit input，不是重算Notch geometry。R13.104a-1已收斂EMS predicate，a-4至a-8已收斂workspace／overview／replay／export status與physical evidence，a-9讓active Step 3／5 guidance共讀current overview cap，a-10再讓active Step 3 target-cap help使用current target／EMS caps、Settings使用current draft target／default EMS cap；兩者共用同一template。EMS-blocked dialog全文保持既有契約，artifact row同步JSON-ignored global-flow fact，serialized schema不變。Overview-only stale／unavailable仍留在boundary，`HasRisk`只表示EMS danger、`NeedsAttention`另含physical／stale，`MarginToCap`只作數值evidence；其他hard-coded cap與cell／high-risk／export／replay display/text debt仍待R13.104 parent。 |

Reader 判定：

- `Validation` 只把 generated `NotchTable` 投影成 DIRECT/IN/OUT trace，未重算 compensation，屬可接受 single-result reader。
- `Runtime export` 經 `RuntimeQueryUseCase` 委派同一 UI `ExportNotchCommand`，屬可接受 multi-entry/single-export-path。
- `query multi-owner` 已由R13.102b-2收斂為每個request只取得一份依current setting或override建立的revisioned `NotchV22ResolvedResult`；visible-CAD guard避免fallback計算，metadata snapshot不建立default-threshold result，rows／summary／rule trace全由`resolved.Compensation.RegularDebugInfos`投影。R13.102a-2b-2再讓目前selected result以bounded bridge接入full batch session；Runtime payload schema與query entry不變。
- `GenerateCurrentNotchTableAsync` completion只可發布入口所捕捉epoch／projection／source identity仍current的結果；current completion才更新cache row count、last generated table與外層workflow，accepted Simulation session沿用該次captured source revision，不得在completion時重讀current revision。
- CAD-level diagnostic `CombinedRatio`、firmware `CombinePercent`與target-coverage projection仍是不同欄位：normal anchored display讀共享final-eligibility projection；unanchored／non-target compatibility顯示則保留Application-owned all-target fallback。不得因名稱相近而互換raw values。

## S4.6 stale-check（2026-03-05）
- 本輪確認為「結構重整、行為不變」：`FreeformHelperViewModel.Selection*`、`NotchV22CompensationService*` 已拆 partial，但對外契約不變。
- deferred app settings 契約仍成立：
  - `Load Project` 後進 deferred：`MarkProjectLoadedForAppGeneralPersistence()`（`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`）
  - `Save Project` 成功才 flush：`FlushDeferredAppGeneralSettingsIfNeeded()`（`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs` / `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.AppSettings.cs`）
- `Ctrl+S` 契約仍成立：
  - shortcut handler：`src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs`
  - 呼叫 `SaveProjectAsync()` 並顯示 top toast：`SaveProjectFromShortcutAsync(...)`（同檔）
- runtime query single-entry 契約仍成立：
  - IPC server 入口仍透過 `RuntimeQueryUseCase.ExecuteAsync(...)`：`src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`

標記說明：
- [重建] 觸發 TriggerGridRebuildAsync / RebuildGridAsync
- [視角] 觸發 FitToContent / ZoomAt / pan 變更
- [清選取] 清除目前選取
- [設選取] 以程式設定選取
- [Undo] 推入或執行復原
- [狀態] 更新 StatusText 或 console/log 顯示

備註：
- RebuildGridAsync 會清除選取，除非 SuppressSelectionClearOnRebuild 為 true。
- RebuildGridAsync 會更新 StatusText，且可能依 requestFit/初次載入而呼叫 FitToContent。

## 已完成功能基線（2026-02）

### 匯入、儲存、專案一致性
- Save/Load 已收斂到 ProjectDocument/Snapshot 路徑，UI 設定 roundtrip 不需逐頁手動對應。
- Build 輸出與 log 目錄已集中到 `build/`，降低 `src/` 汙染。
- Schema migration 採 append-only（保留舊欄位，不破壞舊專案檔）。
- `Load Project` 後 app-level 設定改為 deferred persistence（不立即寫入 app settings）。
- deferred app settings 會在下一次 `Save Project` 成功後一次 flush。
- DXF edit 狀態也屬 project roundtrip 範圍：`hidden / combined / layer move` 會隨 project save/load 還原。
- `Open DXF` / `Load Project` 共用 CAD load overlay scope；視覺呈現是獨立 spinner helper 視窗（透明 top-level + centered spinner）。

### DXF 與 Grid
- Regular grid 已支援由指定 bound layer 建立，且可與 layer 可視狀態分離。
- Layer 篩選、DXF edit（hide/restore）與 rebuild 已串接；可輸出 visible/all DXF。
- 左側 `Layers On / Off` 已收斂為 aggregate 狀態 + batch action；active 態代表「全部 visible」或「全部 hidden」。
- 左側 `DXF edits` summary badge 會開 `DxfEditChangeListWindow`（`DXF edit details` modal），支援逐筆 focus / restore / clear group / restore layer。
- Grid channel 上限已解除（不再限制 X<=255 / IC<=16）。

### Mapping、診斷、人工覆寫
- CAD↔Regular mapping 已有獨立診斷視窗（摘要留在右側，詳細移至 report window）。
- 診斷已支援 best/second/margin 與 top candidates，並可 locate、apply/clear override。
- Overlap check 已改為非阻塞流程，含進度、highlight、selection 與詳細報告。

### Freeform 與 Notch
- Freeform 自動判定已支援 XWay/YWay，並可選擇是否啟用 XYWay 自動判定。
- Legacy notch 流程保留；Notch mode 已可在新舊流程間切換（作為重構過渡）。
- AFEIndex 已退場，核心鍵收斂為 `(IcIndex, DiffIndex)` 與幾何分配。
- Step3 overlay 已收斂為 layer policy（`ToRegularLabel / ToFullSeed / ToFullCandidate / ToFullFinal`）。
- Step3 stage 顯示契約為互斥：
  - `Stage 1` 只顯示 `ToFullSeed`
  - `Stage 2` 只顯示 `ToFullCandidate`
  - `Stage 3` 只顯示 `ToFullFinal`
  - 不再累積顯示前一 stage overlay。
- Step3 顯示開關分離為 `Show To Regular labels` 與 `Show To Full overlay`（僅影響 AA 顯示，不改計算）。
- Step1 match 結果若更新且已有 CAD 選取，會即時刷新 Step3 preview（不需手動重按 Step3）。
- Step3 layer legend 會顯示 `Active/Hidden` 狀態，且 stage/autoplay 控制僅在 To Full overlay 可視時啟用。
- Step6 Validation quick trace 已改為 `DIRECT / IN / OUT` 分組面板，沿用 `NotchValidationTraceService` 同源資料。
- Step3 To Full gate 已拆分 rule engine（可切 legacy），`rule trace` 可開關；`query notch` / `query multi-owner` 可回傳 per-regular trace。

### Step3 Recompute / Compensation 觸發時機（N0.4）
- 名詞定義：
  - `補償計算`：`NotchV22CompensationService.Compute(...)` 路徑（To Regular / To Full ratio 與 stage overlay 來源）。
  - `Recompute selected (Step 3)`：`ExecuteStep3PreviewWorkflow()`，會先清 Step3 downstream，再重算目前選取 CAD 的 preview。
  - `顯示開關`：只影響 canvas overlay 可見性，不改補償演算法結果。
- 自動觸發（不需按 Recompute）：
  - 選取變更（CAD）：`ApplySelection(...)` 會刷新 notch preview（單選即時、多選 deferred）。
  - Step1 Match 完成且目前有 CAD 選取：會即時刷新 Step3 preview。
  - Step3 計算開關/參數變更（`EnableToRegular`、`EnableToFull`、`EnableToFullRuleEngine`、`EnableToFullRuleTrace`、`ToFullStrictOverlapPercent`）：
    - invalidation cache + downstream invalidate + refresh preview（若前置條件成立）。
- 只影響顯示（不重算）：
  - `Show To Regular labels`
  - `Show To Full overlay`
  - `NotchPreviewVisualizationStep`（Stage 1/2/3）
  - `NotchPreviewAutoPlayEnabled` / interval
  - 上述只會觸發 overlay visibility 通知與 `CanvasHost.Invalidate()`。
- `RefreshNotchCanvasPreviewCommand` 目前沒有 production XAML binding；它只供 CLI/internal/test entry 使用。正常使用者流程沒有另一個可點擊的手動 Recompute button，一般參數／選取切換由自動刷新承接。
- 主要程式入口：
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.WorkflowSteps.cs`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`

### Step5 Notch row 契約與 no-op row（U0.3）
- no-op row 定義（v2.2）：
  - `anchor_diff = D`
  - `combine = 100`
  - `target1 = nullDiff 且 ratio1 = 0`
  - `target2 = nullDiff 且 ratio2 = 0`
  - 效果是「維持 anchor diff，不做 target switch」。
- 為何需要存在：
  - 匯出列與 diff/IC 對齊：下游可以拿到完整 anchor mapping，不需要自行補洞。
  - 相容既有 parser：保留固定資料契約，避免下游以「缺列」誤判資料遺失。
- UI 顯示策略（Select notch rows）：
  - 預設 `Rows shown = Transfer-only`，隱藏 no-op row，聚焦有實際轉移的列。
  - 可切到 `All rows` 觀察完整輸出契約（含 no-op row）。
  - 匯出內容以目前可見且勾選列為準；預設行為降低閱讀噪音，但保留完整檢視入口。

### UI 結構與一致性
- 1.3.0 current 設定區仍呈現 numbered Step1~5 與 Step6 validation；Step4/6 是 diagnostics，但尚未搬出 normal-flow surface（R13.305a debt）。
- 右側 Panel 已分頁化為 `Settings / Inspector`，Inspector 與 Step 設定不再混在同一滾動內容。
- `RightWorkflowPanel > General Settings` 保留可編輯快捷欄位（Grid/AA/source/alignment），並與 `SettingsWindow` 共用 single source。
- `WorkspaceHeader` Display popup 保留高頻即時視覺項；`SettingsWindow` 只應承接 operator-facing 必要設定，不以完整 persistence schema 都可編輯為目標。
- 重要控件已 token 化（色彩、間距、邊框、陰影），並加入布局守門測試。
- ScrollViewer/Scrollbar 已改為統一樣式，減少頁面間手感不一致。
- Terminal contract 已固定：
  - `ConsolePanel` 為 `AvaloniaEdit TextEditor` 單一路徑，不再允許 `TextBox fallback`。
  - AvaloniaEdit theme 維持 app-level include，不可再搬到 workspace lazy-load。
  - terminal 預設展開，初始尺寸沿用 `ConsolePanelRowHeight=180`、`ConsolePanelMinHeight=120`。

## Commands

### FreeformHelperViewModel (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs)
- OpenDxfCommand → OpenDxfAsync (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs)
  - [狀態] [重建] [視角] [清選取]
  - 也會更新 layer toggles、CadPads、RegularPads、MarkUnsaved。
  - `CadLoadWorkflowService` 內部會以 `RunWithCadLoadCanvasOverlayAsync(...)` 包住 import / apply / rebuild。
- RebuildGridCommand → TriggerGridRebuildAsync(requestFit: true)
  - [重建] [視角] [清選取] [狀態]
- MatchCommand → MatchAsync
  - [狀態]（CanvasHost.Invalidate）
- AutoDetectFreeformsCommand → AutoDetectFreeformsAsync
  - [狀態] [統計]（更新 Freeform stats table，CanvasHost.Invalidate）
- SaveProjectCommand → SaveProjectAsync (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs)
  - [狀態]（寫入專案、清除 HasUnsavedChanges）
- LoadProjectCommand → LoadProjectAsync
  - [狀態] [重建] [視角] [清選取]
  - 會重載 DXF、設定、layers，並套用 UI snapshot。
  - 若 project 含 DXF reload，DXF load/apply/rebuild 也必須走同一條 canvas overlay scope。
- ExportNotchCommand → ExportNotchAsync
  - [狀態]
- FitCommand → CanvasHost.FitToContent
  - [視角]
- ClearSelectionCommand → CanvasHost.ClearSelection
  - [清選取]
- UndoCommand → Undo (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Undo.cs)
  - [Undo] [狀態]
- SetFreeformNone/X/Y/XY → SetFreeformForSelection (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Overrides.cs)
  - [狀態]（CanvasHost.Invalidate, MarkUnsaved）
- UseSelectionForManualRangeCommand → ApplySelectionToManualRange (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.ManualSizing.cs)
  - [設選取]，且可能經由 ApplySizingToCells 觸發 [重建] [視角] [狀態]
- SelectAllLayersCommand / DeselectAllLayersCommand → SelectAllLayers / DeselectAllLayers
  - 若 RecalcBoundsOnLayerFilter=true，會 [重建] [視角]
  - LayerToggle 改變時會 MarkUnsaved
- CheckDxfQualityCommand → CheckDxfQuality (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfQuality.cs)
  - [狀態]

### ShellViewModel (src/FreeformHelper.UI/ViewModels/ShellViewModel.cs)
- ShowWorkspaceCommand / ShowHowToUseCommand / ShowDevCommand
  - 切換 CurrentViewModel 與頁面可見狀態
- ClearConsoleCommand
  - [狀態]（清空 AppLogStore）
  - 啟動時 terminal 預設展開；console 全文字串由 ring buffer tail 建立，不依賴 UI collection flush。

### Pad Info ViewModels (src/FreeformHelper.UI/ViewModels/PadInfoViewModels.cs)
- CadPadInfoViewModel.ApplyChangesCommand → SetCadPadCustomValues
  - [Undo] [狀態]（MarkUnsaved）
- CadPadInfoViewModel.DiscardChangesCommand
  - 無外部副作用
- RegularPadInfoViewModel.ResetPadSizeCommand → ResetSizingForCells
  - [重建] [視角] [Undo] [狀態]
- RegularPadInfoViewModel.ApplyChangesCommand → ApplySizingToCells
  - [重建] [視角] [Undo] [狀態]
- RegularPadInfoViewModel.DiscardChangesCommand
  - 無外部副作用

### Dialog ViewModels (src/FreeformHelper.UI/Views/*.axaml.cs)
- CadPadDialogViewModel Save/Cancel
  - 僅回傳 dialog result
- RegularPadDialogViewModel Save/Cancel
  - 僅回傳 dialog result

## 設定與屬性變更觸發

### FreeformHelperViewModel.Settings.cs
- OnActiveAreaWidthChanged / OnActiveAreaHeightChanged
  - [重建] [視角] [狀態]
- OnPanelBiasXChanged / OnPanelBiasYChanged（當 IsPanelAlignment）
  - [重建] [視角] [狀態]
- OnCadLineOpacityChanged / OnRegularLineOpacityChanged
  - [重建] [狀態]
- OnSelectedScanOrderOptionChanged / OnSelectedScanOrderChanged
  - [重建] [狀態]
- OnSelectedGridAlignmentOptionChanged / OnGridAlignmentModeChanged
  - [重建] [視角] [狀態]
- OnXChannelsChanged / OnYChannelsChanged / OnCascadeNumChanged / OnGridPaddingPercentChanged
  - [重建] [視角] [狀態]
- MatchThreshold
  - [視覺] [狀態]（只重畫 unmatched highlight；不重建 grid、不改 links）
- `Mode` / centroid fallback / `NearestK`
  - [相容] current overlap matcher 不讀取；無 normal-flow editor 或 live ViewModel owner，僅由 project persistence adapter 原值 round-trip
- OnRecalcBoundsOnLayerFilterChanged
  - [重建] [視角] [狀態]（MarkUnsaved）
- OnShowCadChanged / OnShowRegularChanged / OnHighlightUnmatchedChanged / OnHighlightFreeformChanged / OnColorCadByAreaChanged
  - SyncWorkspaceToggle（UI）+ [Undo]（UndoHooks）
- OnShowNotchCanvasPreviewChanged / OnShowNotchToRegularLabelsChanged / OnNotchPreviewVisualizationStepChanged
  - 更新 overlay visibility + 觸發 CanvasHost.Invalidate（AA overlay 即時刷新）
- OnEnableToFullChanged
  - 更新 `CanShowToFullPreviewToggle` + overlay visibility + Step3 downstream invalidation + 即時重算 preview
- OnEnableToFullRuleEngineChanged / OnEnableToFullRuleTraceChanged
  - Step3 compensation cache invalidation + downstream invalidation + 即時重算 preview
- OnNotchPreviewAutoPlayEnabledChanged / OnNotchPreviewAutoPlayIntervalMsChanged
  - 同步 autoplay summary 文案；interval 變更時重啟 autoplay loop
- OnCadLineColorHexChanged / OnRegularLineColorHexChanged
  - 更新 CadLineColor / RegularLineColor + [Undo]
- OnRegularSelectedColorHexChanged / OnRegularSelectedColorChanged / OnRegularSelectedFillOpacityChanged
  - 更新 RegularSelectedColor / RegularSelectedFillOpacity（UI 可調選取色）
- OnCascadeSettingChanged（每 IC X/Y 變動）
  - [重建] [視角] [狀態]
- OnCascadeSettingChanging
  - [Undo]
- OnPropertyChanged（DirtySettingNames）
  - MarkUnsaved（持久化設定）

### FreeformHelperViewModel.State.cs
- OnUseLocalSizingChanged / OnIsWidthRowLocalChanged / OnIsHeightColumnLocalChanged
  - [重建] [狀態]
- OnStatusTextChanged
  - [狀態]（更新 StatusDisplayText/HasStatusDisplay）

### FreeformHelperViewModel.ManualSizing.cs
- OnManualRowsRangeChanged / OnManualColsRangeChanged / OnPendingColumnWidthChanged / OnPendingRowHeightChanged
  - TryApplyManualSizingAsync
  - 當 ranges 清空時 [清選取]
  - 可能同步選取 [設選取]
  - ApplySizingToCells 時 [重建] [視角] [狀態]

### FreeformHelperViewModel.UndoHooks.cs
- 各種 *Changing hook（toggles, styles, grid, match, notch, manual sizing）
  - [Undo]（TrackUndo）

## View / Control 事件

### FreeformHelperView (src/FreeformHelper.UI/Views/FreeformHelperView*.cs)
- OnAttachedToVisualTree（Pickers.cs）
  - 設定 CanvasHost、訂閱 InteractionState、ConfigureFilePickers
  - EnsureInitialGrid → [重建]
  - EnsureInitialFit → [視角]
- OnTopLevelKeyDown（FreeformHelperView.axaml.cs）
  - Ctrl+Z → UndoCommand [Undo] [狀態]
  - Ctrl+S → SaveProjectAsync（儲存結果顯示 top toast）[狀態]
  - Delete → DeleteSelectedCadPadsCommand（僅非文字輸入焦點時）[狀態]
- F / Ctrl+A → 全域轉發到 Fit / SelectAll（僅非文字輸入焦點時，且非 canvas 來源）[視角]/[設選取]
  - Ctrl +/-（console focus）→ ConsoleFontSize 變更
  - 文字輸入焦點（TextBox/ComboBox/NumberScrubber）時不處理全域捷徑
- OnRootPointerPressed
  - 點外部關閉 pad info / 否則 focus canvas
- WireCanvasEvents（Canvas.cs）
  - SelectionChanged → InteractionState.SetSelection [設選取]
  - ViewChanged → UpdatePadInfoLayout
  - CadPadContextRequested / RegularPadContextRequested → 開啟 pad info
- PadInfoPopover wiring
  - ApplyCloseRequested / DiscardCloseRequested → ClosePadInfoViaState
  - CloseConfirmDismissed → re-enable hit testing
- Console controls（Console.cs）
  - Log 變更 → auto-scroll
- Panel buttons（Panels.cs）
  - 左右面板 show/hide（僅版面）
- File picker delegates（Pickers.cs）
  - 提供 Save/Load/Open dialogs
- ConfirmEmbedDxfAsync（Pickers.cs）
  - 顯示 ConfirmDialog

### PadInfoPopover (src/FreeformHelper.UI/Views/PadInfoPopover.axaml.cs)
- ApplyAndClose_Click
  - 執行 ApplyChangesCommand（CAD/Regular 對應副作用）
- DiscardAndClose_Click
  - 執行 DiscardChangesCommand
- OnPopoverPointerPressed
  - 解除 close confirm

### WorkspaceHeader (src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml.cs)
- Display menu hover/pin handlers
  - 開關 popup，無 model 副作用

### NumberScrubber (src/FreeformHelper.UI/Controls/NumberScrubber.axaml.cs)
- Wheel / scrub / input
  - 觸發 Value 變更（下游屬性變更與副作用）

### MainWindow (src/FreeformHelper.UI/MainWindow.axaml.cs)
- OnClosingAsync
  - 顯示 ConfirmDialog，確認後呼叫 SaveProjectAsync

### ConfirmDialog (src/FreeformHelper.UI/Views/ConfirmDialog.axaml.cs)
- ConfirmButton_Click / CancelButton_Click
  - 關閉 dialog with result

## PadCanvas 互動

### Input 與選取 (src/FreeformHelper.UI/Controls/PadCanvas.Input.cs)
- 左鍵點擊：選取 / Ctrl/Shift additive / toggle
  - 點空白且無 modifier → [清選取]
- 框選（拖曳）
  - ApplyBoxSelection → [設選取]
- 右鍵
  - 依 hit 結果調整選取 + 觸發 CAD/Regular context menu
- 雙擊
  - 若需要先選取，再觸發 context menu
- Row/Col/IC label 點擊
  - 直接選取該 row/col/IC 的 regular pads（可 additive）[設選取]
- SelectionChanged event
  - 發出 SelectionChanged（選取 IDs/indices）

### 視角控制 (src/FreeformHelper.UI/Controls/PadCanvas.View.cs)
- FitToContent
  - [視角]
- ZoomAt（mouse wheel）
  - [視角]
- Pan（中鍵或空白鍵）
  - [視角] + ViewChanged event

### 鍵盤快捷鍵 (src/FreeformHelper.UI/Controls/PadCanvas.Input.cs)
- F → FitToContent [視角]
- Ctrl+A → SelectAllPads [設選取]
- Shift+Arrow → pan nudge [視角]
- 註：同組 F/Ctrl+A 也有 TopLevel 全域入口，目的是在 canvas 未聚焦時維持一致行為。

### 程式化選取 (PadCanvas.Input.cs)
- ClearSelection() → [清選取]
- SetSelection() → [設選取]

### 資源 / Theme 初始化 (src/FreeformHelper.UI/Controls/PadCanvas.cs)
- ResourcesChanged / ActualThemeVariantChanged
  - 清 cache + ApplyThemeDefaults + InvalidateVisual
  - 避免資源初始化時機造成的顏色/字體失效

### ViewChanged event (PadCanvas.cs)
- pan/zoom 變更時觸發，FreeformHelperView 會用來 reposition pad info popovers

---

## 七個 UseCase 現行狀態（R13.005 calibration）

原草案列出的七個 UseCase 目前不是「全數完成」；準確狀態是 6 個部分實作、1 個未實作。`SelectionCoordinator` 與 status/undo 是額外協調元件，不算入這七個 UseCase。

| UseCase | 1.3.0 狀態 | 已收斂責任 | 尚未收斂責任 / owner |
| --- | --- | --- | --- |
| `GridRebuildUseCase` | 部分 | 所有 rebuild request 經 `TriggerGridRebuildAsync -> RequestAsync -> RebuildGridAsync`，並 coalesce pending request | `preserveSelection/reason` 尚非 typed request；selection clear、fit、status 仍由 VM low-level rebuild 擁有（R13.302/R13.403） |
| `ManualSizingUseCase` | 部分 | range parse、validation 與 action plan | mutation 由 VM/`PadEditUseCase` 執行；mixed state、Undo、rebuild、fit 未形成單一 result/side-effect owner（R13.402） |
| `LayerFilterUseCase` | 部分 | pure allowed-layer / visible-CAD result | toggle、workflow reset、FW diff assignment、rebuild/fit 仍由 VM orchestration 擁有（R13.403） |
| `CadLoadUseCase` | 部分 | raw DXF/project import；Open DXF 經 `CadLoadWorkflowService` 共用 outcome | Project load replay、layers、selection、rebuild side effects 仍由 persistence/VM 編排（R13.403） |
| `FreeformTaggingUseCase` | 部分 | manual/auto assignment mutation helpers | manual/auto 各自處理 override、Undo、dirty、preview、status（R13.403） |
| `PadEditUseCase` | 部分 | regular sizing apply/reset/set-dimensions | CAD custom edit仍繞過 use case；Undo/status/rebuild/dirty 仍由 VM 擁有（R13.402） |
| `CanvasViewUseCase` | 未實作 | 無 | Fit/Reset/Zoom 仍分布於 command、rebuild 與 `PadCanvas` input/view；先由 R13.302 明定 fit side-effect policy，再決定是否需要新 type |

## 已存在的協調元件與 side-effect owner

| Behavior | Current convergence | 明確 side effects / debt |
| --- | --- | --- |
| Selection | `SelectionCoordinator`：canvas、manual range、programmatic locate、clear | 同步 selection state/Canvas；已比草案收斂，但不是上述七個 UseCase之一 |
| Workflow policy | `WorkflowPipelineService`：next step、invalidation、expansion、project replay plan | VM `WorkflowSteps` 套用 clear/invalidation；completion 仍從 result content/summary推測，合法零結果可能被誤判（R13.302a） |
| Workflow execution | UI commands直接呼叫 Step handlers；Runtime CLI 經 `RunWorkflowStepAsync` switch | 兩者共用底層 handlers/pipeline policy，但尚未共用同一 execution entry；clear actions已共用 `ClearWorkflowStep` |
| Status / Undo | `UiOperationStatusReporter` / `UndoService` 已存在 | 呼叫與文字組裝仍散在多個 VM path；後續 workspace slice逐步收斂，不另建投機式 facade |
| Settings apply | `SettingsWindowViewModel` draft -> `ApplySettingsWindowDraft` -> orchestration plan | 尚需 dirty-field change set、revisioned stale state與 Step2/4/5 typed invalidation（R13.301/302） |

## Current workflow 與 target

- Current navigation 仍是 Step1 -> Step2 -> Step3 -> Step4 -> Step5；Right panel 另呈現 numbered Step6。Step4 diagnostics 是 optional，`WorkflowStepGateService` 的 Step5並不依賴 Step4。
- `RunWorkflowStepAsync` 仍以 switch 委派底層 handler；文件只能宣稱 UI/CLI 共用 handler/policy，不能宣稱已無 switch或已有唯一 execution entry。
- R13.305a target：Step3 成功直接進 Step5，Step4 mapping與Step6 validation改到未編號 Diagnostics/Inspector；R13.302a先以 revisioned `Completed/Stale` 取代 row count、preview count、freeform value或summary string猜測 readiness。
- Regular Visibility Mask 是 Step1 的必要明確輸入，可保留 normal flow；Step4 Analyze才是 optional diagnostics。
