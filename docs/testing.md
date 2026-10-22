# 測試與驗收

## 驗證紀錄（PostgreSQL 轉換：2026-10-01）

| 項目 | 結果 |
|---|---|
| ASP.NET Core build | 通過 |
| TestHost build | 通過 |
| Angular production build | 通過；初始 bundle 約 364 kB |
| 後端 xUnit 核心與 HTTP 安全 | 19 通過 |
| PostgreSQL 整合測試 | 5 項；實際資料庫驗證結果待本次執行更新 |
| Angular Vitest | 4 通過 |
| EF Migration 與目前 model 比對 | 一致，無待加入變更 |
| HTTP liveness / auth config / 未登入 me | 200 / 200 / 401 |
| HTTPS 開發 proxy CSRF 發放 | 200，具有 Secure cookies |
| 登入頁桌面／390px 手機版 | 內建瀏覽器以獨立唯讀靜態 preview 檢查，版面正常 |
| Playwright smoke | 嘗試執行，但沙箱禁止 Chromium Mach bootstrap，未能啟動瀏覽器 |
| 三人完整帳務 E2E | 已撰寫，未執行；需要 PostgreSQL TestHost 與可啟動的瀏覽器 |
| 真實 Google OAuth | 未驗證；缺少 Client ID/Secret |

以上不將畫面檢查、假身份登入或純計算測試當成真實 SQL/Google 整合成功。

HTTP／HTTPS proxy／畫面檢查為 2026-09-25 至 09-26 的既有驗證；本次 10-01 已重新執行建置、單元測試、Model 比對、Compose 設定與 shell 語法檢查。

## 一般測試

```bash
bash scripts/build.sh
bash scripts/test.sh
```

後端涵蓋整數分攤、餘數、金額限制、付款人不參與分攤、三人指定案例、部分／超額／作廢還款、更正／刪除支出、200 組隨機帳務守恆與結算、無憑證登入設定、API 401、CSRF 缺漏／錯誤／正確、Production 沒有測試登入路由。

前端涵蓋精確字串金額、拒絕非法輸入、餘數排序、應用導覽 shell。

## PostgreSQL 整合測試

Docker 初始化會準備獨立資料庫 `splittogether_test_local`。名稱必須以 `splittogether_test_` 開頭且有後綴，測試帳號具 schema migration 及資料寫入權限。測試不使用一般應用程式資料庫。

```bash
bash scripts/start-db.sh
bash scripts/test-integration.sh
```

若已設定但連線失敗，應視為測試失敗，不會靜默 skip。沒有設定才由 SqlFact 明確跳過。測試套用 migration，不 Drop/EnsureDeleted 資料庫，每次建立自己的使用者／群組。不要用正式資料庫或含正式資料的副本。

涵蓋：空資料庫 Migration、唯一約束與 FK、交易 rollback、跨群組讀取、Member 不能邀請或修改別人的支出、版本衝突、成員歷史不可移除、平行加入只建立一次、平行支出與重複還款冪等、相同 request id 不同 payload、移除與入帳競態，以及完整指定帳務案例。額外驗證 PostgreSQL bigint／date／UTC 時間往返、負數 CHECK 約束及平行首次登入只建立一個身份。

## Playwright

在一般本機終端（不受 Chromium Mach port sandbox 限制的環境）：

```bash
bash
source scripts/env.sh
export PLAYWRIGHT_BROWSERS_PATH="$PWD/.tools/browsers"
cd frontend
node node_modules/@playwright/test/cli.js install chromium
node node_modules/@playwright/test/cli.js test e2e/smoke.spec.ts
```

需先啟動一般後端與 HTTPS 前端。Playwright 的 ignoreHTTPSErrors 僅限本機開發自簽憑證測試，不修改應用程式或系統信任。

完整帳務 E2E：停止一般後端，使用相同 5043 port 的 tests/TestHost：

```bash
# 終端 A，根目錄；會自動讀取 .env 的測試帳號
bash scripts/start-test-host.sh
# 終端 B
bash scripts/start-frontend.sh
# 終端 C，根目錄
bash
source scripts/env.sh
source scripts/database-env.sh
export PLAYWRIGHT_BROWSERS_PATH="$PWD/.tools/browsers"
cd frontend
node node_modules/@playwright/test/cli.js test
```

TestHost 在啟動時強制專用測試 DB 與 Testing environment，才載入只存在於 tests 組件的登入 middleware。三人 E2E 在 UI 建群組、透過邀請加入、新增 A 的支出，再以真實 API 建立 B 支出與還款，最後檢查 UI 與餘額。沒有 mock 帳務回應。

## 人工功能驗收

1. Google A/B/C 分別登入，A 建群組、產生邀請；B/C 必須確認加入。
2. A 代墊 900，A/B/C 平分；B 代墊 300，只分 B/C。
3. 確认 A +600、B -150、C -450，總消費 1,200。
4. B→A 150，C→A 450，三人淨餘額 0。
5. 作廢 B 還款後 A +150、B -150、C 0，總消費仍 1,200。
6. 100 元三人分攤結果 33.34/33.33/33.33；自訂合計錯誤不可存。
7. 非成員打開群組不能讀；Member 不能編輯其他人建立的支出。
8. 兩個視窗修改同筆支出，第二個過期 Version 回 409，不覆蓋第一筆。
9. 還款後改支出，提示會改變結算；還款紀錄保留。
10. 歷史成員不可移除，即使其支出已軟刪除／還款已作廢。
11. 邀請撤銷／過期不可加入，已加入重試不重複增加。
12. 重啟 API 與 PostgreSQL 容器，資料依然存在。
13. 桌面、390px 寬度檢查支出、自訂分攤、結算表格水平捲動、錯誤／空狀態。

Google 另見 `google-auth-setup.md` 的人工驗收清單。
