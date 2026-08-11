import { CdkTrapFocus } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import type { Announcement } from '../../core/api/communications-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { AnnouncementsStore } from './announcements.store';

interface AnnouncementDraft {
  readonly id: string;
  readonly expectedVersion: number;
  readonly title: string;
  readonly body: string;
}

@Component({
  selector: 'drs-announcements-page',
  imports: [CdkTrapFocus, FormsModule, RouterLink],
  providers: [AnnouncementsStore],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="announcements-title">
      <a class="back-link" [routerLink]="['../']">{{ copy().back }}</a>
      <header class="workflow-heading workflow-heading-row">
        <div>
          <p class="identity-kicker">{{ copy().kicker }}</p>
          <h1 id="announcements-title">{{ locale.copy().announcements }}</h1>
          <p>{{ copy().intro }}</p>
        </div>
        <nav class="section-tabs" [attr.aria-label]="copy().sections">
          <a [routerLink]="['../']">{{ copy().metadata }}</a>
          <a [routerLink]="['../curriculum']">{{ copy().curriculum }}</a>
          <a [routerLink]="['../assessments']">{{ copy().assessments }}</a>
          <a [routerLink]="['../media']">{{ copy().media }}</a>
          <a [routerLink]="['../announcements']" aria-current="page">{{
            locale.copy().announcements
          }}</a>
          <a [routerLink]="['../publication']">{{ copy().publication }}</a>
        </nav>
      </header>

      <form class="announcement-form" (ngSubmit)="createAnnouncement()" novalidate>
        <div class="form-heading">
          <div>
            <p class="eyebrow">{{ copy().compose }}</p>
            <h2>{{ copy().createTitle }}</h2>
          </div>
          <span>{{ formatNumber(createBody.length) }} / 10,000</span>
        </div>
        <label for="announcement-title">{{ copy().titleLabel }}</label>
        <input
          id="announcement-title"
          name="announcementTitle"
          maxlength="200"
          required
          dir="auto"
          [disabled]="store.action().status === 'saving' && store.action().operation === 'create'"
          [(ngModel)]="createTitle"
          (ngModelChange)="createChanged()"
        />
        <label for="announcement-body">{{ copy().bodyLabel }}</label>
        <textarea
          id="announcement-body"
          name="announcementBody"
          rows="6"
          maxlength="10000"
          required
          dir="auto"
          [disabled]="store.action().status === 'saving' && store.action().operation === 'create'"
          [(ngModel)]="createBody"
          (ngModelChange)="createChanged()"
        ></textarea>
        <button
          class="primary-button"
          type="submit"
          [disabled]="
            !validContent(createTitle, createBody) ||
            (store.action().status === 'saving' && store.action().operation === 'create')
          "
        >
          {{
            store.action().status === 'saving' && store.action().operation === 'create'
              ? copy().publishing
              : copy().publish
          }}
        </button>
      </form>

      @if (store.action().status === 'conflict') {
        <div class="conflict-panel" role="alert">
          <h2>{{ copy().conflictTitle }}</h2>
          <p>{{ copy().conflictBody }}</p>
          <button class="text-button" type="button" (click)="store.refresh()">
            {{ copy().refreshList }}
          </button>
        </div>
      }
      @if (store.action().status === 'error' || store.action().status === 'offline') {
        <div class="form-alert" role="alert">
          {{ store.action().status === 'offline' ? copy().offline : copy().mutationFailed }}
          @if (store.action().errorCode) {
            <code>{{ store.action().errorCode }}</code>
          }
        </div>
      }

      @switch (store.state().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">{{ copy().loading }}</div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ copy().offline }}
            <button class="text-button" type="button" (click)="store.load(courseId)">
              {{ copy().retry }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{ copy().loadFailed }}
            @if (store.state().errorCode) {
              <code>{{ store.state().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="store.load(courseId)">
              {{ copy().retry }}
            </button>
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            <h2>{{ copy().emptyTitle }}</h2>
            <p>{{ copy().emptyBody }}</p>
          </div>
        }
      }

      <div class="announcement-list">
        @for (announcement of store.state().items; track announcement.id) {
          <article class="announcement-card">
            @if (editDraft()?.id === announcement.id) {
              <form class="edit-form" (ngSubmit)="saveEdit()" novalidate>
                <div class="form-heading">
                  <h2>{{ copy().editTitle }}</h2>
                  <span>v{{ editDraft()?.expectedVersion }}</span>
                </div>
                <label [for]="'edit-title-' + announcement.id">{{ copy().titleLabel }}</label>
                <input
                  [id]="'edit-title-' + announcement.id"
                  name="editTitle"
                  maxlength="200"
                  required
                  dir="auto"
                  [disabled]="store.action().status === 'saving'"
                  [ngModel]="editDraft()?.title"
                  (ngModelChange)="updateDraft('title', $event)"
                />
                <label [for]="'edit-body-' + announcement.id">{{ copy().bodyLabel }}</label>
                <textarea
                  [id]="'edit-body-' + announcement.id"
                  name="editBody"
                  rows="6"
                  maxlength="10000"
                  required
                  dir="auto"
                  [disabled]="store.action().status === 'saving'"
                  [ngModel]="editDraft()?.body"
                  (ngModelChange)="updateDraft('body', $event)"
                ></textarea>
                <div class="action-row">
                  <button class="secondary-button" type="button" (click)="cancelEdit()">
                    {{ copy().cancel }}
                  </button>
                  <button
                    class="primary-button"
                    type="submit"
                    [disabled]="
                      !validContent(editDraft()?.title || '', editDraft()?.body || '') ||
                      store.action().status === 'saving'
                    "
                  >
                    {{ store.action().status === 'saving' ? copy().saving : copy().save }}
                  </button>
                </div>
              </form>
            } @else {
              <header>
                <div>
                  <span class="version-label">v{{ announcement.version }}</span>
                  <h2 dir="auto">{{ announcement.title }}</h2>
                </div>
                <span class="target-count">
                  {{ formatNumber(announcement.targetCount) }} {{ copy().recipients }}
                </span>
              </header>
              <p class="announcement-body" dir="auto">{{ announcement.body }}</p>
              <footer>
                <time [attr.datetime]="announcement.updatedAt">{{
                  formatDate(announcement.updatedAt)
                }}</time>
                <div>
                  <button class="text-button" type="button" (click)="edit(announcement)">
                    {{ copy().edit }}
                  </button>
                  <button
                    class="delete-link"
                    type="button"
                    (click)="deleteCandidate.set(announcement)"
                  >
                    {{ copy().delete }}
                  </button>
                </div>
              </footer>
            }
          </article>
        }
      </div>

      @if (store.state().hasMore) {
        <button
          class="secondary-button load-more"
          type="button"
          [disabled]="store.state().status === 'loadingMore'"
          (click)="store.loadMore()"
        >
          {{ store.state().status === 'loadingMore' ? copy().loadingMore : copy().more }}
        </button>
      }

      @if (deleteCandidate(); as candidate) {
        <div class="confirmation-backdrop">
          <section
            class="delete-confirmation"
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="delete-title"
            cdkTrapFocus
            [cdkTrapFocusAutoCapture]="true"
          >
            <h2 id="delete-title">{{ copy().deleteTitle }}</h2>
            <p dir="auto">{{ candidate.title }}</p>
            <p>{{ copy().deleteBody }}</p>
            <div class="action-row">
              <button class="secondary-button" type="button" (click)="deleteCandidate.set(null)">
                {{ copy().cancel }}
              </button>
              <button
                class="danger-button"
                type="button"
                [disabled]="store.action().status === 'saving'"
                (click)="confirmDelete(candidate)"
              >
                {{ store.action().status === 'saving' ? copy().deleting : copy().confirmDelete }}
              </button>
            </div>
          </section>
        </div>
      }
    </section>
  `,
  styles: `
    .announcement-form,
    .announcement-card {
      margin-block-end: var(--space-5);
      padding: clamp(var(--space-4), 3vw, var(--space-6));
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-1);
    }
    .announcement-form {
      display: grid;
      gap: var(--space-3);
      border-block-start: 5px solid var(--color-brand);
    }
    .form-heading,
    .announcement-card > header,
    .announcement-card > footer {
      display: flex;
      justify-content: space-between;
      align-items: start;
      gap: var(--space-4);
    }
    .form-heading h2,
    .form-heading p,
    .announcement-card h2 {
      margin-block: 0 var(--space-1);
    }
    .form-heading span,
    .target-count,
    .announcement-card time {
      color: var(--color-muted);
      font-size: 0.84rem;
    }
    .announcement-form label,
    .edit-form label {
      font-weight: 650;
    }
    .announcement-form input,
    .announcement-form textarea,
    .edit-form input,
    .edit-form textarea {
      inline-size: 100%;
      min-block-size: 48px;
      padding: var(--space-3);
      color: var(--color-text);
      background: var(--color-canvas);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-1);
      resize: vertical;
    }
    .announcement-form .primary-button {
      justify-self: start;
      min-inline-size: 10rem;
    }
    .announcement-list {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-4);
    }
    .announcement-card {
      display: flex;
      flex-direction: column;
      min-inline-size: 0;
      margin: 0;
    }
    .announcement-card h2,
    .announcement-body {
      overflow-wrap: anywhere;
    }
    .announcement-body {
      flex: 1;
      white-space: pre-wrap;
    }
    .announcement-card > footer {
      align-items: center;
      margin-block-start: var(--space-4);
      padding-block-start: var(--space-3);
      border-block-start: 1px solid var(--color-border);
    }
    .delete-link {
      min-block-size: 44px;
      padding-inline: var(--space-2);
      color: var(--color-danger);
      background: transparent;
      border: 0;
      text-decoration: underline;
    }
    .edit-form {
      display: grid;
      gap: var(--space-3);
    }
    .confirmation-backdrop {
      position: fixed;
      inset: 0;
      z-index: var(--z-overlay);
      display: grid;
      place-items: center;
      padding: var(--space-4);
      background: rgb(0 0 0 / 58%);
    }
    .delete-confirmation {
      inline-size: min(100%, 32rem);
      padding: var(--space-5);
      background: var(--color-surface);
      border: 1px solid var(--color-danger);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-2);
    }
    .delete-confirmation h2 {
      margin-block-start: 0;
    }
    @media (max-width: 760px) {
      .announcement-list {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    @media (max-width: 520px) {
      .form-heading,
      .announcement-card > header,
      .announcement-card > footer {
        align-items: stretch;
        flex-direction: column;
      }
      .announcement-form .primary-button {
        inline-size: 100%;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AnnouncementsStore);
  protected readonly courseId = routeCourseId(inject(ActivatedRoute));
  protected readonly editDraft = signal<AnnouncementDraft | null>(null);
  protected readonly deleteCandidate = signal<Announcement | null>(null);
  protected createTitle = '';
  protected createBody = '';
  private createKey: string | null = null;
  private editKey: string | null = null;

  constructor() {
    this.store.load(this.courseId);
    effect(() => {
      const action = this.store.action();
      if (action.status !== 'success') return;
      if (action.operation === 'create') {
        this.createTitle = '';
        this.createBody = '';
        this.createKey = null;
      }
      if (action.operation === 'update') {
        this.editDraft.set(null);
        this.editKey = null;
      }
      if (action.operation === 'delete') this.deleteCandidate.set(null);
      this.store.resetAction();
    });
  }

  protected copy(): (typeof announcementCopy)[keyof typeof announcementCopy] {
    return announcementCopy[this.locale.locale()];
  }

  protected validContent(title: string, body: string): boolean {
    const normalizedTitle = title.trim();
    const normalizedBody = body.trim();
    return (
      normalizedTitle.length > 0 &&
      normalizedTitle.length <= 200 &&
      normalizedBody.length > 0 &&
      normalizedBody.length <= 10_000
    );
  }

  protected createChanged(): void {
    if (this.store.action().status !== 'saving') this.createKey = null;
  }

  protected createAnnouncement(): void {
    if (!this.validContent(this.createTitle, this.createBody)) return;
    this.createKey ??= globalThis.crypto.randomUUID();
    this.store.create(this.courseId, this.createTitle, this.createBody, this.createKey);
  }

  protected edit(announcement: Announcement): void {
    this.store.resetAction();
    this.editKey = null;
    this.editDraft.set({
      id: announcement.id,
      expectedVersion: announcement.version,
      title: announcement.title,
      body: announcement.body,
    });
  }

  protected updateDraft(field: 'title' | 'body', value: string): void {
    this.editDraft.update((draft) => (draft ? { ...draft, [field]: value } : null));
    if (this.store.action().status !== 'saving') this.editKey = null;
  }

  protected saveEdit(): void {
    const draft = this.editDraft();
    if (!draft || !this.validContent(draft.title, draft.body)) return;
    this.editKey ??= globalThis.crypto.randomUUID();
    this.store.update(
      this.courseId,
      draft.id,
      draft.title,
      draft.body,
      draft.expectedVersion,
      this.editKey,
    );
  }

  protected cancelEdit(): void {
    this.editDraft.set(null);
    this.editKey = null;
    this.store.resetAction();
  }

  protected confirmDelete(announcement: Announcement): void {
    this.store.delete(this.courseId, announcement.id, announcement.version);
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  protected formatNumber(value: number): string {
    return new Intl.NumberFormat(this.locale.locale()).format(value);
  }
}

const routeCourseId = (route: ActivatedRoute): string => {
  const value =
    route.snapshot.paramMap.get('courseId') ?? route.parent?.snapshot.paramMap.get('courseId');
  if (!value) throw new Error('The announcements route requires a courseId parameter.');
  return value;
};

const announcementCopy = {
  ar: {
    back: 'العودة إلى بيانات الدورة',
    kicker: 'موجز الدورة',
    intro: 'اكتب تحديثًا واضحًا يصل إلى الطلاب المؤهلين، مع نسخة مستقلة لكل تعديل.',
    sections: 'أقسام المسودة',
    metadata: 'البيانات',
    curriculum: 'المنهج',
    assessments: 'التقييمات',
    media: 'الوسائط',
    publication: 'النشر',
    compose: 'إعلان جديد',
    createTitle: 'انشر تحديثًا للطلاب',
    titleLabel: 'عنوان الإعلان',
    bodyLabel: 'نص الإعلان',
    publish: 'نشر الإعلان',
    publishing: 'جارٍ النشر…',
    loading: 'جارٍ تحميل الإعلانات…',
    loadingMore: 'جارٍ تحميل المزيد…',
    retry: 'إعادة المحاولة',
    offline: 'لا يمكن تعديل الإعلانات أثناء عدم الاتصال.',
    loadFailed: 'تعذر تحميل الإعلانات.',
    mutationFailed: 'تعذر حفظ عملية الإعلان.',
    emptyTitle: 'لا إعلانات حتى الآن',
    emptyBody: 'استخدم النموذج أعلاه لنشر أول تحديث لهذه الدورة.',
    recipients: 'مستلمًا',
    edit: 'تعديل',
    delete: 'حذف',
    editTitle: 'تعديل الإعلان',
    cancel: 'إلغاء',
    save: 'حفظ التعديل',
    saving: 'جارٍ الحفظ…',
    more: 'إعلانات أقدم',
    conflictTitle: 'تغيّرت نسخة الإعلان',
    conflictBody: 'لم نفقد مسودتك. حدّث القائمة وقارن النسخة الجديدة قبل إعادة الحفظ.',
    refreshList: 'تحديث القائمة مع إبقاء المسودة',
    deleteTitle: 'تأكيد حذف الإعلان',
    deleteBody: 'الحذف نهائي ويستخدم النسخة الحالية لمنع حذف تعديل أحدث بالخطأ.',
    confirmDelete: 'حذف الإعلان',
    deleting: 'جارٍ الحذف…',
  },
  en: {
    back: 'Back to course metadata',
    kicker: 'Course bulletin',
    intro:
      'Write a clear update for eligible learners, with an independent version for every edit.',
    sections: 'Draft sections',
    metadata: 'Metadata',
    curriculum: 'Curriculum',
    assessments: 'Assessments',
    media: 'Media',
    publication: 'Publication',
    compose: 'New announcement',
    createTitle: 'Publish an update for learners',
    titleLabel: 'Announcement title',
    bodyLabel: 'Announcement body',
    publish: 'Publish announcement',
    publishing: 'Publishing…',
    loading: 'Loading announcements…',
    loadingMore: 'Loading more…',
    retry: 'Retry',
    offline: 'Announcements cannot be changed while offline.',
    loadFailed: 'Announcements could not be loaded.',
    mutationFailed: 'The announcement operation could not be saved.',
    emptyTitle: 'No announcements yet',
    emptyBody: 'Use the form above to publish the first update for this course.',
    recipients: 'recipients',
    edit: 'Edit',
    delete: 'Delete',
    editTitle: 'Edit announcement',
    cancel: 'Cancel',
    save: 'Save changes',
    saving: 'Saving…',
    more: 'Older announcements',
    conflictTitle: 'The announcement version changed',
    conflictBody:
      'Your draft is preserved. Refresh the list and compare the new version before saving again.',
    refreshList: 'Refresh list and keep draft',
    deleteTitle: 'Confirm announcement deletion',
    deleteBody:
      'Deletion is final and uses the current version to avoid removing a newer edit by mistake.',
    confirmDelete: 'Delete announcement',
    deleting: 'Deleting…',
  },
} as const;
