import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('Arabic public route renders useful SSR HTML and hydrates accessibly', async ({
  page,
  request,
}) => {
  const response = await request.get('/ar');
  expect(response.status()).toBe(200);
  const html = await response.text();
  expect(html).toContain('مسارك الواضح');
  expect(html).toMatch(/<html[^>]+lang="ar"[^>]+dir="rtl"/u);

  const hydrationErrors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error' && /hydration/iu.test(message.text())) {
      hydrationErrors.push(message.text());
    }
  });
  await page.goto('/ar');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('مسارك الواضح');
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  expect(hydrationErrors).toEqual([]);

  const accessibility = await new AxeBuilder({ page }).analyze();
  expect(
    accessibility.violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
});

test('locale and theme switches preserve the equivalent route', async ({ page }) => {
  await page.goto('/ar/courses');
  await page.getByRole('button', { name: 'English' }).click();

  await expect(page).toHaveURL(/\/en\/courses$/u);
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

  const themeButton = page.getByRole('button', { name: 'Change theme' });
  await themeButton.click();
  await themeButton.click();
  await expect(page.locator('html')).toHaveAttribute('data-bs-theme', 'dark');
});

test('unknown routes return a real 404 and remain noindex', async ({ page, request }) => {
  const response = await request.get('/en/does-not-exist');
  expect(response.status()).toBe(404);

  await page.goto('/en/does-not-exist');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('not on the pathway');
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex,follow');
});

test('protected workspaces redirect anonymous browsers without SSR private data', async ({
  page,
  request,
}) => {
  const response = await request.get('/en/dashboard');
  expect(response.status()).toBe(200);
  expect(await response.text()).not.toContain('Continue your active pathway');

  await page.goto('/en/dashboard');
  await expect(page).toHaveURL(/\/en\/auth\/sign-in\?returnUrl=/u);
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});
