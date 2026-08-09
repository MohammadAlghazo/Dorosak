import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import type {
  ContentLocale,
  CourseLevel,
  CourseLocalization,
  CourseLocalizationInput,
  CourseMetadataRequest,
} from '../../core/api/phase6-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { requiredValidator } from '../auth/auth-form.helpers';
import { CourseEditorStore } from './course-editor.store';

type EditableLevel = CourseLevel | '';

@Component({
  selector: 'drs-course-detail-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="course-detail-title">
      <a class="back-link" [routerLink]="['/', locale.locale(), 'instructor']">
        {{ locale.locale() === 'ar' ? 'العودة إلى الدورات' : 'Back to courses' }}
      </a>
      <header class="workflow-heading workflow-heading-row">
        <div>
          <p class="identity-kicker">
            {{ locale.locale() === 'ar' ? 'بيانات المسودة' : 'Draft metadata' }}
          </p>
          <h1 id="course-detail-title">
            {{
              store.course().value?.localizations?.[0]?.title ??
                (locale.locale() === 'ar' ? 'تفاصيل الدورة' : 'Course details')
            }}
          </h1>
          @if (store.course().value; as course) {
            <p>{{ course.status }} · v{{ course.draftVersion }}</p>
          }
        </div>
        <nav
          class="section-tabs"
          [attr.aria-label]="locale.locale() === 'ar' ? 'أقسام المسودة' : 'Draft sections'"
        >
          <a [routerLink]="['../']" aria-current="page">{{
            locale.locale() === 'ar' ? 'البيانات' : 'Metadata'
          }}</a>
          <a [routerLink]="['../curriculum']">{{
            locale.locale() === 'ar' ? 'المنهج' : 'Curriculum'
          }}</a>
          <a [routerLink]="['../assessments']">{{
            locale.locale() === 'ar' ? 'التقييمات' : 'Assessments'
          }}</a>
          <a [routerLink]="['../media']">{{ locale.locale() === 'ar' ? 'الوسائط' : 'Media' }}</a>
          <a [routerLink]="['../publication']">{{
            locale.locale() === 'ar' ? 'النشر' : 'Publication'
          }}</a>
        </nav>
      </header>

      @switch (store.course().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل البيانات…' : 'Loading metadata…' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar'
                ? 'تعذر تحميل بيانات الدورة.'
                : 'Course metadata could not be loaded.'
            }}
            @if (store.course().errorCode) {
              <code>{{ store.course().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('conflict') {
          <div class="conflict-panel" role="alert" aria-labelledby="metadata-conflict-title">
            <h2 id="metadata-conflict-title">
              {{
                locale.locale() === 'ar'
                  ? 'تغيّرت المسودة في مكان آخر'
                  : 'The draft changed elsewhere'
              }}
            </h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'لم تُحفظ تعديلاتك. أعد تحميل نسخة الخادم قبل المتابعة.'
                  : 'Your edits were not saved. Reload the server version before continuing.'
              }}
            </p>
            @if (store.course().conflictEtag) {
              <code>{{ store.course().conflictEtag }}</code>
            }
            <button class="danger-button" type="button" (click)="reload()">
              {{
                locale.locale() === 'ar'
                  ? 'تجاهل تعديلاتي وإعادة التحميل'
                  : 'Discard my edits and reload'
              }}
            </button>
          </div>
        }
      }

      @if (formError()) {
        <div class="form-alert" role="alert">{{ formError() }}</div>
      }

      @if (store.course().value) {
        <form class="workflow-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <article class="workflow-card">
            <div class="workflow-card-heading">
              <div>
                <h2>{{ locale.locale() === 'ar' ? 'إعدادات الدورة' : 'Course settings' }}</h2>
                <p class="muted">
                  {{
                    locale.locale() === 'ar'
                      ? 'تُحفظ التغييرات تلقائيًا.'
                      : 'Changes save automatically.'
                  }}
                </p>
              </div>
              <span class="save-indicator" role="status">
                {{ saveLabel() }}
              </span>
            </div>
            <div class="form-grid two-columns">
              <div>
                <label for="metadata-default-locale">{{
                  locale.locale() === 'ar' ? 'اللغة الافتراضية' : 'Default language'
                }}</label>
                <select id="metadata-default-locale" formControlName="defaultLocale">
                  <option value="ar">العربية</option>
                  <option value="en">English</option>
                </select>
              </div>
              <div>
                <label for="metadata-level">{{
                  locale.locale() === 'ar' ? 'المستوى' : 'Level'
                }}</label>
                <select
                  id="metadata-level"
                  formControlName="level"
                  aria-describedby="metadata-level-help"
                >
                  <option value="" disabled>
                    {{ locale.locale() === 'ar' ? 'اختر المستوى' : 'Select level' }}
                  </option>
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
                <p id="metadata-level-help" class="field-help">
                  {{
                    locale.locale() === 'ar'
                      ? 'مطلوب قبل الحفظ الأول.'
                      : 'Required before the first save.'
                  }}
                </p>
              </div>
              <div>
                <label for="metadata-categories">{{
                  locale.locale() === 'ar' ? 'رموز التصنيفات' : 'Category codes'
                }}</label>
                <input id="metadata-categories" formControlName="categoryCodes" />
              </div>
              <div>
                <label for="metadata-tags">{{
                  locale.locale() === 'ar' ? 'رموز الوسوم' : 'Tag codes'
                }}</label>
                <input id="metadata-tags" formControlName="tagCodes" />
              </div>
            </div>
          </article>

          <article class="workflow-card localized-editor" dir="rtl">
            <h2>العربية</h2>
            <label for="metadata-ar-title">العنوان</label>
            <input id="metadata-ar-title" formControlName="arTitle" maxlength="200" />
            <label for="metadata-ar-subtitle">العنوان الفرعي</label>
            <input id="metadata-ar-subtitle" formControlName="arSubtitle" maxlength="300" />
            <label for="metadata-ar-description">الوصف</label>
            <textarea
              id="metadata-ar-description"
              formControlName="arDescription"
              rows="6"
              maxlength="10000"
            ></textarea>
            <label for="metadata-ar-slug">Slug</label>
            <input id="metadata-ar-slug" formControlName="arSlug" maxlength="160" dir="ltr" />
          </article>

          <article class="workflow-card localized-editor" dir="ltr">
            <h2>English</h2>
            <label for="metadata-en-title">Title</label>
            <input id="metadata-en-title" formControlName="enTitle" maxlength="200" />
            <label for="metadata-en-subtitle">Subtitle</label>
            <input id="metadata-en-subtitle" formControlName="enSubtitle" maxlength="300" />
            <label for="metadata-en-description">Description</label>
            <textarea
              id="metadata-en-description"
              formControlName="enDescription"
              rows="6"
              maxlength="10000"
            ></textarea>
            <label for="metadata-en-slug">Slug</label>
            <input id="metadata-en-slug" formControlName="enSlug" maxlength="160" />
          </article>

          <button
            class="primary-button"
            type="submit"
            [disabled]="store.course().status === 'saving'"
          >
            {{ locale.locale() === 'ar' ? 'حفظ الآن' : 'Save now' }}
          </button>
        </form>

        @if ((store.course().value?.collaborators?.length ?? 0) > 0) {
          <article class="workflow-card">
            <h2>{{ locale.locale() === 'ar' ? 'المتعاونون' : 'Collaborators' }}</h2>
            <ul class="plain-list">
              @for (
                collaborator of store.course().value?.collaborators ?? [];
                track collaborator.userId
              ) {
                <li>
                  <code>{{ collaborator.userId }}</code
                  ><span>{{ collaborator.role }}</span>
                </li>
              }
            </ul>
          </article>
        }
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseDetailPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(CourseEditorStore);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly courseId = routeCourseId(this.route);
  private syncedVersion = -1;
  protected readonly formError = signal<string | null>(null);
  protected readonly form = new FormGroup({
    defaultLocale: new FormControl<ContentLocale>('ar', { nonNullable: true }),
    level: new FormControl<EditableLevel>('', {
      nonNullable: true,
      validators: [requiredValidator],
    }),
    categoryCodes: textControl(1000),
    tagCodes: textControl(1000),
    arTitle: textControl(200),
    arSubtitle: textControl(300),
    arDescription: textControl(10000),
    arSlug: slugControl(),
    enTitle: textControl(200),
    enSubtitle: textControl(300),
    enDescription: textControl(10000),
    enSlug: slugControl(),
  });

  constructor() {
    this.store.loadCourse(this.courseId);
    effect(() => {
      const course = this.store.course().value;
      if (course === null || course.draftVersion === this.syncedVersion) return;
      this.syncedVersion = course.draftVersion;
      const arabic = course.localizations.find((item) => item.locale === 'ar');
      const english = course.localizations.find((item) => item.locale === 'en');
      this.form.patchValue(
        {
          defaultLocale: course.defaultLocale,
          level: course.level,
          categoryCodes: course.categoryCodes.join(', '),
          tagCodes: course.tagCodes.join(', '),
          arTitle: arabic?.title ?? '',
          arSubtitle: arabic?.subtitle ?? '',
          arDescription: arabic?.description ?? '',
          arSlug: arabic?.slug ?? '',
          enTitle: english?.title ?? '',
          enSubtitle: english?.subtitle ?? '',
          enDescription: english?.description ?? '',
          enSlug: english?.slug ?? '',
        },
        { emitEvent: false },
      );
      this.form.markAsPristine();
    });
    this.form.valueChanges
      .pipe(debounceTime(900), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.form.dirty && this.form.valid) this.save();
      });
  }

  protected save(): void {
    this.form.markAllAsTouched();
    const request = this.requestFromForm();
    if (request === null) return;
    this.formError.set(null);
    this.store.saveMetadata(this.courseId, request);
  }

  protected reload(): void {
    this.formError.set(null);
    this.store.loadCourse(this.courseId);
  }

  protected saveLabel(): string {
    const status = this.store.course().status;
    if (status === 'saving') return this.locale.locale() === 'ar' ? 'جارٍ الحفظ…' : 'Saving…';
    if (status === 'conflict') return this.locale.locale() === 'ar' ? 'تعارض' : 'Conflict';
    return this.locale.locale() === 'ar' ? 'تم الحفظ' : 'Saved';
  }

  private requestFromForm(): CourseMetadataRequest | null {
    const value = this.form.getRawValue();
    const localizations = [
      metadataLocalization(
        'ar',
        value.arTitle,
        value.arSubtitle,
        value.arDescription,
        value.arSlug,
      ),
      metadataLocalization(
        'en',
        value.enTitle,
        value.enSubtitle,
        value.enDescription,
        value.enSlug,
      ),
    ].filter((item): item is CourseLocalizationInput => item !== null);
    if (
      this.form.invalid ||
      value.level === '' ||
      !localizations.some((item) => item.locale === value.defaultLocale)
    ) {
      this.formError.set(
        this.locale.locale() === 'ar'
          ? 'اختر المستوى وأكمل عنوان ووصف اللغة الافتراضية.'
          : 'Select a level and complete the default language title and description.',
      );
      return null;
    }
    return {
      defaultLocale: value.defaultLocale,
      level: value.level,
      localizations,
      categoryCodes: splitCodes(value.categoryCodes),
      tagCodes: splitCodes(value.tagCodes),
    };
  }
}

