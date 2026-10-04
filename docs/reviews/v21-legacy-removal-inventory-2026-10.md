# V21／Legacy 完全移除盤點（2026-10）

## 目的、範圍與證據界線

本文件供 owner 後續決定「V21／Legacy 完全移除」的範圍與版本。Owner 已確認「2.1」指 Notch V21；owner 決定（2026-10-04，經 Commander 轉述）完全移除排在 1.3.x 之後，範圍之後再定，之前不做隱性轉換；目標版本未訂，本文件不提出版本、日期或執行授權。依 `TODO.md` 與 `docs/guides/refactor-roadmap-1.3.x.md` 的 2026-10-03 決定，現階段保留 V21／Legacy 原行為與 zero-diff gates，不再投入額外 legacy 收斂／等價性工作。

盤點基準為分支 `feature/queue/v21-legacy-removal-inventory`、commit `7477b07d3969ad6ad15960d0d1ac8b60ef37a9bc`。先讀 `AGENTS.md`、依賴圖及 domain/reference 文件；現有五個專案的 `ProjectReference` 與依賴圖一致，無須重建。沒有 `.codegraph/`，使用限定於 `src/`、`tests/`、`scripts/`、`docs/` 與根目錄文件的搜尋。

沒有讀取、列舉或驗證 `example/` 內容。下文的私有資料路徑僅來自測試／腳本原始碼及公開文件；資料是否仍齊全、實際 project 的 mode、golden 內容與覆蓋數量，仍需 commander 在允許的環境核對。未將任何私有 payload 帶入此文件。

三個範圍必須分開決定：

| 範圍 | 現況 | 移除影響 |
| --- | --- | --- |
| V21 輸出版本 | `NotchAlgorithmVersion.V21`；CadAllocation 也能產生 V21，並非僅 Legacy mode 使用 | V21 rows、Q7 firmware ABI、C v2.1、CSV 中的 V21、模擬與 UI／IPC 選項 |
| Legacy 計算模式 | `NotchComputationMode.LegacyRegularAnchor`；可產生 V21 **及 V22** | regular-anchor geometry、九欄相容 rows、request-specific table cache、舊 project 重現 |
| 其餘名為 legacy／Q7 的支援 | V22 raw-values reader、共用 allocation Q7、Step3 legacy inline rule gate、舊 UI snapshot 等 | 不等同前兩項；須按 consumer 判斷，不能全字串刪除 |

## Production 路徑：generator、投影與 firmware

以下以 `檔案:符號` 定位；「分支」只指該符號內的相容分支，並非整個檔案可刪。

| 路徑 | 檔案:符號 | 存在原因與依賴 |
| --- | --- | --- |
| mode dispatch | `src/FreeformHelper.Application/Services/NotchTableGenerator.cs:NotchTableGenerator.Generate`、`EvaluateCadRowEligibility` | CadAllocation 以外走 Legacy generation／eligibility；settings validation 先拒絕未定義 mode |
| Legacy generation | `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.cs:GenerateLegacyRegularAnchor`、`CaptureLegacyNotchGenerationRequest`、`CanBuildLegacyRow`、`BuildLegacyRow` | 遍歷 matched freeform regular，按固定版本順序套用 threshold、CanHandle 及 builder；在首個 progress callback 前凍結 request |
| Legacy request | `src/FreeformHelper.Application/Services/NotchAlgorithms/LegacyNotchGenerationRequest.cs:LegacyNotchGenerationRequest` | 專用的 enabled versions、LenScale、NullValue、Q7／有效 V22 threshold 快照 |
| V21 geometry builder | `src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs:V21NotchAlgorithm.CanHandle`、`Build`、`FillAxis` | Legacy dispatch 的 V21 九欄 row；X／Y neighbor orientation、面積百分比、Q7 ADD／SUB legs |
| Legacy 的 V22 builder | `src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs:V22LegacyRowStrategy.CanHandle`、`Build` | matched XWay／YWay 的九欄 geometry row；不是 normal V22 canonical builder。只刪 V21 時仍需要它 |
| 專用 geometry helpers | `src/FreeformHelper.Application/Services/NotchAlgorithms/NotchAlgorithmHelpers.cs:NotchAlgorithmHelpers`；`src/FreeformHelper.Application/Services/NotchAlgorithms/NotchAxisContext.cs:NotchAxisKind`、`NotchAxisGeometryContext`、`NotchAxisNeighborContext` | production consumers 僅為上述兩個 Legacy builders 及彼此內部呼叫；兩個 builder 都移除後才可刪 helpers／contexts |
| Legacy allocation admission | `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.Allocation.cs:BuildCadMaxAllocation`；`src/FreeformHelper.Application/Services/NotchTableGenerator.Eligibility.cs:EvaluateLegacyEligibility` | Legacy 的最大 overlap gate 與 matched-regular row count；normal allocation／anchor 選擇保留 |
| 跨模式的 eligibility 依賴 | `src/FreeformHelper.Application/Services/NotchTableGenerator.Eligibility.cs:EvaluateCadAllocationEligibility` | **normal mode 也呼叫 `CanBuildLegacyRow`** 判定兩版 eligibility；先解除此 consumer，才可刪 Legacy dispatch／V22LegacyRowStrategy.CanHandle |
| V21 threshold | `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.Thresholds.cs:PassesThresholdForVersion`、`PassesV21ThresholdQ7`、`ResolveEffectiveV22ThresholdPercent` | V21 admission 為 Q7 rounding；Legacy request overload 為 Legacy-only。有效 V22 threshold 仍受 linked Q7 控制，需先處理持久化 |
| normal V21 最終投影 | `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.cs:CaptureCadAllocationProjectionRequest`、`BuildCanonicalProjectionCandidateView`、`AppendCanonicalExports`；`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:ProjectV22RowsToV21Rows` | opaque canonical batch 共用；最後可輸出 V21 九欄 Q7 rows。V22 enabled 時採有效 V22 threshold，只有 V21 enabled 時採 Q7 threshold；不可刪 canonical V22 resolution |
| V21 firmware projector | `src/FreeformHelper.Application/Services/NotchV21FirmwareProjector.cs:NotchV21FirmwareProjector.Project`、`ProjectLegacy`、`ProjectCadAllocation`、`ProjectSourceRow` | Legacy destination rows 直接正規化；normal source rows 重排為 destination ABI nodes、拆多 term、處理 self／continuation。C 與 simulation 共用此唯一 final projection |
| V21 projection models | `src/FreeformHelper.Application/Services/NotchV21FirmwareProjector.cs:NotchV21FirmwareProjection`、`NotchV21ProjectedSourceRow`、`NotchV21ProjectedLeg`、`NotchV21FirmwareNode` | 僅供 V21 projector、formatter、evaluator／Actions；先移除 consumers 再刪模型 |
| V21 payload codec | `src/FreeformHelper.Application/Services/NotchV21Q7Codec.cs:NotchV21Q7Codec` | TypeNone／Add／Sub；UINT8 magnitude 0..255、128=100%、AwayFromZero encode、ABI saturation 與 signed integer scale；不是 allocation threshold codec |
| V21 evaluator | `src/FreeformHelper.Application/Services/NotchV21FirmwareEvaluator.cs:NotchV21FirmwareEvaluator.Apply`、`NotchV21FirmwareEvaluation` | MUL → snapshot Q7 offsets → INT16 writeback；V21-only。**`QuantizeToFirmwareInt16` 另由 V22 simulation 呼叫，不能隨檔案刪除** |
| V21 simulation | `src/FreeformHelper.Application/Services/NotchApplySimulationService.cs:Simulate`、`BuildV21Actions`；`src/FreeformHelper.Application/Services/NotchApplySimulationModels.cs:NotchApplySimulationAction.IsLegacyApproximation` | V21 firmware apply／contract text；normal source-flow Actions 與 Legacy exact Cells／EMS。Legacy 不產生 source-flow Actions；action 旗標目前按 V21 判定 |
| Legacy timing | `src/FreeformHelper.Domain/Notch/NotchGenerationPhaseTimings.cs:BuildLegacyRowsElapsedMs`、`TotalElapsedMs` | generator 回報及 VM log 的專用 timing slot；normal mode 填 0。亦為 baseline telemetry shape，需同步消費端 |

