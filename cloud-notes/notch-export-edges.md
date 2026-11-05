# Notch C 表匯出:邊界案例測試與發現

分支:`cloud/notch-export-edges`(自 `origin/main` `5e48bec` 切出)。只動測試,未改任何 production 程式。
沒有開 PR、沒有碰 `main`。本次任務沒有對應的 GitHub issue 編號,commit 未帶 issue 連結,採用前請補上。

## 新增內容

| 檔案 | 內容 |
|---|---|
| `tests/FreeformHelper.Tests/Application/Notch/NotchEdgeCaseHarness.cs` | 共用 harness:列建構、匯出入口、gcc C99 編譯/執行、把編譯後的表傾印成數字、C# 模擬入口 |
| `tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterEdgeCaseTests.cs` | 64 個「契約」測試:目前行為即預期行為 |
| `tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterEdgeCaseCharacterizationTests.cs` | 27 個 characterization 測試:釘住「目前行為」,但該行為疑似是 bug(見下) |

做法:先用 gcc `-std=c99 -pedantic -Wall -Wextra -Werror`(搭配既有測試使用的 `notch.h` stub)編譯產出的 C 檔,
再用一支傾印程式讀回**編譯器實際存下的** `castNHC_TABLE_BY_IC` 內容,逐欄與輸入推得的預期值比對。
所以斷言的是表資料本身,不只是文字。另外對少數案例實際執行 `FUNC_NHC_DiffCompensationByIc`,並與 C# 模擬 (`NotchApplySimulationService`) 做差分比對。
沒有 gcc 時,gcc 相關測試以 `Assert.SkipUnless` 顯示為 skipped(既有測試是靜默 return)。

### 涵蓋範圍(對應任務清單)

- **零筆**:CSV 只有表頭;C 輸出為 `// Notch table is empty.`(兩種 profile);不支援版本的列;全部列都因無效 IC 被丟棄(V21、V22 皆產出可編譯的空表);Release 下全 no-op 的 V22 表。
- **一筆**:CSV 整列、V22、V21(Legacy 與 CadAllocation,後者一列展開成 SELF 與 LEG 兩個節點)的整行文字、傾印資料與韌體執行結果。
- **最大數量**:IC 索引 254(255 個 IC,嚴格 C99 可編譯、可由 UINT8 定址)、IC 索引 255(256 個 IC)、單一 IC 65535 個節點(UINT16 上限)、最大 diff index 65534。
- **負值與邊界值**:V22 combine × ratio 共 13 × 11 組合(含 `int.MinValue`/`int.MaxValue`)、flags 0..255 全部、NullValue 0/1/32768/65534/65535、V21 Legacy 的百分比/Q7/type 邊界、V21 CadAllocation 的 combine 與 Q7 邊界;CSV 的 `int.MinValue`/`int.MaxValue`、`null` CadPadId、`null` 與空白註解。
- **每個 enum 值**:`NotchAlgorithmVersion`(CSV 標籤與代碼)、`NotchExportProfile` × `NotchComputationMode` × `NotchCompensationModel` × 版本(24 組合,各自編譯為 C99 並檢查 metadata)、V21 `NHC_TYPE_NONE/ADD/SUB` 在兩個槽位的 3 × 3 組合與超出範圍的 type、未定義的 enum 值(99)。測試以 `Enum.GetValues` 展開,日後新增 enum 值會自動納入。
- **很長的註解**:10 萬字元註解在 CSV 與 C(V21、V22)皆完整保留且單行;空白/`\t` 註解輸出 `-`;含引號與逗號的 CSV 註解。

## 發現(疑似 bug,未修改)

嚴重度為我的判斷,依「對韌體資料的影響」排序。每項都有對應測試(括號內為測試名稱的關鍵字),測試釘住的是目前行為。

1. **註解含換行 → 註解外的文字變成原始碼**(`Comment_WithALineBreak_…`)。
   V21 與 V22 都把註解原樣寫進 `// …` 行。註解 `first\n#error x` 會讓 `#error x` 自成一行;換成別的內容就是任意程式碼注入韌體檔。
   期望:換行替換或轉義,讓註解保持單行。

