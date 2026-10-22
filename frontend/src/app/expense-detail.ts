import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Api, message } from './api';
import { Expense, Member, Group } from './models';
import { money } from './money';
import { UI } from './ui';
@Component({
  imports: UI,
  template: `<a mat-button [routerLink]="['/groups', groupId]">← 返回群組</a>
    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @if (expense(); as e) {
      <mat-card class="narrow"
        ><mat-card-content
          ><p class="eyebrow">EXPENSE DETAILS</p>
          <h1>{{ e.title }}</h1>
          <p class="amount">{{ money(e.amountMinor) }}</p>
          <p>{{ e.date }} · {{ name(e.payerId) }} 代墊</p>
          <p class="muted">建立者：{{ creator(e.createdBy) }}</p>
          <p>{{ e.note || '沒有備註' }}</p>
          <h2>分攤明細</h2>
          @for (s of e.splits; track s.memberId) {
            <div class="list-row">
              <span>{{ name(s.memberId) }}</span
              ><strong>{{ money(s.amountMinor) }}</strong>
            </div>
          }
          @if (canEdit()) {
            <div class="actions">
              <a mat-flat-button [routerLink]="['/groups', groupId, 'expenses', e.id, 'edit']"
                >編輯支出</a
              ><button mat-button [disabled]="busy()" (click)="remove()">刪除支出</button>
            </div>
          }
        </mat-card-content></mat-card
      >
    }`,
})
export class ExpenseDetail {
  api = inject(Api);
  route = inject(ActivatedRoute);
  router = inject(Router);
  groupId = this.route.snapshot.paramMap.get('id')!;
  expenseId = this.route.snapshot.paramMap.get('expenseId')!;
  expense = signal<Expense | null>(null);
  members = signal<Member[]>([]);
  group = signal<Group | null>(null);
  loading = signal(true);
  busy = signal(false);
  error = signal('');
  money = money;
  constructor() {
    void this.load();
  }
  name(id: string) {
    return this.members().find((x) => x.id === id)?.name ?? '成員';
  }
  creator(id: string) {
    return this.members().find((x) => x.userId === id)?.name ?? '成員';
  }
  canEdit() {
    return (
      this.group()?.ownerId === this.api.user()?.id ||
      this.expense()?.createdBy === this.api.user()?.id
    );
  }
  async load() {
    try {
      const [g, m, e] = await Promise.all([
        this.api.get<Group>(`/groups/${this.groupId}`),
        this.api.get<Member[]>(`/groups/${this.groupId}/members`),
        this.api.get<Expense>(`/groups/${this.groupId}/expenses/${this.expenseId}`),
      ]);
      this.group.set(g);
      this.members.set(m);
      this.expense.set(e);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.loading.set(false);
    }
  }
  async remove() {
    if (!confirm('刪除將重新計算餘額，可能改變已還款成員的帳務。確定刪除？')) return;
    this.busy.set(true);
    try {
      await this.api.delete(
        `/groups/${this.groupId}/expenses/${this.expenseId}?version=${this.expense()!.version}`,
      );
      await this.router.navigate(['/groups', this.groupId]);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
