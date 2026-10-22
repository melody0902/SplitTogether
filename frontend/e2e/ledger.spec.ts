import { test, expect, BrowserContext } from '@playwright/test';
test('三人完整分帳、還款與作廢流程', async ({ browser, baseURL }) => {
  test.skip(
    !process.env['SPLIT_TEST_CONNECTION'],
    'Requires real PostgreSQL and tests/TestHost; no mocked ledger.',
  );
  const contexts: BrowserContext[] = [];
  async function user(name: string) {
    const c = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
    contexts.push(c);
    expect((await c.request.post('/api/testing/login', { data: { name } })).ok()).toBeTruthy();
    const csrf = await (await c.request.get('/api/auth/csrf')).json();
    return { c, headers: { 'X-XSRF-TOKEN': csrf.token } };
  }
  try {
    const a = await user('A'),
      b = await user('B'),
      c = await user('C');
    const page = await a.c.newPage();
    await page.goto('/groups');
    await page.getByRole('button', { name: '＋ 建立群組' }).click();
    await page.getByLabel('群組名稱').fill('三人旅行驗收');
    await page.getByRole('button', { name: '建立群組', exact: true }).click();
    await expect(page).toHaveURL(/\/groups\/[a-f0-9-]+$/);
    const group = page.url().split('/').pop()!;
    const invitation = await (
      await a.c.request.post(`/api/groups/${group}/invitations`, { headers: a.headers, data: {} })
    ).json();
    for (const person of [b, c]) {
      const p = await person.c.newPage();
      await p.goto(`/invite/${invitation.token}`);
      await p.getByRole('button', { name: '確認加入' }).click();
      await expect(p).toHaveURL(new RegExp('/groups/' + group + '$'));
    }
    const members = await (await a.c.request.get(`/api/groups/${group}/members`)).json();
    const id = (name: string) => members.find((x: { name: string }) => x.name === name).id;
    await page.getByRole('link', { name: '＋ 新增支出' }).click();
    await page.getByLabel('支出標題').fill('三人晚餐');
    await page.getByLabel('總金額（TWD）').fill('900');
    await page.getByRole('button', { name: '儲存支出' }).click();
    await expect(page).toHaveURL(new RegExp('/groups/' + group + '$'));
    expect(
      (
        await b.c.request.post(`/api/groups/${group}/expenses`, {
          headers: b.headers,
          data: {
            title: 'BC 午餐',
            amountMinor: 30000,
            date: '2026-09-25',
            payerId: id('B'),
            splitMode: 'Equal',
            splits: [
              { memberId: id('B'), amountMinor: 0 },
              { memberId: id('C'), amountMinor: 0 },
            ],
            note: '',
            clientRequestId: crypto.randomUUID(),
          },
        })
      ).ok(),
    ).toBeTruthy();
    const snapshot = async () =>
      await (await a.c.request.get(`/api/groups/${group}/balances`)).json();
    let s = await snapshot();
    const net = (name: string) =>
      s.members.find((x: { memberId: string }) => x.memberId === id(name)).netMinor;
    expect([net('A'), net('B'), net('C')]).toEqual([60000, -15000, -45000]);
    await page.reload();
    await page.getByRole('button', { name: '分帳與還款' }).click();
    await expect(page.getByRole('heading', { name: '建議這樣結算' })).toBeVisible();
    const repayments: Record<string, string> = {};
    for (const [name, amount] of [
      ['B', 15000],
      ['C', 45000],
    ] as const) {
      const r = await a.c.request.post(`/api/groups/${group}/settlements`, {
        headers: a.headers,
        data: {
          fromId: id(name),
          toId: id('A'),
          amountMinor: amount,
          date: '2026-09-25',
          note: '人工驗收',
          clientRequestId: crypto.randomUUID(),
        },
      });
      expect(r.ok()).toBeTruthy();
      repayments[name] = (await r.json()).id;
    }
    s = await snapshot();
    expect([net('A'), net('B'), net('C')]).toEqual([0, 0, 0]);
    expect(s.totalMinor).toBe(120000);
    await page.reload();
    await page.getByRole('button', { name: '分帳與還款' }).click();
    await expect(page.getByText('目前全部結清，沒有待還款項。')).toBeVisible();
    expect(
      (
        await a.c.request.post(`/api/groups/${group}/settlements/${repayments['B']}/void`, {
          headers: a.headers,
          data: {},
        })
      ).ok(),
    ).toBeTruthy();
    s = await snapshot();
    expect([net('A'), net('B'), net('C')]).toEqual([15000, -15000, 0]);
  } finally {
    for (const c of contexts) await c.close();
  }
});
