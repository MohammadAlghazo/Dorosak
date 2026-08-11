import { expect, test, type Page, type Route } from '@playwright/test';

test.use({ serviceWorkers: 'block' });

test('authenticated communications remain usable at 320px with mocked APIs', async ({
  page,
  request,
}) => {
  for (const path of ['/en/chat', '/en/notifications', '/en/my-learning']) {
    const response = await request.get(path);
    expect(response.headers()['cache-control']).toBe('no-store');
  }

  await mockCommunications(page);
  await page.setViewportSize({ width: 320, height: 800 });

  await page.goto('/en/chat');
  await expect(page.getByRole('heading', { name: 'Chat', level: 1 })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Instructor' })).toBeVisible();
  await expect(page.locator('input')).toHaveCount(0);
  await expectNoHorizontalOverflow(page);

  await page.getByRole('link', { name: 'Notifications' }).first().click();
  await expect(page).toHaveURL(/\/en\/notifications$/u);
  await expect(page.getByRole('heading', { name: 'Notifications', level: 1 })).toBeVisible();
  await expect(page.locator('.notification-link')).toHaveAttribute(
    'href',
    '/en/chat/conversation-1',
  );
  await expectNoHorizontalOverflow(page);
});

const mockCommunications = async (page: Page): Promise<void> => {
  await page.route('**/hubs/communications/negotiate**', (route) =>
    route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }),
  );
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
      await fulfill(route, { data: { count: 1, latestSequence: 1 } });
      return;
    }
    if (path === '/api/v1/me/notifications') {
      await fulfill(route, { data: notificationPage });
      return;
    }
    if (path === '/api/v1/conversations') {
      await fulfill(route, { data: conversationPage });
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
    permissions: ['Conversation.ReadOwn', 'Message.SendAsSelf', 'Notification.ReadOwn'],
    authenticationMethods: ['pwd'],
  },
};

const conversationPage = {
  items: [
    {
      id: 'conversation-1',
      courseId: 'course-1',
      createdByUserId: 'user-1',
      participants: [
        {
          userId: 'user-1',
          displayName: 'Current Learner',
          joinedAt: '2030-01-01T00:00:00Z',
        },
        {
          userId: 'user-2',
          displayName: 'Instructor',
          joinedAt: '2030-01-01T00:00:00Z',
        },
      ],
      lastSequence: 1,
      createdAt: '2030-01-01T00:00:00Z',
      updatedAt: '2030-01-01T00:01:00Z',
    },
  ],
  nextCursor: null,
  hasMore: false,
};

const notificationPage = {
  items: [
    {
      id: 'notification-1',
      sequence: 1,
      type: 'Message',
      resourceId: 'message-1',
      courseId: 'course-1',
      conversationId: 'conversation-1',
      actorUserId: 'user-2',
      announcementVersion: null,
      title: null,
      body: null,
      isRead: false,
      readAt: null,
      createdAt: '2030-01-01T00:01:00Z',
    },
  ],
  nextCursor: null,
  hasMore: false,
  latestSequence: 1,
  unreadCount: 1,
};
