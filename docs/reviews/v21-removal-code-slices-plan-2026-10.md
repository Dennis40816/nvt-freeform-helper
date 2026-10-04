# V21／Legacy 專用 production code 移除切片規劃

本文件只規劃 TODO「V21／Legacy 完全移除」的 production code 切片。Owner 的「先保留」決定仍有效；下列切片均未獲准開始，本次沒有移除程式碼，也不提出執行日期或目標版本。現有 V21／LegacyRegularAnchor 行為與保護它們的 gates 維持原樣。

程式碼基準為 `7477b07d3969ad6ad15960d0d1ac8b60ef37a9bc`。先讀取 `AGENTS.md`、`docs/agents/domain.md`、依賴圖、現行 roadmap／TODO、Notch reference 與 Runtime CLI contract，再定向追查 `src/` 與相關測試；沒有讀取 `example/`。依賴圖的 ProjectReference 與目前五個專案相符，無須重建。契約入口：[Notch reference](../reference/notch-system-reference.md)、[Runtime CLI](../reference/runtime-cli-plan.md)、[現行 owner 決定](../../TODO.md)。文件內可移除性以目前 caller／branch 為證據，不能僅憑 `Legacy`、`Compatibility` 或 `Q7` 名稱判定。

## 範圍、前置條件與零差異的意思

範圍是只因 `NotchAlgorithmVersion.V21` 或 `NotchComputationMode.LegacyRegularAnchor` 存在的 generator、firmware projection／evaluation、export、runtime adapter、cache 與設定／匯出 UI 程式碼。Persistence、測試退休／新增與 golden 資料由其他 brief 處理；此處只列它們必須提供的 gate，不設計遷移、不指定刪除測試或 golden 的方式。

所有移除切片都有共同前置條件 **P0**：owner 另行明確授權退休這兩條行為，並確認不再支持的 public generator／simulation request、CLI format 與舊專案輸入如何處理。目前 P0 未成立，不能把下表當成待執行授權。不能把 LegacyRegularAnchor 請求悄悄改算 CadAllocation，或把 V21 請求悄悄改算 V22。

**P1（另案的 persistence 交付 gate）**：舊值讀取、版本／模式 enum 與 stored export type 的處置已完成；供本計畫使用的 production request 確實是 V22／CadAllocation。UI 欄位刪除前，另案須先移除設定載入、套用、snapshot 與保存端對該欄位的引用，保留尚在本切片使用的型別／成員以維持編譯。本計畫不刪 `NotchAlgorithmVersion.V21`、`NotchComputationMode.LegacyRegularAnchor`、`NotchExportFileType.Cv21` 的序列化成員，不調整 enum 數值。尤其 `Cv22` 現在是 implicit ordinal；直接刪除前面的 `Cv21` 會改變其數值。

**P2（另案的 V22 門檻交付 gate）**：對每個保留的 V22 請求，移除前後的有效門檻必須 exact 相等。`NotchTableGenerator.Generation.Thresholds.cs:ResolveEffectiveV22ThresholdPercent` 在 Link 開啟時使用 `ThresholdQ7 * 100.0 / 128.0`，而非 `ThresholdPercentV22`；UI 會把顯示 percent 四捨五入至兩位小數。以 Q7=1 為例，生成門檻是 0.78125%，UI 顯示 0.78%，不能以後者替代前者。`OnNotchThresholdPercentChanged` 在 Link 開啟時又會量化回 Q7；只刪回呼會改變之後相同輸入動作的門檻。P2 須提供有效值與編輯行為的 characterization／處置證據；沒有此證據，保留 Q7／Link 消費端，停止相關切片。

**P3（另案的測試交付 gate）**：每個切片開始前，對應 V22 characterization 已存在並可執行；退休行為的 assertions／編譯引用由測試 brief 同步處理，不能靠跳過 V22 case 或更新 expected 輸出取得綠燈。下面點名的是已存在的 test seam，補齊項目明列為「需補 characterization」，本次不新增測試。

零差異比較使用同一 CAD／grid、active regular set、CAD output FW diff map、補償設定、有效門檻、sentinel 與 export profile，逐項比較 V22 rows（含順序、Values、V22Node、identity、comment）、coverage audit、simulation Cells／Actions／diagnostics、CSV／C 原始輸出。Cache 的冷／暖／並行路徑必須得到相同結果；elapsed timing 不要求 exact。移除 V21 選項、Legacy badge 或 CLI 支援的 UI／協定文字是退休功能所需的明示變更，不代表可以改 V22 payload、ratio 或 C 註解。原先同時產生兩版的 table，僅比較其 V22 投影與 V22 匯出；不要求移除後仍保留 V21 rows。

