# Source-line length coverage model（S14.012 研究筆記）

> 狀態：候選模型與待驗證假設；不是已驗證的物理契約，也不變更 allocation／compensation 規格。
> 盤點基準：本分支的 `PadMatcher`、`NotchV22TargetAllocationService` 與 `NotchV22CompensationService*.cs`。
> 現行契約入口：[`notch-system-reference.md`](notch-system-reference.md)；數學與歷史背景見 [`notch-2.2-spec.md`](../core/notch-2.2-spec.md)。

本筆記保留一個研究問題：polygon 面積相同，但實際 display source/data line 的覆蓋長度分布不同時，量測的感應量是否仍相同？以下先記錄可由程式碼確認的幾何用途，再列出候選公式與進入 prototype 的條件。程式碼與既有測試能確認演算法行為，不能證明面積或線長與真實電氣訊號的關係；本次沒有面板線路圖、stack-up 或量測驗證。

## 1. 術語與資料邊界

| 名稱 | 本筆記的用途區分 | 不可直接等同的資料 |
| --- | --- | --- |
| display TFT **source/data line** | 顯示 TFT 的資料驅動線，向受選取的像素／子像素提供顯示資料電壓；本候選模型研究的是這類線的幾何覆蓋。 | touch Rx、FW diff、CAD pad ID；程式中的 `SourceArea` 也不是這條線的面積。 |
| display TFT **gate/scan line** | 顯示 TFT 的掃描選取線，控制對應 TFT 的導通時序。 | source/data line；不可只因圖上兩者看似正交，就把任一方向稱作 source line。 |
| **touch Tx/Rx** | 觸控電極／通道；在 mutual-capacitance 語境中 Tx 是激勵端、Rx 是接收端。self-capacitance 或整合式面板的實際角色須另確認。 | display data／scan 線，或 RegularGrid 的 row／col；是否有結構共用必須由面板設計證實。 |
| **pixel/subpixel pitch** | 像素／子像素的重複間距，須指定方向與量測單位。 | 導線寬度、touch 電極 pitch、regular pad 尺寸或 source-line pitch。不能通用地以 pixel pitch 除以三取得所需線距。 |

實際線路方向、走線偏移、有效端點、像素排列，以及 display 與 touch 層的對應，均須由面板資料確認；本筆記不假定 source 一定沿 Y、gate 一定沿 X。`RegularGrid` 是現有幾何／FW mapping 的基準，不是 TFT 線路圖。

## 2. 現有 polygon-area 用途（程式碼事實）

### PadMatcher：overlap 與 matching evidence

[`PadMatcher.cs`](../../src/FreeformHelper.Application/Services/PadMatcher.cs) 以 CAD polygon 和 regular bounds 的交集面積建立多對多 links：

```text
overlapArea = Area(CAD polygon ∩ regular rectangle)
RegularCoverage = overlapArea / regularArea
CadCoverage = overlapArea / cadArea
```

分母有數值保護；overlap 必須大於 absolute floor `1e-6` 與 `regularArea * 0.00001` 的較大值，排除邊界接觸／數值雜訊。links 依 `RegularCoverage`、`OverlapArea`、`CadCoverage` 遞減排序；regular 的相容單一 best match 與 `MatchScore` 取第一筆及其 `RegularCoverage`。`MatchingSettings` 參數目前被忽略。這是幾何匹配證據，沒有 source-line 長度、pitch 或電氣權重。

### NotchV22CompensationService：SourceArea、reachability 與 Stage3

[`NotchV22CompensationService.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.cs) 的 canonical 入口是 `Compute(NotchV22CompensationContext)`。context 可持有既有 allocations；相容 adapter 未收到 allocations 時，才由 [`Stages.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs) 的 `BuildCompensationAllocations` 計算 polygon／regular 交集，以 `overlapArea / cadArea` 建立 allocation。不能把此 fallback 描述成直接消費 PadMatcher links；兩者的過濾門檻也不同。

