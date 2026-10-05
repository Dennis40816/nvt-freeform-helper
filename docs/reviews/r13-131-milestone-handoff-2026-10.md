# 1.3.1 里程碑交接（2026-10）

本交接依 roadmap 的「Milestone handoff」六項欄位整理，補齊 PR-B／PR-C 最終 revision 的驗證證據；本文件不勾選 R13.101～R13.104 parent，1.3.1 里程碑是否退出仍由 owner 決定。（`TODO.md:R13.101`、`R13.102`、`R13.103`、`R13.104`；`docs/guides/refactor-roadmap-1.3.x.md:Milestone handoff`）

Current source note (2026-10-05): the exit-audit document now exists in this worktree; section 7 records the public programme snapshot. Historical source note: 來源固定如下：實作狀態取本工作樹的 `TODO.md:R13.101`～`R13.104`，最終驗證分別對應 PR-B tip `2e3cbd08` 與 PR-C tip `4d54b35d`；指定退出稽核檔未存在於本工作樹，唯讀引用本地 commit `1c195dc0439963c2b1ee15ad9e643edcb15a0590` 的 `docs/reviews/r13-131-exit-audit-2026-10.md`。下文「稽核 A1／A2／B1／B2／C1～C6」均指該版本「去重後的剩餘義務分類」；稽核內的 `E:` 證據仍屬其記載的實作 commit，並非第 4 節的最終 revision 重驗結果。（`TODO.md:R13.102a-3b`、`R13.102a-4`、`R13.102a-5`；稽核「來源與限制」）

Current approval (2026-10-04): the owner approved the PR-C firmware corner case in public PR #3 at `9810edf9` and in chat. Historical delivery description: 交付依序為三個 PR，疊加於 `origin/1.3.x = 2c1c0c84`：PR-A 在另一分支，以 `NotchDisplayProjector` 讓 Disabled-target 顯示與 generator 一致（owner 決定「make the screen agree with the generator」），只改顯示數字；PR-B 為本樹截至 `2e3cbd08` 的 R13.104 safety-text centralization、R13.102 resolved readers（含 PadInfo `resolvedDisplayInput`）、A1 facts 刻畫與 duplicate removal，output zero-diff；PR-C 為 `4d54b35d` 的 R13.102a-5，診斷與 warm export 共用 Q7-positive allocation 集合，包含第 6 節揭露的 firmware corner case，須 owner 明確核准。文中的 commit SHA 是私有主幹上的識別；公開 repo 以補丁重建這些 commit，SHA 不同、內容相同，對照時以 commit 標題為準。

Update (2026-10-04): the owner approved the PR-C firmware corner case in public PR #3 at head `9810edf9` and in chat, as relayed by the Commander session. The owner also decided that 1.3.1 closes with A1/A2 and C1–C6 do not block exit. These decisions supersede the earlier pending firmware-approval and C-class proposal statements; they do not settle other open questions or change parent checkboxes. The dated public PR state is in section 7.

## 1. 已完成的 R13.*

