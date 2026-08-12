import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('Arabic public route renders useful SSR HTML and hydrates accessibly', async ({
  page,
  request,
}) => {
  const response = await request.get('/ar');
  expect(response.status()).toBe(200);
  const html = await response.text();
  const csp = response.headers()['content-security-policy'] ?? '';
  const nonceMatch = /'nonce-([^']+)'/u.exec(csp);
  expect(nonceMatch).not.toBeNull();
  const nonce = nonceMatch?.[1] ?? '';
  expect(nonce).not.toBe('');
  expect(html).toContain(`ngCspNonce="${nonce}"`);
  expect(html).not.toMatch(/<(?:script|style)\b(?![^>]*\bnonce=)/iu);
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
  const mobileLayout = await page.evaluate(() => {
    const localeButton = [...document.querySelectorAll('button')].find(
      (button) => button.textContent.trim() === 'English',
    );
    const navigation = document.querySelector<HTMLElement>('.primary-navigation');
    const searchLink = navigation?.querySelector<HTMLAnchorElement>('a:last-child');
    const rectangle = (element: Element | null | undefined) => {
      if (!element) return null;
      const box = element.getBoundingClientRect();
      return { top: box.top, right: box.right, bottom: box.bottom, left: box.left };
    };
    return {
      localeButton: rectangle(localeButton),
      navigation: rectangle(navigation),
      searchLink: rectangle(searchLink),
      viewport: { width: innerWidth, height: innerHeight },
    };
  });
  expect(mobileLayout.localeButton).not.toBeNull();
  expect(mobileLayout.navigation).not.toBeNull();
  expect(mobileLayout.searchLink).not.toBeNull();
  if (!mobileLayout.localeButton || !mobileLayout.searchLink) {
    throw new Error('The mobile public header controls are unavailable.');
  }
  const localeBox = mobileLayout.localeButton;
  const searchBox = mobileLayout.searchLink;
  const overlaps =
    localeBox.left < searchBox.right &&
    localeBox.right > searchBox.left &&
    localeBox.top < searchBox.bottom &&
    localeBox.bottom > searchBox.top;
  expect(overlaps, JSON.stringify(mobileLayout)).toBe(false);
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

test('certificate verification is public, noindex, and handles an unknown code safely', async ({
  page,
  request,
}) => {
  const response = await request.get('/en/certificates/verify/not_a_real_certificate_code');
  expect(response.status()).toBe(200);
  const html = await response.text();
  expect(html).not.toContain('learnerUserId');
  expect(html).not.toContain('@example.test');

  await page.goto('/en/certificates/verify/not_a_real_certificate_code');
  await expect(page.getByRole('heading', { name: 'Certificate not found' })).toBeVisible();
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex,follow');
});
