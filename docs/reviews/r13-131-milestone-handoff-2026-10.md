# 1.3.1 里程碑交接（2026-10）

本交接依 roadmap 的「Milestone handoff」六項欄位整理，補齊 PR-B／PR-C 最終 revision 的驗證證據；本文件不勾選 R13.101～R13.104 parent，1.3.1 里程碑是否退出仍由 owner 決定。（`TODO.md:R13.101`、`R13.102`、`R13.103`、`R13.104`；`docs/guides/refactor-roadmap-1.3.x.md:Milestone handoff`）

來源固定如下：實作狀態取本工作樹的 `TODO.md:R13.101`～`R13.104`，最終驗證分別對應 PR-B tip `2e3cbd08` 與 PR-C tip `4d54b35d`；指定退出稽核檔未存在於本工作樹，唯讀引用本地 commit `1c195dc0439963c2b1ee15ad9e643edcb15a0590` 的 `docs/reviews/r13-131-exit-audit-2026-10.md`。下文「稽核 A1／A2／B1／B2／C1～C6」均指該版本「去重後的剩餘義務分類」；稽核內的 `E:` 證據仍屬其記載的實作 commit，並非第 4 節的最終 revision 重驗結果。（`TODO.md:R13.102a-3b`、`R13.102a-4`、`R13.102a-5`；稽核「來源與限制」）

交付依序為三個 PR，疊加於 `origin/1.3.x = 2c1c0c84`：PR-A 在另一分支，以 `NotchDisplayProjector` 讓 Disabled-target 顯示與 generator 一致（owner 決定「make the screen agree with the generator」），只改顯示數字；PR-B 為本樹截至 `2e3cbd08` 的 R13.104 safety-text centralization、R13.102 resolved readers（含 PadInfo `resolvedDisplayInput`）、A1 facts 刻畫與 duplicate removal，output zero-diff；PR-C 為 `4d54b35d` 的 R13.102a-5，診斷與 warm export 共用 Q7-positive allocation 集合，包含第 6 節揭露的 firmware corner case，須 owner 明確核准。文中的 commit SHA 是私有主幹上的識別；公開 repo 以補丁重建這些 commit，SHA 不同、內容相同，對照時以 commit 標題為準。

## 1. 已完成的 R13.*

