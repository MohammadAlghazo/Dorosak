import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-admin-page',
  imports: [RouterLink],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="admin-title">
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'مركز العمليات' : 'Operations desk' }}
        </p>
        <h1 id="admin-title">
          {{ locale.locale() === 'ar' ? 'إدارة المحتوى' : 'Content administration' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'اختر مساحة مراجعة أو إدارة متاحة لصلاحياتك.'
              : 'Choose a review or management workspace available to your permissions.'
          }}
        </p>
      </header>
      <div class="admin-card-grid">
        @if (session.hasPermission('TeacherApplication.ReviewAny')) {
          <a class="admin-action-card" [routerLink]="['teacher-applications']">
            <span>01</span>
            <h2>{{ locale.locale() === 'ar' ? 'طلبات المدرسين' : 'Teacher applications' }}</h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'بدء المراجعة واتخاذ القرار.'
                  : 'Start reviews and record decisions.'
              }}
            </p>
          </a>
        }
        @if (session.hasPermission('Course.ReviewAny')) {
          <a class="admin-action-card" [routerLink]="['publication-reviews']">
            <span>02</span>
            <h2>{{ locale.locale() === 'ar' ? 'مراجعات النشر' : 'Publication reviews' }}</h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'اعتماد المسودات أو طلب التغييرات.'
                  : 'Approve drafts or request changes.'
              }}
            </p>
          </a>
        }
        @if (session.hasPermission('Catalog.ManageTaxonomy')) {
          <a class="admin-action-card" [routerLink]="['taxonomy']">
            <span>03</span>
            <h2>{{ locale.locale() === 'ar' ? 'التصنيف' : 'Taxonomy' }}</h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'إدارة التصنيفات والوسوم ثنائية اللغة.'
                  : 'Manage bilingual categories and tags.'
              }}
            </p>
          </a>
        }
        @if (session.hasPermission('Moderation.ReviewAny')) {
          <a class="admin-action-card" [routerLink]="['moderation']">
            <span>04</span>
            <h2>{{ locale.locale() === 'ar' ? 'الإشراف على المحتوى' : 'Content moderation' }}</h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'فرز البلاغات ومراجعة القضايا وتسجيل الإجراءات.'
                  : 'Triage reports, review cases, and record actions.'
              }}
            </p>
          </a>
        }
        @if (session.hasPermission('Analytics.Read')) {
          <a class="admin-action-card" [routerLink]="['analytics']">
            <span>05</span>
            <h2>{{ locale.locale() === 'ar' ? 'نبض المنصة' : 'Platform pulse' }}</h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'مؤشرات مجمعة وصحة طوابير الديمو.'
                  : 'Aggregate indicators and demo queue health.'
              }}
            </p>
          </a>
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly session = inject(SessionStore);
}
