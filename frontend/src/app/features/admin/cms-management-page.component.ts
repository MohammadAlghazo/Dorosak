import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { CmsFaq } from '../../core/api/cms-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { requiredValidator } from '../auth/auth-form.helpers';
import { AdministrationStore, CMS_PAGE_SLOTS, type CmsPageSlot } from './administration.store';

@Component({
  selector: 'drs-cms-management-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [AdministrationStore],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="cms-admin-title">
      <a class="back-link" [routerLink]="['../']">{{
        locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
      }}</a>
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'المحتوى التحريري' : 'Editorial system' }}
        </p>
        <h1 id="cms-admin-title">
          {{ locale.locale() === 'ar' ? 'إدارة الصفحات والأسئلة' : 'Pages and FAQs' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'مسودات ثنائية اللغة مع نشر واضح وقابل للمراجعة.'
              : 'Bilingual drafts with explicit, reviewable publishing.'
          }}
        </p>
      </header>

      @if (store.cms().status === 'loading') {
        <div class="workflow-state" role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل المحتوى…' : 'Loading content…' }}
        </div>
      }
      @if (store.cms().errorCode) {
        <div class="form-alert" role="alert">
          {{ message(store.cms().errorCode) }} <code>{{ store.cms().errorCode }}</code>
          <button class="text-button" type="button" (click)="store.loadCms()">
            {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
          </button>
        </div>
      }

      <section class="workflow-card" aria-labelledby="pages-title">
        <div class="workflow-card-heading">
          <div>
            <p class="eyebrow">01 / CMS</p>
            <h2 id="pages-title">{{ locale.locale() === 'ar' ? 'الصفحات' : 'Pages' }}</h2>
          </div>
          <span class="status-chip">{{
            selectedPage().currentVersion
              ? 'v' + selectedPage().currentVersion
              : locale.locale() === 'ar'
                ? 'فارغة'
                : 'Empty'
          }}</span>
        </div>
        <div
          class="section-tabs"
          role="tablist"
          [attr.aria-label]="locale.locale() === 'ar' ? 'صفحات المحتوى' : 'Content pages'"
        >
          @for (slot of pageSlots; track slot) {
            <button
              role="tab"
              type="button"
              [attr.aria-selected]="selectedSlug() === slot"
              [class.active-tab]="selectedSlug() === slot"
              (click)="selectPage(slot)"
            >
              {{ slot }}
            </button>
          }
        </div>
        <form
          class="workflow-form localized-editor"
          [formGroup]="pageForm"
          (ngSubmit)="savePage()"
          novalidate
        >
          <div class="two-columns form-grid">
            <div>
              <label for="page-title-ar">العنوان بالعربية</label
              ><input id="page-title-ar" formControlName="titleAr" maxlength="200" dir="rtl" />
            </div>
            <div>
              <label for="page-title-en">English title</label
              ><input id="page-title-en" formControlName="titleEn" maxlength="200" dir="ltr" />
            </div>
            <div>
              <label for="page-body-ar">المحتوى بالعربية</label
              ><textarea
                id="page-body-ar"
                formControlName="bodyAr"
                rows="8"
                maxlength="20000"
                dir="rtl"
              ></textarea>
            </div>
            <div>
              <label for="page-body-en">English body</label
              ><textarea
                id="page-body-en"
                formControlName="bodyEn"
                rows="8"
                maxlength="20000"
                dir="ltr"
              ></textarea>
            </div>
          </div>
          <label for="page-audit-reason">{{
            locale.locale() === 'ar' ? 'سبب التدقيق' : 'Audit reason'
          }}</label>
          <input
            id="page-audit-reason"
            [formControl]="auditReason"
            minlength="8"
            maxlength="1000"
          />
          <div class="action-row">
            <button
              class="primary-button"
              type="submit"
              [disabled]="store.cms().status === 'saving'"
            >
              {{ locale.locale() === 'ar' ? 'حفظ المسودة' : 'Save draft' }}
            </button>
            <button
              class="secondary-button"
              type="button"
              [disabled]="!selectedPage().currentVersion || store.cms().status === 'saving'"
              (click)="publishPage()"
            >
              {{ locale.locale() === 'ar' ? 'نشر النسخة الحالية' : 'Publish current draft' }}
            </button>
          </div>
        </form>
      </section>

      <section class="workflow-card" aria-labelledby="faq-title">
        <div class="workflow-card-heading">
          <div>
            <p class="eyebrow">02 / FAQ</p>
            <h2 id="faq-title">{{ locale.locale() === 'ar' ? 'الأسئلة الشائعة' : 'FAQs' }}</h2>
          </div>
          <button class="text-button" type="button" (click)="newFaq()">
            {{ locale.locale() === 'ar' ? 'سؤال جديد' : 'New FAQ' }}
          </button>
        </div>
        <ul class="plain-list">
          @for (faq of store.cms().faqs; track faq.id) {
            <li>
              <button class="text-button" type="button" (click)="selectFaq(faq)">
                {{
                  faq.draft?.questionEn || faq.published?.questionEn || 'FAQ ' + faq.id.slice(0, 8)
                }}</button
              ><span
                >v{{ faq.currentVersion }}{{ faq.publishedVersion ? ' / published' : '' }}</span
              >
            </li>
          } @empty {
            <li class="muted">
              {{ locale.locale() === 'ar' ? 'لا توجد أسئلة بعد.' : 'No FAQs yet.' }}
            </li>
          }
        </ul>
        <form
          class="workflow-form localized-editor"
          [formGroup]="faqForm"
          (ngSubmit)="saveFaq()"
          novalidate
        >
          <div class="two-columns form-grid">
            <div>
              <label for="faq-order">{{
                locale.locale() === 'ar' ? 'ترتيب العرض' : 'Display order'
              }}</label
              ><input
                id="faq-order"
                type="number"
                min="0"
                max="10000"
                formControlName="displayOrder"
              />
            </div>
            <div></div>
            <div>
              <label for="faq-question-ar">السؤال بالعربية</label
              ><input id="faq-question-ar" formControlName="questionAr" maxlength="300" dir="rtl" />
            </div>
            <div>
              <label for="faq-question-en">English question</label
              ><input id="faq-question-en" formControlName="questionEn" maxlength="300" dir="ltr" />
            </div>
            <div>
              <label for="faq-answer-ar">الإجابة بالعربية</label
              ><textarea
                id="faq-answer-ar"
                formControlName="answerAr"
                rows="5"
                maxlength="5000"
                dir="rtl"
              ></textarea>
            </div>
            <div>
              <label for="faq-answer-en">English answer</label
              ><textarea
                id="faq-answer-en"
                formControlName="answerEn"
                rows="5"
                maxlength="5000"
                dir="ltr"
              ></textarea>
            </div>
          </div>
          <label for="faq-audit-reason">{{
            locale.locale() === 'ar' ? 'سبب التدقيق' : 'Audit reason'
          }}</label>
          <input id="faq-audit-reason" [formControl]="auditReason" minlength="8" maxlength="1000" />
          <div class="action-row">
            <button
              class="primary-button"
              type="submit"
              [disabled]="store.cms().status === 'saving'"
            >
              {{
                selectedFaqId()
                  ? locale.locale() === 'ar'
                    ? 'حفظ المسودة'
                    : 'Save draft'
                  : locale.locale() === 'ar'
                    ? 'إنشاء المسودة'
                    : 'Create draft'
              }}
            </button>
            <button
              class="secondary-button"
              type="button"
              [disabled]="!selectedFaq()?.currentVersion || store.cms().status === 'saving'"
              (click)="publishFaq()"
            >
              {{ locale.locale() === 'ar' ? 'نشر السؤال' : 'Publish FAQ' }}
            </button>
          </div>
        </form>
      </section>
    </section>
  `,
  styles: `
    .section-tabs button {
      min-block-size: 44px;
      padding-inline: var(--space-3);
      color: var(--color-muted);
      background: transparent;
      border: 0;
      border-block-end: 2px solid transparent;
    }
    .section-tabs button.active-tab {
      color: var(--color-text);
      border-color: var(--color-brand);
    }
    .section-tabs {
      margin-block-end: var(--space-4);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CmsManagementPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AdministrationStore);
  protected readonly pageSlots = CMS_PAGE_SLOTS;
  protected readonly selectedSlug = signal<CmsPageSlot>('about');
  protected readonly selectedFaqId = signal<string | null>(null);
  protected readonly auditReason = new FormControl('', {
    nonNullable: true,
    validators: [requiredValidator, Validators.minLength(8), Validators.maxLength(1000)],
  });
  protected readonly pageForm = new FormGroup({
    titleAr: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(200)],
    }),
    titleEn: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(200)],
    }),
    bodyAr: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(20000)],
    }),
    bodyEn: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(20000)],
    }),
  });
  protected readonly faqForm = new FormGroup({
    displayOrder: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.min(0), Validators.max(10000)],
    }),
    questionAr: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(300)],
    }),
    questionEn: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(300)],
    }),
    answerAr: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(5000)],
    }),
    answerEn: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.maxLength(5000)],
    }),
  });
  protected readonly selectedPage = computed(() => {
    const page = this.store.cms().pages.find((item) => item.slug === this.selectedSlug());
    return (
      page ?? {
        id: '',
        slug: this.selectedSlug(),
        currentVersion: 0,
        publishedVersion: null,
        draft: null,
        published: null,
        updatedAt: '',
        publishedAt: null,
      }
    );
  });
  protected readonly selectedFaq = computed(
    () => this.store.cms().faqs.find((faq) => faq.id === this.selectedFaqId()) ?? null,
  );

  constructor() {
    this.store.loadCms();
    effect(() => {
      const page = this.selectedPage();
      this.pageForm.reset(
        {
          titleAr: page.draft?.titleAr ?? page.published?.titleAr ?? '',
          titleEn: page.draft?.titleEn ?? page.published?.titleEn ?? '',
          bodyAr: page.draft?.bodyAr ?? page.published?.bodyAr ?? '',
          bodyEn: page.draft?.bodyEn ?? page.published?.bodyEn ?? '',
        },
        { emitEvent: false },
      );
    });
    effect(() => {
      const faq = this.selectedFaq();
      this.faqForm.reset(
        {
          displayOrder: faq?.displayOrder ?? 0,
          questionAr: faq?.draft?.questionAr ?? faq?.published?.questionAr ?? '',
          questionEn: faq?.draft?.questionEn ?? faq?.published?.questionEn ?? '',
          answerAr: faq?.draft?.answerAr ?? faq?.published?.answerAr ?? '',
          answerEn: faq?.draft?.answerEn ?? faq?.published?.answerEn ?? '',
        },
        { emitEvent: false },
      );
    });
  }

  protected selectPage(slug: CmsPageSlot): void {
    this.selectedSlug.set(slug);
  }

  protected savePage(): void {
    this.pageForm.markAllAsTouched();
    this.auditReason.markAsTouched();
    if (this.pageForm.invalid || this.auditReason.invalid) return;
    const page = this.selectedPage();
    this.store.savePageDraft(
      this.selectedSlug(),
      { expectedVersion: page.currentVersion, ...this.pageForm.getRawValue() },
      this.auditReason.value.trim(),
    );
  }

  protected publishPage(): void {
    if (this.selectedPage().currentVersion <= 0 || this.auditReason.invalid) {
      this.auditReason.markAsTouched();
      return;
    }
    this.store.publishPage(
      this.selectedSlug(),
      this.selectedPage().currentVersion,
      this.auditReason.value.trim(),
    );
  }

  protected newFaq(): void {
    this.selectedFaqId.set(null);
    this.faqForm.reset({
      displayOrder: 0,
      questionAr: '',
      questionEn: '',
      answerAr: '',
      answerEn: '',
    });
  }
  protected selectFaq(faq: CmsFaq): void {
    this.selectedFaqId.set(faq.id);
  }

  protected saveFaq(): void {
    this.faqForm.markAllAsTouched();
    this.auditReason.markAsTouched();
    if (this.faqForm.invalid || this.auditReason.invalid) return;
    const value = this.faqForm.getRawValue();
    const faq = this.selectedFaq();
    if (faq)
      this.store.saveFaqDraft(
        faq.id,
        { expectedVersion: faq.currentVersion, ...value },
        this.auditReason.value.trim(),
      );
    else this.store.createFaqDraft({ expectedVersion: 0, ...value }, this.auditReason.value.trim());
  }

  protected publishFaq(): void {
    const faq = this.selectedFaq();
    if (!faq || faq.currentVersion <= 0 || this.auditReason.invalid) {
      this.auditReason.markAsTouched();
      return;
    }
    this.store.publishFaq(faq.id, faq.currentVersion, this.auditReason.value.trim());
  }

  protected message(code: string | null): string {
    if (code === 'CMS.VERSION_CONFLICT')
      return this.locale.locale() === 'ar'
        ? 'تغير المحتوى. أعد التحميل قبل الحفظ.'
        : 'The content changed. Reload before saving.';
    if (code === 'AUTH.FORBIDDEN')
      return this.locale.locale() === 'ar'
        ? 'تحتاج جلسة مدير حديثة وسبب تدقيق.'
        : 'A recent admin session and audit reason are required.';
    return this.locale.locale() === 'ar'
      ? 'تعذر تنفيذ العملية.'
      : 'The operation could not be completed.';
  }
}
