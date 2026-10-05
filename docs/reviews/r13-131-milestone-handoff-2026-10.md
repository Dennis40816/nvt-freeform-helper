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
| R13.102a-3b（Facts 2～6 刻畫；PR-B） | Facts 2／3 證明 reader 重複推導；Fact 5 鎖定修正前 raw owner 與 Q7-positive reader 分歧；Facts 4／6 為保留的 ABI 投影。新增刻畫 `18/18` 通過，entry 與 parent 仍未勾選。（`TODO.md:R13.102a-3b`） |
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
- **A1 normal 同源證據仍須收口。** Fact 2／3 已由 R13.102a-4 去重，Facts 4／6 保留 ABI 投影；其餘必要 consumer 的證據仍依各 parent exit criteria 稽核，不能因 bounded selected sparse/full bridge 完成就宣稱 repository-wide 收斂。（`TODO.md:R13.102a-3b`、`R13.102a-4`、`R13.101c-2`、`R13.102a-2`；稽核 A1）
- **A2 的最終 revision 驗證證據已補入第 4／5 節。** PR-B/PR-C passed full lint, the refactor gate, UI snapshots, and forward/reverse Runtime CLI baselines at the recorded revisions. The owner approved PR-C's warm firmware corner case on 2026-10-04; existing golden-input zero diff does not prove zero diff for every input, and parent checkboxes and milestone exit remain the owner's decision.（稽核 A2、「1.3.1 最小收口清單與 Milestone handoff」；`TODO.md:R13.102a-5`） 更新（2026-10-04）：PR-C 的 firmware corner case 已取得 owner 核准，見上一條。
- **B1/B2 remain accepted exceptions (2026-10-03); the 2026-10-04 owner decision places V21/Legacy removal after 1.3.x, with the specific version and scope decided later and no implicit conversion beforehand.** B1 豁免 Legacy request-specific table identity／cache 或 generator 併入 normal batch/task；B2 豁免 V21／Legacy 額外 projector／formatter 收斂、broader adapter 與新增 typed/legacy 等價性投資。V21／Legacy 保留既有行為與 zero-diff gates，normal V22 projector 不回退；例外不能連帶關閉 A1。Historical timing note: 後續完全移除 V21 的版本與範圍尚未訂定。（稽核 B1、B2、「最新 owner 答覆如何影響分類」；`TODO.md:R13.101c-2`、`R13.102a-2`、`R13.103`、`R13.103c-1`）
- **Owner decision (2026-10-04): 1.3.1 closes with A1/A2; C1–C6 do not block exit, and B1/B2 remain accepted exceptions.** Historical proposal: C 類不列入表格退出仍是提案，尚待 owner 確認，不是已接受豁免。 C1 為額外 repository-wide final-output cache；C2 為無 Inspector snapshot 的 PadInfo public callbacks／partial-input fallback；C3 為 exporter metadata target-cap `255` clamp；C4 為 V22 short-row payload `255` ceiling；C5 為 UI error formatter 的 `255` 解析／提示；C6 為 DevView 固定 `EMS OK` 展示樣本與 static guard 白名單。若 normal flow 確有同結果重算，仍歸 A1；三種 `255` 不因字面相同而合併 policy。（稽核 C1～C6、「1.3.1 最小收口清單與 Milestone handoff」；`TODO.md:R13.101c-2`、`R13.102a-1`、`R13.103c-1`、`R13.104a-14`） 更新（2026-10-04）：owner 已決定以 A1、A2 收口，C1–C6 不擋結案；此提案已被取代（經 Commander 轉述）。
- **Static guard 的範圍較 EMS-cap 規則廣。** `UiLayoutGuardTests.ViewsAndViewModels_DoNotIntroduceLiteralEmsCapOrRiskWording` 使用無條件的 `\b480\b` pattern；Views／ViewModels 中無關的 literal `480` 也會被標記。本里程碑新增整個 guard（`R13.104` 的第一個 commit），並在後兩個 commit 各縮減一條白名單例外；未收窄無條件的 `480` pattern。
- **Compensation diagnostics 的 debug-entry 順序改變。** allocation 改依 Ratio descending 排序，原 raw builder 的 row／column 順序不再保留；review 例 debug order 為 `[col0,col1]→[col1,col0]`。未證明 firmware tie-break 迴歸，也未新增此排序案例測試。（`TODO.md:R13.102a-5`）
- **PR-A 在另一分支、以獨立 PR 交付。** Disabled-target display 修正只改顯示數字；第 4 節的 PR-B／PR-C revision 證據不是 PR-A 的驗證紀錄，交付順序仍為 PR-A → PR-B → PR-C。
- **Mask loading remains a product question.** A missing saved mask path leaves `enabled=true` with no mask, so simulation uses the full grid; a project-relative fallback is undecided. It is an unverified hypothesis that importing a mask CSV while already enabled returns early without refreshing the assignment.
- **Current next step (2026-10-05): the R13.202 characterization leaf is queued; R13.203 is no longer blocked by R13.201 but is not queued yet. Keep 1.3.3 behind the 1.3.2 exit; section 7 records the current sequence.** Historical next-slice plan: R13.201 (1.3.2). 依現有順序，正式化 Pad overlap、DXF audit、Canvas hit-test 三個 bounded contexts；overlap evidence API 定案後再移除 `PadMatcher`／`PadMatchService` 未讀取的 `MatchingSettings` 相容參數。這是下一版既有首項，不代表本交接已授權跳過 1.3.1 的未決退出條件。（`TODO.md:R13.201`；`TODO.md:R13.101`～`R13.104`；稽核「1.3.1 最小收口清單與 Milestone handoff」）