V21 final C formatter 清單如下，皆位於 `src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs`：

| 檔案:符號 | V21-only 部分 |
| --- | --- |
| `NotchFirmwareCExporter.cs:Export` | V21 row 篩選與 dispatch、混版 C 拒絕訊息、無支援版本訊息中的 v2.1 |
| `NotchFirmwareCExporter.cs:ExportV21FwFile` | 呼叫 V21 projector，組合完整 V21 firmware file |
| `NotchFirmwareCExporter.cs:AppendV21TypeBlock` | V21 `ST_PRI_NHC_TABLE_NODE_INFO` 欄位、Q7 macros／types、各 IC table 宣告 |
| `NotchFirmwareCExporter.cs:AppendV21Tables`、`FormatV21NodeLine`、`FormatV21TypeLiteral` | final destination nodes、ADD／SUB literal、null sentinel、comment 序列化 |
| `NotchFirmwareCExporter.cs:AppendV21FunctionBlock` | V21 firmware apply C function 與整數運算契約 |
| `NotchFirmwareCExporter.cs:AppendGenerationMetadata` | V21／V22 section count 文字；其餘 header、mask、Release／Debug、V22 formatter 是共用或保留路徑 |

`src/FreeformHelper.Application/Export/NotchTableExporter.cs:ExportAsCsv` 的九個固定 `payload_01..09` 欄位、version label／code 同時承載 V21、Legacy V22 與 canonical V22；它不是 V21-only formatter。移除資料來源不等於授權縮減 CSV schema。`ExportAsCInitializer` 仍是共用入口。

## Production 路徑：UI、cache 與 Runtime Query

