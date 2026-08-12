import { expect, test, type Page, type Route } from '@playwright/test';

test.use({ serviceWorkers: 'block' });

test('demo subscription and printable certificate remain usable on portfolio viewports', async ({
  page,
  request,
}) => {
  for (const path of ['/en/settings/subscription', '/en/certificates']) {
    const response = await request.get(path);
    expect(response.headers()['cache-control']).toBe('no-store');
  }

  await mockPortfolio(page);
  await page.goto('/en/settings/subscription');
  await expect(page.getByRole('heading', { name: 'Simple demo subscription' })).toBeVisible();
  await expect(page.getByText('0 DEMO')).toBeVisible();
  await page.getByRole('button', { name: 'Activate demo subscription' }).click();
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
  await expectNoHorizontalOverflow(page);

  await page.goto('/en/certificates/certificate-1');
  await expect(page.getByRole('heading', { name: 'Portfolio Architecture' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Current Learner' })).toBeVisible();
  await expect(page.getByText('verify_portfolio_certificate')).toBeVisible();
  await expectNoHorizontalOverflow(page);
});

const mockPortfolio = async (page: Page): Promise<void> => {
  let subscriptionActive = false;
  await page.route('**/api/v1/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/api/v1/auth/csrf') {
      await fulfill(route, {});
      return;
    }
    if (path === '/api/v1/auth/refresh') {
      await fulfill(route, { data: session });
      return;
    }
    if (path === '/api/v1/me/notifications/unread-count') {
      await fulfill(route, { data: { count: 0, latestSequence: 0 } });
      return;
    }
    if (path === '/api/v1/me/subscription' && request.method() === 'GET') {
      await fulfill(route, { data: { subscription: subscriptionActive ? subscription : null } });
      return;
    }
    if (path === '/api/v1/subscriptions' && request.method() === 'POST') {
      subscriptionActive = true;
      await fulfill(route, { data: subscription });
      return;
    }
    if (path === '/api/v1/me/certificates/certificate-1') {
      await fulfill(route, { data: certificate });
      return;
    }
    await route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{}' });
  });
};

const fulfill = (route: Route, body: unknown): Promise<void> =>
  route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

const expectNoHorizontalOverflow = async (page: Page): Promise<void> => {
  const overflow = await page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth;
    return [...document.querySelectorAll<HTMLElement>('body *')]
      .map((element) => ({
        tag: element.tagName.toLowerCase(),
        className: element.className,
        text: element.textContent?.trim().slice(0, 80) ?? '',
        href: element instanceof HTMLAnchorElement ? element.href : '',
        parent: element.parentElement?.className ?? '',
        viewportWidth,
        left: element.getBoundingClientRect().left,
        right: element.getBoundingClientRect().right,
      }))
      .filter((element) => element.left < -1 || element.right > viewportWidth + 1)
      .slice(0, 10);
  });
  expect(overflow).toEqual([]);
};

const session = {
  accessToken: 'local-e2e-token',
  accessTokenExpiresAt: '2099-01-01T00:00:00Z',
  identity: {
    userId: 'user-1',
    sessionId: 'session-1',
    displayName: 'Current Learner',
    email: 'learner@example.test',
    emailVerified: true,
    mfaEnabled: false,
    authenticatedAt: '2030-01-01T00:00:00Z',
    recentAuthenticationExpiresAt: '2099-01-01T00:00:00Z',
    authorizationVersion: 1,
    roles: ['Student'],
    permissions: ['Certificate.ReadOwn', 'Subscription.ManageOwn'],
    authenticationMethods: ['pwd'],
  },
};

const subscription = {
  id: 'subscription-1',
  planCode: 'portfolio-demo',
  status: 'Active',
  activatedAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
  cancelledAt: null,
};

const certificate = {
  id: 'certificate-1',
  learnerName: 'Current Learner',
  courseTitle: 'Portfolio Architecture',
  locale: 'en',
  completedAt: '2030-01-01T00:00:00Z',
  issuedAt: '2030-01-01T00:01:00Z',
  verificationCode: 'verify_portfolio_certificate',
  status: 'Active',
  revokedAt: null,
};
