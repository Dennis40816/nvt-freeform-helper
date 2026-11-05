# Settings 視窗數值欄位邊界調查（cloud/settings-bounds）

狀態：調查完成；已加入 `tests/FreeformHelper.Tests/UI/ViewModels/SettingsWindowNumericBoundsTests.cs`（7 項測試，本地通過，Architecture 102 項仍全過）。本分支未修改任何 production 程式碼。

## 方法

以暫時性的探針測試（未提交）走完整條實際路徑，對每個欄位 × 每個探測值各跑一次：

`SettingsWindowViewModel` 草稿屬性 → `SaveCommand`（`ApplySettingsWindowDraft`）→ 等待 grid 重建 →
`SaveProjectAsync`（`ApplyUiToSettings` → `JsonProjectStore`）→ 新 VM `LoadProjectFromPathAsync` → 讀回 owner 與新草稿。

探測值：min、max、min−一格、max＋一格、0、−1、±1e9、小數（x.4／x.5／x.6）。環境：.NET SDK 10.0.301，Core 套件以 `scripts/fetch_core_packages.py` 取得。

「UI 宣告範圍」取自實際建立的 `SettingsWindow`／`CascadeIcSettingsWindow` 中各 `NumberScrubber` 的 Minimum／Maximum／SmallChange／SnapToStep（以 `BindingOperations.GetBindingExpressionBase(...)` 的 `BindingExpression.Description` 取得綁定屬性名稱）。

## UI 宣告範圍（控制項實際值）

| 區段 | 欄位 | Min | Max | 步進／Snap |
|---|---|---|---|---|
| General | CascadeNum | 1 | 1,000,000 | 1 / snap |
| General | ActiveAreaWidth／Height | 0.1 | 1,000,000 | 0.0001 / 不 snap |
| General | GridPaddingPercent | 0 | **100** | 1 / snap |
| General | CoordinatePixelWidth／Height | 1 | 1,000,000 | 1 / snap |
| General | PanelBiasX／Y | −1,000,000 | 1,000,000 | 0.0001 / 不 snap |
| General | GlobalFontSizePercent | 80 | 140 | 1 / snap |
| General | HighlightStrokeWidthAdjust | −2 | 4 | 0.1 / snap |
| Step 1 | MatchThreshold | 0 | 1 | 0.0001 / 不 snap |
| Step 2 | FreeformAxisThreshold | 0 | 1 | 0.01 / 不 snap |
| Step 3 | ToFullStrictOverlapPercent | 0 | 100 | 0.1 / 不 snap |
| Step 3 | BoundaryVirtualAreaCapPercent | 0 | 1000 | 10 / snap |
| Step 3 | TargetCoverageCapPercent | 0 | 255 | 1 / snap |
| Step 3 | NotchPreviewAutoPlayIntervalMs | 200 | 5000 | 100 / snap |
| Step 3 | NotchPreviewVisualizationStep | 1 | 3 | 1 / snap |
| Step 4 | MappingWeightIou／CentroidDistance／AreaRatio | 0 | 10 | 0.05 / 不 snap |
| Step 4 | MappingCandidateNumber | 1 | 400 | 1 / snap |
| Step 4 | MappingLowConfidenceThreshold／AmbiguousMargin | 0 | 1 | 0.01 / 不 snap |
| Step 4 | CadOutputFwDiffIndexStart | 0 | 1,000,000 | 1 / snap |
| Step 5 | NullValue | 0 | 65,535 | 1 / snap |
| Step 5 | NotchThresholdQ7 | 0 | 128 | 1 / snap |
| Step 5 | NotchThresholdPercent | 0 | 100 | 0.1 / 不 snap |
| Cascade 視窗 | 每顆 IC 的 XChannels／YChannels | 1 | 1,000,000 | 1 / snap |

沒有 Settings 控制項的數值屬性：`LenScale`（被 `_showInternalLegacyNotchFields=false` 隱藏）、`CadOutputFwDiffIndexAnchorCadId`、草稿的總 `XChannels`／`YChannels`（由每顆 IC 列加總覆寫，直接寫入會被忽略）。

## 控制項層的行為（讀程式碼，未另行執行）

`NumberScrubber`（`NumberScrubber.ValueFormatting.cs`、`.Input.cs`、`.Properties.cs`）：

- 空字串／全空白：不改值，文字還原。
- 無法解析（`abc`、`1,5`、超出 decimal 範圍如 `1e30`）：不改值，文字還原。解析使用 `NumberStyles.Float` + InvariantCulture，所以 `1e3` 會被當成 1000。
- 可解析：先依 `SnapToStep`／`SmallChange` 四捨五入（AwayFromZero），再 clamp 到 Min／Max。整數欄位（snap=1）輸入 12.7 會變 13。
- `Value` 被設成範圍外時，控制項會自行 clamp，並以 TwoWay 綁定寫回 VM。