| 路徑 | 檔案:符號／binding | 需要盤點的部分 |
| --- | --- | --- |
| UI enum／選項 | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Models.cs:NotchExportFileType.Cv21`、`NotchExportFileTypeOption`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:FreeformHelperViewModel` 建構子 | C v2.1 選項、extension、V21 pinned version；Csv／Cv22 保留 |
| 選項 metadata | `src/FreeformHelper.UI/ViewModels/NotchExportFileTypeMetadata.cs:TryParseStoredValue`、`IsCExportType`、`GetExportKindLabel`、`TryGetPinnedVersion` | Cv21 識別／文字與存檔 parser；pinning 機制仍供 Cv22 使用 |
| active settings | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.Notch.cs:EnableV21`、`NotchThresholdQ7`、`LinkNotchThresholds`、`IsLenScaleVisible` | V21 enable、Q7 gate、雙版 linkage；internal LenScale 預設隱藏，但 Legacy V22 builder 仍使用 settings.LenScale |
| settings draft／apply | `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:SettingsWindowViewModel`、`OnEnableV21Changed`、`OnNotchThresholdQ7Changed`、`OnNotchThresholdPercentChanged`、`OnLinkNotchThresholdsChanged`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs:ApplySettingsWindowDraft` | draft 複製、Apply 差異判定、寫回、門檻連動；未提供 LegacyRegularAnchor 的正常 operator 切換入口 |
| settings view／help | `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml:EnableV21`、`NotchThresholdQ7`、`LinkNotchThresholds` bindings；`src/FreeformHelper.UI/Views/HowToUseView.axaml` 的 export／BBoxArea 說明 | v2.1 checkbox、Q7 threshold、link UI、雙語 help；後續移除會改變使用者看到的選項與文字 |
| settings replay／save | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.Sync.cs:LoadSettingsToUi`、`ApplyUiToSettings`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.UiSnapshot.cs:BuildUiSnapshot` | V21 enable fallback、enabled set、threshold／link 同步及 snapshot；Legacy mode 保留在 project settings，不由 UI 重設 |
| callback／dirty／undo | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.Step5.cs:OnEnableV21Changed`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.NotchPreview.cs:OnNotchThresholdQ7Changed`、`OnNotchThresholdPercentChanged`、`OnLinkNotchThresholdsChanged`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.DirtyTracking.cs` 的 Step5 property 名單；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.UndoHooks.cs:OnEnableV21Changing`、`OnNotchThresholdQ7Changing`、`OnLinkNotchThresholdsChanging` | dependent property 通知、link rounding、unsaved／invalidation routing、undo。移除欄位時須一起清除名單與 callbacks |
| generation／cache | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:GenerateCurrentNotchTableAsync`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:CreateExportSettingsSnapshot`、`ComputeNotchExportSettingsFingerprint` | normal 路徑共用 resolved batch；Legacy 分支保留完整 request-specific table cache／fingerprint。Legacy-only branch、Q7／link／enabled-version snapshot 與 log 一起受影響，不能刪共用 cache service |
| export 選擇回寫 | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection`、`ResolveDynamicNotchExportTarget`、`ResolveSuggestedNotchExportBaseName` | 使用者選單一 V21 可回寫 EnableV21／EnableV22；V21 檔名與序列化選擇。共用 export command／write path 保留 |
| selection 版本清單 | `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs:BuildVersionOptions`、`ResolvePinnedVersionOption`；`src/FreeformHelper.UI/Services/NotchExportSelectionProjectionBuilder.cs:MatchesSelectedVersion` | V21 row／Cv21 option 的版本篩選；通用選取、workspace linking 與 Cv22 pinning 非專用碼 |
| V21 的 Legacy UI 狀態 | `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Models.cs:NotchExportRowItemViewModel.IsStatusLegacy`、`NotchExportIcGroupViewModel.GroupDetailText`、`NotchExportRowDisplayMode.LegacyOnly`；`src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs:LegacyRowCount`、`IsFilterLegacyOnly`；`src/FreeformHelper.UI/Services/NotchExportSelectionSummaryProjector.cs:Build`；`src/FreeformHelper.UI/Services/NotchExportSelectionProjectionBuilder.cs:MatchesDisplayModeFilter`；`src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml:LegacyOnly` filter／`IsStatusLegacy` binding | **Legacy 狀態按 `row.Version != V22`，不是按 computation mode**；目前會將所有 V21 標為 Legacy，卻不將 Legacy V22 標為 Legacy |
| raw payload 顯示 | `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Models.cs:BuildPayloadText`、`BuildPrimaryOutcomeText`、`BuildPayloadColumnsText` 等 `V22Node is null` fallback | 同時服務 V21、Legacy V22 與 raw V22 rows；這些 fallback 的可刪性需另確認 raw V22 contract |
| simulation UI | `src/FreeformHelper.UI/ViewModels/SimulationWorkspaceViewModel.Source.cs` 的 version options 建立；`src/FreeformHelper.UI/Services/SimulationWorkspaceSession.cs:ComputationMode`；`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Simulation.cs`、`src/FreeformHelper.UI/Services/SimulationWorkspaceUseCase.cs:BuildScenarioSnapshot`、`src/FreeformHelper.UI/Services/NotchApplySimulationReviewUseCase.cs:BuildSnapshot` | 版本選單來自 table；mode 由 generation settings 傳至同一 firmware simulation。只移除 V21 branches／Legacy mode 傳遞，不移除 V22 simulation／EMS |
| runtime export | `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.Export.cs:QueryExportNotchAsync` | `c-v21` parse／normalized response format；使用既有 `ExportNotchCommand`，沒有另一個 generator。暫時 pin selection、還原 file type 並保持 project version state |
| runtime help／status | `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.cs:QueryHelp`、`QueryHelpExamples`；`src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.TerminalStatus.cs:QueryStatus` | help 的 c-v21 accepted format／example，以及 `notchExportState.enableV21`、fileType=Cv21。移除是 CLI／response schema 的可見變更 |

## 持久化欄位、enum 與舊檔載入

`src/FreeformHelper.Infrastructure/Project/JsonProjectStore.cs:Options` 使用 camelCase，沒有 string-enum converter：project settings 的 enum 寫成數字。`Load` 固定為 deserialize → `ProjectFileMigrator.MigrateInPlace` → `ProjectSettings.ValidateOrThrow`。`src/FreeformHelper.Infrastructure/Project/ProjectSchema.cs:CurrentVersion` 目前為字串 `"2"`；這是 schema 值，不是 V21 或本目標版本。

| JSON 欄位／值 | 檔案:符號與目前行為 | 舊檔在移除後需要的處理（尚未選定） |
| --- | --- | --- |
| `settings.notch.enabledVersions` 的 `21` | `src/FreeformHelper.Domain/Notch/NotchAlgorithmVersion.cs:V21=21`；V22 **=30**。`src/FreeformHelper.Application/Settings/NotchSettings.cs:DefaultEnabledVersions`、`NormalizeEnabledVersions`、`ReplaceEnabledVersions` 預設雙版；不支援值被丟棄，空／null／全部不支援則補雙版 | 明確決定 [21]、[21,30]、缺欄位與空集合如何處理。可遷移為 V22，或拒絕需要 V21 的檔；不能把現有「drop + default」當成無行為差異的 migration，也不能把 V22 renumber 成 22／0 |
| `settings.notch.computationMode=0` | `src/FreeformHelper.Application/Settings/NotchComputationMode.cs:LegacyRegularAnchor=0`、`CadAllocation=1`；`NotchSettings.ComputationMode` 的缺欄位預設為 CadAllocation。`ProjectSettings.ValidateOrThrow` 使用 Enum.IsDefined | 需在 validation 前辨識 Legacy：拒絕並說明無法重現，或經核准改 CadAllocation 並告知輸出改變。直接刪 enum 值會讓舊 0 在 validation 失敗；不得重用 0 表示新語意 |
| `settings.notch.thresholdQ7`、`linkVersionThresholds`、`thresholdPercentV22` | `src/FreeformHelper.Application/Settings/NotchSettings.cs` 同名 properties；link 預設 true、Q7 預設 0。`ProjectSettings.ValidateOrThrow` 驗證 Q7 0..128 與 percent 0..100；`ResolveEffectiveV22ThresholdPercent` 在 link=true 時取 clamp(Q7×100/128)，忽略保存的 percent | 去除 linkage 前，需將舊檔的**有效 V22 percent** 物化到既有 `ThresholdPercentV22`，或明確拒絕。Q7 欄位刪掉後直接套 percent 預設會改 admission；保留 V22 threshold 不等於能重現 V21 Q7 rounding |
| `uiSnapshot.notch.enabledVersions`（舊字串，例如 `V21`） | `src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:UiNotchSnapshot.EnabledVersions`：LegacyUiSnapshotField、null 時不寫；目前唯一版本真值為 settings.notch.enabledVersions | 現行 `LoadSettingsToUi` 不以它覆寫 settings。決定舊 UI-only 資料保留、讀取後忽略或拒絕策略；不得藉本目標恢復第二個版本真值 |
| `uiSnapshot.notch.thresholdQ7`、`thresholdPercentV22`、`linkThresholds` | `ProjectUiSnapshot.cs:UiNotchSnapshot`；`src/FreeformHelper.Infrastructure/Project/UiSnapshotPersistenceContract.cs` 將三者列入持久化 contract；`BuildUiSnapshot` 仍寫入，載入 UI 取 settings | 需同步 serializer contract／snapshot builder。若要保留舊欄位只供讀取，應明示其相容目的與退出條件；不把 snapshot 當成 settings migration 真值 |
| `uiSnapshot.view.notchExportFileType="Cv21"` | `ProjectUiSnapshot.cs:UiViewSnapshot.NotchExportFileType`；`FreeformHelperViewModel.Models.cs:NotchExportFileType` 目前 Csv=0、Cv21=1、Cv22=2（implicit）；`NotchExportFileTypeMetadata.TryParseStoredValue` 也可 parse 數字字串。`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs:ApplyViewSnapshot` 只套用仍在 options 的值 | 決定遷移為既有 Csv／Cv22 或拒絕／提示；目前失配會不套用，可能沿用當下選項而非明確 default。刪 implicit enum 項目不能讓舊 `"2"` 被重新解釋 |
| app-general `View.NotchExportFileType="Cv21"`；舊 shape 的 `UiSnapshot.View.NotchExportFileType` | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:AppGeneralSettingsDocument`、`LegacyAppGeneralSettingsDocument`、`TryLoad`；app JSON 保持 PascalCase。`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.AppSettings.cs:CloneViewSnapshot` 複製同欄位 | app preference 也需同一 Cv21 值處理；現行 app whitelist 沒有 Notch settings，不持久化 mode／enabled set／threshold。維持 Load Project 後 deferred writes、成功 Save Project 才 flush 的現有政策 |
| `settings.notch.lenScale`；`uiSnapshot.notch.lenScale` | `NotchSettings.LenScale`、`UiNotchSnapshot.LenScale`；Legacy V22 builder 計算 neighbor lengths 使用它，normal V21 projection 不靠它 | 只刪 V21 時不能刪；Legacy 也移除後才確認可刪的 calculation／UI／validation。NullValue、ExportProfile 仍供 V22，保留 |