| 已完成項目 | 已記錄的成果與依據 |
| --- | --- |
| R13.101a、R13.101b | per-IC candidate pool 採 owned snapshot，消除重複複製／掃描；allocation memo 改以 immutable polygon 與 exact geometry comparison 識別同 ID 的幾何變更。（`TODO.md:R13.101a`、`R13.101b`） |
| R13.101c-1、R13.101c-2a | generated-table key 移除 `ExportProfile`；live guard/cap-only 設定變更收斂為 final-projection-only invalidation。（`TODO.md:R13.101c-1`、`R13.101c-2a`） |
| R13.101d-1、R13.101d-2、R13.101e | 凍結 normal CadAllocation computation context，統一 UI／generator 的 Q7-positive pool admission；完整 compensation context 經唯一 `Compute(context)` 路徑執行。（`TODO.md:R13.101d-1`、`R13.101d-2`、`R13.101e`） |
| R13.102a-1（含 PadInfo follow-up）、R13.102b-1、R13.102b-2 | preview／deferred Inspector 共用 revisioned resolved snapshot，Detail 與 override-aware Runtime Query 投影同一 owner；PadInfo follow-up 擷取單一 `resolvedDisplayInput`。（`TODO.md:R13.102a-1`、`R13.102b-1`、`R13.102b-2`；稽核「最新證據已排除的舊剩餘項」） |
| R13.102a-2a、R13.102a-2b-1 | normal export/generator 重用 output-request-neutral candidate batch；generation completion 依 epoch／final-projection revision／Simulation source identity 拒收過期結果。（`TODO.md:R13.102a-2a`、`R13.102a-2b-1`） |
| R13.102a-2b-2a、R13.102a-2b-2b | 並行 Export／Simulation 共用 full batch-resolution task；目前單一 selected sparse result 接入 batch session，移除 VM 平行 compensation dictionary，收口 2b-2 的 bounded bridge。（`TODO.md:R13.102a-2b-2a`、`R13.102a-2b-2b`） |
| R13.102a-3b（Facts 2～6 刻畫；PR-B） | Facts 2／3 證明 reader 重複推導；Fact 5 鎖定修正前 raw owner 與 Q7-positive reader 分歧；Facts 4／6 為保留的 ABI 投影。新增刻畫 `18/18` 通過，entry 與 parent 仍未勾選。（`TODO.md:R13.102a-3b`） |
| R13.102a-4（PR-B） | normal reader 的 Fact 2／3 改讀 owner 的 ToFull count、唯一 anchor target area／既有 Stage D fallback，刪除重複 IC 篩選、Max 與非負 clamp。（`TODO.md:R13.102a-4`；`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate`） |
| R13.102a-5（PR-C） | 移除 raw allocation builder，compensation diagnostics／warm export 與 generator 共用既有 Q7-positive allocation 集合；單一 Q7=0 診斷改變，混合 allocation 檢視後匯出的 V21／V22 rows 與 FW apply simulation 亦改變，warm 現在等於 cold。cold generator 與 golden inputs 輸出不變；firmware 變更仍須 owner 明確核准。（`TODO.md:R13.102a-5`） |
| R13.103a、R13.103b-1、R13.103c-1、R13.103c-2 | 鎖定 final ABI characterization；threshold admission 移至 final row projection；V22 export／simulation 共用 Firmware projector；NullValue 共用 `0..65535` validation owner。（`TODO.md:R13.103a`、`R13.103b-1`、`R13.103c-1`、`R13.103c-2`） |
| R13.103d-1、R13.103d-2 | Legacy request 在 progress callback 前凍結；以固定 V21／V22 dispatch 取代 strategy registry，保留 request-specific table cache。（`TODO.md:R13.103d-1`、`R13.103d-2`） |
| R13.104a-1～R13.104a-3 | EMS cap predicate 共用；combined-overflow、effective target count／line／role 改讀共同 target eligibility／membership projection。（`TODO.md:R13.104a-1`、`R13.104a-2`、`R13.104a-3`） |
| R13.104a-4～R13.104a-8 | Simulation／replay availability、severity、physical audit warning 及 no-cell cap 文字共用 projector，export review 讀相同 audit 結果。（`TODO.md:R13.104a-4`、`R13.104a-5`、`R13.104a-6`、`R13.104a-7`、`R13.104a-8`） |
| R13.104a-9～R13.104a-14 | safety guidance、target-cap help、初始 cap、guard summary／checklist、export blocked 文字、workspace cap／high-risk label 收斂至共用 projector。（`TODO.md:R13.104a-9`、`R13.104a-10`、`R13.104a-11`、`R13.104a-12`、`R13.104a-13`、`R13.104a-14`） |

A1FACTS 的 Facts 2～6 已完成刻畫，`R13.102a-3b` 仍未勾選；owner 已於 2026-10-03 裁定「診斷跟 generator 一致，Q7=0 不顯示」，由 PR-C 的 R13.102a-5 實作。診斷裁定不代表混合 allocation 的 firmware 變更已獲核准。（`TODO.md:R13.102a-3b`、`R13.102a-4`、`R13.102a-5`）

## 2. single-entry／single-result 變化

- PR-A 的 Disabled-target display 由 `NotchDisplayProjector` 投影與 generator 一致的數字；此 PR 在另一分支，依交付順序先於本樹的 PR-B／PR-C。