## 共用程式碼保留界線

下列均不能列入 V21／Legacy 專用刪除量；目前沒有證據支持為本目標刪除它們。

| 檔案：symbol | 程式碼顯示的 V22 消費關係 | 刪除量與 gate／風險 |
|---|---|---|
| `src/FreeformHelper.Application/Services/NotchAllocationService.cs:BuildAllocations, HasQ7PositiveAllocationInIc, TryComputeAllocation, NotchAllocation`；`NotchThresholdQ7Contract.cs:EncodeFraction` | CadAllocation profiles／memo 與 UI per-IC pool admission 使用 Q7-positive allocation；Q7 在此不是 V21 firmware payload。 | **0 行**；鎖住 Q7=0／正值界線與跨 IC pool。刪除 Q7 會改 anchor／pool membership，風險高。 |
| `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs:CreateContext, Compute` 及其 partials；`NotchV22CompensationService.Stages.cs:BuildCompensationAllocations, RunStageCEvaluateToFullAndDiagnostics` | `NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate` 消費完整 context；`EnableToFullRuleEngine=false` 的 decision adapter 仍是 V22 設定支援的路徑。multi-parameter Compute 是相容 API，沒有按 V21／LegacyRegularAnchor 派送。 | **0 行**；`NotchV22CompensationServiceTests`。不可把 rule-engine fallback、reachability helper 的 legacy 註解當成 LegacyRegularAnchor，風險高。 |
| `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs:Build`；同檔 `NotchV22TargetAllocationPolicy:ResolveAreaMode, ProjectTargetCoverage, ProjectCompatibilityDisplay` | CurrentGain／ConservativeNoGain 使用 target coverage；Disabled 仍解析成 SourceAreaDominant，會走 compatibility display 與 source-area legs。 | **0 行**；`NotchV22TargetAllocationServiceTests.TargetAllocationPolicy_MapsEveryCompensationModelToItsCoveragePath`、`ProjectTargetCoverage_CompatibilityPathsPreserveAllTargetDisplayRatio`。刪 fallback 會改 Disabled V22，風險高。 |
| `src/FreeformHelper.UI/Services/NotchDisplayProjector.cs:Build` 三個 overload、`IsEffectiveTarget` 與 target-coverage fallback | Inspector、Pad Info、Notch Detail 與 Runtime Query 共同投影 V22 compensation／allocation；此檔沒有版本／computation-mode 派送。nullable target coverage、strict-only 顯示有現存測試。 | **0 行**；`NotchDisplayProjectorTests.Build_WhenTargetCoverageIsCompatibilityMode_PreservesStrictOnlyDisplay`。需保留三種輸入 seam；刪 fallback 會改診斷，風險高。 |
| `src/FreeformHelper.Application/Export/NotchTableExporter.cs:ExportAsCsv, ExportAsCInitializer`；`src/FreeformHelper.Application/Services/NotchV22FirmwareProjector.cs:Project` | CSV 的固定九欄是 review schema；V22 formatter 與 simulation 共用 projector，保留 typed／untyped／short row 相容行為。 | **0 行**；`NotchTableExporterTests.ExportAndSimulation_TypedAndUntypedV22Rows_NormalizeIdentically`、`ExportAndSimulation_ShortV22Row_PreserveLegacyCompatibility`。不要把缺少 V22Node 等同 V21。 |
| `src/FreeformHelper.UI/Services/NotchDetailUseCase.cs:BuildFromResolvedResult`；`src/FreeformHelper.UI/ViewModels/NotchDetailViewModel.cs:ThresholdQ7, IsBelowThreshold` | V22 resolved result 的 Detail 仍顯示並比較 Q7 門檻；這些 consumer 不只服務 V21。 | **0 行**（P2 前）；`NotchDetailUseCaseTests` 加有效門檻 characterization。改成 percent 是另一項行為變更，不在本計畫偷偷修正。 |

上述第一列以外的短檔名皆指同一 Services 目錄；表格是保留稽核，不是零刪除量的假實作切片。

## 驗收代號與行數口徑