2. **UINT16 欄位的 diff index 沒有範圍檢查;負值連編譯器都不會警告**(`V22DiffIndexOutsideUint16_…`、`V22NegativeDiffIndex_…`、`V21DiffIndexOutsideUint16_…`)。
   匯出只驗證 `NullValue`。≥ 65536 的值被 gcc 的 `-Woverflow` 擋下,但若工具鏈沒有這個警告,值會 mod 65536 回繞,節點改寫到別的 diff。
   **`-1` 更糟**:嚴格 C99 也不報警(gcc 對負常數轉無號沒有 `-Woverflow`),`-1` 靜默變成 65535,正好等於預設的 `NHC_DIFF_NONE`——目標 `-1` 會變成「沒有目標」,anchor `-1` 變成 diff 65535。
   期望:匯出前拒絕 [0, 65535] 以外的 anchor 與 target(與 `NullValue` 一致)。

3. **註解以反斜線或 `??/` 結尾 → 下一行表格被吞進註解**(`Comment_EndingWithABackslash_…`、`Comment_EndingWithATrigraphBackslash_…`)。
   只影響註解在行尾的 V21 格式(V22 的註解後面還有 `; NODE KEEP=…`)。MISRA C:2012 Rule 3.2、Rule 4.2。
   `-Wall` 下 gcc 以 `-Wcomment`/`-Wtrigraphs` 擋下;關掉警告時,被吞掉的節點在編譯後的陣列中變成全零(測試以傾印驗證),而 `USER_NHC_NODE_NUM_IC1` 仍宣告原本的數量。依生成函式的邏輯(`NHC_REGU_TO_FULL == 0` 時把 `NHC_IDX` 的 diff 設成 0),這個全零節點會把 diff 0 歸零;這一點我沒有另外執行驗證。

4. **V21 列的 `Values` 少於 9 個 → 產生一個作用在 diff 0 的節點**(`V21RowWithNoValues_…`)。
   缺的值當 0 補,節點目的地是 diff 0、`NHC_REGU_TO_FULL = 0`,韌體每個 frame 都會把 diff 0 設成 0(Legacy 與 CadAllocation 皆然)。
   V22 對短 payload 有定義好的 legacy 路徑,V21 沒有。期望:拒絕或略過。

5. **IC 數 ≥ 256 的表無法通過嚴格 C99**(`IcCountAboveUint8_…`)。
   `FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)` 裡的 `u8Ic < USER_NHC_IC_NUM` 在 IC 數 ≥ 256 時恆為真:`-Wextra` 的 `-Wtype-limits` 報錯(MISRA Rule 14.3),且 IC 索引 ≥ 256 的表完全無法被呼叫到。匯出不檢查 IC 數。表資料本身是對的(契約測試以 `-Wno-type-limits` 驗證資料與執行結果)。

6. **單一 IC 超過 65535 個節點 → 節點數靜默變 0**(`NodeCountAboveUint16_…`)。
   `u16NodeNum` 是 UINT16,`(65536u)` 寫入後 gcc 以 `-Woverflow` 擋下;沒有該警告時該 IC 整張表等於不存在。
   相關但未測:`USER_NHC_NODE_NUM_MAX` 也是 `INT16 s16DiffOffsetBk[…]`/`s16SourceBk[…]` 的陣列長度,65535 個節點時單次呼叫要在韌體堆疊上放約 128 KB,沒有任何檢查。

7. **V22 flags 以 `& 0xFF` 靜默截斷**(`V22FlagsOutsideAByte_…`)。256 → 無 flag,`-1` → 所有 bit 全開,`int.MinValue` → 無 flag。對照之下 `NullValue` 超出範圍是丟例外。

8. **CSV:`payload_width` 與欄位不一致、`ic_number` 回繞**(`ExportAsCsv_PayloadLongerThanNineValues_…`、`ExportAsCsv_IcIndexIntMax_…`)。
   payload 超過 9 個值時只輸出前 9 個,但 `payload_width` 仍是實際長度,第 10 個之後的值遺失;`IcIndex = int.MaxValue` 時 `ic_number`(`IcIndex + 1`)回繞成 `-2147483648`。實務上不太會發生,嚴重度低。

9. **註解原樣輸出,含 `//`、`/*`(MISRA Rule 3.1)與非 ASCII 字元**(`Comment_WithCommentMarkers_…`、`Comment_WithNonAsciiText_…`)。
   gcc 都能編;但韌體實際使用的工具鏈與原始碼編碼未知,CJK 或 emoji 的 pad 名稱進入 C 檔是否可行需要確認。