| 已完成項目 | 已記錄的成果與依據 |
| --- | --- |
| R13.101a、R13.101b | per-IC candidate pool 採 owned snapshot，消除重複複製／掃描；allocation memo 改以 immutable polygon 與 exact geometry comparison 識別同 ID 的幾何變更。（`TODO.md:R13.101a`、`R13.101b`） |
| R13.101c-1、R13.101c-2a | generated-table key 移除 `ExportProfile`；live guard/cap-only 設定變更收斂為 final-projection-only invalidation。（`TODO.md:R13.101c-1`、`R13.101c-2a`） |
| R13.101d-1、R13.101d-2、R13.101e | 凍結 normal CadAllocation computation context，統一 UI／generator 的 Q7-positive pool admission；完整 compensation context 經唯一 `Compute(context)` 路徑執行。（`TODO.md:R13.101d-1`、`R13.101d-2`、`R13.101e`） |
| R13.102a-1（含 PadInfo follow-up）、R13.102b-1、R13.102b-2 | preview／deferred Inspector 共用 revisioned resolved snapshot，Detail 與 override-aware Runtime Query 投影同一 owner；PadInfo follow-up 擷取單一 `resolvedDisplayInput`。（`TODO.md:R13.102a-1`、`R13.102b-1`、`R13.102b-2`；稽核「最新證據已排除的舊剩餘項」） |
| R13.102a-2a、R13.102a-2b-1 | normal export/generator 重用 output-request-neutral candidate batch；generation completion 依 epoch／final-projection revision／Simulation source identity 拒收過期結果。（`TODO.md:R13.102a-2a`、`R13.102a-2b-1`） |
| R13.102a-2b-2a、R13.102a-2b-2b | 並行 Export／Simulation 共用 full batch-resolution task；目前單一 selected sparse result 接入 batch session，移除 VM 平行 compensation dictionary，收口 2b-2 的 bounded bridge。（`TODO.md:R13.102a-2b-2a`、`R13.102a-2b-2b`） |
| R13.102a-3b (Facts 2–6; PR-B) | Characterization evidence for anchor IC filtering, source area, Q7-positive admission, and retained ABI projections; see [TODO.md R13.102a-3b/4/5](../../TODO.md) for delivery and owner closeout. |
| R13.102a-4（PR-B） | normal reader 的 Fact 2／3 改讀 owner 的 ToFull count、唯一 anchor target area／既有 Stage D fallback，刪除重複 IC 篩選、Max 與非負 clamp。（`TODO.md:R13.102a-4`；`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate`） |
| R13.102a-5（PR-C） | 移除 raw allocation builder，compensation diagnostics／warm export 與 generator 共用既有 Q7-positive allocation 集合；單一 Q7=0 診斷改變，混合 allocation 檢視後匯出的 V21／V22 rows 與 FW apply simulation 亦改變，warm 現在等於 cold。Cold generator and golden-input outputs are unchanged. The owner approved the firmware corner case on 2026-10-04 in public PR #3 at `9810edf9` and in chat.（`TODO.md:R13.102a-5`） |
| R13.103a、R13.103b-1、R13.103c-1、R13.103c-2 | 鎖定 final ABI characterization；threshold admission 移至 final row projection；V22 export／simulation 共用 Firmware projector；NullValue 共用 `0..65535` validation owner。（`TODO.md:R13.103a`、`R13.103b-1`、`R13.103c-1`、`R13.103c-2`） |
| R13.103d-1、R13.103d-2 | Legacy request 在 progress callback 前凍結；以固定 V21／V22 dispatch 取代 strategy registry，保留 request-specific table cache。（`TODO.md:R13.103d-1`、`R13.103d-2`） |
| R13.104a-1～R13.104a-3 | EMS cap predicate 共用；combined-overflow、effective target count／line／role 改讀共同 target eligibility／membership projection。（`TODO.md:R13.104a-1`、`R13.104a-2`、`R13.104a-3`） |
| R13.104a-4～R13.104a-8 | Simulation／replay availability、severity、physical audit warning 及 no-cell cap 文字共用 projector，export review 讀相同 audit 結果。（`TODO.md:R13.104a-4`、`R13.104a-5`、`R13.104a-6`、`R13.104a-7`、`R13.104a-8`） |
| R13.104a-9～R13.104a-14 | safety guidance、target-cap help、初始 cap、guard summary／checklist、export blocked 文字、workspace cap／high-risk label 收斂至共用 projector。（`TODO.md:R13.104a-9`、`R13.104a-10`、`R13.104a-11`、`R13.104a-12`、`R13.104a-13`、`R13.104a-14`） |

A1FACTS 的 Facts 2～6 已完成刻畫，`R13.102a-3b` 仍未勾選；owner 已於 2026-10-03 裁定「診斷跟 generator 一致，Q7=0 不顯示」，由 PR-C 的 R13.102a-5 實作。The diagnostics decision was separate from the mixed-allocation firmware approval, which the owner granted on 2026-10-04 in public PR #3 at `9810edf9` and in chat.（`TODO.md:R13.102a-3b`、`R13.102a-4`、`R13.102a-5`）

## 2. single-entry／single-result 變化

- PR-A 的 Disabled-target display 由 `NotchDisplayProjector` 投影與 generator 一致的數字；此 PR 在另一分支，依交付順序先於本樹的 PR-B／PR-C。