- **B**：每個切片均須 UI build、對應測試專案 build 與 `git diff --check` 通過；在正常執行環境再跑 repo 要求的 lint，0 errors／warnings，不放寬 analyzer。使用現有 workspace prepare；sandbox 不列行程，使用 `-SkipStopApp -SkipNormalizeLineEndings`，本次唯一文件自行寫成 CRLF。正式程式修改仍依 repo prepare 規則。
- **C-G**：generator 的 V22 rows／coverage／freeze characterization。現存 `NotchTableGeneratorTests` 的 `Generate_CadAllocationMode_V22_SnapshotStable_ForSimpleTriangle`、三個 compensation-model cases、`Generate_CadAllocationMode_V22_UsesCadOutputFwDiffAsSource_AndKeepsRegularFwDiffAsTarget`、`ProjectCadAllocationResolvedBatch_FinalThresholdFiltersRowsAndCoverageAuditFromSameCandidateView`、`Generate_CadAllocationMode_FreezesProjectionRequestAtCandidatePhaseStart`。另需補 V22 eligibility 的 None／XWay／YWay／其他 freeform、空 overlap、缺 match／geometry fallback exact characterization。
- **C-E**：`NotchTableExporterTests` 的 V22 typed／untyped／short row、Release no-op filtering、pre-filter sorting、多 IC／mask／sentinel／continuation，以及 V22 GCC runtime parity。另需補相同輸入的 Release／Debug CSV／C raw bytes 前後 exact characterization；含所有 C 註解與換行。GCC 缺席不能當成 parity 已驗證。
- **C-S**：`NotchApplySimulationServiceTests.Simulate_WhenFrameIsOutsideInt16Domain_UsesFirmwareBaselineForCellsAndAudit` 的 V22 case、V22 main／continuation／mean／duplicate diff／row-column order cases；非有限值、fraction、INT16 上下界均須 exact。
- **C-U**：`NotchExportSelectionViewModelTests`、相關 projection／summary／quick-filter tests；需補 V22-only transfer／no-op／no-CAD rows 的 selection／payload／preview characterization。既有 untyped V22 顯示 fallback 繼續受保護。
- **C-R**：`RuntimeQueryUseCaseTests`；需補可成功匯出 V22／CSV 的 shared command seam、失敗時 delegates／file type 還原、state invariant、V22 pad／notch query 與 display 一致的 characterization。現存 versioned-C「workflow not ready」case 不能代替 successful-export 證據。
- **C-K**：`NotchExportGenerationCacheServiceTests`、`FreeformHelperViewModelTests` 的 NotchExportCache partial cases；需鎖 warm exact output、空 batch、task joining、fault retry、fingerprint collision、selected sparse reuse、epoch／final revision／source revision stale completion 不發布。
- **C-T**：`NotchSettingsTests` 與既有 settings change／draft／ViewModel seams；P2 另補 linked／unlinked 值、編輯與 Save／Discard、undo、downstream invalidation 的 V22 characterization。
- **U**：`UiLayoutGuardTests`、相應 headless smoke、Dev page／Settings／匯出視窗預覽；僅退休控制項消失，token／theme 與 V22 controls 可用。不得為此新增視覺語言、inline 尺寸或顏色。
- **E**：涉及 generator／export／VM／runtime 結果者，另案提供既有 V22 production-equivalent exact export／golden gate；本文件不讀取或變更私有資料。現行雙版本 gate 須先由測試 brief 決定退休部分的處置，不能直接跑掉 V21 failures後宣稱 E 通過。

行數是 **production C#／AXAML physical lines**（含註解與空白），不含 docs、tests、golden、generated code。整檔數是基準檔案實測；區間皆為**推估**，須於實作時計算實際 net diff。移入既有類別的共用 helper 要扣回新增行數；替換文案不計為淨刪除。切片間不重複計數，不推估 DLL size 或工期。

## 建議切片順序

編號是可逐個建置的拓撲順序，不代表排程。每項還要滿足 P0、P3；P1／P2 僅在項目明列時成為額外阻擋條件。暫時留下無 caller 的 internal helper，等其檔案切片刪除，可以避免跨檔原子大改；不新增 adapter、配置、extension point 或一般化層。

### S01：Runtime Query 退休 V21 專用表面

- **檔案：symbol**：`src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.Export.cs:QueryExportNotchAsync` 移除 `c-v21` mapping／normalization，簡化 accepted-format 文案；`RuntimeQueryUseCase.cs:QueryHelpExamples, QueryHelp` 的 V21 example／format 文案；`RuntimeQueryUseCase.Commands.TerminalStatus.cs:QueryStatus` 移除 `notchExportState.enableV21`。後兩檔均在同一 UI Services 目錄。
- **前置**：P0、C-R；需先取得 status 欄位／help／format 的退休契約。`c-v21` 經現有 unsupported-format 分支回 `INVALID_ARGUMENTS`，不得改回 V22。
- **驗收**：B、C-R、E；V22／CSV 仍經 `RuntimeQueryUseCase → ExportNotchCommand`，`finally` 還原三個 transient delegates／file type，保留 export write／progress／summary 的現行副作用。
- **淨刪除**：推估 **5～10 行**。
- **風險**：中；CLI 與 status consumers 是公開表面。明示 user-visible change：V21 format／help／status 欄位退休；V22 計算與其他 query payload 不變。

