import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page, type Route } from '@playwright/test';

test.use({ serviceWorkers: 'block' });

test('Phase 11 administration exposes permission-gated CMS settings and audit workspaces', async ({
  page,
  request,
}) => {
  const response = await request.get('/en/admin/cms');
  expect(response.headers()['cache-control']).toBe('no-store');
  await mockAdministration(page);

  await page.goto('/en/admin');
  await expect(page.getByRole('link', { name: 'Editorial CMS' }).first()).toBeVisible();
  await expect(page.getByRole('link', { name: 'Platform settings' }).first()).toBeVisible();
  await expect(page.getByRole('link', { name: 'Audit logs' }).first()).toBeVisible();

  await page.goto('/en/admin/cms');
  await expect(page.getByRole('heading', { name: 'Pages and FAQs' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'about' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByLabel('English title')).toHaveValue('About the local showcase');
  await expectNoHorizontalOverflow(page);

  await page.goto('/en/admin/settings');
  await expect(page.getByRole('heading', { name: 'Platform settings' })).toBeVisible();
  await expect(page.getByLabel('Featured course limit')).toHaveValue('6');
  await expectNoHorizontalOverflow(page);

  await page.goto('/en/admin/audit-logs');
  await expect(page.getByRole('heading', { name: 'Audit logs' })).toBeVisible();
  await expect(page.getByRole('table')).toHaveCount(0);
  await page.getByLabel('Access reason').fill('Review Phase 11 administration activity.');
  await page.getByRole('button', { name: 'Load logs' }).click();
  await expect(page.getByRole('table')).toBeVisible();
  await expect(page.getByText('cms.page-published')).toBeVisible();
  await expectNoHorizontalOverflow(page);

  const accessibility = await new AxeBuilder({ page }).analyze();
  expect(
    accessibility.violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
});

const mockAdministration = async (page: Page): Promise<void> => {
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
    if (path === '/api/v1/admin/cms') {
      await fulfill(route, { data: adminCms });
      return;
    }
    if (path === '/api/v1/admin/settings') {
      await fulfill(route, { data: settings });
      return;
    }
    if (path === '/api/v1/admin/audit-logs') {
      expect(request.headers()['x-audit-reason']).toBe('Review Phase 11 administration activity.');
      await fulfill(route, { data: auditPage });
      return;
    }
    await route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{}' });
  });
};

const fulfill = (route: Route, body: unknown): Promise<void> =>
  route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

const expectNoHorizontalOverflow = async (page: Page): Promise<void> => {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
};

const session = {
  accessToken: 'phase11-admin-token',
  accessTokenExpiresAt: '2099-01-01T00:00:00Z',
  identity: {
    userId: '01910000-0000-7000-8000-000000000001',
    sessionId: '01910000-0000-7000-8000-000000000002',
    displayName: 'Portfolio Administrator',
    email: 'admin@example.test',
    emailVerified: true,
    mfaEnabled: true,
    authenticatedAt: '2030-01-01T00:00:00Z',
    recentAuthenticationExpiresAt: '2099-01-01T00:00:00Z',
    authorizationVersion: 1,
    roles: ['Admin'],
    permissions: ['Cms.Manage', 'Settings.Manage', 'Audit.Read'],
    authenticationMethods: ['otp'],
  },
};

const revision = {
  version: 1,
  titleAr: 'عن العرض المحلي',
  titleEn: 'About the local showcase',
  bodyAr: 'محتوى عربي آمن.',
  bodyEn: 'Safe English content.',
  createdByUserId: session.identity.userId,
  createdAt: '2030-01-01T00:00:00Z',
};

const adminCms = {
  pages: [
    {
      id: '01910000-0000-7000-8000-000000000010',
      slug: 'about',
      currentVersion: 1,
      publishedVersion: 1,
      draft: revision,
      published: revision,
      updatedAt: '2030-01-01T00:00:00Z',
      publishedAt: '2030-01-01T00:00:00Z',
    },
  ],
  faqs: [],
};

const settings = {
  featuredCourseLimit: 6,
  showPortfolioNotice: true,
  noticeAr: 'نسخة عرض محلية.',
  noticeEn: 'A local portfolio showcase.',
  version: 3,
  updatedAt: '2030-01-01T00:00:00Z',
};

const auditPage = {
  items: [
    {
      id: '01910000-0000-7000-8000-000000000020',
      actorUserId: session.identity.userId,
      action: 'cms.page-published',
      targetType: 'CmsPage',
      targetId: '01910000-0000-7000-8000-000000000010',
      result: 'Succeeded',
      reason: 'Publish reviewed bilingual page.',
      occurredAt: '2030-01-01T00:00:00Z',
    },
  ],
  nextCursor: null,
  hasMore: false,
};
