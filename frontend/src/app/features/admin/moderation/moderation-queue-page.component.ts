import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type {
  ContentReportReason,
  ContentReportTargetKind,
  ModerationWorkflowStatus,
} from '../../../core/api/moderation-api.types';
import { LocaleService } from '../../../core/i18n/locale.service';
import { moderationStatusLabel, moderationTargetLabel, reportReasonLabel } from './moderation-copy';
import {
  ModerationStore,
  type ModerationQueueFilters,
  type ModerationQueueKind,
} from './moderation.store';

type StatusFilter = ModerationWorkflowStatus | '';
type TargetFilter = ContentReportTargetKind | '';

@Component({
  selector: 'drs-moderation-queue-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [ModerationStore],
  template: `
    <section
      class="workflow-page workflow-page-wide moderation-page"
      aria-labelledby="moderation-queue-title"
      [attr.aria-busy]="
        store.queue().status === 'loading' || store.queue().status === 'loadingMore'
      "
    >
      <a class="back-link" [routerLink]="['../']">
        {{ locale.locale() === 'ar' ? 'الإدارة' : 'Administration' }}
      </a>
      <header class="workflow-heading moderation-heading">
        <div>
          <p class="identity-kicker">
            {{ locale.locale() === 'ar' ? 'سلامة المجتمع' : 'Community safety' }}
          </p>
          <h1 id="moderation-queue-title">
            {{ locale.locale() === 'ar' ? 'طابور الإشراف' : 'Moderation queue' }}
          </h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'راجع القضايا والبلاغات بحسب الحالة ونوع المحتوى قبل اتخاذ أي إجراء.'
                : 'Review cases and reports by status and content type before taking action.'
            }}
          </p>
        </div>
        <span class="queue-count" aria-live="polite">{{ itemCount() }}</span>
      </header>

      <form
        class="workflow-card workflow-form moderation-filters"
        [formGroup]="filters"
        (ngSubmit)="applyFilters()"
      >
        <div class="filter-grid">
          <div>
            <label for="moderation-source">
              {{ locale.locale() === 'ar' ? 'نوع الطابور' : 'Queue view' }}
            </label>
            <select id="moderation-source" formControlName="kind">
              <option value="cases">
                {{ locale.locale() === 'ar' ? 'قضايا الإشراف' : 'Moderation cases' }}
              </option>
              <option value="reports">
                {{ locale.locale() === 'ar' ? 'البلاغات الواردة' : 'Incoming reports' }}
              </option>
            </select>
          </div>
          <div>
            <label for="moderation-status">
              {{ locale.locale() === 'ar' ? 'الحالة' : 'Status' }}
            </label>
            <select id="moderation-status" formControlName="status">
              <option value="">
                {{ locale.locale() === 'ar' ? 'كل الحالات' : 'All statuses' }}
              </option>
              <option value="Open">{{ statusLabel('Open') }}</option>
              <option value="InReview">{{ statusLabel('InReview') }}</option>
              <option value="Resolved">{{ statusLabel('Resolved') }}</option>
              <option value="Dismissed">{{ statusLabel('Dismissed') }}</option>
            </select>
          </div>
          @if (filters.controls.kind.value === 'reports') {
            <div>
              <label for="moderation-target">
                {{ locale.locale() === 'ar' ? 'نوع المحتوى' : 'Target type' }}
              </label>
              <select id="moderation-target" formControlName="targetKind">
                <option value="">
                  {{ locale.locale() === 'ar' ? 'كل أنواع المحتوى' : 'All target types' }}
                </option>
                <option value="Course">{{ targetLabel('Course') }}</option>
                <option value="Review">{{ targetLabel('Review') }}</option>
                <option value="Comment">{{ targetLabel('Comment') }}</option>
                <option value="ReportedUser">{{ targetLabel('ReportedUser') }}</option>
              </select>
            </div>
          }
        </div>
        <div class="filter-actions">
          <button class="primary-button" type="submit">
            {{ locale.locale() === 'ar' ? 'تطبيق عوامل التصفية' : 'Apply filters' }}
          </button>
          <button class="secondary-button" type="button" (click)="clearFilters()">
            {{ locale.locale() === 'ar' ? 'مسح التصفية' : 'Clear filters' }}
          </button>
        </div>
      </form>

      @switch (store.queue().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جار تحميل الطابور…' : 'Loading moderation queue…' }}
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            {{
              locale.locale() === 'ar'
                ? 'لا توجد عناصر تطابق عوامل التصفية الحالية.'
                : 'No items match the current filters.'
            }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert state-alert" role="alert">
            <span>
              {{
                locale.locale() === 'ar'
                  ? 'لا يمكن تحميل بيانات الإشراف دون اتصال.'
                  : 'Moderation data cannot be loaded while offline.'
              }}
            </span>
            <button class="text-button" type="button" (click)="store.retryQueue()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert state-alert" role="alert">
            <span>
              {{
                locale.locale() === 'ar' ? 'تعذر تحميل الطابور.' : 'The queue could not be loaded.'
              }}
              @if (store.queue().errorCode) {
                <code>{{ store.queue().errorCode }}</code>
              }
            </span>
            <button class="text-button" type="button" (click)="store.retryQueue()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
      }

      <div class="moderation-list">
        @if (store.queue().kind === 'cases') {
          @for (moderationCase of store.queue().cases; track moderationCase.id) {
            <article class="queue-card">
              <div class="queue-card-heading">
                <div>
                  <p class="eyebrow">
                    {{ targetLabel(moderationCase.report.report.targetKind) }} ·
                    {{ reasonLabel(moderationCase.report.report.reason) }}
                  </p>
                  <h2>{{ moderationCase.report.reporterName }}</h2>
                </div>
                <span class="status-chip" [attr.data-status]="moderationCase.status">
                  {{ statusLabel(moderationCase.status) }}
                </span>
              </div>
              <dl class="queue-meta">
                <div>
                  <dt>{{ locale.locale() === 'ar' ? 'معرّف الهدف' : 'Target ID' }}</dt>
                  <dd>
                    <code>{{ moderationCase.report.report.targetId }}</code>
                  </dd>
                </div>
                <div>
                  <dt>{{ locale.locale() === 'ar' ? 'المسند إلى' : 'Assigned to' }}</dt>
                  <dd>
                    {{
                      moderationCase.assignedToName ??
                        (locale.locale() === 'ar' ? 'غير مسندة' : 'Unassigned')
                    }}
                  </dd>
                </div>
                <div>
                  <dt>{{ locale.locale() === 'ar' ? 'تاريخ الإنشاء' : 'Created' }}</dt>
                  <dd>
                    <time [attr.datetime]="moderationCase.createdAt">{{
                      formatDate(moderationCase.createdAt)
                    }}</time>
                  </dd>
                </div>
              </dl>
              @if (moderationCase.report.report.details) {
                <p class="report-details">{{ moderationCase.report.report.details }}</p>
              }
              <a class="case-link" [routerLink]="[moderationCase.id]">
                {{ locale.locale() === 'ar' ? 'فتح القضية' : 'Open case' }}
              </a>
            </article>
          }
        } @else {
          @for (adminReport of store.queue().reports; track adminReport.report.id) {
            <article class="queue-card">
              <div class="queue-card-heading">
                <div>
                  <p class="eyebrow">
                    {{ targetLabel(adminReport.report.targetKind) }} ·
                    {{ reasonLabel(adminReport.report.reason) }}
                  </p>
                  <h2>{{ adminReport.reporterName }}</h2>
                </div>
                <span class="status-chip" [attr.data-status]="adminReport.report.status">
                  {{ statusLabel(adminReport.report.status) }}
                </span>
              </div>
              <dl class="queue-meta">
                <div>
                  <dt>{{ locale.locale() === 'ar' ? 'معرّف الهدف' : 'Target ID' }}</dt>
                  <dd>
                    <code>{{ adminReport.report.targetId }}</code>
                  </dd>
                </div>
                <div>
                  <dt>{{ locale.locale() === 'ar' ? 'حالة القضية' : 'Case status' }}</dt>
                  <dd>{{ statusLabel(adminReport.caseStatus) }}</dd>
                </div>
                <div>
                  <dt>{{ locale.locale() === 'ar' ? 'وقت البلاغ' : 'Reported' }}</dt>
                  <dd>
                    <time [attr.datetime]="adminReport.report.createdAt">{{
                      formatDate(adminReport.report.createdAt)
                    }}</time>
                  </dd>
                </div>
              </dl>
              @if (adminReport.report.details) {
                <p class="report-details">{{ adminReport.report.details }}</p>
              }
              <a class="case-link" [routerLink]="[adminReport.caseId]">
                {{ locale.locale() === 'ar' ? 'فتح القضية المرتبطة' : 'Open linked case' }}
              </a>
            </article>
          }
        }
      </div>

      @if (
        store.queue().hasMore &&
        store.queue().status !== 'error' &&
        store.queue().status !== 'offline'
      ) {
        <button
          class="secondary-button load-more"
          type="button"
          [disabled]="store.queue().status === 'loadingMore'"
          (click)="store.loadMore()"
        >
          {{
            store.queue().status === 'loadingMore'
              ? locale.locale() === 'ar'
                ? 'جار تحميل المزيد…'
                : 'Loading more…'
              : locale.locale() === 'ar'
                ? 'تحميل المزيد'
                : 'Load more'
          }}
        </button>
      }
    </section>
  `,
  styles: `
    .moderation-heading,
    .queue-card-heading,
    .filter-actions,
    .state-alert {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-4);
    }
    .moderation-heading {
      align-items: end;
    }
    .moderation-heading > div {
      min-inline-size: 0;
    }
    .queue-count {
      color: var(--color-brand);
      font: 700 clamp(2.5rem, 7vw, 5rem) / 1 monospace;
    }
    .moderation-filters {
      position: sticky;
      inset-block-start: var(--space-2);
      z-index: 2;
    }
    .filter-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: var(--space-4);
    }
    .filter-grid > div {
      display: grid;
      gap: var(--space-2);
    }
    .filter-actions {
      justify-content: start;
      margin-block-start: var(--space-4);
    }
    .filter-actions .secondary-button {
      margin-block-start: 0;
    }
    .moderation-list {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-4);
      margin-block-start: var(--space-5);
    }
    .queue-card {
      display: flex;
      flex-direction: column;
      min-inline-size: 0;
      padding: clamp(var(--space-4), 3vw, var(--space-6));
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-block-start: 4px solid var(--color-brand);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-1);
    }
    .queue-card-heading {
      align-items: start;
    }
    .queue-card h2,
    .queue-card p {
      margin-block-start: 0;
    }
    .queue-card h2 {
      margin-block-end: var(--space-4);
      font-size: 1.35rem;
    }
    .queue-meta {
      display: grid;
      gap: var(--space-3);
      margin: 0;
    }
    .queue-meta > div {
      display: grid;
      grid-template-columns: minmax(7rem, 0.35fr) minmax(0, 1fr);
      gap: var(--space-3);
      padding-block-end: var(--space-2);
      border-block-end: 1px solid var(--color-border);
    }
    .queue-meta dt {
      color: var(--color-muted);
      font-size: 0.82rem;
    }
    .queue-meta dd {
      min-inline-size: 0;
      margin: 0;
      overflow-wrap: anywhere;
    }
    .report-details {
      display: -webkit-box;
      margin-block: var(--space-4);
      overflow: hidden;
      color: var(--color-muted);
      line-height: 1.65;
      white-space: pre-wrap;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 3;
    }
    .case-link {
      display: inline-flex;
      align-items: center;
      min-block-size: 44px;
      margin-block-start: auto;
      padding-block-start: var(--space-3);
      font-weight: 700;
    }
    code {
      direction: ltr;
      unicode-bidi: isolate;
    }
    .status-chip[data-status='Open'] {
      color: var(--color-warning);
    }
    .status-chip[data-status='InReview'] {
      color: var(--color-link);
    }
    .status-chip[data-status='Resolved'] {
      color: var(--color-success);
    }
    .state-alert {
      align-items: start;
    }
    @media (max-width: 800px) {
      .moderation-list,
      .filter-grid {
        grid-template-columns: 1fr;
      }
      .moderation-filters {
        position: static;
      }
    }
    @media (max-width: 540px) {
      .moderation-heading,
      .queue-card-heading,
      .filter-actions,
      .state-alert {
        align-items: stretch;
        flex-direction: column;
      }
      .queue-count {
        align-self: start;
      }
      .filter-actions button {
        inline-size: 100%;
      }
      .queue-meta > div {
        grid-template-columns: 1fr;
        gap: var(--space-1);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModerationQueuePageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(ModerationStore);
  protected readonly filters = new FormGroup({
    kind: new FormControl<ModerationQueueKind>('cases', { nonNullable: true }),
    status: new FormControl<StatusFilter>('', { nonNullable: true }),
    targetKind: new FormControl<TargetFilter>('', { nonNullable: true }),
  });

  constructor() {
    this.store.loadQueue();
  }

  protected applyFilters(): void {
    const kind = this.filters.controls.kind.value;
    const filter: ModerationQueueFilters = {
      kind,
      status: this.filters.controls.status.value || null,
      targetKind: kind === 'reports' ? this.filters.controls.targetKind.value || null : null,
    };
    this.store.loadQueue(filter);
  }

  protected clearFilters(): void {
    this.filters.reset({ kind: 'cases', status: '', targetKind: '' });
    this.store.loadQueue({ kind: 'cases', status: null, targetKind: null });
  }

  protected itemCount(): number {
    const queue = this.store.queue();
    return queue.kind === 'cases' ? queue.cases.length : queue.reports.length;
  }

  protected statusLabel(status: ModerationWorkflowStatus): string {
    return moderationStatusLabel(status, this.locale.locale());
  }

  protected targetLabel(target: ContentReportTargetKind): string {
    return moderationTargetLabel(target, this.locale.locale());
  }

  protected reasonLabel(reason: ContentReportReason): string {
    return reportReasonLabel(reason, this.locale.locale());
  }

  protected formatDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.valueOf())) return '';
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(date);
  }
}
