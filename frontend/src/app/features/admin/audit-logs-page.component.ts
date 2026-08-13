import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { requiredValidator } from '../auth/auth-form.helpers';
import { AdministrationStore } from './administration.store';

@Component({
  selector: 'drs-audit-logs-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [AdministrationStore],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="audit-title">
      <a class="back-link" [routerLink]="['../']">{{
        locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
      }}</a>
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'قراءة آمنة' : 'Safe read projection' }}
        </p>
        <h1 id="audit-title">{{ locale.locale() === 'ar' ? 'سجل التدقيق' : 'Audit logs' }}</h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'يعرض الحقول التشغيلية الآمنة فقط. كل قراءة تسجل نفسها.'
              : 'Only safe operational fields are shown. Every read is self-audited.'
          }}
        </p>
      </header>
      <form
        class="audit-reason-form workflow-card"
        [formGroup]="form"
        (ngSubmit)="load()"
        novalidate
      >
        <div class="form-grid two-columns">
          <div>
            <label for="audit-reason">{{
              locale.locale() === 'ar' ? 'سبب الوصول' : 'Access reason'
            }}</label
            ><input id="audit-reason" formControlName="reason" minlength="8" maxlength="1000" />
          </div>
          <div>
            <label for="audit-action">{{
              locale.locale() === 'ar' ? 'الإجراء (اختياري)' : 'Action (optional)'
            }}</label
            ><input id="audit-action" formControlName="action" maxlength="200" dir="ltr" />
          </div>
        </div>
        <div class="inline-control">
          <label for="audit-limit">{{ locale.locale() === 'ar' ? 'الحجم' : 'Page size' }}</label
          ><select id="audit-limit" formControlName="limit">
            <option [ngValue]="25">25</option>
            <option [ngValue]="50">50</option>
            <option [ngValue]="100">100</option></select
          ><button class="primary-button" type="submit">
            {{ locale.locale() === 'ar' ? 'تحميل السجل' : 'Load logs' }}
          </button>
        </div>
      </form>
      @if (store.audit().errorCode) {
        <div class="form-alert" role="alert">
          {{ message(store.audit().errorCode) }} <code>{{ store.audit().errorCode }}</code>
        </div>
      }
      @if (store.audit().status === 'loading') {
        <div class="workflow-state" role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل الأحداث…' : 'Loading events…' }}
        </div>
      }
      @if (store.audit().items.length > 0) {
        <div
          class="audit-table-wrap"
          role="region"
          tabindex="0"
          [attr.aria-label]="locale.locale() === 'ar' ? 'جدول أحداث التدقيق' : 'Audit event table'"
        >
          <table>
            <caption class="sr-only">
              {{
                locale.locale() === 'ar' ? 'أحداث التدقيق' : 'Audit events'
              }}
            </caption>
            <thead>
              <tr>
                <th>{{ locale.locale() === 'ar' ? 'الوقت' : 'Time' }}</th>
                <th>{{ locale.locale() === 'ar' ? 'الإجراء' : 'Action' }}</th>
                <th>{{ locale.locale() === 'ar' ? 'الهدف' : 'Target' }}</th>
                <th>{{ locale.locale() === 'ar' ? 'النتيجة' : 'Result' }}</th>
                <th>{{ locale.locale() === 'ar' ? 'السبب' : 'Reason' }}</th>
              </tr>
            </thead>
            <tbody>
              @for (item of store.audit().items; track item.id) {
                <tr>
                  <td>
                    <time [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time>
                  </td>
                  <td>
                    <code>{{ item.action }}</code>
                  </td>
                  <td>
                    <span>{{ item.targetType }}</span
                    ><code dir="ltr">{{ item.targetId }}</code>
                  </td>
                  <td>{{ item.result }}</td>
                  <td>{{ item.reason || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (store.audit().hasMore) {
          <button class="secondary-button load-more" type="button" (click)="loadMore()">
            {{ locale.locale() === 'ar' ? 'عرض المزيد' : 'Load more' }}
          </button>
        }
      } @else if (store.audit().status === 'success') {
        <div class="empty-state">
          {{ locale.locale() === 'ar' ? 'لا توجد أحداث مطابقة.' : 'No matching events.' }}
        </div>
      }
    </section>
  `,
  styles: `
    .audit-table-wrap {
      overflow-x: auto;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
    }
    table {
      inline-size: 100%;
      min-inline-size: 54rem;
      border-collapse: collapse;
    }
    th,
    td {
      padding: var(--space-3) var(--space-4);
      border-block-end: 1px solid var(--color-border);
      text-align: start;
      vertical-align: top;
    }
    th {
      color: var(--color-muted);
      font-size: 0.8rem;
      white-space: nowrap;
    }
    td {
      overflow-wrap: anywhere;
    }
    td > span,
    td > code {
      display: block;
    }
    .inline-control {
      justify-content: start;
      flex-wrap: wrap;
    }
    .inline-control select {
      min-block-size: 44px;
      padding-inline: var(--space-3);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AdministrationStore);
  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.minLength(8), Validators.maxLength(1000)],
    }),
    action: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    limit: new FormControl(50, {
      nonNullable: true,
      validators: [Validators.min(1), Validators.max(100)],
    }),
  });

  protected load(): void {
    this.form.markAllAsTouched();
    if (this.form.controls.reason.invalid) return;
    const value = this.form.getRawValue();
    this.store.loadAudit(value.reason.trim(), value.limit, value.action.trim() || null);
  }

  protected loadMore(): void {
    const value = this.form.getRawValue();
    this.store.loadMoreAudit(value.reason.trim(), value.limit, value.action.trim() || null);
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }
  protected message(code: string | null): string {
    return code === 'AUTH.FORBIDDEN'
      ? this.locale.locale() === 'ar'
        ? 'تحتاج مصادقة مدير حديثة وسبب وصول واضح.'
        : 'A recent admin authentication and clear access reason are required.'
      : this.locale.locale() === 'ar'
        ? 'تعذر تحميل سجل التدقيق.'
        : 'The audit log could not be loaded.';
  }
}
