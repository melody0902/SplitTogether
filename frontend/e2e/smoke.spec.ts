import { test, expect } from '@playwright/test';
test('登入頁桌面與手機可使用', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: '開始一起記帳' })).toBeVisible();
  await expect(page.getByText('使用 Google 登入', { exact: true })).toBeVisible();
  await page.screenshot({ path: 'test-results/login-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('heading', { name: '開始一起記帳' })).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: 'test-results/login-mobile.png', fullPage: true });
});
