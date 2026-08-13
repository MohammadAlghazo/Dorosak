export interface PublicCmsPage {
  slug: string;
  locale: 'ar' | 'en';
  title: string;
  body: string;
  version: number;
  publishedAt: string;
}

export interface PublicCmsFaq {
  id: string;
  locale: 'ar' | 'en';
  question: string;
  answer: string;
  version: number;
  displayOrder: number;
  publishedAt: string;
}

export interface CmsPageRevision {
  version: number;
  titleAr: string;
  titleEn: string;
  bodyAr: string;
  bodyEn: string;
  createdByUserId: string;
  createdAt: string;
}

export interface CmsPage {
  id: string;
  slug: string;
  currentVersion: number;
  publishedVersion: number | null;
  draft: CmsPageRevision | null;
  published: CmsPageRevision | null;
  updatedAt: string;
  publishedAt: string | null;
}

export interface CmsFaqRevision {
  version: number;
  questionAr: string;
  questionEn: string;
  answerAr: string;
  answerEn: string;
  createdByUserId: string;
  createdAt: string;
}

export interface CmsFaq {
  id: string;
  displayOrder: number;
  currentVersion: number;
  publishedVersion: number | null;
  draft: CmsFaqRevision | null;
  published: CmsFaqRevision | null;
  updatedAt: string;
  publishedAt: string | null;
}

export interface AdminCms {
  pages: readonly CmsPage[];
  faqs: readonly CmsFaq[];
}

export interface PortfolioSettings {
  featuredCourseLimit: number;
  showPortfolioNotice: boolean;
  noticeAr: string;
  noticeEn: string;
  version: number;
  updatedAt: string;
}

export interface PublicPortfolioSettings {
  locale: 'ar' | 'en';
  featuredCourseLimit: number;
  showPortfolioNotice: boolean;
  portfolioNotice: string;
}

export interface AuditLog {
  id: string;
  actorUserId: string;
  action: string;
  targetType: string;
  targetId: string;
  result: string;
  reason: string | null;
  occurredAt: string;
}

export interface AuditLogPage {
  items: readonly AuditLog[];
  nextCursor: string | null;
  hasMore: boolean;
}