## 7. Handoff for 2026-10-06

**Programme state (2026-10-05 02:20, local time UTC+8).** Main development is in the public [Dennis40816/nvt-freeform-helper](https://github.com/Dennis40816/nvt-freeform-helper) repository, with `1.3.x` as default branch and trunk at `269a3528`. Its initial single-commit export came from private `FreeformHelper` `1.3.x` at `2c1c0c84`; public `main` remains at that export. Private `FreeformHelper` `1.3.x` is frozen at `2c1c0c84`, has no open PRs (two closed as superseded), and is not archived yet. Only `FreeformHelper-testdata` remains private for ongoing development through the `example/` submodule. The `codex-queue/nfh` queue uses the public clone; the owner's 2026-10-05 night-shift work is listed below. A done brief does not close a parent or milestone. 1.3.1 closes with A1/A2 evidence under the owner's exit decision, and 1.3.3 waits for the 1.3.2 exit.

**Public PR snapshot — 2026-10-05 02:20 (UTC+8).** #14 is the only open public PR and targets `1.3.x`. PR, approval, and CI status must be re-checked before acting. Merge commits for #1–#11 are recorded from local trunk history; later state follows the verified snapshot.

| PR | Recorded state / merge commit | Delivered scope or remaining action |
| --- | --- | --- |
| [#1](https://github.com/Dennis40816/nvt-freeform-helper/pull/1) | Merged earlier; `b6c2562`. | PR-A: Disabled-target display agrees with the generator. |
| [#2](https://github.com/Dennis40816/nvt-freeform-helper/pull/2) | Merged earlier; `91f2054`. | PR-B: R13.102 A1 deduplication and R13.104 safety-text centralization, with the recorded zero-diff scope. |
| [#3](https://github.com/Dennis40816/nvt-freeform-helper/pull/3) | Merged earlier; `f1c15c3`. | PR-C: Q7-positive diagnostics and warm export, including the firmware corner case approved on 2026-10-04 at head `9810edf9` and in chat; section 6 retains its scope and verification limits. |
| [#4](https://github.com/Dennis40816/nvt-freeform-helper/pull/4) | Merged earlier; `41e10d0`. | R13.131 milestone handoff. |
| [#5](https://github.com/Dennis40816/nvt-freeform-helper/pull/5) | Merged earlier; `75f14cb`. | S14.011 single-pass DXF opening. |
| [#6](https://github.com/Dennis40816/nvt-freeform-helper/pull/6) | Merged earlier; `26187c1`. | S15.018 shell log unsubscription. |
| [#7](https://github.com/Dennis40816/nvt-freeform-helper/pull/7) | Merged earlier; `44d1259`. | Console writer inventory; this does not integrate the S15.017 incremental-parser prototype. |
| [#8](https://github.com/Dennis40816/nvt-freeform-helper/pull/8) | Merged earlier; `070caa2`. | S15.020 terminal auto-follow test wait. |
| [#9](https://github.com/Dennis40816/nvt-freeform-helper/pull/9) | Merged earlier; `d8aad96`. | 2026-10-03 documentation batch. |
| [#10](https://github.com/Dennis40816/nvt-freeform-helper/pull/10) | Merged earlier; `be3a793`. | Public-repository transfer documentation. |
| [#11](https://github.com/Dennis40816/nvt-freeform-helper/pull/11) | Merged earlier; `6aaacf5`. | 2026-10-04 owner-decision record. |
| [#12](https://github.com/Dennis40816/nvt-freeform-helper/pull/12) | Merged 2026-10-05; `c119295b`. | Dependabot: weekly NuGet and GitHub Actions updates, limit 5. |
| [#13](https://github.com/Dennis40816/nvt-freeform-helper/pull/13) | Merged 2026-10-05; `cbd430af`. | Root `VERSION=1.3.0`, read by `Directory.Build.props`, with a structure check in `scripts/verify.ps1`. |
| [#14](https://github.com/Dennis40816/nvt-freeform-helper/pull/14) | Open; CI green (7/7). | `scripts/tests/run-tests.ps1` uses a repository-external test area: default `D:\FreeformHelper-TestArea`, `FREEFORMHELPER_TEST_AREA` override, seven-day retention, and manual cleanup, documented in `docs/guides/build-output-and-disk-space.md`. The owner must review it again after the documentation conflict resolution; it touches `scripts/`, so owner approval is required before merge. |
| [#15](https://github.com/Dennis40816/nvt-freeform-helper/pull/15) | Merged 2026-10-05; `53366ee1`. | R13.201 overlap evidence API without the unused `MatchingSettings` parameter. |
| [#16](https://github.com/Dennis40816/nvt-freeform-helper/pull/16) | Merged 2026-10-05; `b41ebe04`. | R13.204 single `RegularPad.AssignMapping` assignment. |
| [#17](https://github.com/Dennis40816/nvt-freeform-helper/pull/17) | Merged 2026-10-05; `c0c4e39b`. | R13.205 audit-phase characterization tests only; the full pipeline refactor remains pending. |
| [#18](https://github.com/Dennis40816/nvt-freeform-helper/pull/18) | Merged 2026-10-05; `269a3528`. | R13.206 one private coordinate transform. |
| [#19](https://github.com/Dennis40816/nvt-freeform-helper/pull/19) | Merged 2026-10-05; `e48fa2aa`. | Tests-only `ExampleProjectFixture` repair: every example-project test uses a temporary copy, including the golden baseline test that builds its path from manifest data. Historically, #14, #15, #16, #17 and #18 failed the same seven example-data tests: two core C export drift cases (V21 and V22) and five viewmodel regular-visibility-mask tests, because the runner could not load the developer machine's saved mask path. Repository CI checks out each pull request head, so each branch needed the fix merged into it; #19 resolved those failures for all five pull requests. |

Owner GitHub approvals are on record for #15, #16, and #18 at 2026-10-04 23:18, and #12, #13, and #14 at 2026-10-05 00:13 (UTC+8). The own diffs of #15, #16, and #18 at merge time were byte-identical to their approved diffs; only trunk was merged into those branches. #14 changed after approval: merging trunk required one documentation conflict resolution that kept the test-area section of the build-output guide and dropped the sentence saying implementation was pending. The owner should look at that current diff again before merge.

**Merge and next-step order.** The CI repair and the delivered 1.3.2 slices are already on trunk. The remaining merge action is owner review of the changed #14, followed by re-checking its current approval and required checks before integration. Continue the queued characterization work and choose the remaining 1.3.2 leaves; R13.203 is unblocked but not queued. Keep the 1.3.2 exit ahead of 1.3.3 implementation and the shared CI pilot.

Repository CI checks out the PR head, not the merge ref. A trunk fix reaches an open PR only when trunk is merged into its branch; closing and reopening the PR does not bring in that fix.

The resolved CI defect came from the developer-machine absolute mask path in `example/BOE36.35/project_3635.json`, under `uiSnapshot.import.regularSignalMaskSourcePath`. Failure to load the saved mask caused seven example-data failures, including 323/324, `NHC_TYPE_SUB` versus `ADD`, and grid 4992. #19 moved every example-project test onto a temporary copy with a local mask path through `ExampleProjectFixture`, including the manifest-derived golden baseline call site. It changed tests only, with no `src` or golden change. The signed golden manifest hashes the original project JSON, which must not be edited without re-signing. Public CI uses the `policy / structure` and `dotnet / build-test` lanes; it is green, including the data tests with `TESTDATA_DEPLOY_KEY`. The full local trunk run at `269a3528`, with example data, recorded **1119 passed, 0 failed, 0 skipped**; this is supplied verification evidence, not a test run performed by this documentation update.

**Owner decisions and dates.**

- **2026-10-04 — Transfer:** main development and new work move to the public repository; the queue uses the public clone and new work is public-only from this date. Private `1.3.x` stays frozen at `2c1c0c84`; the owner archives private `FreeformHelper` read-only after public CI, including the deploy key, is green and private PRs are closed.
- **2026-10-04 — 1.3.1:** close with A1/A2; C1–C6 do not block, and B1/B2 remain accepted exceptions (accepted on 2026-10-03). The R13.131 parent and milestone checkbox remain the owner's decision. PR-C's firmware corner case was approved in public PR #3 at `9810edf9` and in chat; its warm-output scope and verification limits remain in section 6.
- **2026-10-04 — S15.017:** incremental console links are not merged. Console system work comes later, first with a new model in FreeformHelper, then pushed to NVT Core as the shared console; other modules are abstracted into NVT Core over time.
- **2026-10-04 — VERSION, test area, and Dependabot:** keep the name and version `1.3.0`; use `D:\FreeformHelper-TestArea` by default, overridable with `FREEFORMHELPER_TEST_AREA`, with `TEMP`/`TMP`/`TMPDIR` set only for tests, seven-day retention, and manual cleanup. Dependabot follows the NVT Core template: weekly NuGet and GitHub Actions updates, limit 5. These S15.005a implementations are in #12–#14.
- **2026-10-04 — Agent and contributor documents:** changes to `AGENTS.md` or `CONTRIBUTING.md` require the owner's confirmation in chat before editing; no GitHub review is required for those changes.
- **2026-10-04 — PR language; 2026-10-05 — document language:** new PR titles and bodies default to English from 2026-10-04; already-open PRs are not rewritten, and conflicting `AGENTS.md`/`CONTRIBUTING.md` wording must be raised with the owner first. On 2026-10-05 the owner said: 「後續文件，除了 README 預設要有中文版本，其他都預設用英文，除非我指定新增中文版本」. Existing Chinese documents will be translated later in low-priority codex batches, preserving owner quotes in Chinese (an English translation may accompany them); only the English version is maintained afterwards. README gets English and Chinese versions, with file names decided later.
- **2026-10-04 — Old private issues:** move only publishable issues and rewrite their links after bot Issues read/write permission is active. Nothing has migrated; wait for the Commander session's permission notice. Old PRs are not migrated and their links are not rewritten.
- **2026-10-04 — R13.303:** implement simulation opacity fallback `0.9` in 1.3.3 and update snapshots for this approved visual change.
- **2026-10-04 — V21/Legacy removal:** removal is after 1.3.x; the specific version and scope are decided later, with no implicit conversion beforehand.
- **2026-10-04 — R13.305b:** owner 「禁止關掉最後一個版本」. Neither V21 nor V22 may be switched off when it is the last enabled version; implement the guard in 1.3.3 after the 1.3.2 exit.
- **2026-10-05 — R13.202:** keep the status quo: CadBest and hover retain their own rules, share only parts proven identical, and preserve what users see. The 3635 measurement found 4838 CADs, 582 touching at least two regulars, 6 selecting a different regular between the rules, and 0 exact ties. R13.203 is no longer blocked by R13.201 but is not queued yet.
- **2026-10-05 — Shared CI pilot:** owner selected 「改由 NFH 試點，1.3.2 結束後開始 (Recommended)」. NFH replaces NFU as the NVT Core shared CI pilot after 1.3.2 completion; the mask-path fix is complete, and pilot implementation waits for that exit. The Commander session has codex evaluating a CI path check to reject machine-local paths in test data for later introduction in NVT Core.
- **2026-10-04 22:07 — Ruleset:** the owner created `Protect 1.3.x and main`, requiring `policy / structure` and `dotnet / build-test`, with strict mode off and admin bypass.

**Waiting items and who unblocks them.**

- **#14:** the owner looks again at the current diff after the documentation conflict resolution; the earlier approval predates that change.
- **Issue migration:** wait for the Commander session's notice that the bot has Issues read/write permission before moving any old issue.
- **Private repository archive:** both prerequisites are satisfied: public CI including deploy-key-backed data tests is green, and no private PR is open. The owner archives the private repository at [FreeformHelper settings](https://github.com/Dennis40816/FreeformHelper/settings); the archive step is still pending.
- **Shared CI pilot:** NVT Core contacts the NFH session after the 1.3.2 exit. NFH Avalonia 12 preparation remains planned, with no work now.
- **CI path check:** the evaluation and prototype are queued or running; the prototype is not wired into CI and needs owner approval because it touches `scripts/`. Later NVT Core introduction remains pending.
- **Document translation and README:** translation batches for existing Chinese documents are queued or running at low priority under the language decision above; README still needs English and Chinese versions, with file names undecided.
- `CONTRIBUTING.md` still needs the owner-confirmed sync of its default-branch model and the chat-confirmation rule at line 37.

**Owner night shift — 2026-10-05, queued or running; no decision needed to continue this work.** The work list includes the CI path-guard prototype (a script, an exception config, and a required-mask load assertion; not wired into CI), tests-only characterization of a missing saved mask path and importing a mask while it is already enabled, console ring-tail test isolation (S15.002), the read-only 1.3.3 design inventory, pre-reviews of #12/#13/#14, and batches translating existing Chinese documents to English. Use the table above for current PR state; queued reviews do not imply that those PRs remain open. The prototype's owner-approval gate remains in force.

Done queue briefs in the dated snapshot:

- `r13-201-overlap-api`
- `r13-204-mapping-transition`
- `r13-205-audit-phase-characterization`
- `r13-206-coordinate-transform`
- `s15-005a-version-file`
- `s15-005a-dependabot`
- `s15-005a-test-area`
- `simplify-test-area-launch`
- `transfer-docs-public-repo`
- `record-owner-decisions-2026-10-04`
- `ci-example-mask-path`
- `ci-mask-baseline-loader`
- `docs-state-sync-2026-10-05`

Queue briefs queued or running on the night of 2026-10-05:

- `path-guard-prototype`
- `path-guard-fix-p2`
- `characterize-mask-missing-and-import`
- `mask-char-compile-fix`
- `r13-202-best-match-characterization`
- `r13202-char-fix`
- `s15-002-console-ring-tail-isolation`: the S15.002 branch was kept without a pull request because it touches `src` and the console system will be redone.
- `translate-core-notch-specs`
- `translate-guides-reference-misc`
- `translate-reference-notch-behavior`
- `translate-reviews-avalonia-ui`
- `translate-reviews-v21-removal`

**Remaining 1.3.2 leaves and 1.3.3 gates.** R13.202 retains the status quo or a reduced scope limited to proven-identical parts; a characterization leaf pinning both current CadBest and hover selection rules is queued. R13.203 is no longer blocked by R13.201 and is not queued yet. The full R13.205 pipeline remains pending beyond #17's characterization tests. Select remaining 1.3.2 leaves without reopening delivered slices; the 1.3.2 exit remains the owner's decision. R13.305b's last-version guard and R13.303's opacity `0.9` change with updated snapshots belong to 1.3.3 after that exit; the read-only design inventory does not authorize implementation. No parent, S15.005a, S15.009e, S15.017, or milestone exit is closed by this handoff.

**Open product questions — not decided.** A missing saved mask path leaves `enabled=true` with no mask, so simulation uses the full grid; a project-relative fallback would be a product change and has not been decided. It is an unverified hypothesis that importing a mask CSV while the mask is already enabled returns early without refreshing the assignment; the queued characterization tests do not settle a behavior change. PadMatcher sorting has no final ID tie-break; the zero exact ties in the 3635 measurement do not settle that question.
