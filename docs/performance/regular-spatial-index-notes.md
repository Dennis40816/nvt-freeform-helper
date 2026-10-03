# Regular Spatial Index Notes

## 結論（先看）
- 目前 `PadMatcher` 並非暴力全配對；對於 **規則格點** 已使用「隱含空間索引」：
  - 先用 `XEdges/YEdges` 二分搜尋定位 CAD bbox 對應 row/col 範圍。
  - 只在候選 cell 範圍內做 polygon-rect overlap 計算。
- 對 `RegularGrid` 場景，這個策略通常比額外 R-tree 更直接，且維護成本更低。

## 現況實作
- 檔案：`src/FreeformHelper.Application/Services/PadMatcher.cs`
- 主要流程：
  1. `GetCandidateRange()` 用 bbox + 邊界陣列找 row/col 區間。
  2. 僅遍歷候選區間內的 regular pads。
  3. 先做 bbox 相交快篩，再算 `IntersectionAreaWithRect`。
  4. 低於 overlap floor（絕對值與相對值）直接忽略。

## 複雜度觀點
- 理論上每顆 CAD 不是 `O(totalRegular)`，而是 `O(logR + logC + candidateCells)`。
- 在規則網格中，`candidateCells` 近似 CAD bbox 覆蓋的格數，通常遠小於全域 regular 數量。

## 什麼情況才需要升級到 R-tree / Spatial Hash
- regular 不再是規則格點（例如大比例使用 irregular regular source）。
- 候選區間普遍過大（單 CAD 經常覆蓋大量 cell）導致 `candidateCells` 成本失控。
- 需要跨多種幾何集合共享同一套索引（不只 regular grid）。

## 建議下一步（低風險）
1. [x] 先加 telemetry（每次 Match 的平均候選 cell、p95 候選 cell、intersection 次數）。
   - `PadMatchResult.Telemetry`：`candidate/bounds/polygon` 次數 + `avg/p95`。
   - Step1 log：`PERF PADMATCH: ...`。
   - 報表腳本：`scripts/perf/extract-padmatch-telemetry.ps1`（由 `build/logs/app.log` 產生 `build/perf/padmatch-telemetry-latest.md`）。
2. [x] 以真實專案（如 36.35）量測瓶頸是否在 candidate 掃描。
   - 決策腳本：`scripts/perf/evaluate-padmatch-telemetry.ps1`（輸出 `build/perf/padmatch-decision-latest.md`）。
   - 預設門檻：`avgCandidate/CAD <= 120`、`p95Candidate/CAD <= 400`、`polygonIntersections <= 2,000,000`。
3. [x] 只有在 telemetry 顯示 candidate 成本過高時，再導入 R-tree/Spatial hash。
   - 目前決策：預設維持 `XEdges/YEdges + candidate-range`，避免引入額外索引維護成本。
   - 若決策腳本觸發門檻，才進入 R-tree/Spatial hash POC。
