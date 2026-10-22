import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Api, message } from './api';
import { UI } from './ui';
@Component({
  imports: UI,
  template: `<section class="login-layout">
    <div>
      <p class="eyebrow">SHARED MOMENTS, SIMPLE MATH</p>
      <h1>美好時光一起分享，<br />帳目也能輕鬆分清。</h1>
      <p class="lead">從一頓聚餐到一趟旅行，記下每筆代墊，<br />讓每個人都清楚自己該付多少。</p>
      <div class="feature-row"><span>精確分攤</span><span>群組結算</span><span>還款追蹤</span></div>
    </div>
    <mat-card class="login-card"
      ><mat-card-content
        ><div class="brand-icon large">S</div>
        <h2>開始一起記帳</h2>
        <p class="muted">使用 Google 帳號登入，即可建立你的第一個群組。</p>
        @if (error()) {
          <p role="alert" class="error">{{ error() }}</p>
        }
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <a mat-flat-button [disabled]="!enabled()" [href]="loginUrl">使用 Google 登入</a>
        <p class="small muted">只使用基本個人資料，不讀取郵件或雲端檔案。</p></mat-card-content
      ></mat-card
    >
  </section>`,
})
export class Login {
  api = inject(Api);
  route = inject(ActivatedRoute);
  router = inject(Router);
  error = signal('');
  loading = signal(true);
  enabled = signal(false);
  loginUrl =
    '/api/auth/google?returnUrl=' +
    encodeURIComponent(this.route.snapshot.queryParamMap.get('returnUrl') ?? '/groups');
  constructor() {
    void this.load();
  }
  async load() {
    const code = this.route.snapshot.queryParamMap.get('error');
    if (code)
      this.error.set(
        code === 'not_configured'
          ? 'Google 登入尚未設定，請依專案文件填入 OAuth 憑證。'
          : code === 'connection'
            ? '後端連線失敗，請確認服務是否啟動。'
            : 'Google 登入未完成，請重新嘗試。',
      );
    try {
      const config = await this.api.get<{ googleEnabled: boolean }>('/auth/config');
      this.enabled.set(config.googleEnabled);
      if (!config.googleEnabled)
        this.error.set('Google 登入尚未設定。請先依 docs/google-auth-setup.md 填入憑證。');
      await this.api.init();
      if (this.api.user()) await this.router.navigateByUrl('/groups');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.loading.set(false);
    }
  }
}
