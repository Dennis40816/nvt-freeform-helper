# V21／Legacy 完全移除：測試與基準規劃

- 盤點日期：2026-10-03；source revision：`7477b07d3969ad6ad15960d0d1ac8b60ef37a9bc`。
- 對應工作項：[TODO.md 的「V21／Legacy 完全移除」](../../TODO.md)。本文件只有規劃，不變更 checkbox、roadmap、程式或測試。
- Owner 的「先保留」決定持續有效：V21 與 `LegacyRegularAnchor` 維持原樣，既有 zero-diff gates 繼續保護；不新增 legacy 收斂／等價性工作。不提出移除版本、時程或舊 project 的 migration 政策。
- 依據：[1.3.x roadmap](../guides/refactor-roadmap-1.3.x.md) 第 0、2、3 節、[Notch contract](../reference/notch-system-reference.md)、[Runtime CLI contract](../reference/runtime-cli-plan.md)、[3635 regression baseline](../performance/regression-baseline-3635.md)。
- 未讀取、複製或引述 `example/` 內容。以下私有資料名稱來自測試／腳本中的字串與公開庫文件；沒有驗證私有檔案目前的內容、存在性或 hash。這不是移除授權，也不是 golden 更新授權。

## 1. 盤點口徑與 CI 歸屬

計數單位是測試**方法**，不是 xUnit 展開後的 case。partial class 合併計數；一個 theory 同時跑兩版仍算一個方法，刪除 V21 InlineData 不等於刪除整個方法。表中的「涉及／全類」只列本次相關方法，未列的方法繼續保留。

盤點包含顯式 V21／Legacy 契約、兩版資料或設定，以及透過共用 fixture／helper 實際取得兩版資料的方法。僅建立預設 ViewModel、沒有執行或驗證版本契約者不算雙版測試。另納入三個需防止誤刪的邊界：共享 Q7 allocation、legacy table cache，以及版本由私有 project 決定的 E2E benchmark；分別明示，不能當作 V21 專屬測試。

處置符號：**刪**＝移除獲准且對應行為消失後可刪專屬斷言／方法；**轉**＝先建立 V22 characterization，再移除 V21／Legacy 部分，保留共同契約；**留**＝仍有 V22／共享用途。所有處置均為未來條件，現在一律維持。

[run-tests.ps1](../../scripts/tests/run-tests.ps1) 定義 group；[verify.ps1](../../scripts/verify.ps1) 將 group 映射到 [.github/workflows/ci.yml](../../.github/workflows/ci.yml) 的 shard：

| CI shard | run-tests.ps1 groups | 注意事項 |
|---|---|---|
| `core` | `notch-core`、`application`、`infrastructure`、`uncategorized` | 同一類可出現在多個 group；不要重複加總方法。`notch-golden` 是 `notch-core` 子集，不是獨立 CI shard。 |
| `ui` | `ui-stable` | 本地 `ui-core` 是 `ui-stable` 與 `ui-viewmodel` 的聯集。 |
| `viewmodel` | `ui-viewmodel` | `FreeformHelperViewModelTests` 的獨立、非阻擋 shard。 |
| `snapshots` | `ui-snapshots` | UI source hash、rendered snapshot、layout、persistence contract。 |

目前 `run-refactor-gate.ps1` 固定跑 `notch-core`、`application`、`infrastructure`、`ui-core`、`ui-snapshots`、`uncategorized`；舊 `Include*` switches 已不是開啟這些 group 的必要條件。Roadmap 歷史 case 數不能直接用作移除後的測試數門檻。

### 1.1 依 class 統計

來源皆在 `tests/FreeformHelper.Tests/`；下節列出每個涉及的方法。合計 **23 類、100 個涉及／需確認方法**，含上述三個共享／條件邊界；不是可刪除數量。

| Class | 涉及／全類方法數 | run-tests.ps1 group | CI shard |
|---|---:|---|---|
| `NotchTableGeneratorTests` | 22／42 | `notch-core`、`application` | `core` |
| `NotchTableExporterTests` | 10／27 | `notch-core`、`application` | `core` |
| `NotchSettingsTests` | 2／8 | `notch-core` | `core` |
| `NotchV22CompensationServiceTests` | 1／26 | `notch-core`、`application` | `core` |
| `NotchCadOutputFwDiffProjectionServiceTests` | 1／5 | `notch-core` | `core` |
| `NotchApplySimulationServiceTests` | 6／11 | `notch-core` | `core` |
| `NotchApplySimulationReviewUseCaseTests` | 1／2 | `notch-core` | `core` |
| `SimulationWorkspaceUseCaseTests` | 1／3 | `uncategorized` | `core` |
| `NotchExampleCExportDriftTests` | 2／3 | `notch-golden`、`notch-core` | `core` |
| `NotchGoldenBaselineTests` | 1／1 | `notch-golden`、`notch-core` | `core` |
| `Tm81NotchAcceptanceMatrixTests` | 1／1 | `notch-golden`、`notch-core` | `core` |
| `Notch3635GenerationBenchmarkTests` | 1／1 | `uncategorized` | `core`（工作量 opt-in） |
| `Cad3635EndToEndBenchmarkTests` | 1／1 | `uncategorized` | `core`（工作量 opt-in；版本需確認） |
| `FreeformHelperViewModelTests` | 9／158 | `ui-viewmodel`、本地 `ui-core` | `viewmodel` |
| `NotchExportSelectionViewModelTests` | 26／48 | `ui-stable`、本地 `ui-core` | `ui` |
| `NotchExportSelectionProjectionBuilderTests` | 2／2 | `uncategorized` | `core` |
| `NotchExportSelectionRowSelectionServiceTests` | 5／5 | `uncategorized` | `core` |
| `NotchExportSelectionSummaryProjectorTests` | 2／2 | `uncategorized` | `core` |
| `RuntimeQueryUseCaseTests` | 2／18 | `notch-core` | `core` |
| `ProjectFileMigrationTests` | 1／9 | `application` | `core` |
| `ProjectStoreTests` | 1／2 | `application` | `core` |
| `ProjectModelCollectionIsolationTests` | 1／2 | `uncategorized` | `core` |
| `NotchExportGenerationCacheServiceTests` | 1／12 | `notch-core` | `core` |

這份清單沒有可以現在整類刪除的測試。只有一個方法的 golden／benchmark 類也保護 V22，必須保留或改寫。

## 2. 方法級清單

### 2.1 Generator、exporter 與設定

來源：[NotchTableGeneratorTests.cs](../../tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs)，22 個。

