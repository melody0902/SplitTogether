import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Api, message } from './api';
import {
  Group,
  Member,
  Page,
  Expense,
  Snapshot,
  Settlement,
  Invitation,
  Suggestion,
} from './models';
import { money, decimal, parseMoney, today } from './money';
import { UI } from './ui';
@Component({ imports: UI, templateUrl: './group-detail.html' })
export class GroupDetail {
  api = inject(Api);
  route = inject(ActivatedRoute);
  fb = inject(FormBuilder);
  id = this.route.snapshot.paramMap.get('id')!;
  group = signal<Group | null>(null);
  members = signal<Member[]>([]);
  expenses = signal<Page<Expense>>({ items: [], total: 0, page: 1, pageSize: 20 });
  snapshot = signal<Snapshot>({ totalMinor: 0, members: [], suggestions: [] });
  settlements = signal<Settlement[]>([]);
  invites = signal<Invitation[]>([]);
  tab = signal('expenses');
  loading = signal(true);
  busy = signal(false);
  error = signal('');
  notice = signal('');
  inviteLink = signal('');
  showPayment = signal(false);
  page = 1;
  paymentKey = crypto.randomUUID();
  money = money;
  Math = Math;
  settings = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
  });
  filter = this.fb.nonNullable.group({ search: [''], from: [''], to: [''], payerId: [''] });
  payment = this.fb.nonNullable.group({
    fromId: ['', Validators.required],
    toId: ['', Validators.required],
    amount: ['', Validators.required],
    date: [today(), Validators.required],
    note: ['', Validators.maxLength(1000)],
  });
  constructor() {
    void this.load();
  }
  owner() {
    return this.group()?.ownerId === this.api.user()?.id;
  }
  name(id: string) {
    return this.members().find((x) => x.id === id)?.name ?? '成員';
  }
  creator(id: string) {
    return this.members().find((x) => x.userId === id)?.name ?? '成員';
  }
  mine() {
    return (
      this.snapshot().members.find((x) => x.memberId === this.group()?.myMemberId)?.netMinor ?? 0
    );
  }
  async load() {
    this.error.set('');
    this.loading.set(true);
    try {
      const [g, m, b, s] = await Promise.all([
        this.api.get<Group>(`/groups/${this.id}`),
        this.api.get<Member[]>(`/groups/${this.id}/members`),
        this.api.get<Snapshot>(`/groups/${this.id}/balances`),
        this.api.get<Settlement[]>(`/groups/${this.id}/settlements`),
      ]);
      this.group.set(g);
      this.members.set(m);
      this.snapshot.set(b);
      this.settlements.set(s);
      this.settings.patchValue({ name: g.name, description: g.description });
      if (this.owner())
        this.invites.set(await this.api.get<Invitation[]>(`/groups/${this.id}/invitations`));
      await this.loadExpenses();
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.loading.set(false);
    }
  }
  async loadExpenses() {
    const query = new URLSearchParams({ page: String(this.page), pageSize: '20' });
    for (const [k, v] of Object.entries(this.filter.getRawValue())) if (v) query.set(k, v);
    this.expenses.set(await this.api.get<Page<Expense>>(`/groups/${this.id}/expenses?${query}`));
  }
  async filterExpenses(page = 1) {
    this.page = page;
    await this.act(() => this.loadExpenses(), false);
  }
  async act(fn: () => Promise<unknown>, reload = true) {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await fn();
      if (reload) await this.load();
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async saveSettings() {
    await this.act(() =>
      this.api.patch(`/groups/${this.id}`, {
        ...this.settings.getRawValue(),
        version: this.group()!.version,
      }),
    );
  }
  async invite() {
    await this.act(async () => {
      const i = await this.api.post<{ token: string }>(`/groups/${this.id}/invitations`, {});
      this.inviteLink.set(location.origin + '/invite/' + i.token);
    });
  }
  async copy() {
    try {
      await navigator.clipboard.writeText(this.inviteLink());
      this.notice.set('邀請連結已複製');
    } catch {
      this.notice.set('請選取並手動複製下方連結');
    }
  }
  async revoke(id: string) {
    if (confirm('撤銷後此連結將無法加入群組，確定繼續？'))
      await this.act(() => this.api.delete(`/groups/${this.id}/invitations/${id}`));
  }
  async remove(m: Member) {
    if (confirm(`確定移除 ${m.name}？`))
      await this.act(() => this.api.delete(`/groups/${this.id}/members/${m.id}`));
  }
  canPay(s: Suggestion) {
    return (
      this.owner() || s.fromId === this.group()?.myMemberId || s.toId === this.group()?.myMemberId
    );
  }
  openPayment(s?: Suggestion) {
    this.paymentKey = crypto.randomUUID();
    this.payment.reset({
      fromId: s?.fromId ?? this.group()?.myMemberId ?? '',
      toId: s?.toId ?? '',
      amount: s ? decimal(s.amountMinor) : '',
      date: today(),
      note: '',
    });
    this.showPayment.set(true);
  }
  async pay() {
    if (this.payment.invalid || this.busy()) return;
    this.error.set('');
    try {
      const v = this.payment.getRawValue();
      const amount = parseMoney(v.amount);
      if (amount <= 0 || v.fromId === v.toId) throw Error('請選擇不同的付款／收款人並填入正確金額');
      const from = this.snapshot().members.find((x) => x.memberId === v.fromId)?.netMinor ?? 0;
      const to = this.snapshot().members.find((x) => x.memberId === v.toId)?.netMinor ?? 0;
      const limit = Math.min(Math.max(-from, 0), Math.max(to, 0));
      if (
        amount > limit &&
        !confirm('此金額超過目前雙方可結算的淨額，可能形成反向欠款。你確定要人工登記這筆還款嗎？')
      )
        return;
      await this.act(async () => {
        await this.api.post(`/groups/${this.id}/settlements`, {
          fromId: v.fromId,
          toId: v.toId,
          amountMinor: amount,
          date: v.date,
          note: v.note,
          clientRequestId: this.paymentKey,
        });
        this.showPayment.set(false);
      });
    } catch (e) {
      this.error.set(message(e));
    }
  }
  async voidPayment(s: Settlement) {
    if (confirm('確定作廢這筆還款？餘額將重新計算。'))
      await this.act(() => this.api.post(`/groups/${this.id}/settlements/${s.id}/void`, {}));
  }
}
