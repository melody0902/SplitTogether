import { describe, it, expect } from 'vitest';
import { parseMoney, decimal, equalSplit } from './money';
describe('精確金額', () => {
  it('十進位字串精確轉換', () => {
    expect(parseMoney('0.29')).toBe(29);
    expect(parseMoney('1000000000.00')).toBe(100000000000);
    expect(decimal(3334)).toBe('33.34');
  });
  it('拒絕不合法、過多小數與超額', () => {
    for (const s of ['-1', '0.001', '1e3', '1,000', 'NaN', '1000000000.01'])
      expect(() => parseMoney(s)).toThrow();
  });
  it('平均餘數依固定順序', () => {
    expect(equalSplit(10000, ['c', 'a', 'b'])).toEqual([
      { memberId: 'a', amountMinor: 3334 },
      { memberId: 'b', amountMinor: 3333 },
      { memberId: 'c', amountMinor: 3333 },
    ]);
    expect(equalSplit(1, ['a', 'b', 'c']).map((x) => x.amountMinor)).toEqual([1, 0, 0]);
  });
});