| 方法 | 現有範圍 | 移除後處置／先決條件 |
|---|---|---|
| `Generate_Skips_When_Freeform_None` | Legacy、V22 | 刪；先確認 CadAllocation 的 eligibility／no-op 契約，不能直接搬用 Legacy 的空表預期。 |
| `Generate_LegacyRegularAnchor_V22_BuildsCompatibilityGeometryRow` | Legacy、V22 9 欄 geometry row | 刪；不將此 compatibility payload 當作 canonical V22 預期。 |
| `Generate_LegacyRegularAnchor_VersionDispatchMatchesEligibility` | Legacy、兩版；3 個 freeform InlineData | 刪；先有 canonical V22 XWay／YWay／XYWay eligibility coverage。 |
| `Generate_LegacyRegularAnchor_FreezesRequestBeforeInitialProgressCallback` | Legacy、V22；callback 改 computation mode | 刪；先保留 normal request freezing coverage。 |
| `Generate_LegacyRegularAnchor_OwnsEnabledVersionsBeforeInitialProgressCallback` | Legacy、V22→V21 | 刪；單版不再有跨版本 mutation。 |
| `Generate_LegacyRegularAnchor_FreezesRowSettingsForUnbuiltRowsAndNextCallObservesChanges` | Legacy、V22；threshold／LenScale／NullValue | 刪；先鎖 V22 threshold／sentinel 的 current request 與 next request。 |
| `Generate_LegacyRegularAnchor_FreezesLinkedThresholdForUnbuiltRows` | Legacy、兩版 linked Q7 threshold | 刪；先有 V22 原生 threshold admission 與 request capture。 |
| `Generate_V21_Adds_Row_When_Cad_Matched` | V21、CadAllocation | 轉；先用 V22 鎖 CAD matched 的生成結果。 |
| `Generate_V21_YWay_PreservesLegacyNeighborOrientation` | Legacy、V21 | 刪；先有 canonical V22 YWay source／target orientation，預期不可從 Legacy 照抄。 |
| `Generate_V21_EncodesNeighborLegMagnitudeAsQ7` | Legacy、V21 payload Q7 | 刪；保留 V22 signed percent 的獨立契約。 |
| `Generate_Skips_When_Cad_Not_Matched` | Legacy、V22 | 刪；先確認 CadAllocation 可以依 geometry anchor 生成的相反契約。 |
| `ProjectCadAllocationResolvedBatch_ReusesVersionNeutralCandidatesAcrossFinalRequests` | V21-only／V22-only／both | 轉；保留同一 batch 在不同 V22 threshold／NullValue request 的 fresh parity。 |
| `Generate_CadAllocationMode_FreezesResolutionInputsAtCandidatePhaseStart` | 兩版；2 個 callback timing cases | 轉；兩個 timing case 都留，只移除 V21 expected rows。 |
| `Generate_CadAllocationMode_FreezesProjectionRequestAtCandidatePhaseStart` | V22→V21 mutation；2 個 timing cases | 轉；改鎖 V22 sentinel／threshold mutation，不可只留下原有一筆 V22 InlineData。 |
| `Generate_CadAllocationMode_ReportsOnlyExecutablePhases` | 兩版；有／無 CAD profile 2 cases | 轉；保留四階段、empty profile 與進度順序。 |
| `Generate_V21_WorksFromOverlapMatchAndAutoDetectPipeline` | Legacy、V21 | 轉；先有 V22 normal matching→freeform→generator characterization，再刪舊方法。 |
| `Generate_CadAllocationMode_ProducesFewerRowsThanLegacy_WhenCadSpansMultipleRegularPads` | 兩版、兩種 computation mode | 轉；以 V22 同一 CAD 的唯一 anchor／row set 作固定預期，撤掉相對 Legacy 的 4 對 2 計數。 |
| `Generate_CadAllocationMode_UsesGeometryAnchor_WhenLegacyMatchMissing` | V21、CadAllocation | 轉；換成 V22，仍驗證沒有 matched CAD 時的 geometry fallback。 |
| `Generate_LegacyRegularAnchor_V22_PreservesCompatibilityNeighborOrientation` | Legacy、V22 9 欄 | 刪；先有 canonical V22 orientation characterization。 |
| `ProjectCadAllocationResolvedBatch_ValidatesCombinedPercentOnlyAfterFinalThresholdAdmission` | V21／V22／both | 轉；留 V22 threshold rejection／admission 與 overflow 時機。 |
| `Generate_CadAllocationMode_WhenSubMicroPolygonChangeFlipsAllocation_UsesCurrentGeometry` | 兩版 inline expected snapshots | 轉；保留 V22 左／右 anchor、微小 geometry change 與 cache identity。 |
| `Generate_CadAllocationMode_IsDeterministicAcrossRuns` | 兩版，跨 IC、多次生成 | 轉；以 V22 固定 row order／payload 驗證五次重跑。 |

來源：[NotchTableExporterTests.cs](../../tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs)，10 個。

| 方法 | 現有範圍 | 移除後處置／先決條件 |
|---|---|---|
| `ExportAsCsv_UsesFixedPayloadColumnsForMixedValueLengthRows` | V21 9 欄＋V22 7 欄 | 轉；留 V22 CSV 欄序、escaping、空白 padding；欄位是否收縮另需產品契約。 |
| `ExportAndSimulation_RejectNullDiffOutsideFirmwareUint16Domain` | V21／V22 InlineData | 轉；只刪 V21 case，留 V22 exporter／simulation 的相同例外。 |
| `ExportAndSimulation_PreserveUint16MaxDiffWhenSentinelIsAdjacent` | V21 Legacy／V22 CadAllocation InlineData | 轉；留 V22 `65535` 有效 diff 與相鄰 sentinel 的 exact 結果。 |
| `ExportAsCInitializer_SnapshotsNullDiffBeforeReadingCallerCollections` | V21 Legacy／V22 CadAllocation InlineData | 轉；留 V22 sentinel capture，不刪 caller-collection mutation 測試。 |
| `ExportAsCInitializer_EmitsVersionSpecificFwFiles` | 兩版，各自匯出，混版 C fail-fast | 轉；留 V22 ABI／formatting；混版拒絕斷言隨不可建構的 V21 row 移除。 |
| `ExportAsCInitializer_GccCompilesFwStyleExports_WhenGccIsAvailable` | 同方法編譯兩版 C | 轉；保留 V22 GCC compile。 |
| `ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21_WhenGccIsAvailable` | V21 CadAllocation | 刪；V22 GCC／C# parity 先保持可執行。 |
| `ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21LegacyRegularAnchor_WhenGccIsAvailable` | V21 Legacy | 刪；Legacy cells-only／EMS 契約消失後才刪。 |
| `ExportAsCInitializer_GccAndCSharpSimulationMatchV21Q7BoundaryContract_WhenGccIsAvailable` | V21 magnitude 0／1／127／128／129／255 × ADD／SUB | 刪；不移除共享 allocation rounding；V22 signed percent 邊界另鎖。 |
| `ExportAsCInitializer_V21ThreeTerms_SplitsNodesAndPreservesInt16Wrap` | V21 continuation／INT16 narrowing | 刪；先補足 V22 多項 writeback／wrap characterization，不能只靠一般 continuation case。 |