- normal generator／UI 先組成完整 compensation context，再經唯一 `Compute(context)`；舊 public 多參數入口只補齊 context 並薄委派，明確空 allocations 為 authoritative empty。（`TODO.md:R13.101d-1`、`R13.101e`）
- 同一 selection/revision 的 preview／Inspector／Detail／Runtime 讀 revisioned `NotchV22ResolvedResult`；PadInfo 的 normal snapshot 路徑投影單一 `resolvedDisplayInput`。Runtime `multi-owner` 的 override 只建立一份相應 result，metadata 使用輕量 Inspector snapshot。（`TODO.md:R13.102a-1`、`R13.102b-1`、`R13.102b-2`、`R13.102a-2b-2b`）
- normal Export／Simulation 的 full consumers 共用一個 resolution task，各自依入口凍結的 final request 投影；full batch 至多重用目前單一 selected sparse result，不代表所有 candidate／consumer 已 repository-wide 共用同一 result。（`TODO.md:R13.102a-2a`、`R13.102a-2b-2a`、`R13.102a-2b-2b`、`R13.102a-2`）
- PR-B 以 R13.102a-3b 刻畫 Facts 2～6，R13.102a-4 讓 normal generator Fact 2／3 直接讀 resolved allocation／compensation owner，output zero-diff；Facts 4／6 的 ABI clamp、chunk、ordering 投影刻意保留。（`TODO.md:R13.102a-3b`、`R13.102a-4`；`src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate`）
- PR-C 的 R13.102a-5 移除 `BuildCompensationAllocations` raw builder；`CreateContext` 與相容 `Compute` 共用 `NotchAllocationService.BuildAllocations` 的既有 `q7 > 0` predicate。diagnostics／resolved target 與 generator 使用同一 allocation 集合；真實匯出重用檢視後的 warm resolved result，混合 Q7=0／Q7-positive allocation 的 warm rows 因此改為 cold 值，FW apply simulation 亦消費這組 rows。This output change received explicit owner approval on 2026-10-04; it remains outside PR-B's zero-diff scope.（`TODO.md:R13.102a-5`）
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

PR-C `4d54b35d`：golden／baseline／snapshot data 亦未更新，無更新者、無變更內容，不需 golden 更新簽核；明確的 notch-golden 為 `6 passed / 0 skipped`，正向／反向 Runtime CLI baseline 再次通過。Cold generator output and V21/V22 output for each golden input remain unchanged; tests that failed before R13.102a-5 and passed afterwards capture the mixed-allocation warm-export difference. The owner approved that firmware change on 2026-10-04 in public PR #3 at `9810edf9` and in chat; unchanged golden data is not a substitute for that approval.（`TODO.md:R13.102a-5`）

上述程式／測試交付 revision 相對 `origin/1.3.x` 的 `git diff origin/1.3.x --name-only` 證據只列 source、test 與 `TODO.md`，沒有 golden、baseline 或 snapshot data 差異；唯一命中 `snapshots/` 的名稱是 static guard test `UiLayoutGuardTests.cs`。沒有人更新 golden data，沒有 golden 更新簽核需求。（`docs/guides/refactor-roadmap-1.3.x.md:3.1 Golden 更新規則`）

## 6. 剩餘風險、owner 例外及下一版第一個 slice