`src/FreeformHelper.Infrastructure/Project/ProjectFileMigrator.cs:EnsureSettingsNotNull` 現在僅呼叫 enabled-version normalization，沒有 V21／Legacy 到新語意的 migration。其註解要求 append-only schema，不移除舊欄位；`src/FreeformHelper.Infrastructure/Project/ProjectFile.cs:ExtensionData` 僅保留未知**根層** JSON，不能據此宣稱刪除 nested Notch 欄位後仍 roundtrip。未來「完全移除」若包含 serializer 欄位或舊值 parser，須先決定與這個既有政策的邊界。只保留拒絕訊息中的舊數字／名稱，與繼續支援舊算法是不同範圍。

## 測試盤點與每個 class 的數量

下表為**整個 class** 的宣告數，partial files 合併：包含 Fact／AvaloniaFact／ExampleDataFact；Theory 同理。案例數 = Fact 數 + InlineData rows，這些 class 沒有 MemberData／ClassData。數量不是「全數可刪」：混版、V22 及共用 contracts 必須保留或改寫。除驗證段列明的五個 class 外，其餘為 source inventory，未執行。

定位縮寫僅用於本節：`N` = `tests/FreeformHelper.Tests/Application/Notch/`，`P` = `tests/FreeformHelper.Tests/Application/Project/`，`S` = `tests/FreeformHelper.Tests/Application/Settings/`，`W` = `tests/FreeformHelper.Tests/Workflow/`，`UV` = `tests/FreeformHelper.Tests/UI/ViewModels/`，`US` = `tests/FreeformHelper.Tests/UI/Services/`。

