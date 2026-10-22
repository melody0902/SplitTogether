import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Api, message } from './api';
import { Group } from './models';
import { money } from './money';
import { UI } from './ui';
@Component({
  imports: UI,
  template: `<div class="page-heading">
      <div>
        <p class="eyebrow">YOUR SHARED SPACES</p>
        <h1>我的群組</h1>
        <p class="muted">每一筆共同花費，都有清楚的去向。</p>
      </div>
      <button mat-flat-button (click)="creating.set(!creating())">＋ 建立群組</button>
    </div>
    @if (error()) {
      <p class="error" role="alert">
        {{ error() }} <button mat-button (click)="load()">重試</button>
      </p>
    }
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (creating()) {
      <mat-card
        ><mat-card-content
          ><form [formGroup]="form" (ngSubmit)="create()">
            <h2>建立新群組</h2>
            <mat-form-field
              ><mat-label>群組名稱</mat-label
              ><input matInput formControlName="name" maxlength="100" /></mat-form-field
            ><mat-form-field
              ><mat-label>描述（選填）</mat-label
              ><textarea
                matInput
                formControlName="description"
                maxlength="500"
              ></textarea></mat-form-field
            ><button mat-flat-button [disabled]="form.invalid || saving()">建立群組</button>
          </form></mat-card-content
        ></mat-card
      >
    }
    <div class="group-grid">
      @for (g of groups(); track g.id) {
        <a class="group-card" [routerLink]="['/groups', g.id]"
          ><span class="group-symbol">↗</span>
          <h2>{{ g.name }}</h2>
          <p class="muted">{{ g.description || '開始累積一起度過的時光' }}</p>
          <div class="card-bottom">
            <span>{{
              (g.netMinor ?? 0) > 0 ? '你應收' : (g.netMinor ?? 0) < 0 ? '你應付' : '已結清'
            }}</span
            ><strong>{{ money(Math.abs(g.netMinor ?? 0)) }}</strong>
          </div></a
        >
      } @empty {
        @if (!loading()) {
          <div class="empty">
            <h2>準備好開始第一筆共同花費了嗎？</h2>
            <p>建立群組，再邀請朋友一起加入。</p>
          </div>
        }
      }
    </div>`,
})
export class Groups {
  api = inject(Api);
  router = inject(Router);
  fb = inject(FormBuilder);
  groups = signal<Group[]>([]);
  loading = signal(true);
  saving = signal(false);
  creating = signal(false);
  error = signal('');
  money = money;
  Math = Math;
  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
  });
  constructor() {
    void this.load();
  }
  async load() {
    this.error.set('');
    this.loading.set(true);
    try {
      this.groups.set(await this.api.get<Group[]>('/groups'));
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.loading.set(false);
    }
  }
  async create() {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    try {
      const g = await this.api.post<{ id: string }>('/groups', this.form.getRawValue());
      await this.router.navigate(['/groups', g.id]);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.saving.set(false);
    }
  }
}
