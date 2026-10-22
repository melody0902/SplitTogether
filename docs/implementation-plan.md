# 實作進度

- [x] 讀取原始規格、檢查桌面與環境，保留 specification.txt。
- [x] 安裝專案範圍 Node 24、npm 11、.NET SDK 10，鎖定相依版本。
- [x] Angular / API / PostgreSQL 模型、Migration、DTO、ProblemDetails、OpenAPI。
- [x] Google/Cookie/CSRF、群組、邀請、角色與歷史成員權限。
- [x] 精確金額、分攤、支出修改／軟刪除、版本與冪等。
- [x] 一致餘額快照、結算建議、人工還款與作廢。
- [x] 前端主要頁面、手機版 CSS、錯誤／空狀態、離開未儲存提示。
- [x] 核心單元／HTTP 安全／SQL 整合／E2E 測試原始碼。
- [x] 前後端建置、可執行單元與 HTTP 安全測試。
- [x] 本機 HTTPS proxy、CSRF 與匿名端點實際啟動驗證。
- [x] 獨立唯讀 preview 之桌面／手機登入頁視覺檢查。
- [x] README、OAuth、架構 ERD、規則、測試、啟動脚本、可選 seed。
- [x] 改用 Npgsql、PostgreSQL 原生型別／約束與交易 advisory lock。
- [x] 提供 Apple Silicon Colima / Docker 與 Compose 安裝、啟動、持久化與獨立測試資料庫。
- [ ] 使用真實 PostgreSQL 執行 5 項整合測試及 Migration；結果見 testing.md。
- [ ] 具 Google 憑證的人工驗收。
- [ ] 在允許啟動 Chromium 的本機環境執行 Playwright E2E。

## 實作決策

- 現有環境為 macOS arm64；使用 Colima 原生 ARM Linux VM 的 Docker 與 PostgreSQL 18。
- Docker CLI／Compose／Colima／Lima 放在 .tools；Colima VM 保存在 ~/.colima/splittogether。沒有改變全域 Node／Keychain trust。
- runtime、schema migration、測試資料庫使用獨立非 superuser 帳號；本機密碼隨機產生並只保存在 .env。
- 新 PostgreSQL baseline 已取代 SQL Server Migration；原始規格與舊 schema 備份保留，未執行外部舊資料搬移。
- 開發由 Angular HTTPS server 終結 TLS，再以 loopback HTTP 代理至 API；Production 保持 HTTPS。
- 本機 file watcher 受限制時使用 polling，避免啟動卡住。
- Guid Version 為 EF concurrency token；不必額外傳 rowversion base64。
- LedgerMemberHistory 保存所有歷史成員關聯，包括支出更正後不再存在的 split/payer。
- 額外採後端 CsrfFilter 適配純 Web API，不依賴 MVC Views antiforgery filter。
- 群組列表／還款歷史定位小型群組，未加入大量資料優化。

## 尚需外部設定

1. 首次執行 Docker 啟動腳本，建立本機 PostgreSQL 與必要帳號；其他主機可自行設定連線字串。
2. 使用者建立 Google OAuth client 並填 .env。
3. 使用者信任本機開發憑證，或自行使用受信任的開發憑證。

這些是尚未完成的整合驗證，不是已通過的驗收。
