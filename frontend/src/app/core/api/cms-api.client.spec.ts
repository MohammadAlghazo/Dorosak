import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { API_REQUEST, PUBLIC_API_REQUEST, SKIP_AUTH } from './api-context';
import { CmsApiClient } from './cms-api.client';
import type {
  AdminCms,
  AuditLogPage,
  CmsFaq,
  CmsPage,
  PortfolioSettings,
  PublicCmsFaq,
  PublicCmsPage,
  PublicPortfolioSettings,
} from './cms-api.types';
import { IdentityApiClient } from './identity-api.client';

describe('CmsApiClient', () => {
  let client: CmsApiClient;
  let http: HttpTestingController;
  const bootstrapCsrf = vi.fn(() => of(undefined));

  beforeEach(() => {
    bootstrapCsrf.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf } },
      ],
    });
    client = TestBed.inject(CmsApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  const expectPublicRead = (url: string) => {
    const request = http.expectOne(url);
    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    expect(request.request.context.get(PUBLIC_API_REQUEST)).toBe(true);
    return request;
  };

  const expectAuthenticatedRead = (url: string) => {
    const request = http.expectOne(url);
    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    expect(request.request.context.get(PUBLIC_API_REQUEST)).toBe(false);
    expect(request.request.context.get(SKIP_AUTH)).toBe(false);
    return request;
  };

  it('uses public request context for CMS reads and encodes page slugs', async () => {
    const pagePromise = firstValueFrom(client.getPublicPage('privacy/policy'));
    expectPublicRead('pages/privacy%2Fpolicy').flush({ data: publicPage });
    await expect(pagePromise).resolves.toEqual(publicPage);

    const faqsPromise = firstValueFrom(client.getFaqs());
    expectPublicRead('faqs').flush({ data: publicFaqs });
    await expect(faqsPromise).resolves.toEqual(publicFaqs);

    const settingsPromise = firstValueFrom(client.getPublicSettings());
    expectPublicRead('portfolio-settings').flush({ data: publicSettings });
    await expect(settingsPromise).resolves.toEqual(publicSettings);
  });

  it('uses authenticated request context for admin CMS and settings reads', async () => {
    const cmsPromise = firstValueFrom(client.getAdminCms());
    expectAuthenticatedRead('admin/cms').flush({ data: adminCms });
    await expect(cmsPromise).resolves.toEqual(adminCms);

    const settingsPromise = firstValueFrom(client.getSettings());
    expectAuthenticatedRead('admin/settings').flush({ data: settings });
    await expect(settingsPromise).resolves.toEqual(settings);
    expect(bootstrapCsrf).not.toHaveBeenCalled();
  });

  it('saves a page draft with its request body after CSRF bootstrap', async () => {
    const draft = {
      expectedVersion: 3,
      titleAr: 'Arabic privacy policy',
      titleEn: 'Privacy policy',
      bodyAr: 'Arabic body',
      bodyEn: 'English body',
    };
    const promise = firstValueFrom(
      client.savePageDraft('privacy/policy', draft, '  Refresh privacy copy  '),
    );
    const request = http.expectOne('admin/cms/pages/privacy%2Fpolicy/draft');

    expect(bootstrapCsrf).toHaveBeenCalledOnce();
    expect(request.request.method).toBe('PUT');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    expect(request.request.headers.get('X-Audit-Reason')).toBe('Refresh privacy copy');
    expect(request.request.body).toEqual(draft);
    request.flush({ data: cmsPage });
    await expect(promise).resolves.toEqual(cmsPage);
  });

  it('publishes encoded page slugs and FAQ ids with version-only bodies', async () => {
    const pagePromise = firstValueFrom(client.publishPage('about/team', 7, '  Publish page  '));
    const pageRequest = http.expectOne('admin/cms/pages/about%2Fteam/publish');

    expect(pageRequest.request.method).toBe('POST');
    expect(pageRequest.request.body).toEqual({ expectedVersion: 7 });
    expect(pageRequest.request.headers.get('X-Audit-Reason')).toBe('Publish page');
    pageRequest.flush({ data: cmsPage });
    await expect(pagePromise).resolves.toEqual(cmsPage);

    const faqPromise = firstValueFrom(client.publishFaq('faq/with space', 5, '  Publish FAQ  '));
    const faqRequest = http.expectOne('admin/cms/faqs/faq%2Fwith%20space/publish');

    expect(faqRequest.request.method).toBe('POST');
    expect(faqRequest.request.body).toEqual({ expectedVersion: 5 });
    expect(faqRequest.request.headers.get('X-Audit-Reason')).toBe('Publish FAQ');
    faqRequest.flush({ data: cmsFaq });
    await expect(faqPromise).resolves.toEqual(cmsFaq);
    expect(bootstrapCsrf).toHaveBeenCalledTimes(2);
  });

  it('creates and saves FAQ drafts with caller-provided bodies', async () => {
    const createDraft = {
      expectedVersion: 0,
      displayOrder: 4,
      questionAr: 'Arabic question?',
      questionEn: 'Question?',
      answerAr: 'Arabic answer',
      answerEn: 'Answer',
    };
    const createPromise = firstValueFrom(client.createFaqDraft(createDraft, '  Add FAQ  '));
    const createRequest = http.expectOne('admin/cms/faqs');

    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual(createDraft);
    expect(createRequest.request.headers.get('X-Audit-Reason')).toBe('Add FAQ');
    createRequest.flush({ data: cmsFaq });
    await expect(createPromise).resolves.toEqual(cmsFaq);

    const updateDraft = { ...createDraft, expectedVersion: 2, displayOrder: 5 };
    const updatePromise = firstValueFrom(
      client.saveFaqDraft('faq/1', updateDraft, '  Revise FAQ  '),
    );
    const updateRequest = http.expectOne('admin/cms/faqs/faq%2F1/draft');

    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(updateDraft);
    expect(updateRequest.request.headers.get('X-Audit-Reason')).toBe('Revise FAQ');
    updateRequest.flush({ data: cmsFaq });
    await expect(updatePromise).resolves.toEqual(cmsFaq);
    expect(bootstrapCsrf).toHaveBeenCalledTimes(2);
  });

  it('updates portfolio settings as an audited CSRF-protected mutation', async () => {
    const update = {
      featuredCourseLimit: 8,
      showPortfolioNotice: true,
      noticeAr: 'Arabic notice',
      noticeEn: 'Notice',
      expectedVersion: 6,
    };
    const promise = firstValueFrom(client.updateSettings(update, '  Update portfolio settings  '));
    const request = http.expectOne('admin/settings');

    expect(bootstrapCsrf).toHaveBeenCalledOnce();
    expect(request.request.method).toBe('PUT');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    expect(request.request.headers.get('X-Audit-Reason')).toBe('Update portfolio settings');
    expect(request.request.body).toEqual(update);
    request.flush({ data: settings });
    await expect(promise).resolves.toEqual(settings);
  });

  it('sends audit cursor and action values without depending on query serialization', async () => {
    const promise = firstValueFrom(
      client.getAuditLogs('  Review CMS publishing  ', 25, 'cursor/+ token', 'cms.page/published'),
    );
    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'admin/audit-logs' &&
        candidate.params.get('limit') === '25' &&
        candidate.params.get('cursor') === 'cursor/+ token' &&
        candidate.params.get('action') === 'cms.page/published',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.context.get(API_REQUEST)).toBe(true);
    expect(request.request.context.get(SKIP_AUTH)).toBe(false);
    expect(request.request.headers.get('X-Audit-Reason')).toBe('Review CMS publishing');
    expect(bootstrapCsrf).not.toHaveBeenCalled();
    request.flush({ data: auditPage });
    await expect(promise).resolves.toEqual(auditPage);
  });
});