其他涉及方法：

| Class／來源 | 方法 | 處置 |
|---|---|---|
| [NotchSettingsTests](../../tests/FreeformHelper.Tests/Application/Settings/NotchSettingsTests.cs)（2 個） | `ProjectSettingsValidate_RejectsV21ThresholdOutsideIndependentQ7Range`；`ProjectSettingsValidate_AcceptsV21ThresholdQ7Boundaries` | 刪；各有 2 個 InlineData。先有 V22 原生 threshold 的有效範圍／邊界 characterization；保留同類 NullValue 與 compensation model 測試。 |
| [NotchV22CompensationServiceTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchV22CompensationServiceTests.cs)（1 個共享邊界） | `BuildAllocations_UsesAwayFromZeroAtV21ThresholdHalfStep` | 留／改名：實際測 `NotchAllocationService.BuildAllocations`，沒有選 V21。Q7-positive allocation 仍供 V22 使用，不能因名字刪除。 |
| [NotchCadOutputFwDiffProjectionServiceTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchCadOutputFwDiffProjectionServiceTests.cs)（1 個） | `Project_WhenCadDiffsShiftLeaveGap_GapDoesNotRemainAsAnchor` | 轉；fixture 只有 V21，先用 V22 覆蓋同一 gap／anchor mapping 行為。既有 typed V22 anchor-only case 不能自動證明此 gap 情境。 |

### 2.2 Simulation 與 review

來源：[NotchApplySimulationServiceTests.cs](../../tests/FreeformHelper.Tests/Workflow/NotchApplySimulationServiceTests.cs)，6 個。

| 方法 | 現有範圍 | 處置 |
|---|---|---|
| `Simulate_WhenCanonicalContinuationRowsAreProjected_PreservesEachFirmwareContract` | 同方法跑兩版 | 轉；保留 V22 continuation 的 cells、actions、legs、contract text。 |
| `Simulate_WhenVersionIsV21_UsesProjectedCanonicalContract` | V21 imported 9 欄 row | 刪；保留 V22 imported canonical row 路徑。 |
| `Simulate_WhenV21LegTargetsItsOwnSource_KeepsActionFlowAlignedWithFirmwareCells` | V21 self-target | 刪；先有 V22 self-target 的 firmware／cells／flow 一致性。 |
| `Simulate_WhenV21ContinuationRowsClampIndependently_ReportsEffectiveRetainedPercent` | V21 continuation Q7/clamp | 刪；先鎖 V22 continuation 的 retained percent／INT16 writeback。 |
| `Simulate_WhenFrameIsOutsideInt16Domain_UsesFirmwareBaselineForCellsAndAudit` | V21／V22 InlineData | 轉；留 V22 case 的量化、非有限值、cells／audit baseline。 |
| `Simulate_WhenV21ProjectionReferencesMissingDiffs_WritesExactDiagnostics` | V21 缺 source／target | 刪；先有 V22 missing-diff diagnostics 與 dropped-node／action 一致性。 |

| Class／來源 | 方法（各 1 個） | 處置 |
|---|---|---|
| [NotchApplySimulationReviewUseCaseTests](../../tests/FreeformHelper.Tests/Workflow/NotchApplySimulationReviewUseCaseTests.cs) | `BuildSnapshot_WhenV21RowsImported_UsesSharedProjectedCanonicalPath` | 刪；保留 `BuildSnapshot_WhenV22RowsImported_UsesSharedSimulationPath` 的 review／snapshot projection。 |
| [SimulationWorkspaceUseCaseTests](../../tests/FreeformHelper.Tests/UI/Services/SimulationWorkspaceUseCaseTests.cs) | `BuildScenarioSnapshot_PropagatesLegacyCompatibilityModeToTheSharedFirmwareProjection` | 刪；另兩個 V22 scenario／copper replay tests 保留，EMS／global-flow audit 不隨 Legacy 移除。 |

### 2.3 Golden 與 benchmark

| Class／來源 | 方法 | 處置 |
|---|---|---|
| [NotchExampleCExportDriftTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchExampleCExportDriftTests.cs)（2 個） | `Boe3635_SignedGoldenManifest_MatchesCheckedInArtifacts`；`Boe3635_CheckedInCExample_MatchesCurrentExportPipeline` | 轉；前者留 V22 與 input manifest lock，後者只刪 V21 InlineData。`NormalizeText_OnlyNormalizesLineEndingsAndSingleEofNewline` 保留。 |
| [NotchGoldenBaselineTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs)（1 個） | `RealProjects_MatchGoldenBaseline` | 轉；內部及 workflow helper 強制兩版，改成 V22 characterization 後保留 3635／TM8.1 regression。 |
| [Tm81NotchAcceptanceMatrixTests](../../tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs)（1 個） | `TM81_MatchesAcceptanceMatrixSnapshot` | 轉；內部強制兩版。保留 V22 no-op／warning、ToFull、diff repair、case contracts。 |
| [Notch3635GenerationBenchmarkTests](../../tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs)（1 個） | `Boe3635_NotchGenerationBenchmark_WhenEnabled` | 轉；`NotchCurrentWorkflowTableTestHelper.GenerateRawTableAsync` 強制兩版。改成 V22 workload 並重新量測，保留 opt-in。 |
| [Cad3635EndToEndBenchmarkTests](../../tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs)（1 個條件邊界） | `Boe3635_EndToEndWorkflowBenchmark_WhenEnabled` | 留／確認後轉；Load project→Step5 跟隨保存的版本／file type，程式不強制雙版。公開 3635 provenance 記載兩版 enabled，但本盤點未讀 private project；不得宣稱此 benchmark 固定產生兩版 C。 |

`NotchToFullCoverageSnapshotBuilder`／`NotchCurrentWorkflowTableTestHelper` 是 helper，不是 test class；後者在 raw／export 入口都設 `EnableV21=true`、`EnableV22=true`，故不能只改上表 theory 的 InlineData 而漏掉 helper。