| class／檔案:代表性符號 | Fact | Theory | 案例 | V21／Legacy 或共用覆蓋 |
| --- | ---: | ---: | ---: | --- |
| `N/NotchTableGeneratorTests.cs:NotchTableGeneratorTests` | 36 | 6 | 49 | V21 matched／unmatched、X／Y、Q7 legs；Legacy V22 geometry／neighbor、eligibility parity、callback 前 request freeze；both／single-version 投影、mode 差異、cache batch、determinism |
| `N/NotchTableExporterTests.cs:NotchTableExporterTests` | 24 | 3 | 30 | mixed CSV、版別 C、GCC compile／runtime parity、V21 normal／Legacy、Q7 boundaries、三個 terms／INT16 wrap、mask、UINT16 sentinel 與 snapshot；同 class 的 V22 gates 保留 |
| `W/NotchApplySimulationServiceTests.cs:NotchApplySimulationServiceTests` | 10 | 1 | 12 | V21 projected contract、self target、continuation clamp、missing diff diagnostics、兩版 INT16 baseline |
| `W/NotchApplySimulationReviewUseCaseTests.cs:BuildSnapshot_WhenV21RowsImported_UsesSharedProjectedCanonicalPath` | 2 | 0 | 2 | V21 經共用 review／firmware apply 入口 |
| `US/SimulationWorkspaceUseCaseTests.cs:BuildScenarioSnapshot_PropagatesLegacyCompatibilityModeToTheSharedFirmwareProjection` | 3 | 0 | 3 | session mode 傳遞與 V21 Legacy exact contract text |
| `N/NotchCadOutputFwDiffProjectionServiceTests.cs:Project_WhenCadDiffsShiftLeaveGap_GapDoesNotRemainAsAnchor` | 5 | 0 | 5 | V21 raw row／V22 typed source diff 投影；其餘 mapping tests 保留 |
| `S/NotchSettingsTests.cs:ProjectSettingsValidate_RejectsV21ThresholdOutsideIndependentQ7Range`、`ProjectSettingsValidate_AcceptsV21ThresholdQ7Boundaries` | 4 | 4 | 12 | 4 個 Q7 boundary cases；其餘 NullValue／compensation diagnostics／guard defaults 為共用 |
| `P/ProjectFileMigrationTests.cs:Load_MigratesNullCollections`、`Load_DropsUnsupportedLegacyNotchVersions` | 9 | 0 | 9 | null collections、version normalization（unsupported 舊值不是 V21）、空集合 fallback；目前沒有「V21／mode 0 移除後 migration／rejection」測試 |
| `P/ProjectStoreTests.cs:SaveAndLoad_RoundTripsMatchingAndIndexMappingSettings` | 2 | 0 | 2 | 保存 Legacy mode、V21-only enabled set 與門檻連動欄位 |
| `P/ProjectModelCollectionIsolationTests.cs:ProjectUiSnapshot_ListSetters_CloneInputCollections` | 2 | 0 | 2 | 舊 UI-only EnabledVersions 中的 V21 字串與 defensive copying |
| `UV/FreeformHelperViewModelTests.*.cs:FreeformHelperViewModelTests` | 153 | 5 | 163 | `.Basics.CoreFlags.cs`：雙版預設、V21 LenScale hidden；`.SettingsPersistence.ProjectReplay.cs`：`LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields`、Step5 selection roundtrip；`.NotchExportCache.cs`：Legacy request-specific miss、V21↔V22 resolved-batch reuse／threshold；`.NotchExportNaming.cs`：V21／mixed 檔名；`.NotchResolvedSnapshot.cs`：version change 的 final-only invalidation／reuse |
| `US/RuntimeQueryUseCaseTests.*.cs:RuntimeQueryUseCaseTests` | 18 | 0 | 18 | `.ExportAndStatus.cs`：enableV21 status 與 c-v21 accepted format／project state 保持；workflow-not-ready 測試不能代替真實 C golden gate |
| `UV/NotchExportSelectionViewModelTests.*.cs:NotchExportSelectionViewModelTests` | 47 | 1 | 49 | `.cs` 共用 mixed table fixture；`.Basics.cs` 的 Cv21 pin（含沒有 V21 rows）；`.Filtering.cs` 的 V21 version filter。多個共用 fixture cases 含 V21，不能只刪名稱含 V21 的 tests |
| `US/NotchExportSelectionProjectionBuilderTests.cs:BuildColumnFilterValues_ExcludeTargetFieldFilter_ButKeepSharedScope` | 2 | 0 | 2 | mixed V21／V22 scope fixture |
| `US/NotchExportSelectionRowSelectionServiceTests.cs:SetWorkspaceLinkedRowsSelected_OnlyTouchesWorkspaceRows` | 5 | 0 | 5 | shared mixed-row fixture、workspace selection |
| `US/NotchExportSelectionSummaryProjectorTests.cs:Build_NoSelection_UsesDefaultExportText` | 2 | 0 | 2 | V21 legacy count／summary fixture |
| `N/NotchExampleCExportDriftTests.cs:Boe3635_SignedGoldenManifest_MatchesCheckedInArtifacts`、`Boe3635_CheckedInCExample_MatchesCurrentExportPipeline` | 2 | 1 | 4 | manifest lock 1、V21／V22 export 2、純文字 normalization 1；3 cases 需私有資料，全部未在本輪執行 |
| `N/NotchGoldenBaselineTests.cs:RealProjects_MatchGoldenBaseline` | 1 | 0 | 1 | 一個 test 迭代 baseline projects，強制雙版；row／version／IC counts、Legacy distribution、FirstV21Adjusted、payload samples。project 數不是 case 數 |
| `N/Tm81NotchAcceptanceMatrixTests.cs:TM81_MatchesAcceptanceMatrixSnapshot` | 1 | 0 | 1 | TM8.1 強制雙版，matrix／workflow table／ToFull audit；matrix 項目數不是 test case 數 |
| `N/NotchV22CompensationServiceTests.cs:BuildAllocations_UsesAwayFromZeroAtV21ThresholdHalfStep` | 26 | 0 | 26 | 名稱含 V21，但鎖的是共用 allocation Q7 half-step；不可將整個 class 當成待刪 legacy 測試 |
| `UV/PadInfoViewModelTests.cs:CadPadInfo_TargetAllocationDisplay_UsesFinalEmittedTargetEligibility` | 6 | 0 | 6 | V22 display fixture 將 Q7 threshold 設 0；屬共用欄位 consumer，並非 V21 輸出測試 |