const cmsPage: CmsPage = {
  id: 'page-1',
  slug: 'privacy/policy',
  currentVersion: 4,
  publishedVersion: 3,
  draft: null,
  published: null,
  updatedAt: '2030-01-02T00:00:00Z',
  publishedAt: '2030-01-01T00:00:00Z',
};

const cmsFaq: CmsFaq = {
  id: 'faq/1',
  displayOrder: 5,
  currentVersion: 3,
  publishedVersion: 2,
  draft: null,
  published: null,
  updatedAt: '2030-01-02T00:00:00Z',
  publishedAt: '2030-01-01T00:00:00Z',
};

const publicPage: PublicCmsPage = {
  slug: 'privacy/policy',
  locale: 'en',
  title: 'Privacy policy',
  body: 'English body',
  version: 3,
  publishedAt: '2030-01-01T00:00:00Z',
};

const publicFaqs: readonly PublicCmsFaq[] = [
  {
    id: 'faq-1',
    locale: 'en',
    question: 'Question?',
    answer: 'Answer',
    version: 2,
    displayOrder: 1,
    publishedAt: '2030-01-01T00:00:00Z',
  },
];

const publicSettings: PublicPortfolioSettings = {
  locale: 'en',
  featuredCourseLimit: 6,
  showPortfolioNotice: true,
  portfolioNotice: 'Notice',
};

const adminCms: AdminCms = { pages: [cmsPage], faqs: [cmsFaq] };

const settings: PortfolioSettings = {
  featuredCourseLimit: 8,
  showPortfolioNotice: true,
  noticeAr: 'Arabic notice',
  noticeEn: 'Notice',
  version: 7,
  updatedAt: '2030-01-02T00:00:00Z',
};

const auditPage: AuditLogPage = {
  items: [],
  nextCursor: 'next-cursor',
  hasMore: true,
};
