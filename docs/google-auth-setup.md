# Google OAuth 設定與人工驗收

## Google Cloud

1. 在 Google Cloud Console 選取／建立你自己的專案。
2. 設定 Google Auth Platform 的 Branding、Audience、Data Access（介面名稱可能隨 Google 更新）。
3. 外部測試應用加入需要登入的 Google 測試使用者。只使用基本 profile/email 範圍；不要求 Gmail/Drive 存取。
4. 建立 OAuth Client ID，類型為 **Web application**。
5. Authorized redirect URIs 新增 **https://localhost:4200/signin-google**。本專案使用後端 redirect flow，不需要前端 Google JavaScript SDK。
6. 將 Client ID/Secret 填入根目錄 `.env`。不要貼到聊天、commit、前端 environment 或 log。
7. 信任 `.cert/localhost.crt` 後啟動前後端。登入取消／失败可回到登入頁重試。

## 流程

瀏覽器 `/api/auth/google` → ASP.NET Google handler → Google → `/signin-google`（middleware 驗證 state/correlation）→ 暫存 External Cookie → `/api/auth/complete` → 以 `(Google, provider user ID)` 查找／建立 User → App Cookie → `/groups` 或原邀請。

App Cookie：HttpOnly、Secure、SameSite=Lax、12 小時固定有效期，不滑動續期。External Cookie：5 分鐘。Google access/refresh token 不保存。前端不保存 JWT 或密鑰。

`GET /api/auth/csrf` 發出 HttpOnly antiforgery cookie 與可讀的 `XSRF-TOKEN`；Angular 對同源修改請求送出 `X-XSRF-TOKEN`。登入返回後重新取得，登出清除前端狀態並刷新整個頁面。

returnUrl 僅允許 `/groups` 與 `/invite/<64 位十六進位 token>`；其餘回 `/groups`。不以 email 自動合併身份。

## URLs

| 用途 | URL |
|---|---|
| 前端入口 | https://localhost:4200 |
| Google callback | https://localhost:4200/signin-google |
| 完成應用登入 | https://localhost:4200/api/auth/complete |
| 後端 loopback | http://127.0.0.1:5043 |

後端 loopback 不供瀏覽器直接登入。proxy 保留 Host 並傳送 scheme；Development/Testing 僅信任 loopback。正式同源部署需改用你的 HTTPS 網域與對應 callback。

## 常見錯誤

- `redirect_uri_mismatch`：檢查協定、埠、路徑與 Google 設定完全一致。
- Google 拒絕測試使用者：將該帳號加到 audience 測試名單。
- correlation/state 失敗：使用固定 `localhost` 而非混用 IP；確認 HTTPS、Cookie、proxy `/signin-google` 設定。
- 登入後 500：確認 PostgreSQL 連線、Migration、應用帳號權限與 Db readiness。
- 400 csrf_invalid：刷新頁面取得新的 token；不要直接呼叫未附 token 的修改 API。
- 憑證警告：由使用者自行信任本機開發憑證，不關閉瀏覽器安全驗證。
- 其他供應商帳號無法合併：第一版刻意只支援 Google；不可直接以同 email 綁定。

## 待執行人工驗收

- [ ] 第一個 Google 帳號登入後建立 User/ExternalLogin。
- [ ] 同帳號再次登入沿用同一 User ID，更新名稱／頭像。
- [ ] 取消授權有可讀錯誤，不留下應用登入狀態。
- [ ] 登入後重新整理仍為登入狀態。
- [ ] 登出後群組 API 回 401，前端快取清空。
- [ ] 從邀請連結登入後回到原邀請，不自動加入。
- [ ] 外部 returnUrl 被忽略。
- [ ] 修改 API 缺少 CSRF token 失敗。
- [ ] App Cookie 無法由 JavaScript 讀取，具有 Secure/HttpOnly。

目前沒有真實 Google 憑證，以上 OAuth 人工項目尚未驗證；自動測試身份不代表 Google 驗證通過。
