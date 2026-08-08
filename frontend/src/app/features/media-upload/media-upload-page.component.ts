import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import type { MediaPurpose } from '../../core/api/media-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { MediaUploadStore, type MediaUploadStatus } from './media-upload.store';

@Component({
  selector: 'drs-media-upload-page',
  imports: [RouterLink],
  providers: [MediaUploadStore],
  templateUrl: './media-upload-page.component.html',
  styleUrl: './media-upload-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaUploadPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(MediaUploadStore);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly courseId = routeCourseId(this.route);
  protected readonly purpose = signal<MediaPurpose>('CourseImage');
  protected readonly dragging = signal(false);
  protected readonly announcement = signal('');
  protected readonly acceptedTypes = computed(() => acceptByPurpose[this.purpose()]);
  protected readonly statusText = computed(
    () => statusCopy[this.store.state().status][this.locale.locale()],
  );
  private announcementTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    void this.store.restore(this.courseId);
    effect(() => {
      const status = this.store.state().status;
      const progress = this.store.progressPercent();
      const locale = this.locale.locale();
      clearTimeout(this.announcementTimer);
      this.announcementTimer = setTimeout(() => {
        const label = statusCopy[status][locale];
        this.announcement.set(status === 'uploading' ? `${label} ${String(progress)}%` : label);
      }, 750);
    });
    this.destroyRef.onDestroy(() => {
      clearTimeout(this.announcementTimer);
    });
  }

  protected choosePurpose(event: Event): void {
    this.purpose.set((event.target as HTMLSelectElement).value as MediaPurpose);
  }

  protected chooseFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0);
    if (file) void this.store.selectFile(file, this.purpose(), this.courseId);
    input.value = '';
  }

  protected drop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files.item(0);
    if (file) void this.store.selectFile(file, this.purpose(), this.courseId);
  }

  protected allowDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy';
  }

  protected leaveDrop(): void {
    this.dragging.set(false);
  }

  protected activateFileInput(event: KeyboardEvent, input: HTMLInputElement): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    input.click();
  }

  protected errorText(code: string | null): string {
    const copy = code ? errorCopy[code] : undefined;
    return (copy ?? errorCopy['MEDIA.UPLOAD_FAILED'])?.[this.locale.locale()] ?? '';
  }

  protected formatBytes(bytes: number): string {
    if (bytes < 1024) return `${String(bytes)} B`;
    const units = ['KB', 'MB', 'GB'];
    let value = bytes / 1024;
    let unit = units[0] ?? 'KB';
    for (let index = 1; value >= 1024 && index < units.length; index++) {
      value /= 1024;
      unit = units[index] ?? unit;
    }
    return `${new Intl.NumberFormat(this.locale.locale(), { maximumFractionDigits: 1 }).format(value)} ${unit}`;
  }
}

const routeCourseId = (route: ActivatedRoute): string => {
  const value =
    route.snapshot.paramMap.get('courseId') ?? route.parent?.snapshot.paramMap.get('courseId');
  if (!value) throw new Error('The media route requires a courseId parameter.');
  return value;
};

const acceptByPurpose: Readonly<Record<MediaPurpose, string>> = {
  ProfileImage: 'image/jpeg,image/png,image/webp',
  CourseImage: 'image/jpeg,image/png,image/webp',
  CourseDocument: 'application/pdf',
  AssignmentSubmission: 'application/pdf',
  SourceVideo: 'video/mp4,video/quicktime',
};

