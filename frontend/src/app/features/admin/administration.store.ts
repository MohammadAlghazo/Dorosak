import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiProblem } from '../../core/api/api-problem';
import { CmsApiClient } from '../../core/api/cms-api.client';
import type {
  AdminCms,
  AuditLog,
  AuditLogPage,
  CmsFaq,
  CmsPage,
  PortfolioSettings,
} from '../../core/api/cms-api.types';

export const CMS_PAGE_SLOTS = ['about', 'contact', 'privacy', 'terms'] as const;
export type CmsPageSlot = (typeof CMS_PAGE_SLOTS)[number];

type OperationStatus = 'idle' | 'loading' | 'saving' | 'success' | 'conflict' | 'offline' | 'error';

export interface AdministrationState {
  status: OperationStatus;
  pages: readonly CmsPage[];
  faqs: readonly CmsFaq[];
  errorCode: string | null;
}

export interface SettingsState {
  status: OperationStatus;
  settings: PortfolioSettings | null;
  errorCode: string | null;
}

export interface AuditState {
  status: OperationStatus;
  items: readonly AuditLog[];
  nextCursor: string | null;
  hasMore: boolean;
  errorCode: string | null;
}

@Injectable()
export class AdministrationStore {
  private readonly api = inject(CmsApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cmsState = signal<AdministrationState>({
    status: 'idle',
    pages: pageSlots([]),
    faqs: [],
    errorCode: null,
  });
  private readonly settingsState = signal<SettingsState>({
    status: 'idle',
    settings: null,
    errorCode: null,
  });
  private readonly auditState = signal<AuditState>({
    status: 'idle',
    items: [],
    nextCursor: null,
    hasMore: false,
    errorCode: null,
  });

  readonly cms = this.cmsState.asReadonly();
  readonly settings = this.settingsState.asReadonly();
  readonly audit = this.auditState.asReadonly();

  loadCms(): void {
    this.cmsState.update((state) => ({ ...state, status: 'loading', errorCode: null }));
    this.api
      .getAdminCms()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (cms) => {
          this.cmsState.set({ status: 'success', ...cmsState(cms), errorCode: null });
        },
        error: (error: unknown) => {
          this.setCmsError(error);
        },
      });
  }

  savePageDraft(
    slug: CmsPageSlot,
    request: {
      expectedVersion: number;
      titleAr: string;
      titleEn: string;
      bodyAr: string;
      bodyEn: string;
    },
    auditReason: string,
  ): void {
    this.cmsState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    this.api
      .savePageDraft(slug, request, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadCms();
        },
        error: (error: unknown) => {
          this.setCmsError(error);
        },
      });
  }

  publishPage(slug: CmsPageSlot, expectedVersion: number, auditReason: string): void {
    this.cmsState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    this.api
      .publishPage(slug, expectedVersion, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadCms();
        },
        error: (error: unknown) => {
          this.setCmsError(error);
        },
      });
  }

  createFaqDraft(
    request: {
      expectedVersion: number;
      displayOrder: number;
      questionAr: string;
      questionEn: string;
      answerAr: string;
      answerEn: string;
    },
    auditReason: string,
  ): void {
    this.cmsState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    this.api
      .createFaqDraft(request, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadCms();
        },
        error: (error: unknown) => {
          this.setCmsError(error);
        },
      });
  }

  saveFaqDraft(
    faqId: string,
    request: {
      expectedVersion: number;
      displayOrder: number;
      questionAr: string;
      questionEn: string;
      answerAr: string;
      answerEn: string;
    },
    auditReason: string,
  ): void {
    this.cmsState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    this.api
      .saveFaqDraft(faqId, request, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadCms();
        },
        error: (error: unknown) => {
          this.setCmsError(error);
        },
      });
  }

  publishFaq(faqId: string, expectedVersion: number, auditReason: string): void {
    this.cmsState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    this.api
      .publishFaq(faqId, expectedVersion, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadCms();
        },
        error: (error: unknown) => {
          this.setCmsError(error);
        },
      });
  }

  loadSettings(): void {
    this.settingsState.update((state) => ({ ...state, status: 'loading', errorCode: null }));
    this.api
      .getSettings()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (settings) => {
          this.settingsState.set({ status: 'success', settings, errorCode: null });
        },
        error: (error: unknown) => {
          this.setSettingsError(error);
        },
      });
  }

  updateSettings(
    request: {
      featuredCourseLimit: number;
      showPortfolioNotice: boolean;
      noticeAr: string;
      noticeEn: string;
      expectedVersion: number;
    },
    auditReason: string,
  ): void {
    this.settingsState.update((state) => ({ ...state, status: 'saving', errorCode: null }));
    this.api
      .updateSettings(request, auditReason)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (settings) => {
          this.settingsState.set({ status: 'success', settings, errorCode: null });
        },
        error: (error: unknown) => {
          this.setSettingsError(error);
        },
      });
  }

  loadAudit(auditReason: string, limit: number, action: string | null = null): void {
    this.auditState.update((state) => ({
      ...state,
      status: 'loading',
      items: [],
      nextCursor: null,
      hasMore: false,
      errorCode: null,
    }));
    this.api
      .getAuditLogs(auditReason, limit, null, action)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          this.auditState.set(auditState(page));
        },
        error: (error: unknown) => {
          this.setAuditError(error);
        },
      });
  }

  loadMoreAudit(auditReason: string, limit: number, action: string | null): void {
    const current = this.auditState();
    if (!current.hasMore || current.nextCursor === null || current.status === 'loading') return;
    this.auditState.update((state) => ({ ...state, status: 'loading', errorCode: null }));
    this.api
      .getAuditLogs(auditReason, limit, current.nextCursor, action)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          const knownIds = new Set(current.items.map((item) => item.id));
          this.auditState.set({
            ...auditState(page),
            items: [...current.items, ...page.items.filter((item) => !knownIds.has(item.id))],
          });
        },
        error: (error: unknown) => {
          this.setAuditError(error);
        },
      });
  }

  private setCmsError(error: unknown): void {
    this.cmsState.update((state) => ({
      ...state,
      status: errorStatus(error),
      errorCode: error instanceof ApiProblem ? error.code : 'ADMIN.REQUEST_FAILED',
    }));
  }

  private setSettingsError(error: unknown): void {
    this.settingsState.update((state) => ({
      ...state,
      status: errorStatus(error),
      errorCode: error instanceof ApiProblem ? error.code : 'ADMIN.REQUEST_FAILED',
    }));
  }

  private setAuditError(error: unknown): void {
    this.auditState.update((state) => ({
      ...state,
      status: errorStatus(error),
      errorCode: error instanceof ApiProblem ? error.code : 'ADMIN.REQUEST_FAILED',
    }));
  }
}

const cmsState = (cms: AdminCms): Pick<AdministrationState, 'pages' | 'faqs'> => ({
  pages: pageSlots(cms.pages),
  faqs: [...cms.faqs],
});

const pageSlots = (pages: readonly CmsPage[]): readonly CmsPage[] =>
  CMS_PAGE_SLOTS.map((slug) => pages.find((page) => page.slug === slug) ?? emptyPage(slug));

const emptyPage = (slug: CmsPageSlot): CmsPage => ({
  id: '',
  slug,
  currentVersion: 0,
  publishedVersion: null,
  draft: null,
  published: null,
  updatedAt: '',
  publishedAt: null,
});

const auditState = (page: AuditLogPage): AuditState => ({
  status: 'success',
  items: [...page.items],
  nextCursor: page.nextCursor,
  hasMore: page.hasMore,
  errorCode: null,
});

const errorStatus = (error: unknown): OperationStatus =>
  error instanceof ApiProblem && error.status === 0
    ? 'offline'
    : error instanceof ApiProblem && error.code.endsWith('VERSION_CONFLICT')
      ? 'conflict'
      : 'error';