### S02：UI 匯出格式只列現有 V22／CSV

- **檔案：symbol**：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:FreeformHelperViewModel` 的 `NotchExportFileTypeOptions` 移除 Cv21 option；`NotchExportFileTypeMetadata.cs:IsCExportType, GetExportKindLabel` 移除 Cv21 分支。使用現有 option／pinned-version 機制，不改造 selection model。
- **前置**：S01、P1 的 stored Cv21 處置 gate、C-U／C-R。尚未完成 enum／persistence brief 時保留 `FreeformHelperViewModel.Models.cs:NotchExportFileType.Cv21` 宣告，不重排 Cv22 ordinal。
- **驗收**：B、C-U、C-R、E、U；V22 pinned selection、CSV selection、filename、取消／preview side effects 相同。
- **淨刪除**：推估 **3～8 行**。
- **風險**：中；option 缺席時舊 saved selection 不可落入意外 default。明示 user-visible change：C v2.1 選項消失。

### S03：Generator 移除 LegacyRegularAnchor orchestration，先解開 eligibility 依賴

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchTableGenerator.cs:Generate, EvaluateCadRowEligibility` 移除 legacy dispatch，Generate 沿用既有 CadAllocation request validation 拒絕非 CadAllocation，eligibility 入口同樣須由 P1 驗證拒絕退休 mode；`NotchTableGenerator.Generation.cs:GenerateLegacyRegularAnchor, CaptureLegacyNotchGenerationRequest, CanBuildLegacyRow, BuildLegacyRow` 刪除；`NotchTableGenerator.Eligibility.cs:EvaluateLegacyEligibility` 刪除，`EvaluateCadAllocationEligibility` 對保留的 V22 使用現有 `V22LegacyRowStrategy.CanHandle` 的 exact predicate（CAD 非 null，且 XWay／YWay），不保留對該 strategy 的 caller；`NotchTableGenerator.Generation.Allocation.cs:BuildCadMaxAllocation` 刪除；`NotchTableGenerator.Generation.Thresholds.cs:PassesThresholdForVersion(..., LegacyNotchGenerationRequest)` 刪除。短檔名均在 Application Services。
- **前置**：P1 的合法 mode 請求 gate、C-G，特別是需補的 eligibility characterization；S03 仍可產生 CadAllocation V21 projection，留給 S07。
- **驗收**：B、C-G、E；V22 eligibility reason、estimated count 與 anchor 逐項相同；normal generation 四個 phases、callback freeze 邊界、active-set／output-map snapshot 不移動。非 freeform anchor 的 ToFull emission 繼續由 canonical generator 處理，不能因 eligibility 的 X／Y predicate 而新增生成 gate。
- **淨刪除**：推估 **200～240 行**，不含下三個檔案刪除切片。
- **風險**：高；normal eligibility 今日仍呼叫 `CanBuildLegacyRow`，直接刪除 strategy 會編譯失敗；將兩個 mode 合併會改舊請求語意。只對已獲退休授權的 mode 取消分支。

### S04：刪除 V21 regular-anchor 演算法檔案

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs:V21NotchAlgorithm`（含 `CanHandle, Build, FillAxis`）整檔刪除。
- **前置**：S03；所有 production caller 已離開此類別，C-G 的 V22 cases 維持。
- **驗收**：B、C-G、E；定向 reference search 顯示無 production caller。保留仍被 final V21 projection 使用的 Q7 codec。
- **淨刪除**：整檔實測 **174 行**。
- **風險**：低（前置成立後）；不要連帶刪除 V22 allocation 的 Q7。

### S05：刪除 V22 legacy geometry-row strategy

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs:V22LegacyRowStrategy`（`CanHandle, Build`）整檔刪除。
- **前置**：S03 已把 normal eligibility 的 exact predicate 留在原有 eligibility 方法；C-G。
- **驗收**：B、C-G、E；canonical V22 的七欄 node、continuation 與 target coverage 均相同；本檔九欄 legacy geometry row 不再有 production caller。
- **淨刪除**：整檔實測 **232 行**。
- **風險**：中；「V22」檔名仍可能被誤認是 canonical algorithm；不刪 `NotchV22Node`／firmware projector 的合法 untyped row 相容行為。

