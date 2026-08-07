import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { InstructorApiClient } from '../../core/api/instructor-api.client';
import type {
  ContentLocale,
  CourseLevel,
  CourseLocalizationInput,
} from '../../core/api/phase6-api.types';
import { LocaleService } from '../../core/i18n/locale.service';

@Component({
  selector: 'drs-course-create-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="workflow-page" aria-labelledby="create-course-title">
      <a class="back-link" [routerLink]="['/', locale.locale(), 'instructor']">
        {{ locale.locale() === 'ar' ? 'العودة إلى الدورات' : 'Back to courses' }}
      </a>
      <header class="workflow-heading">
        <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'مسودة جديدة' : 'New draft' }}</p>
        <h1 id="create-course-title">
          {{ locale.locale() === 'ar' ? 'إنشاء دورة' : 'Create a course' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'ابدأ بلغة افتراضية وبيانات وصفية واضحة. يمكنك إضافة اللغتين الآن.'
              : 'Start with a default language and clear metadata. You can add both localizations now.'
          }}
        </p>
      </header>

      @if (error()) {
        <div class="form-alert" role="alert">
          {{ error() }}
          @if (errorCode()) {
            <code>{{ errorCode() }}</code>
          }
        </div>
      }

      <form class="workflow-form workflow-card" [formGroup]="form" (ngSubmit)="create()" novalidate>
        <div class="form-grid two-columns">
          <div>
            <label for="default-locale">{{
              locale.locale() === 'ar' ? 'اللغة الافتراضية' : 'Default language'
            }}</label>
            <select id="default-locale" formControlName="defaultLocale">
              <option value="ar">العربية</option>
              <option value="en">English</option>
            </select>
          </div>
          <div>
            <label for="course-level">{{ locale.locale() === 'ar' ? 'المستوى' : 'Level' }}</label>
            <select id="course-level" formControlName="level">
              <option value="Beginner">
                {{ locale.locale() === 'ar' ? 'مبتدئ' : 'Beginner' }}
              </option>
              <option value="Intermediate">
                {{ locale.locale() === 'ar' ? 'متوسط' : 'Intermediate' }}
              </option>
              <option value="Advanced">
                {{ locale.locale() === 'ar' ? 'متقدم' : 'Advanced' }}
              </option>
              <option value="AllLevels">
                {{ locale.locale() === 'ar' ? 'كل المستويات' : 'All levels' }}
              </option>
            </select>
          </div>
        </div>

        <fieldset>
          <legend>العربية</legend>
          <label for="ar-title">العنوان</label>
          <input id="ar-title" formControlName="arTitle" maxlength="200" dir="rtl" />
          <label for="ar-subtitle">العنوان الفرعي</label>
          <input id="ar-subtitle" formControlName="arSubtitle" maxlength="300" dir="rtl" />
          <label for="ar-description">الوصف</label>
          <textarea
            id="ar-description"
            formControlName="arDescription"
            rows="5"
            maxlength="10000"
            dir="rtl"
          ></textarea>
        </fieldset>

        <fieldset>
          <legend>English</legend>
          <label for="en-title">Title</label>
          <input id="en-title" formControlName="enTitle" maxlength="200" dir="ltr" />
          <label for="en-subtitle">Subtitle</label>
          <input id="en-subtitle" formControlName="enSubtitle" maxlength="300" dir="ltr" />
          <label for="en-description">Description</label>
          <textarea
            id="en-description"
            formControlName="enDescription"
            rows="5"
            maxlength="10000"
            dir="ltr"
          ></textarea>
        </fieldset>

        <div class="form-grid two-columns">
          <div>
            <label for="category-codes">{{
              locale.locale() === 'ar' ? 'رموز التصنيفات' : 'Category codes'
            }}</label>
            <input
              id="category-codes"
              formControlName="categoryCodes"
              aria-describedby="category-help"
            />
            <p id="category-help" class="field-help">
              {{ locale.locale() === 'ar' ? 'افصل الرموز بفواصل.' : 'Separate codes with commas.' }}
            </p>
          </div>
          <div>
            <label for="tag-codes">{{
              locale.locale() === 'ar' ? 'رموز الوسوم' : 'Tag codes'
            }}</label>
            <input id="tag-codes" formControlName="tagCodes" aria-describedby="tag-help" />
            <p id="tag-help" class="field-help">
              {{ locale.locale() === 'ar' ? 'افصل الرموز بفواصل.' : 'Separate codes with commas.' }}
            </p>
          </div>
        </div>

        <button class="primary-button" type="submit" [disabled]="submitting()">
          {{
            submitting()
              ? locale.locale() === 'ar'
                ? 'جارٍ الإنشاء…'
                : 'Creating…'
              : locale.locale() === 'ar'
                ? 'إنشاء المسودة'
                : 'Create draft'
          }}
        </button>
      </form>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseCreatePageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly api = inject(InstructorApiClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly form = new FormGroup({
    defaultLocale: new FormControl<ContentLocale>('ar', { nonNullable: true }),
    level: new FormControl<CourseLevel>('Beginner', { nonNullable: true }),
    arTitle: textControl(200),
    arSubtitle: textControl(300),
    arDescription: textControl(10000),
    enTitle: textControl(200),
    enSubtitle: textControl(300),
    enDescription: textControl(10000),
    categoryCodes: textControl(1000),
    tagCodes: textControl(1000),
  });

  protected create(): void {
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    const localizations = [
      localization('ar', value.arTitle, value.arSubtitle, value.arDescription),
      localization('en', value.enTitle, value.enSubtitle, value.enDescription),
    ].filter((item): item is CourseLocalizationInput => item !== null);
    const defaultLocalization = localizations.find((item) => item.locale === value.defaultLocale);
    if (
      this.form.invalid ||
      defaultLocalization === undefined ||
      defaultLocalization.title.length < 1 ||
      defaultLocalization.description.length < 1
    ) {
      this.error.set(
        this.locale.locale() === 'ar'
          ? 'أدخل عنوانًا ووصفًا للغة الافتراضية.'
          : 'Enter a title and description for the default language.',
      );
      return;
    }
    if (this.submitting()) return;

    this.error.set(null);
    this.errorCode.set(null);
    this.submitting.set(true);
    this.api
      .createCourse({
        defaultLocale: value.defaultLocale,
        level: value.level,
        localizations,
        categoryCodes: codes(value.categoryCodes),
        tagCodes: codes(value.tagCodes),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          void this.router.navigate([
            '/',
            this.locale.locale(),
            'instructor',
            result.value.courseId,
          ]);
        },
        error: (requestError: unknown) => {
          this.submitting.set(false);
          this.errorCode.set(requestError instanceof ApiProblem ? requestError.code : null);
          this.error.set(
            this.locale.locale() === 'ar'
              ? 'تعذر إنشاء المسودة. راجع البيانات وحاول مجددًا.'
              : 'The draft could not be created. Check the fields and try again.',
          );
        },
      });
  }
}

const textControl = (maximumLength: number): FormControl<string> =>
  new FormControl('', { nonNullable: true, validators: [Validators.maxLength(maximumLength)] });

const localization = (
  locale: ContentLocale,
  title: string,
  subtitle: string,
  description: string,
): CourseLocalizationInput | null => {
  const normalizedTitle = title.trim();
  const normalizedDescription = description.trim();
  if (normalizedTitle.length === 0 && normalizedDescription.length === 0) return null;
  return {
    locale,
    title: normalizedTitle,
    subtitle: subtitle.trim(),
    description: normalizedDescription,
  };
};

const codes = (value: string): readonly string[] =>
  value
    .split(',')
    .map((code) => code.trim())
    .filter((code, index, values) => code.length > 0 && values.indexOf(code) === index);