| 量 | 目前計算與用途 |
| --- | --- |
| `OverlapArea` | Stage A 從 `allocation.Ratio * cad.Area` 還原非負 overlap；大於 `AreaEpsilon` 的項目進入 overlap 診斷，並累加 `overlap / regularArea`。 |
| `SourceArea` | Stage C 只有在 `overlap > max(AreaEpsilon, regularArea * strictOverlapRatio)` 時，才記錄 reachability 的 source area，否則記為零。reachability helper 保留原始非負 overlap 作為 source area，並非用 `SourceCellCount * cellArea` 取代。因此小 overlap 可以仍有 `OverlapArea`，卻沒有可用的 `SourceArea`。 |
| `ReachableArea` | ToFull 實際 apply 時取至少 overlap 的可達面積；未 apply 時為 overlap。用來表示允許的幾何補償範圍，不是實測感應面積。 |
| `Stage3EffectiveArea` | 以 overlap 作為 inside area；未 apply 時為 overlap，apply 時使用可達面積。既有 virtual-area cap 啟用時，僅允許 `inside + min(reachable - inside, inside * capRatio)` 進入 Stage3。 |
| `Stage3Area` | CAD 整體診斷基準：`safeCadArea + sum(max(0, Stage3EffectiveArea - OverlapArea))`，sum 僅包含 `IsToFullApplied` 的 regular。它不是逐格 `Stage3EffectiveArea` 的總和，也不能直接由 preview polygons 面積替代。 |
| CAD-level ratios | ToRegular 啟用時為 `sum(overlap / regularArea)`，否則為 `1`；`ToFullRatio = max(1, Stage3Area / safeCadArea)`；`CombinedRatio = ToRegularRatio * ToFullRatio`。目前保留為診斷，不能代替 final per-target coverage。 |

[`Boundary.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Boundary.cs) 由 grid 外圈／不存在或 inactive 鄰居建立 boundary evidence；[`SpatialIndex.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.SpatialIndex.cs) 以交集面積查 strict owners 與 blockers。[`Geometry.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Geometry.cs) 產生 clipped overlap／occupied union 與 preview polygons；[`Geometry.Reachability.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Geometry.Reachability.cs) 使用目前 `40 × 40` 計算格處理 ownership／可達 free cells，無 foreign occupied cells時可擴至 regular bounds，並保留 overlap 下限。

這些計算格、cell counts、blocked／reachable area 與 ToFull rule decision 是演算法幾何證據。計算格不是 pixel/subpixel，也不是 TFT 或 touch 線距。`Stage3EffectiveArea` 可因 cap 小於 `ReachableArea`；preview 的可達輪廓不等於獲准進入 allocation 的 effective area。

### NotchV22TargetAllocationService：coverage 與 target weight

[`NotchV22TargetAllocationService.cs`](../../src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs) 的 `Build` 消費 compensation 的 `RegularDebugInfos`，選擇每格 `baseWeightArea`，再按 `(IcIndex, DiffIndex)` 分組。指定 anchor IC 時先排除其他 IC，再計算 allocation basis 與 threshold。

| Area mode | 每格 baseWeightArea | 分組 Ratio |
| --- | --- | --- |
| `SourceAreaDominant` | 非負 `SourceArea` | group area / allocation basis area |
| `Stage3EffectiveArea` | `IsToFullApplied` 時取 `max(SourceArea, Stage3EffectiveArea)`，否則取 SourceArea | group area / allocation basis area |
| `TargetRegularSourceCoverage` | 非負 `SourceArea` | `sum(baseWeightArea / regularArea)` |
| `TargetRegularStage3Coverage` | `IsToFullApplied` 時取 `max(SourceArea, Stage3EffectiveArea)`，否則取 SourceArea | `sum(baseWeightArea / regularArea)` |

`CurrentGain` 選 `TargetRegularStage3Coverage`；`ConservativeNoGain` 選 `TargetRegularSourceCoverage`，ToFull 不直接增加其 amount；其他 model 選 `SourceAreaDominant`。`Stage3EffectiveArea` 同時是 debug 欄位與 area mode 名稱，兩者不可混用。

allocation basis 是留下的 groups 的 baseWeightArea 總和；`PassesStrictThreshold` 使用嚴格的 `group area > max(AreaEpsilon, allocationBasisArea * normalizedStrictRatio)`，不是 PadMatcher noise floor，也不是 Stage C 的每格 strict threshold。target-regular coverage 是逐格除以各自 regular area 後加總，不是 group area 除以所有 regular area 的總和；它不要求所有 targets 加總為 `1`。

每組先 `Math.Round(Ratio * 100)`。`ProjectTargetCoverage` 只 emit anchor IC、非 anchor/source diff、rounded percent 非零，且通過 strict threshold 或有 ToFull applied 的 targets。target-regular 模式的 `RawCombinedPercent` 是 retained anchor percent 加實際 emitted 正向 target percents；其他／缺 anchor 情境保留 compatibility display。這些 coverage／percent 供既有 allocation、顯示、final projection 與 guard 使用，不是 TFT line coverage 或校準後的感應量；實際 row chunking 與 target coverage guard 仍依現行契約。

## 3. 候選 source-line 公式（尚未驗證）

```text
effective signal ~= sum(source-line covered length * pitch * weight)
S_candidate(P) ~= Σ_i length(P ∩ L_i) * p_i * w_i
```

