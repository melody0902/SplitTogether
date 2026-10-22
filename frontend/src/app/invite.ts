import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Api, message } from './api';
import { UI } from './ui';
@Component({
  imports: UI,
  template: `<mat-card class="narrow"
    ><mat-card-content
      ><p class="eyebrow">YOU'RE INVITED</p>
      <h1>一起加入分帳</h1>
      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (name()) {
        <p>
          你受邀加入「<strong>{{ name() }}</strong
          >」
        </p>
        <p class="muted">加入後即可查看群組帳務、記錄代墊。</p>
        @if (api.user()) {
          <button mat-flat-button [disabled]="busy()" (click)="accept()">確認加入</button>
        } @else {
          <a mat-flat-button [href]="loginUrl">使用 Google 登入後加入</a>
        }
      }
    </mat-card-content></mat-card
  >`,
})
export class Invite {
  api = inject(Api);
  route = inject(ActivatedRoute);
  router = inject(Router);
  token = this.route.snapshot.paramMap.get('token')!;
  loginUrl = '/api/auth/google?returnUrl=' + encodeURIComponent('/invite/' + this.token);
  name = signal('');
  error = signal('');
  loading = signal(true);
  busy = signal(false);
  constructor() {
    void this.load();
  }
  async load() {
    try {
      const i = await this.api.get<{ groupName: string }>('/invitations/' + this.token);
      this.name.set(i.groupName);
      await this.api.init();
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.loading.set(false);
    }
  }
  async accept() {
    this.busy.set(true);
    try {
      const r = await this.api.post<{ groupId: string }>(
        '/invitations/' + this.token + '/accept',
        {},
      );
      await this.router.navigate(['/groups', r.groupId]);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