- normal generator／UI 先組成完整 compensation context，再經唯一 `Compute(context)`；舊 public 多參數入口只補齊 context 並薄委派，明確空 allocations 為 authoritative empty。（`TODO.md:R13.101d-1`、`R13.101e`）
- 同一 selection/revision 的 preview／Inspector／Detail／Runtime 讀 revisioned `NotchV22ResolvedResult`；PadInfo 的 normal snapshot 路徑投影單一 `resolvedDisplayInput`。Runtime `multi-owner` 的 override 只建立一份相應 result，metadata 使用輕量 Inspector snapshot。（`TODO.md:R13.102a-1`、`R13.102b-1`、`R13.102b-2`、`R13.102a-2b-2b`）
- normal Export／Simulation 的 full consumers 共用一個 resolution task，各自依入口凍結的 final request 投影；full batch 至多重用目前單一 selected sparse result，不代表所有 candidate／consumer 已 repository-wide 共用同一 result。（`TODO.md:R13.102a-2a`、`R13.102a-2b-2a`、`R13.102a-2b-2b`、`R13.102a-2`）
- PR-B 以 R13.102a-3b 刻畫 Facts 2～6，R13.102a-4 讓 normal generator Fact 2／3 直接讀 resolved allocation／compensation owner，output zero-diff；Facts 4／6 的 ABI clamp、chunk、ordering 投影刻意保留。（`TODO.md:R13.102a-3b`、`R13.102a-4`；`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate`）
- PR-C 的 R13.102a-5 移除 `BuildCompensationAllocations` raw builder；`CreateContext` 與相容 `Compute` 共用 `NotchAllocationService.BuildAllocations` 的既有 `q7 > 0` predicate。diagnostics／resolved target 與 generator 使用同一 allocation 集合；真實匯出重用檢視後的 warm resolved result，混合 Q7=0／Q7-positive allocation 的 warm rows 因此改為 cold 值，FW apply simulation 亦消費這組 rows。這是需要明確核准的 output 變更，不能併稱 PR-B 的 zero-diff。（`TODO.md:R13.102a-5`）
- final threshold admission 產生共用 admitted view；V22 C formatter 與 simulation 共讀 `NotchV22FirmwareProjector` 的 projected nodes。Simulation 執行整數物理後聚合 Actions 是既有契約，不能視為再次計算 compensation；Release no-op filtering 仍屬 exporter emission policy。（`TODO.md:R13.103b-1`、`R13.103c-1`；稽核「最新證據已排除的舊剩餘項」）
- target coverage／overflow 由 Application policy 擁有，display 只格式化 supplied ratio／risk；EMS predicate、Simulation／replay severity 與 safety 文字由共用 owner 投影。這些完成範圍不等於 `R13.104` parent 已退出。（`TODO.md:R13.104a-1`～`R13.104a-14`、`R13.104`）

## 3. side effects 是否變化

有局部政策變化，也有明確保留的副作用契約；來源未逐項記載的項目不推定為全域不變。（`TODO.md:R13.101c-2a`、`R13.102a-2b-1`、`R13.103d-1`、`R13.104a-12`）