### 2.4 主 ViewModel、RuntimeQuery 與 project

`FreeformHelperViewModelTests`（9 個；來源為下列 partial files，皆在 `UI/ViewModels/`）：

| Partial file | 方法 | 處置 |
|---|---|---|
| `FreeformHelperViewModelTests.Basics.CoreFlags.cs` | `NotchVersion_DefaultsToV21AndV22Enabled` | 轉；V22-only default characterization 先存在。 |
| 同上 | `LenScale_RemainsHidden_WhenV21Enabled` | 刪；移除 V21／Legacy 設定後先鎖剩餘 V22 設定可見性。 |
| `FreeformHelperViewModelTests.NotchExportNaming.cs` | `ResolveSuggestedNotchExportBaseName_WhenMixedVersionsOrNonC_KeepsRequestedName` | 轉；移除 mixed-version 情境，留 CSV／non-C 的 requested name。 |
| `FreeformHelperViewModelTests.NotchExportCache.cs` | `ExportNotchCommand_LegacyRegularAnchor_FinalSettingChangesRemainRequestSpecificTableMisses` | 刪；legacy request-specific table cache 消失後才刪。 |
| 同上 | `ExportNotchCommand_WhenVersionChanges_ReusesResolvedBatchAndMatchesFreshProjection` | 轉；2 個 V21↔V22 theory cases 換成 V22 final-only request 的 warm／fresh parity，不能連 cache contract 一起刪。 |
| 同上 | `ExportNotchCommand_ThresholdReprojectsButResolutionSettingRebuildsBatch` | 轉；目前只匯出 V21。先鎖 V22 threshold／NullValue 可 reproject、computation settings 需 rebuild、zero-row batch 可重用。 |
| `FreeformHelperViewModelTests.NotchResolvedSnapshot.cs` | `ResolvedSnapshot_OutputOnlyStateReusesIdentity_ComputationChangeInvalidatesIt` | 轉；拿掉 V21-only／both toggles，留 V22 profile／format／threshold 的 output-only identity 與 computation invalidation。 |
| `FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs` | `LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields` | 刪／替換須等舊 project 處置決策；目前是 Legacy＋V22，不能因只刪 V21 exporter 就刪。 |
| 同上 | `SaveThenLoadProject_PersistsStep5NotchVersionSelection` | 轉；原測 V21-only 保存／回載。先有 V22 設定／選擇 roundtrip；不在此規劃決定舊欄位如何遷移。 |

| Class／來源 | 方法 | 處置 |
|---|---|---|
| [RuntimeQueryUseCaseTests.ExportAndStatus.cs](../../tests/FreeformHelper.Tests/UI/Services/RuntimeQueryUseCaseTests.ExportAndStatus.cs)（2 個） | `ExecuteAsync_QueryStatus_IncludesCadLoadSpinnerDebugTelemetry`；`ExecuteAsync_QueryExportNotch_AcceptsVersionedCFormat_WhenWorkflowNotReady` | 轉；前者移除 `enableV21` schema 斷言，留 V22/export state、spinner telemetry；後者目前 V21=true／V22=false，留 V22 format 暫切後 state 恢復與 workflow-not-ready failure。 |
| [ProjectFileMigrationTests.cs](../../tests/FreeformHelper.Tests/Application/Project/ProjectFileMigrationTests.cs)（1 個） | `Load_MigratesNullCollections` | 轉；移除預設集合含 V21 的斷言，保留 null collection normalization 與 V22 fallback。 |
| [ProjectStoreTests.cs](../../tests/FreeformHelper.Tests/Application/Project/ProjectStoreTests.cs)（1 個） | `SaveAndLoad_RoundTripsMatchingAndIndexMappingSettings` | 轉；`UiNotchSnapshot.EnabledVersions` fixture 含兩版；保留其餘 matching／mapping／mask／V22 settings roundtrip。 |
| [ProjectModelCollectionIsolationTests.cs](../../tests/FreeformHelper.Tests/Application/Project/ProjectModelCollectionIsolationTests.cs)（1 個） | `ProjectUiSnapshot_ListSetters_CloneInputCollections` | 轉；用 V22 list 保留 defensive copying，不能因兩版字串 fixture 刪 isolation contract。 |
| [NotchExportGenerationCacheServiceTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs)（1 個共享邊界） | `CadAllocationBatchAndLegacyTable_AreTypeIsolatedAndTrackLatestProjectedRowCount` | 轉；沒有生成 LegacyRegularAnchor row，實際驗證 normal batch 與 request-specific table cache 隔離。保留正常 batch lease／row-count coverage，再撤 legacy cache 半段。其餘 fault／retry／epoch／collision 方法保留。 |

### 2.5 Export selection 的混合 fixture

`NotchExportSelectionViewModelTests.BuildSampleTable()` 放入兩個 V21 row 與一個 V22 row；不能只搜尋方法本文的 V21。以下 **26 個**方法涉及此 fixture 或顯式 Cv21 選擇；其餘 22 個使用 V22 fixture／其他獨立情境，不列作可刪除項。

