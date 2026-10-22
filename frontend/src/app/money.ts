export function parseMoney(value: string): number {
  if (!/^\d{1,10}(\.\d{1,2})?$/.test(value.trim())) throw Error('請輸入最多兩位小數的正確金額');
  const [whole, fraction = ''] = value.trim().split('.');
  const n = BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0'));
  if (n > 100000000000n) throw Error('金額不可超過十億元');
  return Number(n);
}
export function decimal(n: number): string {
  const v = BigInt(n);
  const sign = v < 0n ? '-' : '';
  const a = v < 0n ? -v : v;
  return `${sign}${a / 100n}.${(a % 100n).toString().padStart(2, '0')}`;
}
export function money(n: number): string {
  const [whole, fraction] = decimal(n).split('.');
  return `NT$ ${whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}.${fraction}`;
}
export function equalSplit(
  amount: number,
  ids: string[],
): { memberId: string; amountMinor: number }[] {
  if (!ids.length) return [];
  return [...ids]
    .sort()
    .map((id, i) => ({
      memberId: id,
      amountMinor: Math.floor(amount / ids.length) + (i < amount % ids.length ? 1 : 0),
    }));
}
export function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