| 範圍 | 變化／保留與依據 |
| --- | --- |
| guard/cap invalidation | direct Step3 controls／Settings Save 共用 `InvalidateNotchFinalProjection`；保留 Step3 revision、per-CAD result identity、export batch，只清 Step5 summary／last table／validation 並通知 Simulation source revision。完整 Step5 clear 才重設 operation progress／busy 並 evict batch。（`TODO.md:R13.101c-2a`） |
| completion／busy lifetime | 只有 current completion 可提交 row count、table、progress 與 continuation；final-only invalidation 保留 batch 但將 projected row count 歸零、拒絕舊 projection／session；full invalidation 拒絕舊 store。Export／Simulation 共用 ref-count busy scope，單項完成不會提早解除另一項 busy。（`TODO.md:R13.102a-2b-1`、`R13.102a-2b-2a`） |
| deferred Inspector／PadInfo | debounce、pending、cancellation、selection/revision stale rejection、UI-thread apply 保留；Detail cold result apply 前的 full-key recheck 防止覆蓋 current identity。PadInfo follow-up 未新增 selection clear、rebuild、fit／zoom、cache invalidation 或 status side effect。（`TODO.md:R13.102a-1`、`R13.102b-1`） |
| callback freeze | normal generation 保留 phase-2 initial callback 返回後凍結的時序；Legacy 改為第一次 progress callback 前凍結 request，callback 修改仍回到 caller，但只影響下一次 generation。（`TODO.md:R13.101d-1`、`R13.103d-1`） |
| validation failure | 越界 NullValue 在 Firmware formatting／evaluation 前 fail-fast；Named Pipe boundary 以既有 `IPC_ERROR` envelope 傳回原訊息；empty table／unsupported simulation 保留 fast path。（`TODO.md:R13.103c-2`） |
| audit warning／通知 | physical-only audit 在 status／overview／export review 顯示 `audit warning`，export safety clean flag 為 false，但不阻擋 export。current cap 變更通知三個 guidance properties；workspace overview 的 `HasRisk` 仍只表示 EMS danger，`NeedsAttention` 包含 physical risk／stale。（`TODO.md:R13.104a-7`、`R13.104a-8`、`R13.104a-9`） |
| 文字收斂的副作用 | guard summary／checklist 未改 selection clear、rebuild、fit／zoom、undo、通知、export eligibility；blocked 文字與 workspace labels 未改選列、通知、rebuild 或 layout。（`TODO.md:R13.104a-12`、`R13.104a-13`、`R13.104a-14`） |

刻意修正的可見結果另列：cross-IC UI computation 的 `ToRegular / ToFull / Combined` 由 `0.5 / 2 / 1` 收斂為 generator 的 `0.5 / 1 / 0.5`；normal Detail 改投影 anchor IC，characterization 的 unanchored `200%` 與 authoritative `100%` 差異已記錄；explicit empty allocations 不再以 geometry fallback 得到 `ToRegular=0.5`，而是零結果。（`TODO.md:R13.101d-2`、`R13.102b-1`、`R13.101e`）

## 4. G0～G6 實際執行結果

以下為最終 revision 的驗證證據（由 1.3.x session 於 sandbox 外執行）：Windows、Debug，於 sandbox 外執行，機密 example data 存在且 submodule 固定 `8c84e4d6`；本次文件整理不重跑這些 gates。PR-B `2e3cbd08` 的 refactor gate 約 3 分鐘完成，PR-C `4d54b35d` 同一命令亦通過；兩次所列全部測試群組均為 `0 failed / 0 skipped`。