const statusCopy: Readonly<Record<MediaUploadStatus, { ar: string; en: string }>> = {
  idle: { ar: 'اختر ملفًا للبدء', en: 'Choose a file to begin' },
  validating: { ar: 'جارٍ إعداد الملف والتحقق من البصمة', en: 'Preparing and hashing the file' },
  uploading: { ar: 'جارٍ الرفع', en: 'Uploading' },
  paused: { ar: 'الرفع متوقف مؤقتًا', en: 'Upload paused' },
  finalizing: { ar: 'جارٍ إنهاء الرفع', en: 'Finalizing upload' },
  scanning: { ar: 'جارٍ فحص الملف', en: 'Scanning file' },
  processing: { ar: 'جارٍ تجهيز الوسائط', en: 'Processing media' },
  ready: { ar: 'الوسائط جاهزة', en: 'Media ready' },
  rejected: { ar: 'رُفض الملف', en: 'File rejected' },
  cancelled: { ar: 'أُلغي الرفع', en: 'Upload cancelled' },
  error: { ar: 'تعذر إكمال الرفع', en: 'Upload could not be completed' },
  offline: { ar: 'توقف الرفع لعدم وجود اتصال', en: 'Upload paused while offline' },
};

const errorCopy: Readonly<Record<string, { ar: string; en: string }>> = {
  'MEDIA.ACTIVE_UPLOAD': {
    ar: 'أعد اختيار الملف نفسه أو ألغِ جلسة الرفع الحالية أولًا.',
    en: 'Reselect the same file or cancel the current upload session first.',
  },
  'MEDIA.CANCEL_REQUIRES_ONLINE': {
    ar: 'اتصل بالإنترنت لإلغاء الجلسة على الخادم.',
    en: 'Reconnect to cancel the server upload session.',
  },
  'MEDIA.CHECKSUM_MISMATCH': {
    ar: 'لا تطابق بصمة الملف البيانات المرفوعة.',
    en: 'The file checksum did not match the uploaded bytes.',
  },
  'MEDIA.EMPTY_FILE': { ar: 'لا يمكن رفع ملف فارغ.', en: 'An empty file cannot be uploaded.' },
  'MEDIA.ETAG_MISSING': {
    ar: 'لم تُرجع خدمة التخزين معرّف الجزء المطلوب.',
    en: 'Storage did not expose the required part identifier.',
  },
  'MEDIA.FILE_RESELECT_REQUIRED': {
    ar: 'أعد اختيار الملف المحلي نفسه لمتابعة الرفع.',
    en: 'Reselect the same local file to continue uploading.',
  },
  'MEDIA.FILE_TOO_LARGE': {
    ar: 'يتجاوز حجم الملف الحد المسموح لهذا النوع.',
    en: 'The file exceeds the size hint for this media type.',
  },
  'MEDIA.FILE_TYPE_HINT': {
    ar: 'لا يتوافق نوع الملف مع الصيغ المقترحة.',
    en: 'The file type does not match the suggested formats.',
  },
  'MEDIA.OFFLINE': {
    ar: 'لا تُخزّن بيانات الملف دون اتصال. اتصل ثم تابع يدويًا.',
    en: 'File bytes are not stored offline. Reconnect, then resume manually.',
  },
  'MEDIA.REJECTED': {
    ar: 'رفض الفحص أو التجهيز هذا الملف.',
    en: 'Scanning or processing rejected this file.',
  },
  'MEDIA.SESSION_EXPIRED': {
    ar: 'انتهت جلسة الرفع. اختر الملف لبدء جلسة جديدة.',
    en: 'The upload session expired. Choose the file to start a new session.',
  },
  'MEDIA.SIGNED_URL_EXPIRED': {
    ar: 'انتهت صلاحية رابط الجزء. ألغِ الجلسة وابدأ من جديد.',
    en: 'The part URL expired. Cancel the session and start again.',
  },
  'MEDIA.STORAGE_UPLOAD_FAILED': {
    ar: 'تعذر رفع الجزء إلى التخزين. حاول مجددًا.',
    en: 'The part could not be uploaded to storage. Retry it.',
  },
  'MEDIA.UPLOAD_FAILED': {
    ar: 'تعذر إكمال الرفع. حاول مجددًا أو ألغِ الجلسة.',
    en: 'The upload could not be completed. Retry or cancel the session.',
  },
};