| Partial file（`UI/ViewModels/`） | 方法 | 處置 |
|---|---|---|
| `NotchExportSelectionViewModelTests.Basics.cs`（20 個） | `Collections_ExposeReadOnlyFacade`；`Ctor_SelectsAllRowsByDefault`；`SelectNoneCommand_ClearsAllSelections`；`BuildSelectedTable_ReturnsOnlyCheckedRows`；`Ctor_BuildsIcGroupsAndSelectsFirstPreviewRow`；`SelectPreviewRowCommand_UpdatesPreviewSelection`；`SelectedRowPositionText_TracksVisibleRowIndex`；`GroupCommands_OnlyAffectTargetIcRows`；`SelectPreviewRowCommand_InvokesPreviewCallback`；`SelectPreviewRowCommand_ReinvokesPreviewCallback_WhenSameRowIsSelected`；`CodePreview_UsesBracketOnlyValuesFormat`；`Ctor_WithExportTypeOptions_UsesSelectedType`；`SelectedExportTypeOption_Cv21_PinsVersionSelection`；`SelectedExportTypeOption_Cv21_StillPinsVersionWhenTableHasNoV21Rows`；`Ctor_ReportsPassingToFullCoverageAudit`；`Ctor_ReportsMissingToFullCoverageAudit`；`AttachSimulationSafetyAudit_WhenViolationExists_BlocksExport`；`AttachSimulationSafetyAudit_WhenClean_DoesNotBlockExport`；`AttachSimulationSafetyAudit_WhenPhysicalAuditWarns_ShowsNonBlockingWarning`；`AttachSimulationSafetyAudit_WhenUnavailable_RemainsNotRun` | 轉；用 typed V22 rows 保留多 IC、selection、preview callback、no-CAD／warning、safety 情境。兩個 Cv21 方法先補 Cv22 pin／empty-table counterpart，再刪舊情境；最後一個 theory 的 null／empty 2 cases 都留。 |
| `NotchExportSelectionViewModelTests.Filtering.cs`（3 個） | `SearchKeyword_FiltersVisibleRows_ByMappingTokens`；`ClearSearchKeywordCommand_ResetsAllViewFilters_ToViewAll`；`SelectedVersionOption_FiltersVisibleRows_AndExportRows` | 轉；前兩個保留 filter／reset。第三個先鎖 V22 visible／selected export scope，單版 version filter 消失後才刪舊斷言。 |
| `NotchExportSelectionViewModelTests.RowsAndWorkspace.cs`（3 個） | `ApplyWorkspaceSelection_BuildsLinkedRows_AndToggleCommandsAffectThoseRows`；`ApplyWorkspaceSelection_DoesNotInvokePreviewCallback_UntilUserClicksRow`；`HidePanelForInspectCommand_InvokesAttachedAction` | 轉；保留 workspace linkage、callback 時機與 hide action。 |

以下 UI service fixture 亦含 V21；雖然檔案位於 `UI/`，CI 仍屬 `core/uncategorized`：

| Class／來源 | 方法 | 處置 |
|---|---|---|
| [NotchExportSelectionProjectionBuilderTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportSelectionProjectionBuilderTests.cs)（2 個） | `Build_ProjectsVisibleRowsGroupsAndWorkspaceLinks_FromSingleScope`；`BuildColumnFilterValues_ExcludeTargetFieldFilter_ButKeepSharedScope` | 轉；`BuildProjectionRows` 含 V21，即使目前 scope 選 V22 也須清理 fixture；留 visible／group／workspace/filter scope。 |
| [NotchExportSelectionRowSelectionServiceTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportSelectionRowSelectionServiceTests.cs)（5 個） | `SelectAll_MarksAllRowsSelected`；`SelectNone_ClearsAllSelections`；`SelectVisibleOnly_KeepsOnlyVisibleRowsSelected`；`SetGroupSelection_AppliesSelectionFlagToGroupRows`；`SetWorkspaceLinkedRowsSelected_OnlyTouchesWorkspaceRows` | 轉；`BuildRows` 含兩版；保留跨 IC、visible 與 linked subset selection。 |
| [NotchExportSelectionSummaryProjectorTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportSelectionSummaryProjectorTests.cs)（2 個） | `Build_ProjectsScopedCountsAndTexts`；`Build_NoSelection_UsesDefaultExportText` | 轉；`BuildRows` 含 V21；保留 V22 scoped counts／summary／empty selection，不照抄混版總數。 |

### 2.6 名稱含 Legacy／Q7 但不屬於移除清單

- V22 `ShortV22Row_PreserveLegacyCompatibility`／`ShortV22Row_PreserveExactOverflowException` 是 V22 的舊 9 欄 input adapter，不等於 `LegacyRegularAnchor`。除非另行核准移除該 adapter，保留 typed/untyped normalization、overflow 與 payload characterization。
- `NotchV22CompensationServiceTests.HasQ7PositiveAllocationInIc_MatchesBuildAllocationsAcrossIcAndQ7Boundary` 及 `BuildAllocations_UsesAwayFromZeroAtV21ThresholdHalfStep` 保護共享 allocation admission。移除 V21 output 不代表可改 `NotchThresholdQ7Contract` 或 Q7-positive geometry evidence。
- `Compute_ToFullRuleEngineToggle_KeepsLegacyGateResultEquivalent`／`Compute_ToFullRuleTraceEnabledWithLegacyPath_ReportsLegacyTraceStep` 的 Legacy 是 V22 ToFull rule-engine fallback，並非 `LegacyRegularAnchor`。matching 的 `LegacyOverlap`、舊 channel limits／ignored matching values 也不是本次目標。
- `ProjectFileMigrationTests.Load_DropsUnsupportedLegacyNotchVersions` 測的是 `30/31` 與 `v3.0/v3.1` fallback 到 V22，不是 V21 migration；保留。
- `UiSnapshotPersistenceContractTests` 檢查 snapshot settable-property contract，已排除 `[LegacyUiSnapshotField]`；不是雙版 execution test。保留 schema guard，將來若 active contract 有已核准變更，再同步期待值。

## 3. Golden、snapshot 與 baseline 處置

以下「刪除／保留／重建基準」都是移除獲准後的建議；本次沒有讀取或更新私有檔案。G2 包含 G3 的三個 golden classes；G6 透過 refactor gate 重跑它們。G0 不消費資料 golden，G1 依 class filter 消費該測試的 expected data。

### 3.1 固定預期與簽核輸入