另有 raw V22 相容性保留證據：`US/NotchValidationUseCaseTests.cs:NotchValidationUseCaseTests` 為 1 Fact／1 case，`US/NotchValidationTraceServiceTests.cs:NotchValidationTraceServiceTests` 為 1 Fact／1 case，`US/NotchDisplayProjectorTests.cs:NotchDisplayProjectorTests` 為 2 Fact／2 cases。前兩者比較 typed 與 raw-values V22，不能因字面 legacy 一併刪除。

`N/NotchToFullCoverageSnapshotBuilder.cs:NotchToFullCoverageSnapshotBuilder.Build` 投影 golden／matrix 的 audit；同檔 `NotchCurrentWorkflowTableTestHelper.GenerateTableAsync` 會設定 EnableV21=true 並呼叫共用 VM generation。兩者沒有獨立 test cases。`tests/FreeformHelper.Tests/TestInfrastructure/ExampleData.cs:ExampleDataFactAttribute`、`ExampleDataTheoryAttribute` 控制私有資料 availability／skip；skip 不代表 golden 驗證通過。

### Golden、baseline 與 gate 索引（未讀私有檔）

| 資料／入口 | 由何處得知、覆蓋與後續決策 |
| --- | --- |
| `example/BOE36.35/project_3635.json`、`notch_export_v21_current.c`、`notch_export_v22_current.c`、`notch_export_golden_manifest.json` | `NotchExampleCExportDriftTests`、`scripts/perf/run-3635-regression-baseline.ps1` 的參數與 `docs/performance/regression-baseline-3635.md`。manifest 鎖 project／mask／兩版 nodes、bytes、SHA；本輪未驗 hash／內容。移除 V21 時須明確退役其 gate，保留 V22 provenance |
| `example/golden-snapshots/notch-golden-baseline.json` | `NotchGoldenBaselineTests.cs:GetBaselinePath`；文件宣告涵蓋 3635／TM8.1。V21 version count、FirstV21Adjusted、Legacy distribution、row samples／總數受影響，不能只刪一份 C golden |
| `example/TM 8.1/TM8.1.json`、`SeeRegular.csv`；`example/golden-snapshots/tm81-notch-acceptance-matrix.json` | `Tm81NotchAcceptanceMatrixTests.cs:BuildActualMatrixAsync`、`GetSnapshotPath`；兩版 generation 與同一 workflow audit。資料實際 mode／matrix item 數留待外部核對 |
| `scripts/perf/run-3635-regression-baseline.ps1` | `GoldenV21Path`、`GoldenV22Path`、`GoldenManifestPath`、`Get-GoldenComparisonText`、`Assert-GoldenExport`、`ReverseCExportOrder`；真實 UI／IPC 分別跑 c-v21→c-v22 與反向，另驗 export state invariant。相容性只允許各版 before==after，不要求 V21==V22 |
| `docs/performance/regression-baseline-3635.md`、`regression-baseline-3635.budget.json`、`perf-baseline-howto.md` | 公開基線／budget 與測量說明；version export telemetry、row counts 與 schema 改變需重新審視解讀，移除目標不授權改寫歷史 signed evidence |
| `scripts/tests/run-tests.ps1:$notchGoldenClasses`／`$notchCoreClasses`、`scripts/tests/run-refactor-gate.ps1`；`tests/README.md`、`scripts/README.md`、`docs/guides/test-categories.md` | notch-golden 含 drift／snapshot／TM8.1 三 class；notch-core 含 generator／exporter／simulation／settings／runtime 等。後續按 case 改 gate，不可整組刪除 |

## 描述 V21／Legacy 的文件

以下為現有直接描述、契約、使用說明或歷史證據；本輪不更動。表中的章節／主題便於下一輪定位。