### S06：回收兩個 legacy builder 的 private 支援碼

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchAlgorithms/NotchAlgorithmHelpers.cs:NotchAlgorithmHelpers`、`NotchAxisContext.cs:NotchAxisKind, NotchAxisGeometryContext, NotchAxisNeighborContext`、`LegacyNotchGenerationRequest.cs:LegacyNotchGenerationRequest` 整檔刪除。
- **前置**：S04、S05；定向查詢所有上述型別／helper，確認只剩宣告與彼此引用；C-G。
- **驗收**：B、C-G、E；不回收 `NotchTableGenerator.Generation.Allocation.cs:SelectCadAllocationAnchor` 或 V22 compensation 幾何。
- **淨刪除**：整檔實測 **210 + 85 + 10 = 305 行**。
- **風險**：低（caller 清空後）；這三個檔案的 geometry／neighbor context 與 V22 compensation 的幾何工具不同。

### S07：CadAllocation 不再投影 V21 rows

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:ProjectV22RowsToV21Rows`（含 local `FillLegacyLeg`）刪除；`NotchTableGenerator.Generation.cs:CadAllocationProjectionRequest` 的 ExportsV21／ThresholdQ7、`CaptureCadAllocationProjectionRequest, BuildCanonicalProjectionCandidateView, AppendCanonicalExports` 刪除 V21-only payload／admission 分支；`NotchTableGenerator.Generation.Thresholds.cs:PassesV21ThresholdQ7, PassesThresholdForVersion` 去掉 V21 派送，V22 eligibility 使用原有 percent gate。
- **前置**：S03、S02、P1 的 enabled-version request gate、C-G；**仍保留 `ResolveEffectiveV22ThresholdPercent` 的 Link／Q7 換算，直到 P2／S15**。
- **驗收**：B、C-G、E；V22-only 與原 mixed request 的 V22 投影 exact；rows／audit 從同一 admitted candidate view 產生；guard／cap 與 empty projection 不改；candidate context、resolution fingerprint／memo 不動。
- **淨刪除**：推估 **85～115 行**。
- **風險**：高；V21-only request 今天走 Q7 admission，mixed request 今天優先走 V22 admission，不能直接用兩者任一替代全部舊請求。V22 linked threshold 也仍依賴 Q7。

### S08：Firmware C exporter 移除 V21 formatter

- **檔案：symbol**：`src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs:Export` 去掉 orderedV21Rows／V21 dispatch 與退休的 mixed-version 分支；刪 `ExportV21FwFile, AppendV21TypeBlock, AppendV21Tables, AppendV21FunctionBlock, FormatV21NodeLine, FormatV21TypeLiteral, FormatDiffLiteral(int,int)`。保留 V22 overload `FormatDiffLiteral(int?)` 與共用 IC dispatch／mask／header。
- **前置**：S02、C-E；P0 已交付 unsupported V21／mixed input 的處置。可在 S03／S07 前執行，因 UI／runtime 的 V22 C selection 已使用現有 pinned mechanism。
- **驗收**：B、C-E、E；`AppendGenerationMetadata` 的 V22 輸出仍須逐 byte 保留 `Sections: v2.1=empty, v2.2=...`。可把 v21Count 的參數簡化為原有固定 empty 文案，不能因移除功能刪掉該行。保留 pre-filter sort、Release no-op pruning、Debug mask、sentinel snapshot、encoding／newline。
- **淨刪除**：推估 **255～280 行**。
- **風險**：高；V22 C bytes 包括看似 V21 的 metadata，專用 C function block 中也呼叫共用 formatter，不能整段連帶刪除共享符號。

### S09：Simulation 移除 V21 apply，同切片保留 V22 INT16 baseline

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchApplySimulationService.cs:Simulate, BuildV21Actions` 刪 V21 branch／contract text／actions builder；將 `NotchV21FirmwareEvaluator.cs:QuantizeToFirmwareInt16` 原樣移入既有 `NotchApplySimulationService`，V22 呼叫改指向這個 helper；刪 `NotchV21FirmwareEvaluator.cs:NotchV21FirmwareEvaluator, NotchV21FirmwareEvaluation` 其餘部分。`NotchApplySimulationModels.cs:NotchApplySimulationAction.IsLegacyApproximation` 在無 production caller、P3 完成 test 引用處置後刪除。
- **前置**：S07、C-S、C-E；V21 simulation request 已由 P0 退休。保留 existing unsupported-version 回應，不建立另一條 simulation 路徑。
- **驗收**：B、C-S、C-E、E；量化仍是 finite truncate→INT16 saturation，NaN／Infinity→0，modular writeback 與 V22 continuation evaluation 不改；Cells、Actions、audit 的 before baseline exact 相同。
- **淨刪除**：推估 **205～230 行**（evaluator 整檔實測 110 行，扣回量化 helper 移入量）。
- **風險**：高；只按檔名刪 evaluator 會破壞 V22。`NotchApplySimulationRequest.ComputationMode` 與 UI session 傳遞欄位先留給 P1，不在這一步跨多個 API 刪參數。

### S10：刪除無 consumer 的 V21 final projector 與 node models

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchV21FirmwareProjector.cs:NotchV21FirmwareProjector, NotchV21FirmwareProjection, NotchV21ProjectedSourceRow, NotchV21ProjectedLeg, NotchV21FirmwareNode` 整檔刪除。
- **前置**：S08、S09；exporter／simulation 對全部上述型別的 production references 已清空，C-E／C-S。
- **驗收**：B、C-E、C-S、E；V22 exporter／simulation 都繼續使用 `NotchV22FirmwareProjector.Project`；沒有新 projector 或重算。
- **淨刪除**：整檔實測 **339 行**。
- **風險**：低（前置成立後）；不能刪 shared sentinel validation 或 V22 typed/untyped normalization。

