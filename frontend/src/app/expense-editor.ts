import { Component, HostListener, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Api, message } from './api';
import { Member, Expense, Split } from './models';
import { money, decimal, parseMoney, equalSplit, today } from './money';
import { UI } from './ui';
@Component({ imports: UI, templateUrl: './expense-editor.html' })
export class ExpenseEditor {
  api = inject(Api);
  route = inject(ActivatedRoute);
  router = inject(Router);
  fb = inject(FormBuilder);
  groupId = this.route.snapshot.paramMap.get('id')!;
  expenseId = this.route.snapshot.paramMap.get('expenseId');
  members = signal<Member[]>([]);
  selected = signal<string[]>([]);
  custom = signal<Record<string, string>>({});
  error = signal('');
  loading = signal(true);
  saving = signal(false);
  version: string | undefined;
  key = crypto.randomUUID();
  saved = false;
  changed = false;
  money = money;
  form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    amount: ['', [Validators.required]],
    date: [today(), Validators.required],
    payerId: ['', Validators.required],
    splitMode: ['Equal'],
    note: ['', Validators.maxLength(1000)],
  });
  constructor() {
    void this.load();
  }
  async load() {
    try {
      this.members.set(await this.api.get<Member[]>(`/groups/${this.groupId}/members`));
      this.form.patchValue({
        payerId: this.members().find((x) => x.userId === this.api.user()?.id)?.id ?? '',
      });
      this.selected.set(this.members().map((x) => x.id));
      if (this.expenseId) {
        const e = await this.api.get<Expense>(`/groups/${this.groupId}/expenses/${this.expenseId}`);
        this.version = e.version;
        this.form.patchValue({
          title: e.title,
          amount: decimal(e.amountMinor),
          date: e.date,
          payerId: e.payerId,
          splitMode: e.splitMode,
          note: e.note,
        });
        this.selected.set(e.splits.map((x) => x.memberId));
        this.custom.set(
          Object.fromEntries(e.splits.map((x) => [x.memberId, decimal(x.amountMinor)])),
        );
      }
      this.form.markAsPristine();
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.loading.set(false);
    }
  }
  toggle(id: string, on: boolean) {
    this.changed = true;
    this.selected.update((ids) => (on ? [...ids, id] : ids.filter((x) => x !== id)));
  }
  changeCustom(id: string, e: Event) {
    this.changed = true;
    this.custom.update((v) => ({ ...v, [id]: (e.target as HTMLInputElement).value }));
  }
  preview(): Split[] {
    try {
      const n = parseMoney(this.form.controls.amount.value);
      return this.form.controls.splitMode.value === 'Equal'
        ? equalSplit(n, this.selected())
        : this.selected().map((id) => ({
            memberId: id,
            amountMinor: parseMoney(this.custom()[id] || '0'),
          }));
    } catch {
      return [];
    }
  }
  previewAmount(id: string) {
    return this.preview().find((x) => x.memberId === id)?.amountMinor ?? 0;
  }
  remaining() {
    try {
      return (
        parseMoney(this.form.controls.amount.value) -
        this.preview().reduce((a, b) => a + b.amountMinor, 0)
      );
    } catch {
      return 0;
    }
  }
  canLeave() {
    return (
      this.saved || (!this.form.dirty && !this.changed) || confirm('尚有未儲存的內容，確定離開？')
    );
  }
  @HostListener('window:beforeunload', ['$event']) unload(e: BeforeUnloadEvent) {
    if (!this.saved && (this.form.dirty || this.changed)) e.preventDefault();
  }
  async submit() {
    if (this.form.invalid || this.saving()) return;
    this.error.set('');
    try {
      const v = this.form.getRawValue();
      const amount = parseMoney(v.amount);
      if (amount <= 0 || !this.selected().length) throw Error('請輸入大於零的金額並選擇分攤成員');
      const splits =
        v.splitMode === 'Equal'
          ? equalSplit(amount, this.selected())
          : this.selected().map((id) => ({
              memberId: id,
              amountMinor: parseMoney(this.custom()[id] || '0'),
            }));
      if (splits.reduce((s, x) => s + x.amountMinor, 0) !== amount)
        throw Error('分攤合計必須等於總金額');
      if (
        this.expenseId &&
        !confirm('更正支出會重新計算餘額，可能改變已還款成員的應收／應付，確定儲存？')
      )
        return;
      this.saving.set(true);
      const body = {
        title: v.title,
        amountMinor: amount,
        date: v.date,
        payerId: v.payerId,
        splitMode: v.splitMode,
        note: v.note,
        splits,
        clientRequestId: this.key,
        version: this.version,
      };
      if (this.expenseId)
        await this.api.put(`/groups/${this.groupId}/expenses/${this.expenseId}`, body);
      else await this.api.post(`/groups/${this.groupId}/expenses`, body);
      this.saved = true;
      await this.router.navigate(['/groups', this.groupId]);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.saving.set(false);
    }
  }
}