| 文件 | 相關主題 |
| --- | --- |
| `TODO.md`；`docs/guides/refactor-roadmap-1.3.x.md` | owner 未定版的完全移除目標、現階段保留政策；R13.101／102／103 legacy 邊界與 two-version zero-diff gates |
| `docs/reference/notch-system-reference.md` | §4.2／4.4 的兩 mode、final V2.1 projector、§6 export、Q7／simulation／sentinel、remaining legacy debt；canonical 入口 |
| `docs/reference/runtime-cli-plan.md` | c-v21／c-v22、status version state、3635 exact export／reverse order、Legacy cache 邊界 |
| `docs/reference/behavior-inventory.md`；`docs/guides/settings-entry-matrix.md` | command／setting 的入口、版本／TH／link／invalidation 與持久化消費端 |
| `docs/reference/tm81-notch-acceptance-matrix.md` | TM8.1 matrix 與 V21／V22 覆蓋 |
| `docs/core/notch-v21-algorithm.md` | V21 geometry、九欄／Q7 payload 與 legacy firmware 算法詳解 |
| `docs/core/notch-v21-v22-flow.md`；`docs/core/notch-overall-flow-mermaid.md`；`docs/core/freeform-helper-algorithms.md` | 雙版生成、mode／diff projection、export 與 simulation 資料流 |
| `docs/core/notch-v22-algorithm.md`；`docs/core/notch-2.2-spec.md`；`docs/core/notch-beta05-refactor-slices.md` | V22 與 V21／九欄 compatibility 的對照、歷史設計／refactor slices；不能將歷史草案當成目前唯一真值 |
| `docs/core/notch-verification-ia-field-contract-spec.md`；`docs/core/simulation-cylindrical-input-model-spec.md` | version-aware verification 欄位與 simulation contract |
| `docs/guides/settings-parameter-guide.md`；`docs/guides/settings-overview-redesign-plan-2026-04-25-beta08.md` | Export versions、v2.1 Q7、v2.2 percent、Link TH；後者含歷史 UI 方案 |
| `docs/guides/app-user-manual.md`；`docs/guides/new-user-reading-guide.md`；`README.md`；`docs/README.md` | export／版本功能、演算法閱讀入口；UI 的 HowToUseView 另見 production 表 |
| `docs/guides/refactor-test-plan.md`；`docs/guides/test-categories.md`；`tests/README.md`；`scripts/README.md` | 雙版驗收、golden／core gate 與操作方法 |
| `docs/performance/regression-baseline-3635.md`；`docs/performance/perf-baseline-howto.md` | signed provenance、V21／V22 各自 byte-exact、真實 runtime 基線 |
| `docs/diagrams/notch-simulation/zh-TW/README.md`、`notch-table.md`、`notch-table-candidate.md`、`notch-table-output.md`；`docs/diagrams/notch-simulation/en/` 同四檔 | 兩版 candidate／table／output 圖與說明；其他 simulation 圖不因本目標自動退役 |
| `docs/reviews/ui-feature-inventory-2026-10.md`；`docs/reviews/r13-131-exit-audit-2026-10.md`；`docs/reviews/r13-slice-inventories-2026-10.md` | owner 問題、既有 B1／B2 例外與 Legacy／final projection inventory；保存決策脈絡 |
| `docs/archive/TODO-history-2026-03-24.md`、`TODO-history-2026-04-06.md`、`TODO-history-2026-05-05.md`；`docs/archive/repo-refactor-scans/repo-refactor-scan-2026-03-13.md`、`repo-refactor-scan-2026-04-22-beta07.md`、`repo-refactor-scan-2026-04-30-release-readiness.md` | 舊版本／firmware 相容／測試治理的歷史紀錄；保留歷史與更新 current contract 是不同決策 |

文件用詞需覆核兩點：reference 舊段落以 `stV21`／`stV22` 描述 sections，但目前 formatter 產生單版 firmware file；roadmap 將 `ProjectV22RowsToV21Rows` 稱為 V22 內部 legacy Q7 compatibility projection，而 source 實際新增 `Version=V21` rows，只由 ExportsV21 分支加入最終 table。這些是待決策時需對照的文件／實作差異，本輪沒有修訂契約。

## 可拆成最小步驟的依賴順序（無版本、無日期）

這是移除的依賴關係，不是開始移除的指令；不為即將退役的 Legacy 新建 adapter、抽象層或額外等價性工程。

1. **先定外部範圍與舊檔政策。** 分別回答 V21 ABI 是否退役、Legacy mode 是否退役、既有 project 是否需重匯，以及 migration／rejection、CSV／IPC schema 與 append-only policy。沒有 firmware／project 使用證據前不把「未定版目標」當成 breaking-change 授權。
2. **處理載入邊界。** 在既有 `JsonProjectStore`／`ProjectFileMigrator`／settings validation 路徑落實已選政策；先辨識舊 21／mode 0，再做 normalization，必要時保存有效 V22 threshold 到既有 percent 欄位。舊值處理與 serializer 欄位刪除分開，避免 normalizer 先吃掉拒絕所需證據。
3. **關閉已退役的外部入口。** UI settings／draft／Cv21 export／simulation option、CLI c-v21／help／status schema 及 persisted preference 同步處理；保留原有 command／UseCase／write path。同步 dirty、undo、callback、link／final-only invalidation，先保留底層讓每個入口步驟可獨立編譯驗證。
4. **移除 Legacy mode 的 consumers 與 producer。** 先解除 normal `EvaluateCadAllocationEligibility → CanBuildLegacyRow` 的依賴，再刪 VM request-specific Legacy generation/cache branch、`EvaluateLegacyEligibility`、`GenerateLegacyRegularAnchor`、request、V21 geometry／V22LegacyRowStrategy、專用 max-allocation／axis helpers。若 V21 ABI 尚保留，normal V21 projector／firmware 路徑仍保留。
5. **移除 normal V21 row producer。** 刪 final projection request 的 ExportsV21／V21-only admission、`AppendCanonicalExports` 的 V21 分支、`ProjectV22RowsToV21Rows`；保留 V22 canonical candidates、coverage audit、resolved-batch cache。此步不重新設計 generation pipeline。
6. **移除 V21 firmware consumers 再移除 models。** 先將 V22 所需的既有 INT16 quantization 保留於可繼續呼叫的位置，再刪 simulation V21 apply／Actions、C V21 formatter、projector／evaluator／codec 及專用 records。只移動必需的既有量化邏輯，不新建通用數值架構。
7. **最後整理持久化型別與共用顯示。** 確認舊檔不再需要讀取 adapter 後，才處理 V21／Legacy enum、Q7／link／LenScale 欄位、snapshot whitelist、timing slot、Legacy UI filter／count／labels；raw V22 readers、CSV schema、allocation Q7 與共用 settings 欄位各自確認 consumer，不搭便車刪除。
8. **各步保留 V22 gate，最後退役已決定不用的證據。** 測試隨所保護的行為同步調整；完整 removal 才清掉不再需要的 V21 golden case／manifest entry／runtime forward-reverse 步驟、修正現行 docs／gate 清單。私有 golden 及歷史 evidence 的保留／退役需明確決定，不覆寫 expected 來取得綠燈。