const routeCourseId = (route: ActivatedRoute): string => {
  const value =
    route.snapshot.paramMap.get('courseId') ?? route.parent?.snapshot.paramMap.get('courseId');
  if (value === null || value === undefined) {
    throw new Error('The course route requires a courseId parameter.');
  }
  return value;
};

const textControl = (maximumLength: number): FormControl<string> =>
  new FormControl('', { nonNullable: true, validators: [Validators.maxLength(maximumLength)] });

const slugControl = (): FormControl<string> =>
  new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(160), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/u)],
  });

const metadataLocalization = (
  locale: ContentLocale,
  title: string,
  subtitle: string,
  description: string,
  slug: string,
): CourseLocalizationInput | null => {
  const normalizedTitle = title.trim();
  const normalizedDescription = description.trim();
  if (normalizedTitle.length === 0 && normalizedDescription.length === 0) return null;
  const value: Omit<CourseLocalization, 'slug'> = {
    locale,
    title: normalizedTitle,
    subtitle: subtitle.trim(),
    description: normalizedDescription,
  };
  const normalizedSlug = slug.trim();
  return normalizedSlug.length > 0 ? { ...value, slug: normalizedSlug } : value;
};

const splitCodes = (value: string): readonly string[] =>
  value
    .split(',')
    .map((code) => code.trim())
    .filter((code, index, values) => code.length > 0 && values.indexOf(code) === index);