已確認**沒有問題**的項目:combine 超過 255、ratio 超過 ±100 時 exporter 會夾住,C# 模擬與編譯後的韌體給出相同的補償結果(`ExportAndSimulation_ClampedV22Input_…`);
V21 CadAllocation 的 combine/Q7/type 邊界與手算一致;NullValue 在 0..65535 邊界的 `NHC_DIFF_NONE` 寫法與傾印值一致;同一 NullValue 與目標撞號的問題已有 `NotchNullValueCollisionCharacterizationTests`(issue #64),本次未重複。

## 驗證

環境:Linux、gcc 13.3、.NET SDK 10.0.301(裝在 scratchpad)。`core-packages` 以 `python3 scripts/fetch_core_packages.py` 下載。

- `dotnet build tests/FreeformHelper.Tests`:0 error。新增警告只有 1 個,`NotchEdgeCaseHarness.cs` 的 `RS0030`(`Process.Start`),與既有 gcc 測試 `NotchTableExporterTests` 的同類警告相同,未壓制。
- 新增的 91 個測試全部通過(64 + 27)。
- `FullyQualifiedName~Notch` 與 `~Architecture`:926 個測試,920 通過、4 skipped、2 失敗。
  - 2 個失敗(`NotchDisplayProjectorTests.Build_FromPadInspectorSnapshot_…`、`PadInfoViewModelTests.CadPadInfo_UsesSharedNotchDisplayProjectionTexts`)是百分比格式 `94.10 %` 對 `94.10%`,屬 UI 顯示與此環境的 invariant culture,與本次變更無關;我沒有在乾淨的 `main` 上重跑確認。
  - 4 個 skipped 是私有 `example/` submodule 缺失(未取得,依規則也不應帶入)。
- 變異檢查:暫時把 `NotchV22FirmwareProjector` 的 combine 夾值從 255 改成 254,3 個新測試失敗;已還原,`git diff` 對 `src/` 為空。
- `dotnet format --verify-no-changes --severity warn`(參數取自 `lint.ps1`,但以測試專案為範圍、只含新增的 3 個檔案):無格式差異。

**沒有執行**:`scripts/tests/lint.ps1`、`prepare-ui-workspace.ps1`(此環境沒有 `pwsh`)、`repo-health.ps1 -Mode Verify`(約 25 分鐘,且需要 pwsh)、完整測試套件。新檔已手動轉成 CRLF。

## 採用前需要決定的事

1. **債務帳本**:`eng/code-health/test-debt.json` 有 `childProcessInTests` 規則(帳本只降不升)。`NotchEdgeCaseHarness.Run` 會新增 2 筆(`new Process`、`new ProcessStartInfo`)。
   我無法在這裡跑 Verify,也不能手動加帳本項目,所以 Verify 很可能會因此失敗。可行方向:(a) 把既有 `NotchTableExporterTests` 的 3 組 gcc 行程碼與這個 `Run` 合併成同一個共用 helper,淨增為 0 或更少(重新命名/搬移有債務的成員會改變 fingerprint,需在同一個變更內完成);(b) 在審查時明確接受這 2 筆。
2. **gcc 旗標**:`-Wno-type-limits` 只用在兩處契約斷言(讓 256 個 IC 的資料斷言與警告斷言分開);characterization 測試用 `-Wno-overflow`/`-Wno-comment` 來呈現「警告關掉之後」的靜默後果。這些是 gcc 命令列旗標,不是 C#/editorconfig 的壓制,但仍請審查時確認可接受。
3. **characterization 測試的預期值**:第 1–9 項若修正,對應測試的斷言會改變(修正的 PR 需說明),這是刻意的。
4. **`ROADMAP.md`**:依 `AGENTS.md`,新發現的項目應寫入 `ROADMAP.md` 的 fix 軌。為避免與同批其他分支在共用檔案衝突,這裡沒有修改,建議條目:第 1、2、3 項為一組「匯出前輸入驗證與註解淨化」;第 4 項「V21 短 payload 處理」;第 5、6 項「IC 數與節點數上限」;第 7、8、9 項為低優先。
5. **MISRA**:規則編號是依 gcc 警告與規則文字對照,**沒有**跑 MISRA 檢查工具(如 cppcheck premium、PC-lint);是否合規仍需用正式工具確認。

## 未涵蓋

- 沒有在 Windows 上跑(`Environment.NewLine` 為 CRLF 時,CSV 與註解行的處理理論上相同,但未實測)。
- 傾印程式與執行測試只用 stub 的 `INT16 as16Buf[65536]`;真實韌體緩衝區大小未知,最大 diff index 65534 是否在真機範圍內需確認。
- 沒有對 V21 CadAllocation 做大範圍參數掃描:這條路徑會重新計算節點,預期值需要重寫一份演算法,改以手算的邊界案例與既有的 Q7 邊界測試涵蓋。