| 名稱 | V21／Legacy 關聯與使用 gate | 處置與理由 |
|---|---|---|
| `example/BOE36.35/notch_export_v21_current.c` | V21 signed C；G1 drift、G2、G3、G4／3635 script、G6 | **刪除** active golden／manifest reference；V21 output 消失後無 active consumer。舊 signed provenance 保留在歷史文件／既有版本紀錄，不把它改造成 V22 expected。 |
| `example/BOE36.35/notch_export_v22_current.c` | V22 signed C，但 shared formatter 的 metadata 仍寫 `Sections: v2.1=empty, v2.2=present`；同上 gates | **重建基準（限核准的移除文字差異）**：先證明原 V22 firmware nodes／函式／順序仍 exact；移除 V21 metadata 若改整檔 bytes，逐項簽核後重簽。若獲准實作保持整檔不變，直接保留原 signed golden；不能一併放行演算法差異。 |
| `example/BOE36.35/notch_export_golden_manifest.json` | drift test／3635 script 讀 `outputs.v21`、`outputs.v22` 及 project／regularMask hash、bytes、nodes；G2、G3、G4、G6 | **重建基準**：移除 active `outputs.v21`，保留 input lock；只有上一列核准變更時更新 V22 hash／bytes，nodes 不應因刪版本而改。不能同時改 input 與 expected 掩蓋漂移。 |
| `example/golden-snapshots/notch-golden-baseline.json` | `RealProjects_MatchGoldenBaseline` 強制兩版；`rowCount`、`versionCounts`、`icCounts`、distribution、first/last、`firstV21Adjusted` 可帶 V21；G1、G2、G3、G6 | **重建基準**：以移除前的 V22 projection 作 reference，移除 V21 sample／version bucket，重新建立 V22-only aggregate。保留 V22 main/no-op samples、ToFull coverage 與 3635／TM8.1 projects；不可刪整份 snapshot。 |
| `example/golden-snapshots/tm81-notch-acceptance-matrix.json` | 兩版 totals／distribution，另含 V22 no-op、warning、ToFull、diff repair／case contracts；G1、G2、G3、G6 | **重建基準**：只讓雙版 totals／version counts／Legacy distribution 等已核准 shape 改變；V22 case contracts、warning maps、repair、ToFull 仍比移除前 V22 預期。 |
| `example/BOE36.35/project_3635.json`、manifest 的 `regularMask.path`（公開 provenance 稱 `SeeRegular.csv`；完整路徑未讀） | 3635 test／script input；公開 provenance 記載兩版 enabled、linked threshold；manifest 鎖 project／mask；G2、G3、G4、G6 | **保留** authoritative input 與 hash。單版如何載入舊 version/mode 欄位須先由 owner 決定，不能為測試通過改 private input。mask 是共享 V22 geometry evidence。 |
| `example/TM 8.1/TM8.1.json`、`example/TM 8.1/SeeRegular.csv` | matrix test 的 project／mask 名稱；測試載入後強制兩版；G2、G3、G6 | **保留** V22 characterization input；其原始內容未讀。測試改用 V22-only request，不假設 project 原來有哪些 legacy 欄位。 |
| `NotchTableGeneratorTests` 的 inline snapshots：`ExpectedSubMicroLeftAnchorRows`、`ExpectedSubMicroRightAnchorRows`，以及第 2.1 節 Legacy／兩版方法中的 expected rows | 明列 V21 row、Legacy V22 9 欄、混版 row order/count；G1、G2、`application`、G6 | **重建基準／刪除專屬部分**：先抽出固定 V22 expected，再刪 V21 entries／Legacy row snapshots。保留 `Generate_CadAllocationMode_V22_SnapshotStable_ForSimpleTriangle` 的 canonical V22 snapshot。 |
| exporter／simulation 中 `ExpectedV21Q7Boundary*`、`V21CanonicalMainPayload`、`V21CanonicalContinuationPayload`、`V21LegacySamplePayload`、`V21LegacyValues` 等 inline fixture | 第 2.1、2.2 節專屬與混版 parity 方法；G1、G2、G6 | **刪除**無 V22 consumer 的 V21 fixture；混版方法先留下獨立 V22 expected cells／actions／C，不能把 Q7 fixture 自動轉 percent 後當真值。 |
| `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json` | 9 個 XAML source hashes；所列 source 沒有直接 V21/Cv21/LegacyRegularAnchor binding，沒有 Firmware payload；G1 UI snapshot、G5、G6 | **保留**；不是直接帶 V21 payload 的 baseline。將來若已核准移除實際觸及所列 source，才逐 entry 重建基準；子 view 改動不會自動改 parent source hash，不可把這 9 個 entries 說成完整版本選項 coverage。 |
| `tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json` | `MainWindow.ConsoleExpanded`、`SettingsWindow.Default`、`HowToUseView.Default` 的 rendered hashes；G1、G5、G6 | **重建基準（受影響 surfaces）**：V21 控制項／說明消失若影響畫面，先 dry-run／檢視後逐 surface 簽核；未影響者保留，尺寸及 `maxDistance` 不隨意放寬。既有三個 surface 不代表已預覽所有 Step5／Dev page。 |

`notch-golden-baseline.json` 的 project keys／私有實際 values 沒有讀取；3635／TM8.1 範圍依 roadmap，矩陣與 workflow table seams 依測試程式。`distribution.legacy` 是 export-review row 分類，不能未經分析就等同 computation mode；單版後其刪除或歸零需由既有分類 predicate 決定。

### 3.2 3635 script 產物與量測基準

腳本預設 output root 是 `build/perf/3635-regression-latest/`，另可指定 forward／reverse run 目錄。以下是程式宣告的檔名，沒有檢查任何既有 run。它們是 actual／measurement artifact，不能取代 checked-in signed expected。

| 名稱 | 使用 gate／V21 關聯 | 處置與理由 |
|---|---|---|
| `notch_v2.1.c`、`runtime-export-c-v21.json` | G4／3635 script 的 V21 actual 與 IPC response | **刪除**未來 run 的 V21 export step／artifact；歷史 run 不重寫。 |
| `notch_v2.2.c`、`runtime-export-c-v22.json` | G4 V22 actual；response含 format／elapsed／path | **重建基準**：重新生成後對 signed V22 golden exact；elapsed 是量測，不要求零差異。 |
| `notch_table.csv`、`runtime-export-csv.json` | G4，CSV 可含兩版 rows；budget 量測 CSV | **重建基準**：以 V22-only rows、保留的 CSV 欄位契約與固定排序生成；舊雙版 size／row count 不可沿用。 |
| `runtime-status-before-exports.json`、`runtime-status.json`、`regression-baseline-summary.json`、`regression-baseline-summary.md` | G4，包含 `enableV21`、兩版 order／checks／signedActuals／exports／state | **重建基準**：只保留 V22 gate/schema；比較 before/after 的剩餘 export state，golden check 必須非 skipped 且 pass=true。 |
| `runtime-load-project.json`、`runtime-run-step1.json`、`runtime-run-step2.json`、`runtime-run-step3.json`、`runtime-run-step4.json`、`runtime-ui-instance-status.json`（isolated mode 才有） | G4 workflow／UI instance 診斷，load/status 可能攜帶舊設定或摘要 | **重建基準** operational run；共同幾何／workflow 的語義預期保留。不主張每個 response 一定含 V21，依公開 code/schema 判讀差異。 |
| `runtime-selection.json`、`runtime-notch-stage.json`、`runtime-notch.json`、`runtime-notch-validation.json` | G4 query artifacts，可能含 versions、row totals／projection diagnostics；部分純 V22 evidence | **重建基準** run/schema；保留同一 V22 result 的 stage／selection／validation 一致性。不能把 query output 一律當作零差異文字檔，須排除 pid／path／timing 等 operational 欄位。 |
| `selection-latency.json` | G4／3635 budget；可能因少一版 projection 影響 timing，沒有 V21 payload | **保留**量測機制，重新量測；elapsed/p95 不設 zero-diff，舊可比環境紀錄留存。 |
| `docs/performance/regression-baseline-3635.budget.json` | script budget：selection sample count／total p95／inspector p95／CSV elapsed／C V22 elapsed | **保留**既有上限。沒有 C V21 budget 欄位，不需要因版本移除修改數字；工作量改變後重新量測，不放寬 budget。 |
| `docs/performance/regression-baseline-3635.md` | G4 操作／signed provenance／兩版 forward-reverse 與 performance reference | **重建基準紀錄**：新增經簽核的 V22-only 操作與結果，清楚區分原兩版 measurement；保留舊簽核來源與比對證據。本次不改此檔。 |
| `docs/performance/3635-selection-export-baseline-2026-03-05.md` | 既有 non-isolated 歷史量測；不是 current gate | **保留**歷史註記，不升格為移除後 expected。 |
| `build/perf/3635-notch-generation-benchmark.json`、`build/perf/3635-end-to-end-benchmark.json`、`build/perf/3635-e2e-export/run-*.c` | opt-in benchmarks；前者 helper 強制兩版，後者跟隨保存設定；非 G3/G4 必跑 golden | **重建基準** V22 workload measurement；先確認相同環境／輸入／warmup，再比較 performance。舊 `BuildLegacyRowsElapsedMs` telemetry 不代表實際進 Legacy mode。 |
| `build/notch-golden-baseline.actual.json`、`build/tm81-notch-acceptance-matrix.actual.json`、`build/notch-example-drift-{version.ToDisplayLabel()}.actual.c` | G3 失敗時 actual dump，可帶 V21；G2/G6 也可能產生 | **刪除**過期 V21-only 本地 actual；V22/mixed dump 重新生成作診斷，永不自動覆蓋 expected。 |

