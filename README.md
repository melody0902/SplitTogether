# SplitTogether｜團體分帳

Angular 21 + ASP.NET Core 10 + **PostgreSQL 18**。繁體中文、Google 登入、群組邀請、精確分攤、結算與人工還款。資料保存在 PostgreSQL；沒有 SQLite 或記憶體資料庫替代模式。

## Mac 第一次啟動

```bash
cd ~/Desktop/SplitTogether
# 已安裝 Docker Desktop 並啟動者，可略過下面兩行。
bash scripts/setup-docker-mac.sh
bash scripts/start-docker-mac.sh
# 產生本機密碼並啟動 ARM PostgreSQL 容器。
bash scripts/start-db.sh
# 使用獨立的 schema 管理帳號建立資料表。
bash scripts/migrate.sh
```

再開兩個終端，皆切到 `~/Desktop/SplitTogether`：

```bash
# 終端 A
bash scripts/start-backend.sh
```

```bash
# 終端 B
bash scripts/start-frontend.sh
```

瀏覽器開啟 **https://localhost:4200**。首次需由你自行信任 `.cert/localhost.crt` 開發憑證。

Docker 工具採 Colima 0.10.3 / Lima 2.2.0 / Docker CLI 29.8.2 / Compose 5.5.1 的官方 ARM 執行檔，放在 `.tools/container`，不依賴系統 Homebrew 版本。Colima 的 `splittogether` 專用 profile 使用 Apple Virtualization、2 CPU / 2 GiB RAM / 20 GiB 資料磁碟，實際執行 Linux Docker Engine，不啟用 Kubernetes。停止 VM：`source scripts/env.sh; source scripts/docker-env.sh; colima stop --profile splittogether`（請在 bash 執行）。

如果已使用 Docker Desktop，直接執行 `start-db.sh` 即可；不要同時啟動兩套引擎承載相同開發資料。專案自行安裝 CLI 後，脚本預設使用專案的 Docker 設定。若要使用 Docker Desktop 可設定 `DOCKER_HOST=unix://$HOME/.docker/run/docker.sock`。

## 本機資料庫與帳號

`init-local-env.sh` 自動建立 `.env` 並生成四組隨機密碼（權限 600）；重跑保留既有檔案。`start-db.sh` 會自動呼叫它。不要提交 `.env`。

| 用途 | 資料庫 | 帳號 | 權限 |
|---|---|---|---|
| API | splittogether | split_app | 資料讀寫，不能建立／刪除資料表 |
| Migration | splittogether | split_migrator | 此資料庫 schema 管理 |
| 整合測試 | splittogether_test_local | split_test | 僅測試資料庫管理，不能連主資料庫 |
| 容器初始化 | postgres | postgres | 管理員，不供 API 使用 |

- Host：`127.0.0.1`，Port：`5432`，可在 `.env` 的 `POSTGRES_PORT` 改成例如 `55432`。
- Compose 使用固定版本及 digest 的官方 `postgres:18.6-bookworm`，同時支援 ARM64 與 x86-64，無強制 amd64 模擬。
- 資料保存在 `splittogether_postgres-data` Docker volume，PostgreSQL 18 掛載 `/var/lib/postgresql`。
- `bash scripts/stop-db.sh` 停止容器並保留資料；重啟使用 `start-db.sh`。
- 初始化帳號腳本只在空 volume 首次執行。已有 volume 後改 `.env` 密碼不會自動改資料庫密碼；勿為改密碼刪除 volume。
- `start-sql.sh` 保留為相容入口，現在呼叫 PostgreSQL `start-db.sh`。

GUI 資料庫工具可用上述 Host／DB／Username，加上 `.env` 中對應密碼連線。開發 Docker 僅綁 loopback，連線設定為 `SSL Mode=Disable`；外部 PostgreSQL 使用受信任 TLS 與 `SSL Mode=VerifyFull`。

`scripts/database-env.sh` 從 `.env` 密碼建立 Npgsql 連線字串，以下變數可用於外部實例：