| Gate | PR-B tip `2e3cbd08` | PR-C tip `4d54b35d` |
| --- | --- | --- |
| G0 Build/Lint | 最終 gate 的 lint `-AllFiles` 完成、build-ui OK。 | 最終 gate 的 lint `-AllFiles` 完成、build-ui OK。 |
| G1 Targeted | leaf 落地時已跑直接契約測試：R13.102a-3b 新增刻畫 `18/18`；R13.102a-4 的 `A1Fact*` `18 passed`、PadInfo `6/6`。最終 revision 改由 gate 跑全部群組。（`TODO.md:R13.102a-3b`、`R13.102a-4`） | leaf 落地時已跑 R13.102a-5 的 Fact 5／Q7=1 正控制、warm/cold V21／V22 與 partial-overlap 三例；A1-F5b 新增三例及 `A1Fact*` `21` 通過，新增三例在舊 builder 下皆失敗。最終 revision 改由 gate 跑全部群組。（`TODO.md:R13.102a-5`） |
| G2 Notch Core | 最終 gate：`209 passed / 0 failed / 0 skipped`。 | 最終 gate及明確的 `-Group notch-core` 呼叫皆為 `214 passed / 0 failed / 0 skipped`。 |
| G3 Golden | 最終 gate 的 notch-core 群組包含 golden 測試；兩版使用同一群組清單，無失敗或略過。本 revision 未另提供獨立 `-Group notch-golden` 呼叫紀錄。（群組包含關係見 `TODO.md:R13.102a-3b`、`R13.102a-4`） | 明確執行 `-Group notch-golden`：`6 passed / 0 failed / 0 skipped`，含 C export drift、golden baseline、TM8.1 acceptance matrix。 |
| G4 Runtime CLI | baseline `-LaunchIsolatedUi -EnforceBudget` 正向 OK，加 `-ReverseCExportOrder` 反向亦 OK；所有 `passesStrictThreshold` 均為 true。 | 同一 baseline 正向／反向均 OK；所有 `passesStrictThreshold` 均為 true。 |
| G5 UI | 最終 gate：ui-core `283 passed`、ui-snapshots `22 passed`；均 `0 failed / 0 skipped`。 | 最終 gate：ui-core `283 passed`、ui-snapshots `22 passed`；均 `0 failed / 0 skipped`。 |
| G6 Merge | roadmap 三行的 workspace prepare 由 gate scripts 的 caller 在每次 cycle 前執行；gate 本身執行 lint `-AllFiles`、build 與全部 test groups。除上述群組外，application `239`、infrastructure `11`、uncategorized `382` 通過，均 `0 failed / 0 skipped`。 | 同樣由 caller 在每次 cycle 前 prepare；gate 本身執行 lint `-AllFiles`、build 與全部 test groups。除上述群組外，application `244`、infrastructure `11`、uncategorized `382` 通過，均 `0 failed / 0 skipped`。 |

兩版使用相同群組清單；notch-core `209→214` 與 application `239→244` 的差額均只來自 A1 facts／PR-C 新增的五個測試。（`TODO.md:R13.102a-5`）

命令列表保留 G2／G3 分組與 G6 的 roadmap 三行；G4 列出最終證據的正向／反向 budget 呼叫。G2／G3 分組來源為 roadmap R13.001 與 `scripts/README.md`「常用命令」；G6 三行逐字引用 roadmap 第 12 節「Merge 前額外執行」。兩版實際執行的最終 refactor gate 命令如下：

```powershell
./scripts/tests/run-refactor-gate.ps1 -IncludeUiSnapshots -LintAllFiles -UseNoAppHost
```

G2：

```powershell
./scripts/tests/run-tests.ps1 -Group notch-core -UseNoAppHost
```

G3：

```powershell
./scripts/tests/run-tests.ps1 -Group notch-golden -UseNoAppHost
```

G4：

```powershell
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget -ReverseCExportOrder
```

G6（roadmap 原文）：

```powershell
./scripts/dev/prepare-ui-workspace.ps1
./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -IncludeUiSnapshots
```

## 5. golden 是否更新、由誰更新及簽核

PR-B `2e3cbd08`：golden／baseline／snapshot data 未更新，無更新者、無變更內容，不需 golden 更新簽核；output zero-diff，由第 4 節的最終 gate 與正向／反向 Runtime CLI baseline 提供證據。

PR-C `4d54b35d`：golden／baseline／snapshot data 亦未更新，無更新者、無變更內容，不需 golden 更新簽核；明確的 notch-golden 為 `6 passed / 0 skipped`，正向／反向 Runtime CLI baseline 再次通過。cold generator output 與每個 golden input 的 V21／V22 輸出維持不變；混合 allocation 的 warm export 差異由 R13.102a-5 在舊程式失敗、修正後通過的測試鎖定，該 firmware 變更的 owner 明確核准仍是 PR-C 的交付條件，不能以 golden 未變代替。（`TODO.md:R13.102a-5`）