G0 的 analyzer／code-size baseline 與 G5 的 layout／headless guard 不因 V21 消失而整體刪除。`UiLayoutGuardTests`、`UiVisualSnapshotTests`、`UiRenderedVisualSnapshotTests`、`HeadlessUiSmokeTests` 仍使用 `ui-snapshots`／`ui-stable`（另有本地 `smoke`）原有入口。

## 4. 單版後 zero-diff 的比較對象

現在仍是 `V21_before == V21_after` **且** `V22_before == V22_after`；不是 `V21 == V22`。移除獲准前不得改寫這條 gate。獲准後應先從移除前 source、相同 CAD／grid／active mask／layer-filter／compensation settings／V22 threshold／NullValue／profile 捕捉 **V22-only reference**，再對移除後結果比較。

若只刪 V21，V22 semantic output 應 exact；若從 `LegacyRegularAnchor` V22 geometry row 改成 canonical CadAllocation，兩者不是相同契約，不能宣稱 zero diff。舊 Legacy project 如何拒絕／讀取／轉換仍需 owner 決定，並由另一組產品行為測試驗收，不能以新 baseline 掩蓋轉換差異。

| Gate | 移除獲准後的規劃措辭／比較對象 |
|---|---|
| G0 Build/Lint | 維持 build、analyzer、CRLF 與 lint 標準；不是數值 zero-diff。沒有放寬 lint 或以「少一版」降低檢查範圍。 |
| G1 Targeted | V22 caller 的 current/fresh、typed/untyped、request capture、排序、sentinel、exception、cache、audit 契約等於移除前 V22 characterization；需要變更的產品行為有獨立預期與決策。 |
| G2 Notch Core | 剩餘 generator/export/simulation/projection/CLI tests 全綠；保留 V22 GCC／C# exact parity、共享 geometry／allocation、single-result projection。測試數下降要對到方法／case 清單，不用舊 case 數當 hard gate。 |
| G3 Golden | V22 actual C 對**已簽核 V22 golden**；V22-only 3635／TM8.1 snapshot 對**移除前捕捉的 V22 子集與核准的單版 schema**。不比兩版 aggregate，也不比本次執行自己產生的 actual。 |
| G4 Runtime CLI | 隔離 UI/IPC 的 C V22 對同一 G3 signed golden，並與 Application production-equivalent seam 一致。保留 production selected layers／active mask。以 CSV→C V22、C V22→CSV、重複 C V22 的 cold/warm sequences 保護輸出與 state invariance，代替跨版本 export order。 |
| G5 UI | 未受影響 XAML hash／surface 保持既有基準；已核准刪除版本控制項的 surfaces 對**經檢視簽核的 V22-only UI baseline**。Rendered guard 延用既有 hash distance 規則，不能說所有 pixels literal zero diff；layout、theme、Dev preview／headless guards 繼續有效。 |
| G6 Merge | 維持 full lint＋refactor gate＋UI snapshots；引用新的 V22-only expected 與產品變更簽核紀錄。`uncategorized` 仍須覆蓋沒有列入 named groups 的 service／benchmark classes。 |

C 比較沿用既有 normalization：CRLF／LF／lone CR 統一為 LF，最多容許一個 optional EOF newline；其他 whitespace、comments、排序維持 ordinal exact。Manifest 的 raw SHA-256／bytes／nodes 是另一個保留的嚴格檢查，不能因 normalized compare pass 而省略。

刪除 `Sections` 中 V21 文字、version control 或 mixed-version aggregate 是可列舉的預期差異；先以舊 V22 reference 證明除此之外無差異，再簽核新 expected。現行 roadmap 第 3.1 節只允許已完成的 R13.002 provenance correction；本文件不新增例外。Golden 失敗仍先保留 actual diff，不啟用 update 環境變數取得綠燈。

## 5. 先保 coverage，再刪測試的順序

此處是依賴順序，沒有日期、release 版本或「現在開始」的指令。每一步的先決條件尚未成立時，V21／Legacy 測試與 gate 都保留。