```text
ConnectionStrings__Default=Host=YOUR_HOST;Port=5432;Database=splittogether;Username=split_app;Password=YOUR_PASSWORD;SSL Mode=VerifyFull
SPLIT_MIGRATION_CONNECTION=Host=YOUR_HOST;Port=5432;Database=splittogether;Username=split_migrator;Password=YOUR_PASSWORD;SSL Mode=VerifyFull
SPLIT_TEST_CONNECTION=Host=YOUR_HOST;Port=5432;Database=splittogether_test_local;Username=split_test;Password=YOUR_PASSWORD;SSL Mode=VerifyFull
```

正式應用啟動不會自動 migration，也不重建資料庫。`migrate.sh` 使用 `SPLIT_MIGRATION_CONNECTION`，不要求將應用帳號換成管理員。

## Google 登入

Google Cloud 建立 Web application OAuth client，在 `.env` 填入：

```dotenv
Authentication__Google__ClientId='YOUR_CLIENT_ID.apps.googleusercontent.com'
Authentication__Google__ClientSecret='YOUR_SECRET'
```

Authorized redirect URI：`https://localhost:4200/signin-google`。完整步驟見 [Google 設定](docs/google-auth-setup.md)。憑證留空時介面會提示尚未設定，不啟用假登入。

開發由 Angular HTTPS proxy 把 `/api/**` 與 `/signin-google` 送到 `http://127.0.0.1:5043`；後端只在 Development/Testing 信任 loopback proxy 的 scheme。Cookie 仍保持 Secure / HttpOnly。不要直接使用 HTTP 後端進行 Google 登入。

## 驗證

```bash
bash scripts/build.sh             # 前後端建置
bash scripts/test.sh              # 不需 DB 的單元 / HTTP 安全 / 前端測試
bash scripts/test-integration.sh  # 載入本機測試 DB，執行全部後端測試
```

整合測試若 PostgreSQL 尚未啟動會失敗，不會把錯誤當成通過；未設定測試連線時一般單元測試會明確跳過 SQL 測試。測試帳號只能管理 `splittogether_test_local`，測試程式不刪庫。執行結果與 E2E 步驟見 [testing.md](docs/testing.md)。

存活檢查：`http://127.0.0.1:5043/api/health`。資料庫檢查：`https://localhost:4200/api/health/ready`。開發 OpenAPI：`http://127.0.0.1:5043/openapi/v1.json`。

## 工具、範例資料與發佈

- .NET SDK 10.0.401、EF Core 10.0.12、Npgsql EF Provider 10.0.3、Node.js 24.21.0；版本鎖定於 global.json、csproj、lockfiles。
- 換電腦還原前後端相依套件：`bash scripts/bootstrap.sh`。
- 三位真實 Google 使用者登入後，`SPLIT_SEED_DEMO=1 bash scripts/start-backend.sh` 建立冪等示範群組；僅 Development/Testing 可用，不建立假帳號。
- `bash scripts/publish-local.sh` 建立 `artifacts/app`，由 ASP.NET Core wwwroot 託管 Angular。部署前設定 Production、HTTPS、AllowedHosts、Google／PostgreSQL 憑證與持久化 Data Protection keys；未自動部署雲端。

## 由舊版 SQL Server 轉換

這是 PostgreSQL 的新 schema baseline，不是跨資料庫自動資料搬移。未修改或刪除任何外部 SQL Server。舊 migrations／DDL 備份於 `.tools/legacy-sqlserver/schema-backup.tar.gz`；若另有舊資料，需另外匯出匯入並核對餘額，勿把舊 `__EFMigrationsHistory` 複製到 PostgreSQL。

原始規格保留於 `docs/specification.txt` 作歷史參考；本 README 與目前程式碼採 PostgreSQL。架構與交易說明見 [architecture.md](docs/architecture.md)。

官方參考：[Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html)、[PostgreSQL transaction advisory locks](https://www.postgresql.org/docs/18/functions-admin.html#FUNCTIONS-ADVISORY-LOCKS)、[Colima](https://colima.run/docs/getting-started/)。
