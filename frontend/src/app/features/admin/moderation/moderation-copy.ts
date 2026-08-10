import type {
  ContentReportReason,
  ContentReportTargetKind,
  ModerationActionType,
  ModerationWorkflowStatus,
} from '../../../core/api/moderation-api.types';

type SupportedLocale = 'ar' | 'en';
type BilingualLabel = readonly [ar: string, en: string];

const statusLabels: Record<ModerationWorkflowStatus, BilingualLabel> = {
  Open: ['مفتوحة', 'Open'],
  InReview: ['قيد المراجعة', 'In review'],
  Resolved: ['تم الحل', 'Resolved'],
  Dismissed: ['مرفوضة', 'Dismissed'],
};

const targetLabels: Record<ContentReportTargetKind, BilingualLabel> = {
  Course: ['دورة', 'Course'],
  Review: ['تقييم دورة', 'Course review'],
  Comment: ['تعليق نقاش', 'Discussion comment'],
  ReportedUser: ['حساب مستخدم', 'User account'],
};

const reasonLabels: Record<ContentReportReason, BilingualLabel> = {
  Spam: ['محتوى مزعج', 'Spam'],
  Harassment: ['مضايقة', 'Harassment'],
  HateSpeech: ['خطاب كراهية', 'Hate speech'],
  Misinformation: ['معلومات مضللة', 'Misinformation'],
  Copyright: ['حقوق نشر', 'Copyright'],
  PersonalData: ['بيانات شخصية', 'Personal data'],
  Other: ['سبب آخر', 'Other'],
};

const actionLabels: Record<ModerationActionType, BilingualLabel> = {
  StartReview: ['بدء المراجعة', 'Start review'],
  HideContent: ['إخفاء المحتوى', 'Hide content'],
  RestoreContent: ['استعادة المحتوى', 'Restore content'],
  Resolve: ['حل البلاغ', 'Resolve report'],
  Dismiss: ['رفض البلاغ', 'Dismiss report'],
};

export const moderationStatusLabel = (
  status: ModerationWorkflowStatus,
  locale: SupportedLocale,
): string => statusLabels[status][locale === 'ar' ? 0 : 1];

export const moderationTargetLabel = (
  target: ContentReportTargetKind,
  locale: SupportedLocale,
): string => targetLabels[target][locale === 'ar' ? 0 : 1];

export const reportReasonLabel = (reason: ContentReportReason, locale: SupportedLocale): string =>
  reasonLabels[reason][locale === 'ar' ? 0 : 1];

export const moderationActionLabel = (
  action: ModerationActionType,
  locale: SupportedLocale,
): string => actionLabels[action][locale === 'ar' ? 0 : 1];