因此「小數輸入整數欄位」「空值」「負值」「超出範圍」在**透過 UI 輸入**時都由控制項處理；下列發現多數是 VM／儲存層在控制項被繞過時的行為。

## 實測結果摘要（VM → Save → 專案檔 → 重新載入）

### A. 與 UI 宣告相符：重新載入後落在 [Min, Max]
GlobalFontSizePercent、HighlightStrokeWidthAdjust、MatchThreshold、FreeformAxisThreshold、ToFullStrictOverlapPercent、BoundaryVirtualAreaCapPercent、TargetCoverageCapPercent、NotchPreviewAutoPlayIntervalMs、NotchPreviewVisualizationStep、MappingCandidateNumber、MappingLowConfidenceThreshold、MappingAmbiguousMargin、CadOutputFwDiffIndexStart、NotchThresholdQ7、CascadeNum（小值）、CoordinatePixelWidth／Height（下限）。
Min 與 Max 邊界值本身都能無損 round trip。0、−1、−1e9 一律落在下限；max＋一格與 1e9 一律落在上限。

### B. 發現（依嚴重度）

**B1（可由 UI 重現，建議優先）GridPaddingPercent：UI 宣告 0–100，儲存路徑上限實際為 50。**
`FreeformHelperViewModel.Settings.Sync.cs:221`：`BoundsPaddingRatio = Math.Clamp(GridPaddingPercent / 100.0, 0.0, 0.5)`。
實測：草稿輸入 100 → Save 後 owner 仍顯示 100 → 存檔重新載入變 **50**（101、1e9 同）。
專案檔內 `UiSnapshot.Grid.GridPaddingPercent` 另外以未 clamp 的值寫入，載入時以 settings 的 0.5 為準。結果是 UI 與生效值不一致。
需要決定：把控制項 Maximum 降到 50，或放寬 0.5 的上限。

**B2（可由 UI 重現）CascadeNum／每顆 IC 通道數的宣告上限（1,000,000）實際無法使用。**
CascadeNum 重建 grid 的耗時超線性成長（本環境 Debug 建置實測）：

| CascadeNum | 重建等待 | 全程 |
|---|---|---|
| 32 | ≈0.12 s | 0.3 s |
| 1,000 | ≈1 s | 3 s |
| 10,000 | ≈16 s | 62 s |
| 100,000 | 超過測試輔助的 30 s 上限（`Building regular grid...`） | >100 s |

每 IC 32 欄，所以 10,000 顆 = 320,000 欄。另外把單顆 IC 的 X 通道設為 1e9（超出控制項上限，只能由 VM 繞過控制項達成）會在 `ManualSizingService.Resize` 拋 `OutOfMemoryException`。
`MaxChannelsX` 的保護沒有擋住這條路徑。建議在控制項或儲存路徑加上實務上限，或至少顯示警告。

**B3（VM 層缺口，控制項會擋；低）** 下列欄位的草稿／owner／儲存路徑都不強制 UI 的 Min 或 Max，只靠 `NumberScrubber`：

- ActiveAreaWidth／Height：Min 0.1 未強制。≤0 在記憶體保留負值（owner 讀到 −0.9），存檔時 `Math.Max(0, …)` 變 0，重新載入由 `EnsureActiveAreaDefaults` 還原成**預設值 310**（不是 0.1）。0＜v＜0.1 原樣保存。Max 未強制（1e9 原樣 round trip）。
- PanelBiasX／Y：Min／Max 皆未強制。
- CoordinatePixelWidth／Height：Max 未強制（1e9 原樣保存）。Min 與四捨五入有強制。
- MappingWeightIou／CentroidDistance／AreaRatio：Max 10 未強制（10.0001、1e9 原樣保存）。Min 有強制。
- MatchThreshold、FreeformAxisThreshold、MappingLowConfidenceThreshold、MappingAmbiguousMargin、CadOutputFwDiffIndexStart、MappingCandidateNumber、NullValue（下限）：只在**存檔時** clamp。Save 之後 owner 仍持有範圍外的值（例如 MatchThreshold = −0.0001），直到重新載入才變成 clamp 後的值。

**B4（VM 層缺口，控制項會擋；低）「整數欄位」收到小數時，不同欄位處理不一致。**

- 草稿即四捨五入：CascadeNum、CoordinatePixelWidth／Height、每顆 IC 通道數。
- 存檔時才取整（owner 顯示 127.9、重新載入變 128）：TargetCoverageCapPercent、MappingCandidateNumber、CadOutputFwDiffIndexStart、NullValue、NotchThresholdQ7。
- **完全不取整，小數原樣寫入專案檔**：GlobalFontSizePercent（80.4）、NotchPreviewAutoPlayIntervalMs（200.4）、NotchPreviewVisualizationStep（1.4／2.4；此值語意上只有 1–3）。