- **A1 Fact 5 is implemented in PR-C; the owner approved the firmware corner case on 2026-10-04 in public PR #3 at `9810edf9` and in chat.** owner 於 2026-10-03 裁定「診斷跟 generator 一致，Q7=0 不顯示」，R13.102a-5 共用 Q7-positive allocation 集合。單一 `10×10` regular 完全位於 `2570×10` CAD，raw ratio=`1/257`、Q7=`0`：三模型 allocation `1→0`、overlap `100→0`、overlapped regular area `100→0`、regular/debug count `1→0`、resolved target `1 筆 100%→空集合`；CurrentGain／ConservativeNoGain 的 ToRegular／Combined `100%→0%`，Disabled 仍為 `100%`；三模型 ToFull 仍為 `100% (disabled)`、Stage3 area 仍為 `25700`，generator 維持 `0 candidates／0 V22 rows`。但 CAD 同時含 Q7=0／Q7-positive allocation，且先檢視 CAD 再匯出時，warm resolved result 的 V21／V22 firmware rows 與 FW apply simulation 確實改變，warm 現在等於 cold；不能宣稱 firmware C 輸出一概不變。review 例為 CAD `(0,0)–(2570,10)`、1×2 grid `xEdges=[0,10,2570]`、`yEdges=[0,10]`、IC0，diff0 overlap=`100`／Q7=`0`、diff1 overlap=`25600`／Q7=`128`；diff1=`XWay`、`MatchedCadPadId`=CAD、`MatchScore=1`、CAD output diff=`1`。ConservativeNoGain、strict overlap=`0.001`、threshold=`0` 時，CombinePercent=`200→100`；V21 移除 destination diff0／ref diff1 的 `ADD Q7=128` term，V22 Release row 成為 no-op 而不再輸出，FW apply simulation 隨同 rows 改變。兩版 warm/cold 完整 row snapshot 測試在舊 builder 下失敗、修正後通過；此 synthetic case 未另跑 C formatter 或實際 UI 匯出。另 partial-overlap 例 CAD `(5,0)–(2575,10)`、regular `(0,0)–(10,10)`、overlap=`50`／Q7=`0`，strict=`0.001`、ToFull 開啟、boundary virtual-area cap 啟用且 ratio=`1`，Stage3Area=`25750→25700`、`IsToFullEnabled=true→false`；stage polygons、Notch canvas preview／`notch-stage` counts 亦會改變，R label 可能改變；此例 core test 鎖 area／flag，未另操作 canvas／R label 或查詢 `notch-stage`。Historical approval requirement: PR-C 必須獨立交付並取得 owner 對 firmware corner case 的明確核准；既有診斷裁定不代表 firmware 變更已獲核准。（`TODO.md:R13.102a-3b` Fact 5、`R13.102a-5`） Update (2026-10-04): the owner approved this firmware corner case in public PR #3 at head `9810edf9` and in chat, as relayed by the Commander session.
- **A1 normal single-owner evidence.** Fact 2/3 deduplication, the Fact 5 Q7-positive correction, and retained Fact 4/6 ABI projections are recorded in [TODO.md R13.102a-3b/4/5](../../TODO.md). The owner selected A1/A2 as the closeout scope; use [TODO.md R13.101/R13.102](../../TODO.md) for status. The bounded sparse/full bridge does not claim repository-wide convergence.
- **A2 的最終 revision 驗證證據已補入第 4／5 節。** PR-B/PR-C passed full lint, the refactor gate, UI snapshots, and forward/reverse Runtime CLI baselines at the recorded revisions. The owner approved PR-C's warm firmware corner case on 2026-10-04; existing golden-input zero diff does not prove zero diff for every input, and parent checkboxes and milestone exit remain the owner's decision.（稽核 A2、「1.3.1 最小收口清單與 Milestone handoff」；`TODO.md:R13.102a-5`） 更新（2026-10-04）：PR-C 的 firmware corner case 已取得 owner 核准，見上一條。
- **B1/B2 remain accepted exceptions (2026-10-03); the 2026-10-04 owner decision places V21/Legacy removal after 1.3.x, with the specific version and scope decided later and no implicit conversion beforehand.** B1 豁免 Legacy request-specific table identity／cache 或 generator 併入 normal batch/task；B2 豁免 V21／Legacy 額外 projector／formatter 收斂、broader adapter 與新增 typed/legacy 等價性投資。V21／Legacy 保留既有行為與 zero-diff gates，normal V22 projector 不回退；例外不能連帶關閉 A1。Historical timing note: 後續完全移除 V21 的版本與範圍尚未訂定。（稽核 B1、B2、「最新 owner 答覆如何影響分類」；`TODO.md:R13.101c-2`、`R13.102a-2`、`R13.103`、`R13.103c-1`）
- **Owner decision (2026-10-04): 1.3.1 closes with A1/A2; C1–C6 do not block exit, and B1/B2 remain accepted exceptions.** Historical proposal: C 類不列入表格退出仍是提案，尚待 owner 確認，不是已接受豁免。 C1 為額外 repository-wide final-output cache；C2 為無 Inspector snapshot 的 PadInfo public callbacks／partial-input fallback；C3 為 exporter metadata target-cap `255` clamp；C4 為 V22 short-row payload `255` ceiling；C5 為 UI error formatter 的 `255` 解析／提示；C6 為 DevView 固定 `EMS OK` 展示樣本與 static guard 白名單。若 normal flow 確有同結果重算，仍歸 A1；三種 `255` 不因字面相同而合併 policy。（稽核 C1～C6、「1.3.1 最小收口清單與 Milestone handoff」；`TODO.md:R13.101c-2`、`R13.102a-1`、`R13.103c-1`、`R13.104a-14`） 更新（2026-10-04）：owner 已決定以 A1、A2 收口，C1–C6 不擋結案；此提案已被取代（經 Commander 轉述）。
- **Static guard 的範圍較 EMS-cap 規則廣。** `UiLayoutGuardTests.ViewsAndViewModels_DoNotIntroduceLiteralEmsCapOrRiskWording` 使用無條件的 `\b480\b` pattern；Views／ViewModels 中無關的 literal `480` 也會被標記。本里程碑新增整個 guard（`R13.104` 的第一個 commit），並在後兩個 commit 各縮減一條白名單例外；未收窄無條件的 `480` pattern。
- **Compensation diagnostics 的 debug-entry 順序改變。** allocation 改依 Ratio descending 排序，原 raw builder 的 row／column 順序不再保留；review 例 debug order 為 `[col0,col1]→[col1,col0]`。未證明 firmware tie-break 迴歸，也未新增此排序案例測試。（`TODO.md:R13.102a-5`）
- **PR-A 在另一分支、以獨立 PR 交付。** Disabled-target display 修正只改顯示數字；第 4 節的 PR-B／PR-C revision 證據不是 PR-A 的驗證紀錄，交付順序仍為 PR-A → PR-B → PR-C。
- **Mask product questions.** The owner must decide how a missing saved mask path should behave and how importing a mask while enabled should affect assignment refresh. Use [PR #22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) for characterization evidence and [TODO.md](../../TODO.md) for the decision status; characterization does not authorize a behavior change.
- **1.3.2 sequence.** Consult [TODO.md R13.201–R13.206](../../TODO.md) for delivery, review, and remaining work. Keep 1.3.3 behind the owner's 1.3.2 exit; section 7 provides IDs and links.

## 7. Handoff for 2026-10-06

Last verified: 2026-10-05 19:44 (+08:00), local full test run with the example data on the tree of trunk 1.3.x fd078295 (PR #35 merged at 19:49): 1230 passed, 0 failed, 0 skipped.

This line records the integrating session's local full run on the tree of the merges listed below. Re-check the table, current PR head, approvals, and required checks before acting.

**Status source and repository context.** [TODO.md](../../TODO.md) is the single status table until Issues replace it after the issue migration. This handoff references IDs and purposes; consult the corresponding TODO.md rows for execution status. The integrator verifies merge results and writes the table back once per batch. Main development uses the public [Dennis40816/nvt-freeform-helper](https://github.com/Dennis40816/nvt-freeform-helper) repository. The default branch is `1.3.x` (trunk); `main` currently equals the initial import commit and will hold released versions only. Work uses `feature/<version>/<topic>` branches targeting `1.3.x`. The original public import came from private `FreeformHelper` `1.3.x` at `2c1c0c84`; private archive and issue-migration actions are tracked by S15.009e.

**Pull request references — IDs, links, and purposes.**

| PR | TODO.md reference | Purpose |
| --- | --- | --- |
| [#1](https://github.com/Dennis40816/nvt-freeform-helper/pull/1) | [R13.102](../../TODO.md) | PR-A: align Disabled-target display with the generator. |
| [#2](https://github.com/Dennis40816/nvt-freeform-helper/pull/2) | [R13.102a-3b/4; R13.104](../../TODO.md) | PR-B: characterize and deduplicate normal readers; centralize safety text. |
| [#3](https://github.com/Dennis40816/nvt-freeform-helper/pull/3) | [R13.102a-5](../../TODO.md) | PR-C: use Q7-positive allocations in diagnostics and warm export; see section 6 for the firmware scope. |
| [#4](https://github.com/Dennis40816/nvt-freeform-helper/pull/4) | [R13.101–R13.104](../../TODO.md) | R13.131 milestone handoff. |
| [#5](https://github.com/Dennis40816/nvt-freeform-helper/pull/5) | [S14.011](../../TODO.md) | Single-pass DXF opening. |
| [#6](https://github.com/Dennis40816/nvt-freeform-helper/pull/6) | [S15.018](../../TODO.md) | Shell log unsubscription. |
| [#7](https://github.com/Dennis40816/nvt-freeform-helper/pull/7) | [S15.017](../../TODO.md) | Console writer inventory. |
| [#8](https://github.com/Dennis40816/nvt-freeform-helper/pull/8) | [S15.020](../../TODO.md) | Terminal auto-follow test waits. |
| [#9](https://github.com/Dennis40816/nvt-freeform-helper/pull/9) | [S15.005b](../../TODO.md) | 2026-10-03 documentation batch. |
| [#10](https://github.com/Dennis40816/nvt-freeform-helper/pull/10) | [S15.009e](../../TODO.md) | Public-repository transfer documentation. |
| [#11](https://github.com/Dennis40816/nvt-freeform-helper/pull/11) | [Owner decisions](../../TODO.md) | 2026-10-04 owner-decision record. |
| [#12](https://github.com/Dennis40816/nvt-freeform-helper/pull/12) | [S15.005a](../../TODO.md) | Dependabot: weekly NuGet and GitHub Actions updates, limit 5. |
| [#13](https://github.com/Dennis40816/nvt-freeform-helper/pull/13) | [S15.005a](../../TODO.md) | VERSION 1.3.0 and its structure check. |
| [#14](https://github.com/Dennis40816/nvt-freeform-helper/pull/14) | [S15.005a; S15.006 external test area](../../TODO.md) | Repository-external test area with seven-day retention and manual cleanup. |
| [#15](https://github.com/Dennis40816/nvt-freeform-helper/pull/15) | [R13.201](../../TODO.md) | Overlap evidence API without the unused MatchingSettings parameter. |
| [#16](https://github.com/Dennis40816/nvt-freeform-helper/pull/16) | [R13.204](../../TODO.md) | RegularPad.AssignMapping for matched-pair assignment. |
| [#17](https://github.com/Dennis40816/nvt-freeform-helper/pull/17) | [R13.205](../../TODO.md) | Audit-phase characterization tests. |
| [#18](https://github.com/Dennis40816/nvt-freeform-helper/pull/18) | [R13.206](../../TODO.md) | One private coordinate transform. |
| [#19](https://github.com/Dennis40816/nvt-freeform-helper/pull/19) | [S15.005a; R13.002](../../TODO.md) | ExampleProjectFixture temporary project copies with a local saved-mask path. |
| [#20](https://github.com/Dennis40816/nvt-freeform-helper/pull/20) | [S15.005a](../../TODO.md) | Dependabot actions/checkout 7.0.1. |
| [#21](https://github.com/Dennis40816/nvt-freeform-helper/pull/21) | [S15.005c](../../TODO.md) | Report-only scripts/tests/path-guard.ps1 prototype. |
| [#22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) | [Mask product questions](../../TODO.md) | Characterize a missing saved mask path and importing a mask while enabled. |
| [#23](https://github.com/Dennis40816/nvt-freeform-helper/pull/23) | [R13.202](../../TODO.md) | Characterize CadBest and hover best-match rules. |
| [#24](https://github.com/Dennis40816/nvt-freeform-helper/pull/24) | [Owner decisions](../../TODO.md) | Documentation state synchronization. |
| [#25](https://github.com/Dennis40816/nvt-freeform-helper/pull/25) | [R13.202](../../TODO.md) | Characterize allocation-anchor and freeform-classification selection. |
| [#28](https://github.com/Dennis40816/nvt-freeform-helper/pull/28) | [R13.203](../../TODO.md) | Typed CadPadId/RegularPadId for the DXF manual override chain. |
| [#29](https://github.com/Dennis40816/nvt-freeform-helper/pull/29) | [Owner decisions; S15.009e](../../TODO.md) | Status refresh, 2026-10-05 decisions, README in two languages, AGENTS.md/CONTRIBUTING.md corrections. |
| [#30](https://github.com/Dennis40816/nvt-freeform-helper/pull/30) | [Owner decisions](../../TODO.md) | First translation batch: 35 documents to English. |
| [#31](https://github.com/Dennis40816/nvt-freeform-helper/pull/31) | [R13.205](../../TODO.md) | Explicit seed and segment stages of the DXF regular mask audit. |
| [#32](https://github.com/Dennis40816/nvt-freeform-helper/pull/32) | [R13.201](../../TODO.md) | Contract guards for the three matching bounded contexts. |
| [#33](https://github.com/Dennis40816/nvt-freeform-helper/pull/33) | [Owner decisions](../../TODO.md) | Status-table write-back of the 2026-10-05 merges. |
| [#34](https://github.com/Dennis40816/nvt-freeform-helper/pull/34) | [R13.205](../../TODO.md) | Explicit local-repair and passive-compensation inputs and results. |
| [#35](https://github.com/Dennis40816/nvt-freeform-helper/pull/35) | [R13.203](../../TODO.md) | Typed IcIndex/DiffIndex behind the mapping adapters. |

Repository CI checks out the PR head, not the merge ref. A trunk fix reaches a PR when trunk is merged into its branch; closing and reopening the PR does not bring in the fix. PR #19 documents the tests-only CI fixture repair. The signed golden manifest hashes the original project JSON; editing that data requires re-signing.

**Owner decisions and dates.**

- **2026-10-04 — Transfer:** main development and new work move to the public repository; the queue uses the public clone and new work is public-only from this date. Private `1.3.x` stays frozen at `2c1c0c84`; the owner archives private FreeformHelper read-only after public CI, including the deploy key, is green and private PRs are closed.
- **2026-10-04 — 1.3.1:** close with A1/A2; C1–C6 do not block, and B1/B2 remain accepted exceptions from 2026-10-03. Parent and milestone checkboxes remain the owner's decision. The owner approved PR-C's firmware corner case in public PR #3 at `9810edf9` and in chat; section 6 preserves its warm-output scope and verification limits.
- **2026-10-04 — S15.017:** incremental console links are not merged. Console-system work comes later, first in FreeformHelper, then in NVT Core as the shared console; other modules are abstracted into NVT Core over time. The 2026-10-05 model decision returns model selection to the default.
- **2026-10-04 — VERSION, test area, and Dependabot:** keep the name and version 1.3.0; use `D:\FreeformHelper-TestArea` by default, overridable with FREEFORMHELPER_TEST_AREA, with TEMP/TMP/TMPDIR set only for tests, seven-day retention, and manual cleanup. Dependabot follows the NVT Core template: weekly NuGet and GitHub Actions updates, limit 5. Reference: S15.005a.
- **2026-10-04 — Agent and contributor documents:** the owner said 「讓我在聊天中確認即可」 ("Confirming in the chat is enough"). Changes to AGENTS.md or CONTRIBUTING.md require prior owner confirmation in chat; a GitHub review is not required for this confirmation. On 2026-10-05 the owner selected 「同意，改好開 PR 給我看 (Recommended)」 ("Agreed; make the corrections and open a PR for me to inspect (Recommended)") for exactly the current branch model and this confirmation flow.
- **2026-10-04 — PR language; 2026-10-05 — document language:** new PR titles and bodies default to English; already-open PRs are not rewritten, and conflicting AGENTS.md/CONTRIBUTING.md wording must first be raised with the owner. The owner said 「後續文件，除了 README 預設要有中文版本，其他都預設用英文，除非我指定新增中文版本」 ("Future documents default to English, except that README should have a Chinese version by default, unless I request an additional Chinese version"). Existing Chinese documents are translated in low-priority Codex batches, preserving Chinese owner quotes with English translations; only English is maintained afterwards, except the bilingual README. The owner selected 「可以 (Recommended)」 ("Allowed (Recommended)") for [README.md](../../README.md) in English and [README.zh-TW.md](../../README.zh-TW.md) in Traditional Chinese.
- **2026-10-04 — Old private issues; 2026-10-05 decision:** move only publishable issues. On 2026-10-05 the owner chose 「同意，只搬 #1 改寫版 (Recommended)」 ("Agreed, move only the rewritten issue 1"): the old parent spec was rewritten and created as public [#27](https://github.com/Dennis40816/nvt-freeform-helper/issues/27) (old 1 -> new 27); the other 42 open issues are delivered child tickets and are not migrated. Old PRs are not migrated and their links are not rewritten. The owner also kept the 1.3.1 parent unticked.
- **2026-10-04 — R13.303:** implement simulation opacity fallback 0.9 in 1.3.3 and update snapshots for this visual change.
- **2026-10-04 — V21/Legacy removal:** removal is outside 1.3.x; the specific version and scope are decided later, with no implicit conversion beforehand. Avalonia 12 is also outside 1.3.x.
- **2026-10-04 — R13.305b:** the owner said 「禁止關掉最後一個版本」 ("Do not allow the last version to be disabled"). Neither V21 nor V22 may be switched off when it is the last enabled version; implement the guard in 1.3.3 after the 1.3.2 exit.
- **2026-10-05 — R13.202:** preserve distinct CadBest and hover rules and share only parts proven identical. For exactly equal scores, the owner selected 「維持現狀 (Recommended)」 ("Keep the status quo (Recommended)"): add no ID tie-break to PadMatcher sorting or R13.202 selection. Any later change is a separate behavior-change item; the ID tie-break question is resolved.
- **2026-10-05 — Approval after trunk integration:** the owner selected 「可以，但手動解衝突要重批 (Recommended)」 ("Allowed, but manual conflict resolution requires approval again (Recommended)"). After owner approval, a new commit that only merges trunk cleanly needs no new approval if tests pass; any manual conflict resolution requires owner approval again.
- **2026-10-05 — Integration batches:** the owner selected 「不設上限」 ("No limit") for batches awaiting review.
- **2026-10-05 — Status table:** the owner said 「可以先用各自的，但路徑未來要統一」 ("Each project may use its own table for now, but the path will be unified later"). TODO.md is NFH's single status table until Issues replace it after migration. Roadmap, handoff, and WIP files reference IDs without restating execution status; the integrator verifies merge results and writes the table back once per batch.
- **2026-10-05 — Shared CI pilot:** the owner selected 「改由 NFH 試點，1.3.2 結束後開始 (Recommended)」 ("Use NFH as the pilot, starting after 1.3.2 completion (Recommended)"). NFH replaces NFU as NVT Core's shared CI pilot after the 1.3.2 exit. CI path-check evaluation is tracked under S15.005c for later NVT Core introduction.
- **2026-10-05 — Models and usage:** models return to the default; Codex usage remains economical after its reset.
- **2026-10-04 22:07 — Ruleset:** the owner created Protect 1.3.x and main, requiring policy / structure and dotnet / build-test, with strict mode off and admin bypass.

**Owner actions — consult TODO.md for status before acting.**

- Archive the private repository read-only: [FreeformHelper settings](https://github.com/Dennis40816/FreeformHelper/settings), with prerequisites tracked by S15.009e.
- Coordinate the shared CI pilot after the owner's 1.3.2 exit: [S15.005c/S15.005d](../../TODO.md), then NVT Core contacts the NFH session.
- Decide the exception and calibration policy of the merged report-only path check and when it starts to block; later NVT Core introduction: [#21](https://github.com/Dennis40816/nvt-freeform-helper/pull/21), [S15.005c](../../TODO.md).
- Decide how a missing saved mask path should behave and how importing a mask while enabled should affect assignment refresh: [#22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22), [TODO.md Mask product questions](../../TODO.md).

**Queue references.** These names identify work, not execution status; consult TODO.md.

- S15.002: `s15-002-console-ring-tail-isolation`.
- R13.202: best-match, allocation-anchor, and freeform-classification characterization in PR #23/#25.
- R13.203: DXF manual override chain in PR #28 and the mapping chain in PR #35 (`r13203-mapping-ids`).
- R13.205: audit-phase characterization in PR #17, seed and segment stages in PR #31, and the local-repair/passive-compensation stage in PR #34 (`r13205-repair-passive`).
- S15.005c: path-check evaluation; the report-only prototype is PR #21.

**Release gates.** Consult [TODO.md R13.201–R13.206](../../TODO.md) for 1.3.2 work. R13.202 shares only parts proven identical; R13.203 covers its typed-ID boundary; R13.205 covers the production pipeline. The owner's 1.3.2 exit precedes 1.3.3 implementation and the shared CI pilot. R13.303 opacity 0.9 with updated snapshots and R13.305b's last-version guard retain their 1.3.3 gates. No parent, milestone, or exit checkbox is closed by this handoff.

**Open product questions.** The missing saved mask path and importing a mask while enabled require owner behavior decisions; consult [PR #22](https://github.com/Dennis40816/nvt-freeform-helper/pull/22) for characterization and [TODO.md](../../TODO.md) for status. PadMatcher sorting and R13.202 have no open ID tie-break question: exact ties preserve the current behavior under the 2026-10-05 decision.
