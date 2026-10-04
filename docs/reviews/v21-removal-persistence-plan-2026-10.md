# V21／Legacy 完全移除：持久化相容性規劃

本文件只盤點 TODO「V21／Legacy 完全移除」的持久化影響與政策選項。Owner 已確認「2.1」指 V21，且目前仍決定保留 V21 與 `LegacyRegularAnchor`；本文件不授權移除、不提出產品或檔案格式新版本，也不安排移除順序或日期。

依據為目前分支 `feature/queue/v21-removal-persistence-plan` 的程式與測試宣告、[依賴圖](../generated/project-dependency-graph.md)、[Notch 契約](../reference/notch-system-reference.md)、[設定入口矩陣](../guides/settings-entry-matrix.md)及 [1.3.x roadmap 的 owner 決定](../guides/refactor-roadmap-1.3.x.md)。以下為現況與尚未採納的建議；未讀取私有範例資料，也未檢查或變更 golden、baseline。

## 1. 檔案格式與 enum 契約

| 載體／值 | 檔案：symbol | 可確認的格式與現況 |
|---|---|---|
| 專案 JSON | `src/FreeformHelper.Infrastructure/Project/ProjectSchema.cs:ProjectSchema.CurrentVersion`；`ProjectFile.cs:ProjectFile.SchemaVersion`；`JsonProjectStore.cs:JsonProjectStore.Options` | 現行 `schemaVersion` 是字串 `"2"`；JSON 欄位使用 camelCase，enum 使用數值，沒有 string-enum converter。下節專案欄位均可由格式 `"2"` 承載。 |
| App 白名單 JSON | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:AppGeneralSettingsDocument.CurrentSchemaVersion/Save` | 現行 `SchemaVersion` 是整數 `2`；欄位保留 PascalCase。只有 `View`、`Import`、`Behavior`，沒有 `Settings.Notch` 或 `UiSnapshot.Notch`。 |
| 舊 app JSON | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:LegacyAppGeneralSettingsDocument/TryLoad/LooksLikeWhitelistShape` | `AppGeneralSettingsStoreTests.TryLoad_LegacyDocument_MapsToWhitelistAndDefaultsBehavior` 明列整數 `SchemaVersion = 1` 的舊形狀。讀取器依頂層 `View`／`Import`／`Behavior` 是否存在選形狀，不以版本號分派；舊形狀只轉入 `UiSnapshot.View` 與 `UiSnapshot.Import`。 |
| V21 | `src/FreeformHelper.Domain/Notch/NotchAlgorithmVersion.cs:NotchAlgorithmVersion.V21` | 數值 `21`；專案 `settings.notch.enabledVersions` 可包含此值。 |
| V22 | 同檔：`NotchAlgorithmVersion.V22` | 數值 **`30`**，不是 `22`；移除 V21 不代表可重編這個持久化數值。 |
| Legacy 模式 | `src/FreeformHelper.Application/Settings/NotchComputationMode.cs:NotchComputationMode.LegacyRegularAnchor` | 數值 `0`；專案 `settings.notch.computationMode = 0` 是明確舊模式選擇。 |
| CAD 模式 | 同檔：`NotchComputationMode.CadAllocation` | 數值 `1`；`NotchSettings.ComputationMode` 的初始化預設也是此值，欄位缺漏不等於 Legacy 的 `0`。 |
| V21 C 匯出選項 | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Models.cs:FreeformHelperViewModel.NotchExportFileType.Cv21` | enum 隱含數值 `1`（`Csv = 0`、`Cv22 = 2`）；專案／app 實際寫入字串 `"Cv21"`，而非此 enum 的數值。 |

格式 `"2"` 的版本與欄位已有程式／測試佐證；本次資料不足以認定每個欄位最初加入哪一個歷史格式。`ProjectFileMigrator.MigrateInPlace` 只補空白版本與空集合等資料，不依 `"1"`／`"2"` 分派，也沒有 V21／Legacy 遷移；非空的其他版本標記目前沒有相應拒絕檢查。因此舊檔處置須檢查內容，不能只看版本號。未標版或其他版本標記的檔案可能由同一 reader 接受，不因此被認定具備已驗證的歷史格式契約。

## 2. 選擇入口與持久化欄位完整盤點

### 2.1 直接選擇 V21／Legacy 的設定與入口

| 設定／入口 | 檔案：symbol | 持久化結果／格式 |
|---|---|---|
| 演算法集合與預設／正規化 | `src/FreeformHelper.Application/Settings/NotchSettings.cs:NotchSettings.EnabledVersions/ReplaceEnabledVersions/DefaultEnabledVersions/NormalizeEnabledVersions` | 專案 `"2"`：`settings.notch.enabledVersions`，例如 `[21]`、`[21,30]`。預設同時含 V21／V22；缺漏、null、空清單或全為不支援值會回到兩版預設。目前 `[30,31]` 只留下 `30`。 |
| 計算模式 | 同檔：`NotchSettings.ComputationMode`；`src/FreeformHelper.Application/Settings/ProjectSettings.cs:ProjectSettings.ValidateOrThrow` | 專案 `"2"`：`settings.notch.computationMode`；`0` 選 Legacy，`1` 選 CAD。驗證要求 enum 有定義。V21／V22 與計算模式是獨立維度；只用 V22 的檔案仍可能選 Legacy。 |
| Live V21 開關 | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.Notch.cs:FreeformHelperViewModel.EnableV21/EnableV22`；`FreeformHelperViewModel.Settings.Sync.cs:LoadSettingsToUi/ApplyUiToSettings` | 投影／寫回上述版本集合；沒有獨立的 `enableV21` JSON 欄位，也沒有 app 演算法開關欄位。 |
| Settings draft V21 開關 | `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:SettingsWindowViewModel.EnableV21/EnableV22`；`FreeformHelperViewModel.SettingsWindow.cs:ApplySettingsWindowDraft`；`src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml:EnableV21 binding` | Draft Save 套用到 live 開關，再由專案 `"2"` 的版本集合保存；Cancel 不形成另一份持久化設定。 |
| C v2.1 檔案類型與固定版本 | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:FreeformHelperViewModel constructor`；`FreeformHelperViewModel.Models.cs:NotchExportFileTypeOption.PinnedVersion`；`NotchExportFileTypeMetadata.cs:TryGetPinnedVersion` | `Cv21` 選項的 `PinnedVersion = V21`；檔案類型偏好存為下一表的字串，`PinnedVersion` 本身沒有 JSON 欄位。檔案類型能要求 V21 匯出，不能只清版本集合而留下此選項。 |
| Export Notch Rows 的版本／類型選擇 | `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs:SelectedVersionOption/SelectedExportTypeOption/BuildVersionOptions`；`FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection/ApplyNotchExportTypeSelection` | 視窗可選 V21，C 類型會固定版本；套用後回到 live 開關與檔案類型偏好，於專案儲存時保存。沒有另一個專案版本欄位；`All versions` 保留原專案集合。 |

Legacy 模式由專案設定載入並保留，沒有對應的 normal operator 模式切換 editor；`ApplyUiToSettings` 不把它改回 CAD 預設。現有 `LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields` 明確保護「Legacy + V22」的往返。其他名稱含 Legacy 的 matching／To Full 設定不等於 `LegacyRegularAnchor`，不列入本移除規劃。

### 2.2 專案 JSON 欄位

以下 symbol 未重複完整前綴時，`ProjectUiSnapshot.cs` 位於 `src/FreeformHelper.Infrastructure/Project/`，`NotchSettings.cs` 位於 `src/FreeformHelper.Application/Settings/`。

| JSON 路徑 | 檔案：symbol | 格式／用途與舊值 |
|---|---|---|
| `settings.notch.enabledVersions` | `NotchSettings.cs:NotchSettings.EnabledVersions` | 專案 `"2"`；數值 `21` 是 V21，`30` 是 V22；唯一 authoritative 演算法集合。 |
| `settings.notch.computationMode` | `NotchSettings.cs:NotchSettings.ComputationMode` | 專案 `"2"`；`0` 是 Legacy，獨立於版本集合。 |
| `uiSnapshot.notch.enabledVersions` | `ProjectUiSnapshot.cs:UiNotchSnapshot.EnabledVersions` | 專案 `"2"` 可承載舊字串清單，例如 `["V21","V22"]`。標記 `LegacyUiSnapshotField`；目前 `BuildUiSnapshot` 不寫它，載入 UI 不用它恢復版本。但直接 `JsonProjectStore.Save` 可再次寫出非 null 的清單，不能宣稱所有 save 都已排除此欄位。 |
| `uiSnapshot.view.notchExportFileType` | `ProjectUiSnapshot.cs:UiViewSnapshot.NotchExportFileType` | 專案 `"2"`；`"Cv21"` 選 C v2.1，預設 `"Csv"`。`FreeformHelperViewModel.UiSnapshot.cs:BuildUiSnapshot` 寫入；`FreeformHelperViewModel.Operations.cs:ApplyViewSnapshot` 解析並選現有 option。 |
| `settings.notch.thresholdQ7`、`settings.notch.linkVersionThresholds` | `NotchSettings.cs:NotchSettings.ThresholdQ7/LinkVersionThresholds` | 專案 `"2"`；不選版本，卻保存 V21 Q7 門檻與跨版連動（預設 true）。連動時 V22 也使用 `ThresholdQ7 * 100 / 128`。 |
| `settings.notch.thresholdPercentV22` | `NotchSettings.cs:NotchSettings.ThresholdPercentV22` | 專案 `"2"`；非連動才採用這個獨立 V22 門檻。單純保留此欄位不足以保留連動專案的結果。 |
| `settings.notch.lenScale` | `NotchSettings.cs:NotchSettings.LenScale` | 專案 `"2"`；LegacyRegularAnchor 的 V22 legacy 9-column 長度倍率（`V22LegacyRowStrategy.cs:51`、`:76`–`:77`），預設 `10`；V21 與 CadAllocation 不使用；不是 V21／Legacy selector。 |
| `uiSnapshot.notch.thresholdQ7`、`uiSnapshot.notch.linkThresholds`、`uiSnapshot.notch.thresholdPercentV22`、`uiSnapshot.notch.lenScale` | `ProjectUiSnapshot.cs:UiNotchSnapshot.ThresholdQ7/LinkThresholds/ThresholdPercentV22/LenScale` | 專案 `"2"`；目前 snapshot 仍寫入這些副本，但載入設定以 `Settings.Notch` 為準，不以副本重新推導門檻或版本。 |

最後四列是移除時不能漏掉的伴隨欄位，不是額外的演算法開關。`NullValue`、`ExportProfile` 與補償設定為共用參數，不因 V21 移除便成為可忽略資料。專案保存輸入與設定，沒有保存已產生的 Notch table 或 C bytes；runtime row 的 `Version` 也不是另一個專案欄位。

### 2.3 App settings 欄位與舊形狀

| JSON 路徑 | 檔案：symbol | 格式／現況 |
|---|---|---|
| `View.NotchExportFileType` | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:AppGeneralSettingsDocument.View` → `src/FreeformHelper.Infrastructure/Project/ProjectUiSnapshot.cs:UiViewSnapshot.NotchExportFileType` | App `2`；可保存 `"Cv21"`。`FreeformHelperViewModel.AppSettings.cs:CloneViewSnapshot/PersistAppGeneralSettingsNow` 複製並保存，啟動時還原。 |
| `UiSnapshot.View.NotchExportFileType` | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:LegacyAppGeneralSettingsDocument.UiSnapshot/TryLoad` → 同一 `UiViewSnapshot.NotchExportFileType` | 舊 app `1` 形狀；轉到現行 `View`，所以 `"Cv21"` 不會自動消失。 |
| `UiSnapshot.Notch.EnabledVersions` | 同檔：`LegacyAppGeneralSettingsDocument.UiSnapshot/TryLoad` → `ProjectUiSnapshot.cs:UiNotchSnapshot.EnabledVersions` | 舊 app `1` 形狀可承載 V21 字串；反序列化後不轉入白名單，不選演算法。 |
| `UiSnapshot.Notch.ThresholdQ7/LinkThresholds/ThresholdPercentV22/LenScale` | 同一舊 document → `ProjectUiSnapshot.cs:UiNotchSnapshot` 對應 properties | 舊 app `1` 形狀的伴隨副本；同樣不轉入白名單。不存在 `UiNotchSnapshot.ComputationMode`。 |
| `Settings.Notch.EnabledVersions/ComputationMode` 及伴隨設定 | `src/FreeformHelper.UI/Services/AppGeneralSettingsStore.cs:LegacyAppGeneralSettingsDocument/TryLoad` | 舊 app `1` 的整份 `Settings` 形狀可夾帶數值 V21／Legacy；現行相容 DTO **沒有 `Settings` property**，這些資料是未知欄位，直接略過，並非目前仍有效的 app 設定。舊 fixture 證明頂層 `Settings` 形狀，不證明每個 Notch 欄位的歷史寫入起點。 |
| `Behavior.ApplyVisualPreferencesOnProjectLoad` | 同檔：`AppGeneralBehaviorSettings.ApplyVisualPreferencesOnProjectLoad`；`FreeformHelperViewModel.AppSettings.cs:TryApplyAppGeneralVisualPreferencesAfterProjectLoad` | App `2`；不是 selector，但為 true 時 app `View` 在專案載入後覆蓋專案 view，包含 C 類型偏好。 |

未標版舊 app 也按形狀讀取；不能用 `SchemaVersion` 推定哪些舊欄位被套用。現行白名單不存在 app `EnableV21`、`NotchAlgorithmVersion` 或 `ComputationMode` 欄位。舊 app 的算法／模式欄位不應升格為新的 project source-of-truth。

## 3. 移除後的讀取政策選項與建議

此節只回答「若 owner 另行同意完全移除」的相容性選擇，不改目前載入行為。V21 → V22 與 Legacy → CAD 是兩項不同的行為變更；兩者均不能宣稱等價或 byte-exact。

### 3.1 三種選項的使用者後果

| 載入策略 | 舊專案的後果 | 舊 app settings 的後果 |
|---|---|---|
| 遷移到 V22／CadAllocation | `21` 改為 `30`、Legacy `0` 改為 CAD `1`；專案可以開啟，但 row、補償、匯出 ABI／C bytes 可能改變，儲存後會失去原選擇。門檻也須按既有有效值處理。須明示「已轉換，不能重現原 V21／Legacy 輸出」，不能只顯示一般載入成功。 | `Cv21` 偏好可改為 `Cv22`；使用者下一次會看到 C v2.2 選項，並須知道其 firmware 契約不同。舊算法／模式欄位目前已忽略，無須再把它們移成有效 app 算法預設。 |
| 清楚拒絕 | 原檔保留；使用者須以仍支援 V21／Legacy 的舊版開啟，不能在移除版重匯相同 C。訊息須指出路徑／欄位／舊值與原因，避免只報 enum invalid。 | 若整份拒絕，使用者也會失去其他視覺與 import 偏好。現行 `TryLoad` 只記錄 warning 並回 null，不等於已提供清楚的使用者訊息。 |
| 繼續讀取並忽略 | 適用已無效的 UI 副本。若忽略真正的版本集合／模式，改用 V22／CAD 預設，實質上是未告知的轉換；舊檔看似成功開啟，卻無法重現輸出。 | 適合已被白名單排除的舊算法／模式／Notch 副本；保留其他偏好。若 `Cv21` 解析失敗只保留目前選項，使用者可能得到 CSV 或前一個偏好，結果受 session 狀態影響。 |

### 3.2 逐欄建議

| 舊資料情況 | 建議 | 使用者可見結果 |
|---|---|---|
| `settings.notch.enabledVersions` 含 `21`，包括 `[21,30]` | **清楚拒絕**，不自行刪掉 `21` 或自動轉版。 | 知道此專案需要 V21，相同 C 須用保留相容性的舊版重匯；原檔不被改寫。即使 `[21,30]` 含 V22，也不能假定 owner 同意放棄 V21 交付。 |
| `settings.notch.computationMode = 0`，即使只含 `[30]` | **清楚拒絕**。 | 知道此專案需要 Legacy；只把 V21 清掉仍不足以安全開啟。 |
| 舊檔缺漏／null／空／全不支援的 `enabledVersions`，或整個 `Settings.Notch` 缺漏 | **清楚拒絕這項歧義**，不把舊預設 V21＋V22 默默當成 V22。 | 明示舊檔依賴含 V21 的歷史預設，現有資料無法證明原輸出版本；以舊版確認後再決定是否接受轉換。 |
| 明列 `[30]`，模式為 `1` 或缺漏 | **保留 V22／CadAllocation**；缺漏模式沿用目前初始化的 CAD 語意。 | 不因舊格式標記或多餘 UI V21 字串而誤拒絕已使用 V22／CAD 的專案。 |
| `uiSnapshot.notch.enabledVersions`、舊 app 的 `UiSnapshot.Notch` 與未知 `Settings.Notch` | **繼續讀取並忽略**；專案只以 authoritative settings 判斷，不從副本推導算法。 | 不會因過時字串切回 V21／Legacy，也不丟掉其他可用資料。這不代表可忽略專案 `settings.notch`。 |
| 專案 `uiSnapshot.view.notchExportFileType = "Cv21"` 或 app `View`／舊 `UiSnapshot.View` 中同值 | 在其餘資料可讀的前提下，**明示遷移成 `Cv22` 偏好**，不自動匯出。 | 顯示「原 C v2.1 偏好已改為 C v2.2；不能重現原 C，請確認 firmware 支援」。App 偏好本身不應阻止開啟可用的 V22／CAD 專案，也不能解除對真正 V21／Legacy settings 的拒絕。 |
| V22／CAD 專案的 Q7／連動欄位 | 若完全移除這些有效欄位，**遷移有效 V22 門檻**；非連動保留 `ThresholdPercentV22`，連動採用既有 `ThresholdQ7 * 100.0 / 128.0`，不要改採 snapshot 副本。 | V22 有效門檻維持原值；不再顯示跨版連動。不得把 UI 顯示用的兩位小數當成演算法精度。 |
| `LenScale` 與 Notch UI 的 Q7／連動副本 | **保留相容讀取並忽略不再使用的副本**；不因其存在選 V21。 | 載入已採 V22／CAD 的專案不因過時顯示欄位失敗。此建議不刪共用 `NullValue`、profile 或補償值。 |

拒絕訊息建議表達：「此專案的 `settings.notch.enabledVersions` 含 V21（21）／`settings.notch.computationMode` 為 LegacyRegularAnchor（0）。此版本無法重現原輸出，未載入或改寫原檔；請用仍支援該模式的舊版開啟。」這是擬議內容，不是本次 UI 文字變更。

政策落點應使用既有 `JsonProjectStore.Load` → `ProjectFileMigrator.MigrateInPlace` → `ProjectSettings.ValidateOrThrow` 與 `AppGeneralSettingsStore.TryLoad` 的機制，不設另一套持久化層。現行數值 enum 刪成員後不保證反序列化立刻拒絕：版本集合會先經 `NormalizeEnabledVersions`，可能把 `21` 丟掉再套預設；模式 `0` 則可能在 enum validation 才失敗。因此若採拒絕，必須仍能辨識原始舊值，不能讓正規化先抹去證據。既有 `LoadProjectFromPathAsync` 的錯誤結果／status 可承接清楚訊息；不在 UI 另做一份遷移判斷。

載入不應自行儲存原檔。專案成功載入後的 app 設定 deferred mode，以及下一次成功 Save Project 才 flush 的規則仍適用；拒絕、取消或儲存失敗不可提早寫入 app 偏好。只有頂層 `ProjectFile.ExtensionData` 保留未知欄位，不能據此保證移除後的巢狀 Notch 欄位仍能 lossless roundtrip。讀取相容欄位不等於保留舊計算路徑。

## 4. 測試名稱盤點

以下只列名稱；本次不修改測試、fixture 或預期值。

### 4.1 現有期待必須調整

- `ProjectFileMigrationTests.Load_MigratesNullCollections`
- `FreeformHelperViewModelTests.LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields`
- `FreeformHelperViewModelTests.SaveThenLoadProject_PersistsStep5NotchVersionSelection`

### 4.2 相容欄位是否保留會影響的現有 roundtrip／契約測試

- `ProjectStoreTests.SaveAndLoad_RoundTripsMatchingAndIndexMappingSettings`
- `UiSnapshotPersistenceContractTests.SectionContract_MatchesAllSnapshotSettableProperties`

### 4.3 應擴充覆蓋的現有 load／save／roundtrip 測試

- `ProjectFileMigrationTests.Load_DropsUnsupportedLegacyNotchVersions`
- `ProjectFileMigrationTests.LoadAndSave_PreservesUnknownRootProperties`
- `ProjectStoreTests.SaveAndLoad_RoundTripsSettings`
- `ProjectPersistenceUseCaseTests.SaveAsync_WritesMatchingAndMappingSettingsAndUiSnapshot`
- `ProjectPersistenceUseCaseTests.SaveThenLoadAsync_RoundTrip_PreservesCriticalProjectData`
- `AppGeneralSettingsStoreTests.SaveThenLoad_WhitelistShape_RoundTrips`
- `AppGeneralSettingsStoreTests.TryLoad_LegacyDocument_MapsToWhitelistAndDefaultsBehavior`
- `FreeformHelperViewModelTests.SaveThenLoadProject_RestoresStepAndExportSettingsFromProjectSnapshot`
- `FreeformHelperViewModelTests.ApplyViewSnapshot_Cv22ExportType_SelectsCv22`
- `FreeformHelperViewModelTests.SettingsLayerPrecedence_ProjectOverridesAppGeneralAndAppGeneralOverridesDefault`
- `FreeformHelperViewModelTests.LoadProject_WhenApplyAppVisualPreferencesEnabled_ReappliesAppVisualLayer`
- `FreeformHelperViewModelTests.AppGeneralSettings_AfterLoadProject_DefersUntilNextProjectSave`
- `FreeformHelperViewModelTests.SaveProjectAsync_WhenUserCancels_KeepsUnsavedState`
- `FreeformHelperViewModelTests.SaveProjectAsync_WhenIoError_ReturnsFalseAndKeepsUnsavedState`

### 4.4 採上述建議時需要的新案例

- `ProjectFileMigrationTests.Load_V21Only_RejectsWithCompatibilityMessage`
- `ProjectFileMigrationTests.Load_V21AndV22_RejectsWithoutDroppingV21Evidence`
- `ProjectFileMigrationTests.Load_V22WithLegacyRegularAnchor_RejectsWithCompatibilityMessage`
- `ProjectFileMigrationTests.Load_MissingNullEmptyOrUnsupportedVersions_RejectsLegacyDefaultAmbiguity`
- `ProjectFileMigrationTests.Load_V22WithMissingComputationMode_KeepsCadAllocation`
- `ProjectFileMigrationTests.Load_OldOrMissingSchemaVersion_UsesSameCompatibilityPolicy`
- `ProjectFileMigrationTests.Load_V22CadAllocation_IgnoresConflictingLegacyUiVersionStrings`
- `ProjectFileMigrationTests.Load_LinkedQ7Threshold_PreservesExactEffectiveV22Percent`
- `ProjectFileMigrationTests.Load_UnlinkedThreshold_PreservesIndependentV22Percent`
- `ProjectFileMigrationTests.Load_RemovedNumericEnumValues_DoesNotReinterpretAsSurvivingValues`
- `ProjectStoreTests.SaveThenLoad_V22CadAllocation_DoesNotReintroduceV21OrLegacySelections`
- `ProjectStoreTests.SaveThenLoad_CompatibilityFields_FollowsDeclaredReadAndWritePolicy`
- `AppGeneralSettingsStoreTests.TryLoad_LegacyNotchSettings_AreIgnoredWithoutDroppingOtherPreferences`
- `AppGeneralSettingsStoreTests.TryLoad_LegacyUiNotchVersionStrings_AreIgnored`
- `AppGeneralSettingsStoreTests.TryLoad_OldAndWhitelistCv21Preference_MapsToCv22`
- `FreeformHelperViewModelTests.LoadProject_UnsupportedNotchSettings_ShowsMessageAndPreservesCurrentProject`
- `FreeformHelperViewModelTests.LoadProject_Cv21Preference_ShowsConversionNoticeWithoutExporting`
- `FreeformHelperViewModelTests.LoadProject_AppCv21Overlay_DoesNotRestoreRemovedExportType`
- `FreeformHelperViewModelTests.LoadRejectedProject_DoesNotWriteProjectOrAppSettings`
- `FreeformHelperViewModelTests.LoadConvertedPreferences_CancelledOrFailedSave_DoesNotFlushAppSettings`

### 4.5 若另採允許算法／模式轉換，才需要的替代案例

- `ProjectFileMigrationTests.Load_V21Selection_MigratesToV22AndReportsChangedOutputContract`
- `ProjectFileMigrationTests.Load_LegacyRegularAnchor_MigratesToCadAllocationAndReportsChangedComputation`
- `ProjectStoreTests.SaveThenLoad_MigratedNotchSettings_PreservesConvertedValues`
- `FreeformHelperViewModelTests.LoadMigratedProject_ShowsNonReproducibilityNoticeAndMarksUnsaved`

## 5. 風險與待決事項

- **已交付 C 的可重現性**：保存的 project 可能曾以 V21 匯出 C，但 JSON 沒有 C artifact、export history 或 byte-exact 證明。當前 `Cv22` 偏好也不能證明過去未交付 V21。V21 Q7 與 V22 signed-percent firmware 契約不同；改版或換計算模式不能維持同一 C。若交付必須可重現，完全移除版本不能承擔這項重匯能力，仍需可用的舊版與原輸入／設定／既有交付檔。
- **只保留 V22 仍可能改結果**：Legacy 與版本集合獨立；連動 Q7 門檻也仍影響 V22。只刪 V21 enum／UI 開關會漏掉兩種不同風險。Application 使用完整換算值，UI 顯示兩位小數；誤用顯示值可在門檻邊界改變 row。
- **資料損失與回讀**：移除 enum／property 可能導致 silent fallback、generic error 或下次 save 丟欄位。現在 schema 採 append-only，未知根層資料可保留，巢狀欄位沒有同等保障。必須明確決定哪些舊欄位只讀、哪些不再寫，以及新儲存檔能否再由舊版使用；本文件不指定新 schema。
- **跨專案偏好污染**：app `View` overlay 可能把專案 Cv22 選擇覆蓋成舊 Cv21。若移除後只靠 parse failure 留目前值，結果受啟動／載入順序影響。app 轉換與 project 拒絕都必須遵守既有 deferred-write 契約與測試設定隔離。
- **相容讀取被誤認為相容計算**：保留舊字串／欄位讀取可以避免 JSON 失敗，卻不代表仍可 V21 export 或 Legacy simulation。訊息與 roundtrip 期待必須說明這項能力差異；現行 zero-diff 保護仍有效，不能因本規劃更新 baseline。
- **尚待 owner 決定**：是否接受交付專案在移除版無法重匯；是否採本文件的拒絕策略，或明示接受 V22／CAD 的非等價轉換；是否接受 app／UI Cv21 偏好轉 Cv22，以及相容欄位的讀寫保留政策。這些尚未採納的選擇不改變「目前先保留」決策。

本次交付僅為這份持久化規劃文件；不變更 production code、測試、TODO、roadmap、golden 或 baseline，不建立轉換工具／配置選項，也不提出移除實作安排。