1. **先確認產品邊界。** Owner 必須另行授權移除，並決定舊 project 的 V21-only／both／Legacy+V22 設定與舊格式輸入如何處理，以及 V22 C comments／CSV schema／RuntimeQuery status 的可見差異。先保存原 signed references；不預先做 migration、adapter 或新的 extension point。
2. **先鎖單版 V22 結果。** 在既有 test classes／fixture 機制內建立下表 characterization，保存移除前 V22 expected；不要從新版 actual 反推 expected。既有相同 coverage 能沿用者只需明確保持，不要求重複測試。
3. **先拆混版 fixture／theory。** 第 2.5 節 UI fixtures 先換 typed V22，保留原 selection／filter／preview／safety 情境；exporter 的 sentinel theories、simulation 的 INT16 theory、generator callback timing theories 只撤 V21 部分，保留共同 cases。更新 G3 的單版 expected 必須走簽核；不能先刪 golden classes。
4. **再刪 V21-only assertions／methods。** 對應 V21 output 已移除且 V22 characterization 全綠後，刪 payload Q7／V21 GCC／projected simulation 專屬方法與無 consumer fixture。共享 Q7-positive allocation、INT16、UINT16、V22 compatibility input tests 保留。
5. **Legacy 方法最後獨立處理。** 只有 LegacyRegularAnchor 的 computation／讀取政策也已移除，才刪 Legacy V22 geometry-row、dispatch/freezing、request-specific cache、scenario legacy apply 與保存舊 mode 的測試。先留 normal batch lease／row-count、V22 orientation／eligibility、project loading／roundtrip 的獨立 coverage。移除 V21 exporter 本身不充分。
6. **最後清 gate 依賴與 active baseline references。** V22 G2/G3/G4 成立後才撤 V21 C path／manifest entry／script step／artifact，重建受影響 aggregate／metadata／UI expected。刪除或改名 class 時同步既有 `run-tests.ps1` lists，避免 stale-name validation 或 CI coverage 縮水；留所有 V22 golden classes。完整驗證仍用 G6，不能只依 class-filtered unit tests 宣稱完成移除。

### 5.1 必須先存在的 V22 characterization

| 契約 | 可保留的現有證據 | 刪除前須補足／拆出 |
|---|---|---|
| Canonical geometry／matching／orientation | generator 的 `Generate_CadAllocationMode_V22_*`（source/target、跨 IC、ToFull、模型／active set）；typed V22 projection tests | XWay／YWay／XYWay eligibility、matched／unmatched CAD、geometry fallback、同 CAD 多 regular 的固定 V22 anchor／rows；不能借 Legacy row 語義當 expected。 |
| Request capture／final admission／determinism | `Generate_CadAllocationMode_FreezesProjectionThresholdAtCandidatePhaseStart`、`SnapshotsCadOutputFwDiffMapAfterProfiles`、`FinalThresholdFiltersRowsAndCoverageAuditFromSameCandidateView` | 混版 freeze-resolution/request、submicro snapshots、five-run determinism 轉 V22；保留每個 callback timing、threshold admission／overflow 與 row-count/audit consistency。 |
| Firmware ABI／numeric／sentinel | exporter 的 V22 GCC parity、continuation、INT16 truncation、typed/untyped、flags、per-IC dispatch、FwBaseMask、Release no-op tests | V22 signed percent／combine 邊界、self-target、多項 writeback/wrap、非有限／超界 baseline；兩版 sentinel theories 的 V22 exact 斷言保持。GCC 不存在時 early return 的綠燈不算 compiled parity 證據。 |
| Simulation／review／safety | `Simulate_WhenV22RowExists_TransformsAnchorAndTargetUsingCombinePayload`、`Simulate_WhenV22ContinuationRowsExist_MergesAllLegsIntoSingleAnchorAction`、V22 review、scenario／copper replay | 明確保留 mixed continuation 的 V22 cells/actions/legs、INT16 audit baseline、missing-diff diagnostics；V22 cell／action／EMS/global-flow 一致性不可由 V21 coverage 代替。 |
| Cache／single result／stale completion | VM 兩個 Release↔Debug profile cache tests、`ExportNotchCommand_TargetCoverageRequestReusesResolvedBatchAndMatchesFreshProjection`；`NotchGenerationStaleCompletion` partial 的 completion/currentness tests；cache fault/retry/epoch/collision tests | 把只跑 V21 的 threshold-reproject test 改為 V22：warm/fresh exact、zero-row reuse、sentinel/final-only change、computation rebuild；resolved identity test 留 V22-only output state cases；normal row-count lease 不隨 legacy cache 半段刪除。 |
| Export selection／presentation | 現有 V22-only transfer/no-op/no-CAD/filter tables 與第 2.5 節共同情境 | Cv22 pinning、沒有 V22 rows／empty selection、CSV/C V22 naming、selection/export scope、workspace preview callback、blocking EMS／nonblocking physical audit；先取代 Cv21 tests。 |
| Settings／project／RuntimeQuery | `ApplyViewSnapshot_Cv22ExportType_SelectsCv22`、unknown-property／collection isolation／V22 fallback tests | V22-only default、原生 threshold validation、V22 save/load roundtrip、accepted V22 format 成功／失敗都恢復 state；舊 V21/Legacy project 的預期先由 owner 決定。App settings isolation/deferred flush、Ctrl+S/toast 不在移除範圍。 |
| Production provenance／snapshots | V22 drift theory、signed manifest、TM8.1 V22 case contracts、G4 isolated IPC | 先捕捉相同 production filter/mask 下的 V22-only snapshot；metadata 差異與 firmware semantic 差異分開驗收。G3/G4 私有資料重驗由有權限的 commander／owner 執行，不以 missing-data skip 代替通過。 |

## 6. 本次驗證與尚待決定

本次只驗證規劃文件與可在沙箱執行、無須讀取私有資料的 class tests；不宣稱完成 G2～G6 的移除驗收。

- `dotnet build tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false`：成功；含 UI project，0 warnings／0 errors。
- 4 次 `dotnet test ... --no-build -p:UseAppHost=false --nologo --filter "FullyQualifiedName~<Class>"`：`NotchSettingsTests` 12、`NotchApplySimulationServiceTests` 12、`NotchApplySimulationReviewUseCaseTests` 2、`SimulationWorkspaceUseCaseTests` 3；合計 29 passed／0 failed／0 skipped。
- 文件檢查確認 23 類／100 個涉及方法，105 個完整方法名稱引用皆存在，CRLF 無 bare LF；`git diff --check` 通過。`pwsh -NoProfile -File ./scripts/verify.ps1 -StructureOnly` 通過：56 個 listed classes、12 個 workspace ownership cases、13 個 build-output selection cases，CRLF normalized=0、XAML issues=0；temporary report 已移除。
- build／test 前使用既有 prepare script 並略過 process stop／全工作區 normalization；指定文件單獨以 CRLF 寫入。沙箱不執行 lint、process listing、restore、submodule update、G3/G4 私有資料 gate、全專案 tests、commit、push、git config 或網路操作；沒有建立額外報告檔。

尚待 owner 決定的是移除授權、舊 project／compatibility input 的處置，以及可見 format/schema/UI 差異與重簽 golden 的範圍；這些不妨礙本次只做盤點的交付，也不代表已決定任何移除版本或時程。