上述程式／測試交付 revision 相對 `origin/1.3.x` 的 `git diff origin/1.3.x --name-only` 證據只列 source、test 與 `TODO.md`，沒有 golden、baseline 或 snapshot data 差異；唯一命中 `snapshots/` 的名稱是 static guard test `UiLayoutGuardTests.cs`。沒有人更新 golden data，沒有 golden 更新簽核需求。（`docs/guides/refactor-roadmap-1.3.x.md:3.1 Golden 更新規則`）

## 6. 剩餘風險、owner 例外及下一版第一個 slice

- **A1 fact 5 已裁定並由 PR-C 實作；firmware corner case 仍需 owner 明確核准。** owner 於 2026-10-03 裁定「診斷跟 generator 一致，Q7=0 不顯示」，R13.102a-5 共用 Q7-positive allocation 集合。單一 `10×10` regular 完全位於 `2570×10` CAD，raw ratio=`1/257`、Q7=`0`：三模型 allocation `1→0`、overlap `100→0`、overlapped regular area `100→0`、regular/debug count `1→0`、resolved target `1 筆 100%→空集合`；CurrentGain／ConservativeNoGain 的 ToRegular／Combined `100%→0%`，Disabled 仍為 `100%`；三模型 ToFull 仍為 `100% (disabled)`、Stage3 area 仍為 `25700`，generator 維持 `0 candidates／0 V22 rows`。但 CAD 同時含 Q7=0／Q7-positive allocation，且先檢視 CAD 再匯出時，warm resolved result 的 V21／V22 firmware rows 與 FW apply simulation 確實改變，warm 現在等於 cold；不能宣稱 firmware C 輸出一概不變。review 例為 CAD `(0,0)–(2570,10)`、1×2 grid `xEdges=[0,10,2570]`、`yEdges=[0,10]`、IC0，diff0 overlap=`100`／Q7=`0`、diff1 overlap=`25600`／Q7=`128`；diff1=`XWay`、`MatchedCadPadId`=CAD、`MatchScore=1`、CAD output diff=`1`。ConservativeNoGain、strict overlap=`0.001`、threshold=`0` 時，CombinePercent=`200→100`；V21 移除 destination diff0／ref diff1 的 `ADD Q7=128` term，V22 Release row 成為 no-op 而不再輸出，FW apply simulation 隨同 rows 改變。兩版 warm/cold 完整 row snapshot 測試在舊 builder 下失敗、修正後通過；此 synthetic case 未另跑 C formatter 或實際 UI 匯出。另 partial-overlap 例 CAD `(5,0)–(2575,10)`、regular `(0,0)–(10,10)`、overlap=`50`／Q7=`0`，strict=`0.001`、ToFull 開啟、boundary virtual-area cap 啟用且 ratio=`1`，Stage3Area=`25750→25700`、`IsToFullEnabled=true→false`；stage polygons、Notch canvas preview／`notch-stage` counts 亦會改變，R label 可能改變；此例 core test 鎖 area／flag，未另操作 canvas／R label 或查詢 `notch-stage`。PR-C 必須獨立交付並取得 owner 對 firmware corner case 的明確核准；既有診斷裁定不代表 firmware 變更已獲核准。（`TODO.md:R13.102a-3b` Fact 5、`R13.102a-5`） 更新（2026-10-04）：owner 已在 GitHub 核准 PR-C（公開 repo 的 pull request #3，head `9810edf9`），並在聊天明確核准此 firmware corner case（經 Commander 轉述）；PR-C 已合併。
- **A1 normal 同源證據仍須收口。** Fact 2／3 已由 R13.102a-4 去重，Facts 4／6 保留 ABI 投影；其餘必要 consumer 的證據仍依各 parent exit criteria 稽核，不能因 bounded selected sparse/full bridge 完成就宣稱 repository-wide 收斂。（`TODO.md:R13.102a-3b`、`R13.102a-4`、`R13.101c-2`、`R13.102a-2`；稽核 A1）
- **A2 的最終 revision 驗證證據已補入第 4／5 節。** PR-B／PR-C 的 full lint、refactor gate、UI snapshots 與正向／反向 Runtime CLI baseline 均通過；PR-C 的 warm firmware corner case 仍須明確核准，不以既有 golden inputs 零差異宣稱所有輸入 output zero-diff，也不由本文件勾選 parent 或決定 milestone exit。（稽核 A2、「1.3.1 最小收口清單與 Milestone handoff」；`TODO.md:R13.102a-5`） 更新（2026-10-04）：PR-C 的 firmware corner case 已取得 owner 核准，見上一條。
- **B1、B2 已由 owner 接受為例外（accepted exceptions，2026-10-03），不執行。** B1 豁免 Legacy request-specific table identity／cache 或 generator 併入 normal batch/task；B2 豁免 V21／Legacy 額外 projector／formatter 收斂、broader adapter 與新增 typed/legacy 等價性投資。V21／Legacy 保留既有行為與 zero-diff gates，normal V22 projector 不回退；例外不能連帶關閉 A1。後續完全移除 V21 的版本與範圍尚未訂定。（稽核 B1、B2、「最新 owner 答覆如何影響分類」；`TODO.md:R13.101c-2`、`R13.102a-2`、`R13.103`、`R13.103c-1`）
- **C 類不列入表格退出仍是提案，尚待 owner 確認，不是已接受豁免。** C1 為額外 repository-wide final-output cache；C2 為無 Inspector snapshot 的 PadInfo public callbacks／partial-input fallback；C3 為 exporter metadata target-cap `255` clamp；C4 為 V22 short-row payload `255` ceiling；C5 為 UI error formatter 的 `255` 解析／提示；C6 為 DevView 固定 `EMS OK` 展示樣本與 static guard 白名單。若 normal flow 確有同結果重算，仍歸 A1；三種 `255` 不因字面相同而合併 policy。（稽核 C1～C6、「1.3.1 最小收口清單與 Milestone handoff」；`TODO.md:R13.101c-2`、`R13.102a-1`、`R13.103c-1`、`R13.104a-14`） 更新（2026-10-04）：owner 已決定以 A1、A2 收口，C1–C6 不擋結案；此提案已被取代（經 Commander 轉述）。
- **Static guard 的範圍較 EMS-cap 規則廣。** `UiLayoutGuardTests.ViewsAndViewModels_DoNotIntroduceLiteralEmsCapOrRiskWording` 使用無條件的 `\b480\b` pattern；Views／ViewModels 中無關的 literal `480` 也會被標記。本里程碑新增整個 guard（`R13.104` 的第一個 commit），並在後兩個 commit 各縮減一條白名單例外；未收窄無條件的 `480` pattern。
- **Compensation diagnostics 的 debug-entry 順序改變。** allocation 改依 Ratio descending 排序，原 raw builder 的 row／column 順序不再保留；review 例 debug order 為 `[col0,col1]→[col1,col0]`。未證明 firmware tie-break 迴歸，也未新增此排序案例測試。（`TODO.md:R13.102a-5`）
- **PR-A 在另一分支、以獨立 PR 交付。** Disabled-target display 修正只改顯示數字；第 4 節的 PR-B／PR-C revision 證據不是 PR-A 的驗證紀錄，交付順序仍為 PR-A → PR-B → PR-C。
- **下一版第一個 slice：R13.201（1.3.2）。** 依現有順序，正式化 Pad overlap、DXF audit、Canvas hit-test 三個 bounded contexts；overlap evidence API 定案後再移除 `PadMatcher`／`PadMatchService` 未讀取的 `MatchingSettings` 相容參數。這是下一版既有首項，不代表本交接已授權跳過 1.3.1 的未決退出條件。（`TODO.md:R13.201`；`TODO.md:R13.101`～`R13.104`；稽核「1.3.1 最小收口清單與 Milestone handoff」）