## 風險與尚待 owner／外部證據回答的問題

- **既有 firmware 可能只吃 V21 ABI。** V21 的 UINT8 Q7 ADD／SUB、destination rows 與 V22 的 INT8 signed percent／source rows、function contract 不同；將選項改成 Cv22 不能視為替代相容。仍需下游 firmware 型號／使用者／交付 project 的需求證據。
- **Legacy 不是 V21 的別名。** Legacy V22 的 matched-regular geometry、neighbor lengths 與 normal CadAllocation canonical allocation 不同；改 mode 可能改 row count、方向、payload 與 simulation。不能由兩版目前各自 zero-diff 推得兩 mode 等價。
- **舊檔可能被默默改寫。** enabled set 的 fallback、linked threshold、app／project Cv21 preference、未知 nested fields 不被 ExtensionData 保存，均可能在 Load→Save 改變後續匯出。migration 必須明確告知行為改變；rejection 必須在執行／保存前生效。
- **命名不足以判定刪除。** V22 仍使用 `NotchV21FirmwareEvaluator.QuantizeToFirmwareInt16`；`NotchThresholdQ7Contract` 仍服務 `NotchAllocationService` 與 `NotchV22CompensationService.Stages`；Notch Detail 的 `ThresholdQ7`／`MaxAllocationQ7` 比較亦是共用 UI consumer。`EnableToFullRuleEngine=false` 的 legacy inline gate 與本次 LegacyRegularAnchor 不同。
- **raw V22 reader 邊界需另定。** `NotchTableRow.cs:NotchTableRow` 的 raw-values constructor、`NotchV22FirmwareProjector.cs:ProjectRow`、`NotchValidationTraceService.cs:BuildV22Payload` 的 raw V22 fallback，以及 `NotchCadOutputFwDiffProjectionService.cs:ProjectRow` 的 values fallback，不能只因含 legacy／九欄而刪；validation 目前只納入 V22 rows。此目標是否包括這類 public raw-input contract 尚未指定。
- **UI／IPC／CSV 與 baseline 都可能有外部 consumers。** `enableV21`、`Cv21`、version_code=21、九欄 CSV、Legacy count／timing 退役會改可見文字或 schema；完整 class 數字不是可刪 case 數。移除後需保留 V22 state／cache／invalidation、sentinel、INT16／EMS 等回歸保護。
- **私有 golden 未驗證。** 本輪沒有檢查資料內的 mode、非零 linked thresholds、已交付 project 的分布；公開 3635 基線宣告 CadAllocation／雙版且 threshold=0，不能代表全部 Legacy project。也未取得 downstream firmware 可退役 V21 的確認。
- **治理與驗收政策尚未改。** 目前 owner 要求保留、既有 1.3.x two-version zero-diff／append-only 約束仍在；完全移除時哪些 gate／舊檔政策可以退出，需要新決定。本文件不替 owner 選版本或核准 breaking behavior。

## 本輪驗證與刻意未做事項

- UI 及 Tests 專案各執行一次 `dotnet build <proj> --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false`：兩次成功，各 0 warnings／0 errors，沒有 restore。
- 依序執行 class-filtered `dotnet test ... --no-build -p:UseAppHost=false --nologo --filter "FullyQualifiedName~<Class>"`：NotchSettingsTests **12**、NotchTableGeneratorTests **49**、NotchTableExporterTests **30**、NotchApplySimulationServiceTests **12**、ProjectFileMigrationTests **9**；合計 **112 passed／0 failed／0 skipped**。只驗現有 contracts，未執行全專案測試或私有 golden class；GCC tests 內部為可用時才驗，不由 class 全綠推定所有外部工具皆已執行。
- `scripts/verify.ps1 -StructureOnly` 首輪其餘檢查通過，唯一 finding 為新增文件的 LF；normalizer 記錄只正規化本文件。最終檔案改為 CRLF 後第二輪通過，normalized=0、XAML issues=0；沒有修改其他 tracked 文字檔。另執行限定此檔的非空／CRLF／trailing-whitespace 檢查與 `git diff --check`；shell `test -s` 交由 commander 外部確認。
- 依任務限制未執行 lint、實際 process listing、submodule 初始化或會停止 app 的 workspace preparation；StructureOnly 中的 process ownership 檢查為合成測試，不列舉／停止 app。
- 只新增本文件；沒有 code／test／TODO／roadmap 修改、版本提議、排程、removal implementation、network、commit、push、git config 變更或其他 worktree 操作。未建立額外報告檔。