**B5（可由 UI 重現，可能是設計）NotchThresholdPercent 與 Q7 連動時，輸入值不等於重新載入值。**
`LinkNotchThresholds` 預設開啟時，百分比以 Q7 格（100/128 = 0.78125%）為準。實測輸入 0.4 → 重新載入 0.78；輸入 50.4 → 50.78。控制項允許 0.1 解析度，所以使用者輸入的值在存檔後會被換成格點值，Save 後 owner 仍顯示輸入值。

**B6（VM 層；以明確訊息拒絕，符合預期）NullValue > 65535。**
草稿與 owner 都接受 65536，`SaveProjectAsync` 回傳 false，訊息：`Save project failed: NullValue must be in [0,65535].`。控制項 Max 是 65535，所以 UI 無法達成。此欄位沒有存檔前的 clamp，因此 settings 視窗 Save 之後 owner 帶著非法值、專案無法存檔。

**B7（不可由 UI 達成）CadOutputFwDiffIndexAnchorCadId。** 範圍外或不存在的 pad ID 會被 `TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue` 拒絕（owner 變 −1，重新載入 −1）。草稿接受 −2、−0.6 等。

### C. 已知現況（既有測試已記錄）
`SettingsWindowDraftSnapshotCharacterizationTests`（SETTINGS-D01）：Save 會把整份草稿寫回，覆蓋視窗開啟後的 live 修改。本次探針沒有碰到此問題（每次只改一個欄位且無 live 修改），但會影響「只改一個欄位」的測試設計。

## 已加入的測試與未完成項目

已加入（`SettingsWindowNumericBoundsTests`）：

- 反射守門：`SettingsWindowViewModel` 的所有 `decimal` 屬性必須在欄位表內，或在「未涵蓋」清單（CascadeNum、XChannels、YChannels、LenScale、CadOutputFwDiffIndexAnchorCadId）。新增數值欄位卻沒補邊界測試會失敗。
- 以真正的草稿 VM → Settings Save → `SaveProjectAsync` → `LoadProjectFromPathAsync`，對 25 個欄位各做 min、中間值、max、min−一格、max＋一格的 round trip（每組一條管線，約 1–2 秒）。
- 表中的 `MinEnforced`／`MaxEnforced` 旗標釘住 B3 的現況（模型不強制的那一側原樣通過）；`GridPaddingPercent` 的 `PersistedMax = 50` 釘住 B1。修正時要同步改旗標。
- NullValue＝65536：專案存檔回傳 false，訊息含 `NullValue must be in [0,65535]`（B6）。

尚未涵蓋（建議後續補）：

1. 實際建立 `SettingsWindow`，用 `BindingOperations.GetBindingExpressionBase` 對照每個 `NumberScrubber` 的 Min／Max 與欄位表一致（探針已證實此 API 可用；隱藏 section 要先切換 `SelectedSection` 才會實體化；Cascade 視窗只實體化第一列 IC）。
2. 透過輸入框驅動空字串、空白、非數字、`1e3`、小數、範圍外，確認值不變或被 clamp／snap，且草稿屬性等於 `Value`（行為見上方「控制項層的行為」，目前僅讀程式碼，未用測試驗證）。
3. CascadeNum 與每顆 IC 通道數：只能用小值（32、64、2000）；邊界與負值、小數在探針中行為正確（草稿即 clamp／四捨五入、重新載入相同），但未寫成測試。
4. 小數輸入整數欄位的 B4 不一致、B5 的 Q7 連動、B3 中「存檔後 owner 仍持有範圍外值」，只在本文記錄，沒有測試。
5. ActiveAreaWidth／Height 的 ≤0 → 預設值（310）行為沒有測試（探針已驗證，測試中刻意排除）。

注意事項：

- 完整管線每筆約 0.8 s；逐欄逐值各跑一次約 4 分鐘。測試檔改為每組探測值一條管線。
- 新測試要避開專案禁用的 API：`Path.GetTempPath`、`BindingFlags.NonPublic`、`File.ReadAllTextAsync`，且 `catch (Exception)` 會觸發 CA1031（error）。可重用 `LoadingModeTestScope`（`SettingsLoadingRegionBehaviorTests.cs`）。
- 專案規定提交前跑 `./scripts/tests/lint.ps1 -UseNoAppHost`，本環境沒有 `pwsh`，未執行；只跑了 `dotnet test`（含 analyzer 編譯，新檔案零 warning）與 Architecture 測試。