### S11：刪除最後無 consumer 的 V21 payload Q7 codec

- **檔案：symbol**：`src/FreeformHelper.Application/Services/NotchV21Q7Codec.cs:NotchV21Q7Codec` 整檔刪除。
- **前置**：S04、S07、S08、S09、S10；production codec references 清空，C-G／C-E／C-S。
- **驗收**：B、C-G、C-E、C-S、E；保留 `NotchThresholdQ7Contract`、`NotchAllocation.Q7`、Q7-positive pool predicate 與 compensation allocations。
- **淨刪除**：整檔實測 **63 行**。
- **風險**：低（前置成立後）；payload Q7 與 allocation／threshold Q7 是不同用途。

### S12：VM generation 與 cache 移除 request-specific legacy table 分支

- **檔案：symbol**：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:GenerateCurrentNotchTableAsync` 去掉 legacy fingerprint 選擇、`legacyTableToStore`、TryGet／Generate fallback、TryStore 分支，保留既有 CadAllocation batch owner；`FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ComputeNotchExportSettingsFingerprint` 刪除無 caller 的舊 generation fingerprint（不是 persistence schema）；`src/FreeformHelper.UI/Services/NotchExportGenerationCacheService.cs:TryGet` 的 table overloads、`Store` overloads、`TryStore, TryStoreEntry, CacheEntry.Table` 刪除，必要時只簡化供 table API 使用的 matching helper。
- **前置**：S03、S07、P1 的 mode gate、C-K。`TryGet/Store` 是 public API；caller inventory 與 P0／P3 必須明確包含它們，不能用「只有測試引用」自動推定可破壞相容。
- **驗收**：B、C-K、C-G、C-S、E；空 batch 仍可 cache；concurrent export／simulation join 同一 resolution task，各自 final projection；fault retry、identity equality、epoch／revision stale refusal、busy scope `finally`、sparse promotion 均保留。
- **淨刪除**：推估 **210～280 行**，只計 table storage/API 與 VM fallback，不把 batch matching／signature／metrics 整組刪除。
- **風險**：高；兩種 cache 今日共用 `_entry`／lookup metrics，機械式刪 table branch 容易改 batch hit／miss 或舊 completion 的發布時序。

### S13：匯出選列 UI 退休非 V22 的 status／filter

- **檔案：symbol**：`src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Models.cs:NotchExportRowDisplayMode.LegacyOnly, NotchExportRowItemViewModel.IsStatusLegacy, BuildMappingStatusText, BuildStatusText` 簡化；`NotchExportSelectionViewModel.cs` 的 Legacy option／`IsFilterLegacyOnly, LegacyRowCount`、`NotchExportSelectionViewModel.Filters.cs:NotifyFilterStateChanged` 與 `NotchExportSelectionViewModel.Selection.cs:UpdateCounts` 相關通知簡化；`src/FreeformHelper.UI/Services/NotchExportSelectionProjectionBuilder.cs:MatchesDisplayModeFilter`、`NotchExportSelectionQuickFilterService.cs:StatusFilterCycle`、`NotchExportSelectionSummaryProjector.cs:Build, NotchExportSelectionSummarySnapshot.LegacyRowCount` 移除 legacy-only 消費；`src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml` 移除 Legacy filter／badge 綁定。
- **前置**：S07、C-U；收到的 rows 都是 V22。`IsStatusLegacy` 的 predicate 是版本不等於 V22，**與 `V22Node is null` 不同**。
- **另有 consumer**：`src/FreeformHelper.UI/Views/NotchExportSelectionRowsPaneView.axaml:178` 仍以 `Classes.legacy` 綁定 `IsStatusLegacy`（匯出視窗使用）；回收該屬性時要同步移除這個 binding，否則會留下失效的 runtime binding。
- **驗收**：B、C-U、E、U；V22 transfer／no-op／no-CAD selection、排序、workspace preview 與 payload 文字相同；保留所有針對 `V22Node is null` 的 Values／flags／analysis fallback，不為了刪「Legacy payload」字串而強制 node 存在。
- **淨刪除**：推估 **45～75 行**。
- **風險**：中；缺 typed node 的 V22 rows 仍合法。明示 user-visible change：Legacy filter／badge／summary 項目消失，status cycle 不再經過 LegacyOnly。

### S14：設定畫面移除 V21／Q7／Link 控制項

- **檔案：symbol**：`src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml` 的 EnableV21 checkbox、NotchThresholdQ7 editor、LinkNotchThresholds toggle／help 移除；沿用現有 Grid／settingsFieldTile／token 排列剩餘 V22 control，簡化雙版本說明。
- **前置**：S02、S07、P2、C-T；此切片只移除 visual bindings，backing properties 先保留，讓本步可獨立 build。若 P2 尚未處理 V22 percent 編輯的 Q7 量化，不能刪 Link／Q7 的 consumer 或假設剩餘 editor 已獨立。
- **驗收**：B、C-T、E、U；V22 percent／NullValue／export profile 的有效值、Save／Discard 與 downstream invalidation 相同；Dev／Settings 預覽確認無空欄或 clipping，沒有 token redesign。
- **淨刪除**：推估 **45～65 行**（只計 AXAML）。
- **風險**：高；Link 是 V22 的行為依賴，不只是 V21 開關。明示 user-visible change：V21 enable、Q7 與 Link controls／說明消失。

### S15：最後回收 UI backing／callback，移除 V22 的已解除門檻橋接

- **檔案：symbol**：`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.Notch.cs:_enableV21, _lenScale, IsLenScaleVisible, _notchThresholdQ7, _linkNotchThresholds` 與無 consumer 的 internal-legacy visibility state；`SettingsWindowViewModel.cs` 對應 draft state、constructor copy、`OnEnableV21Changed, OnNotchThresholdQ7Changed, OnLinkNotchThresholdsChanged` 刪除，`OnNotchThresholdPercentChanged` 只移除已解除的跨版本同步；`FreeformHelperViewModel.Settings.NotchPreview.cs` 同名同步 callbacks 簡化；`FreeformHelperViewModel.Settings.PropertyCallbacks.Step5.cs:OnEnableV21Changed`、`FreeformHelperViewModel.UndoHooks.cs` 對應 Changing hooks、`FreeformHelperViewModel.Settings.DirtyTracking.cs:DirtySettingNames` 對應 entries、`FreeformHelperViewModel.SettingsWindow.cs:ApplySettingsWindowDraft` 對應 draft compare／apply 刪除；`FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection` 與 `FreeformHelperViewModel.Persistence.cs:BuildNotchTableForExportAsync` 的相關呼叫，僅在 P1 已取消 persist-on-save version choice 後回收。`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.Thresholds.cs:ResolveEffectiveV22ThresholdPercent` 只在 P2 交付後簡化成原 percent clamp；`FreeformHelperViewModel.NotchTableGeneration.cs:GenerateCurrentNotchTableAsync` 去掉退休欄位的 log arguments／labels。
- **前置**：S01、S02、S03、S07、S12、S14、P1、P2、C-T。另案必須先解除 `FreeformHelperViewModel.Settings.Sync.cs`、`FreeformHelperViewModel.UiSnapshot.cs`、`CreateExportSettingsSnapshot` 與 Application settings／validation／schema 對退休欄位的橋接引用；本計畫不安排這些 persistence 修改；FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection／FreeformHelperViewModel.Persistence.cs:BuildNotchTableForExportAsync 中 persist-on-save version choice 對退休欄位的引用，也先由 P1 解除，不在 S15 設計其保存行為。Detail Q7 consumer 未由另案解除時，相關門檻成員保留，此 slice 不得宣稱完整回收。
- **驗收**：B、C-T、C-G、C-R、C-K、E、U；生成有效門檻 exact，不把 rounded UI percent 當舊 linked threshold；V22 settings draft Save／Discard／undo 與原有 final-only／full invalidation 政策相同。不改 app-settings deferred flush、Ctrl+S／toast、selection clear、fit／zoom reset。
- **淨刪除**：推估 **100～150 行**；不計另案 Settings.Sync／snapshot／schema／enum 刪除量，Application threshold helper 不與 S07 重複計算。
- **風險**：高；generated observable properties、partial callbacks、nameof、XAML 與 undo 都是 consumer。P2 若無法證明保留同一編輯動作的門檻語意，維持這部分程式碼，不以新量化政策或預設值繞過 gate。

## 可以平行的檔案群與串行邊界

「可平行」指未來另行授權後的獨立切片；本次沒有委派或執行移除。各分支先在自己的切片後 build／test，整合再做 B 與對應 exact gates。共享 production type 的宣告保持到所有 callers 移除後才刪；測試 brief 若修改同一測試檔，另行串行整合。

| 已滿足的依賴 | 可平行切片 | 檔案不相交的理由／整合關卡 |
|---|---|---|
| P0／P3，各自 characterization 可用 | S01 與 S03（S03 另需 P1） | RuntimeQueryUseCase partials 與 Application generator partials 不相交；整合跑 C-R／C-G。 |
| S01／P1 | S02 與 S03 | Core／file-type metadata 與 generator 不相交。 |
| S03 | S04、S05、S07（S07 另需 S02／P1） | 兩個 algorithm 檔與 generator Generation／V22／Thresholds partials 不相交；legacy request／helpers 暫留至 S06。 |
| S02／S07 | S08、S09、S12、S13 | Application exporter、Application simulation＋evaluator、UI generation＋cache、UI selection＋view 四群 production files 不相交；分別跑 C-E、C-S、C-K、C-U。S08 實際只需 S02，等待 S07 可統一整合 frontier。 |
| S04／S05，加上各自依賴 | S06 可與 S08／S09／S12／S13 平行 | S06 僅刪 NotchAlgorithms 三個支援檔；沒有 final projection／cache consumer。 |
| S08／S09；另各自前置已成立 | S10 可與 S12／S13／S14 平行；S14 另需 P2 | S10 只刪 V21 projector；S14 只改 SettingsStep5 AXAML，與 export-selection view 不相交。 |

不得平行：S03 與 S07 共用 Generation／Thresholds；S09→S10 因 evaluator 仍用 projector models；S10→S11 因 projector 仍用 codec；S14→S15 因 visual bindings／VM consumers 的解除順序；S12／S07 與 S15 分別共用 VM generation／threshold 檔。S01 若尚未移除 QueryStatus.EnableV21，S15 不可刪 backing property。Persist-on-save 版本選擇的退休欄位橋接由 P1 另案解除；不把保存行為修改塞進 S02／S12／S15。

建議依賴鏈為 `S01 → S02`、`S03 → (S04 || S05) → S06`、`(S02,S03) → S07 → (S09,S12,S13)`、`S02 → S08`、`(S08,S09) → S10 → S11`、`(S02,S07,P2) → S14 → S15`。S15 另等待 P1 與上述共用檔案切片；P0／P3 套用於所有節點。

## 移除量與完成界線

上述互斥切片的 production net 刪除推估合計 **2,266～2,566 physical lines**。其中 S04／S05／S06／S10／S11 的整檔量共 **1,113 行**為基準實測，其餘是推估；不含另案 persistence／enum／tests／golden，也不把 compensation／allocation／NotchDisplayProjector 算成可刪除。這不是效能、binary size 或工期承諾。

實作完成後需定向搜尋 production references（C#／AXAML、nameof／binding 與 explicit strings），逐一解釋 V21／LegacyRegularAnchor 的殘留。V22 C 中 `v2.1=empty` metadata、allocation Q7、rule-engine compatibility 與 untyped V22 fallback 都是刻意保留，不能要求文字命中數為零。`NotchAlgorithmVersionDisplay.cs`／settings enum／schema 等 persistence 邊界由 P1 另案交付；該 gate 未結案，整體「完全移除」仍未完成，不以本文件 production slices 的完成取代它。

未決事項只有外部依賴：P0 退休授權及 unsupported request／CLI／status 契約、P1 舊設定與 enum／UI 橋接處置、P2 有效門檻及既有編輯語意、P3 characterization／退休測試協調。程式碼能證明 caller／數值關係，不能證明 owner 已批准這些產品決定；本文件不猜其答案。

## 本次規劃驗證

本次未實作任何上述切片。測試專案 `--no-restore` build（包含 UI）通過，**0 warnings／0 errors**；以 class filter 執行 `NotchTableGeneratorTests`、`NotchV22CompensationServiceTests`、`NotchV22TargetAllocationServiceTests`、`NotchApplySimulationServiceTests`、`NotchDisplayProjectorTests`，結果 **101 passed／0 failed／0 skipped**。這是現況 characterization seams 的健康檢查，不是尚未發生之移除的等價性證明，也不是 GCC／runtime export／golden gate 證明。

命令使用 `AVALONIA_TELEMETRY_OPTOUT=1`、`GIT_CONFIG_GLOBAL=NUL`；沒有 restore、網路、完整測試、lint、行程列舉、git writes、commit／push，沒有讀取 `example/`，沒有改程式、測試、TODO 或 roadmap。新增文件維持 CRLF；`git diff --check`、文件非空與 structure verifier 的結果由本次執行回覆記錄，不在工作樹另留報告檔。
