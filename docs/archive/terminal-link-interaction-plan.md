# Terminal Link Interaction Plan
最後更新：2026-02-20

## 目標
- 讓 Terminal link 互動穩定且可驗證，接近 VSCode 體感：
  - 平常藍字（無底線）
  - hover 顯示底線 + 手型
  - click 直接開啟（URL / 檔案 / 路徑）

## 現況
- 已支援 URL/檔案/路徑 link parse 與 click 開啟。
- 已支援 hover 手型與 hover-only 底線（colorizer 控制）。
- 已提供 runtime 命令：
  - `freeformhelper.exe query terminal-links --tail 300 --limit 300`
  - 可直接檢查目前 console 被解析出的 link spans 與 target。

## 風險點（為何可能看起來不一致）
- AvaloniaEdit 的 `DocumentColorizingTransformer` 屬於文字重繪管線，hover 更新時可能受重繪時序影響。
- link hit-test 與 link underline 同時依賴 TextView 行內定位，若滑鼠移動頻率高可能出現短暫不同步。

## 可測試方案（先驗證，再決策）
1. Parser 正確性（無 UI）
- 用 `query terminal-links` 驗證：
  - `lineNumber/startOffset/length/target/isUrl` 是否符合預期
  - 不同類型（URL、絕對路徑、相對路徑、含標點尾碼）是否被正確 trim/解析

2. Open 行為正確性（半自動）
- 在 Terminal 點擊已知 link，檢查 log：
  - `Console link open requested`
  - `Console link resolved`
  - 成功/失敗原因

3. Hover 視覺一致性（人工）
- 重點看「手型出現時是否總有底線」：
  - 高頻移動
  - 橫向捲動後
  - 搜尋高亮同時開啟時

## 若仍不穩定的 fallback（低優先）
- 方案 A：在 TextView 上增加獨立 overlay underline renderer（不依賴 colorizer 的文字 decoration）。
- 方案 B：改為 Tokenized Run 呈現（可控度高，但重構成本較大）。
- 方案 C：保留 hand + 顏色，不強依賴底線（作為短期 UX 降級）。

## 驗收標準
- `query terminal-links` 能穩定輸出目前可點 link 清單。
- click 開啟成功率可重現（URL/檔案/路徑）。
- hover 命中時，底線顯示不再偶發缺失（或明確採用 fallback 並文件化）。