- `P`：待研究的實際覆蓋 polygon；比較某個 regular 時，使用 `CAD polygon ∩ regular`。Stage3 ToFull 虛擬擴展不能直接當作真實導體覆蓋。
- `L_i`：面板上第 i 條 display source/data line 的實際有效路徑；`length(P ∩ L_i)` 是沿該路徑被覆蓋的各段長度總和，不是線條數、bbox 高度或 polygon 周長。同一條線的重複交集須去重，缺口不計入。
- `p_i`：該線代表的橫向取樣間距／帶寬，單位為長度；須由 source-line 排列確定，不能直接套用名稱未辨明的 pixel/subpixel pitch。彎折或不均勻走線是否能用此帶寬近似仍待驗證。
- `w_i`：相對耦合／敏感度的候選權重，來源與是否隨位置、display 狀態或 touch 通道變化尚未知。本筆記不指定權重、係數或校準值。

若 weight 無量綱，右側單位是長度平方，只是加權幾何 proxy；`effective signal` 是研究名稱，不能聲稱它已輸出電容、raw count 或 FW diff 數值。若要預測電氣單位，必須另有量測支持的轉換。也不能直接將此量填入既有 area／ratio 欄位；其正規化基準與目標量尚未確認。

## 4. 與 polygon-area 模型的差異

polygon-area 模型積分整個覆蓋區域；候選模型在指定的離散 source-line 路徑上取樣，再乘橫向 pitch 與 weight。現有演算法仍會對 polygon 形狀、跨 regular 的 overlap、blockers 與 reachability 作不同處理，不能概括成「只看整顆 CAD 總面積」。

對密集、平行、等距的線路與均勻 weight，`sum(length * pitch)` 可近似 polygon 面積積分；此時候選模型可能只是以另一種方式計算同一個量。**等面積不必然產生不同 effective signal。**只有離散線位置／間距、有效線段、非均勻權重等因素使取樣有意義時，兩種模型才可能有可分辨的預測。

研究比較應保持每個 regular 的 overlap 面積與現有 ToFull／blocker 條件相同，再改變 polygon 相對實際線路的位置或方向。例如，在有限線距下，等面積的窄片落在線上或線間可能得到不同 covered length；這是幾何取樣的示例，不是已觀測的面板效應。若只保持 CAD 總面積相同卻改變 target overlap，現有模型本來就可能不同，不能作為 source-line 模型較好的證據。

## 5. 物理推論與待驗證假設

1. **假設 H1：**目標感應量的某部分與被覆蓋的 display source/data line 有可辨認的耦合，線長分布能提供面積以外的資訊。現有程式並未描述這種電氣關係；先辨明要解釋的是 touch response、baseline，還是 display-induced noise。
2. **假設 H2：**研究範圍內可用各線貢獻相加的近似。fringing、層間距離／介電結構、遮蔽、走線阻抗與時序等可能使關係非線性或位置相關；此處只列為可能的影響因素，未確認任何因素在特定面板上的主導性。
3. **假設 H3：**pitch 與 weight 能由獨立資料辨識，且跨樣本仍有效；不能用任意權重配合既有 allocation 結果，然後宣稱驗證成功。

若 gate/scan line 或 touch Tx/Rx 才是主要影響來源，必須重新檢視 H1；不能把它們改名為 source line，或無證據地把三類線的貢獻相加。ToFull 的幾何虛擬面積也不證明物理上新增了任何線路耦合。

## 6. 是否值得進 prototype

**目前判斷：保留為研究假設；僅憑現有程式碼與幾何測試，尚不足以支持進入 prototype。**以下條件有證據支持後，才值得另立研究工作：

1. 有可授權使用、可與 CAD 座標對齊的 source/data line 路徑、方向、pitch、offset 與有效端點資料，並辨識 gate/scan、touch Tx/Rx 及 pixel/subpixel 的對應。
2. 明確定義目標量與可重現量測條件，包含 display／touch 工作狀態；有等 overlap 面積、不同線長分布的受控樣本與重複量測，差異超過量測不確定度。
3. 線長模型在比較前能提出可否證的預測；資料顯示 area-only 模型的殘差與線長分布有關，且改善不只是既有 target overlap、ToFull、blocker 或正規化差異。
4. 權重有獨立依據；以未參與估計的樣本檢查改善與穩定性，並與 polygon-area 基準比較。不能只看訓練樣本 fit 或現有 Simulation，因為 Simulation 消費既有 notch table，不能獨立驗證物理假設。
5. 在研究前議定實用改善門檻、量測誤差界線與可接受計算成本；本筆記不替這些條件指定數值。研究比較能維持 production allocation／compensation、Firmware C 與 golden 原樣。

缺乏線路資料／量測、差異可由現有模型解釋、均勻線距下退化為 area，或改善未超出不確定度時，應暫緩 prototype，繼續使用既有 polygon-area 契約。即使後續 prototype 成立，也只構成研究證據；採用為 production 模型仍須另行審查，不由本筆記授權。
