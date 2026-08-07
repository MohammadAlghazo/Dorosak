import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { Category, ContentLocale, Tag } from '../../core/api/phase6-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { requiredValidator } from '../auth/auth-form.helpers';
import { AdminPhase6Store } from './admin-phase6.store';

@Component({
  selector: 'drs-taxonomy-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [AdminPhase6Store],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="taxonomy-title">
      <a class="back-link" [routerLink]="['../']">{{
        locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
      }}</a>
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'كتالوج المنصة' : 'Platform catalog' }}
        </p>
        <h1 id="taxonomy-title">
          {{ locale.locale() === 'ar' ? 'إدارة التصنيف' : 'Taxonomy management' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'تحافظ على رموز مستقرة وأسماء عربية وإنجليزية لكل تصنيف ووسم.'
              : 'Maintain stable codes with Arabic and English names for every category and tag.'
          }}
        </p>
      </header>

      @switch (store.taxonomy().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل التصنيف…' : 'Loading taxonomy…' }}
          </div>
        }
        @case ('saving') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ حفظ التغيير…' : 'Saving change…' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'تعذر تحميل التصنيف.' : 'Taxonomy could not be loaded.' }}
            @if (store.taxonomy().errorCode) {
              <code>{{ store.taxonomy().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="store.loadTaxonomy()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('empty') {
          <div class="empty-state">
            {{ locale.locale() === 'ar' ? 'لا توجد عناصر تصنيف.' : 'There are no taxonomy terms.' }}
          </div>
        }
      }

      <div class="taxonomy-grid">
        <article class="workflow-card">
          <div class="workflow-card-heading">
            <h2>
              {{
                categoryEditId()
                  ? locale.locale() === 'ar'
                    ? 'تعديل تصنيف'
                    : 'Edit category'
                  : locale.locale() === 'ar'
                    ? 'تصنيف جديد'
                    : 'New category'
              }}
            </h2>
            @if (categoryEditId()) {
              <button class="text-button" type="button" (click)="resetCategory()">
                {{ locale.locale() === 'ar' ? 'إلغاء التعديل' : 'Cancel edit' }}
              </button>
            }
          </div>
          <form
            class="workflow-form"
            [formGroup]="categoryForm"
            (ngSubmit)="saveCategory()"
            novalidate
          >
            <label for="category-code">Code</label>
            <input
              id="category-code"
              formControlName="code"
              maxlength="80"
              pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
              dir="ltr"
            />
            <label for="category-parent">{{
              locale.locale() === 'ar' ? 'التصنيف الأب (اختياري)' : 'Parent category (optional)'
            }}</label>
            <input id="category-parent" formControlName="parentId" dir="ltr" />
            <label for="category-order">{{
              locale.locale() === 'ar' ? 'ترتيب العرض' : 'Display order'
            }}</label>
            <input id="category-order" type="number" min="0" formControlName="displayOrder" />
            <label class="checkbox-label" for="category-active">
              <input id="category-active" type="checkbox" formControlName="isActive" />
              {{ locale.locale() === 'ar' ? 'تصنيف نشط' : 'Active category' }}
            </label>
            <fieldset>
              <legend>العربية / English</legend>
              <label for="category-ar-name">الاسم بالعربية</label>
              <input id="category-ar-name" formControlName="arName" maxlength="200" dir="rtl" />
              <label for="category-en-name">English name</label>
              <input id="category-en-name" formControlName="enName" maxlength="200" dir="ltr" />
            </fieldset>
            <button
              class="primary-button"
              type="submit"
              [disabled]="store.taxonomy().status === 'saving'"
            >
              {{ locale.locale() === 'ar' ? 'حفظ التصنيف' : 'Save category' }}
            </button>
          </form>
        </article>

        <article class="workflow-card">
          <div class="workflow-card-heading">
            <h2>
              {{
                tagEditId()
                  ? locale.locale() === 'ar'
                    ? 'تعديل وسم'
                    : 'Edit tag'
                  : locale.locale() === 'ar'
                    ? 'وسم جديد'
                    : 'New tag'
              }}
            </h2>
            @if (tagEditId()) {
              <button class="text-button" type="button" (click)="resetTag()">
                {{ locale.locale() === 'ar' ? 'إلغاء التعديل' : 'Cancel edit' }}
              </button>
            }
          </div>
          <form class="workflow-form" [formGroup]="tagForm" (ngSubmit)="saveTag()" novalidate>
            <label for="tag-code">Code</label>
            <input
              id="tag-code"
              formControlName="code"
              maxlength="80"
              pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
              dir="ltr"
            />
            <label class="checkbox-label" for="tag-active">
              <input id="tag-active" type="checkbox" formControlName="isActive" />
              {{ locale.locale() === 'ar' ? 'وسم نشط' : 'Active tag' }}
            </label>
            <fieldset>
              <legend>العربية / English</legend>
              <label for="tag-ar-name">الاسم بالعربية</label>
              <input id="tag-ar-name" formControlName="arName" maxlength="200" dir="rtl" />
              <label for="tag-en-name">English name</label>
              <input id="tag-en-name" formControlName="enName" maxlength="200" dir="ltr" />
            </fieldset>
            <button
              class="primary-button"
              type="submit"
              [disabled]="store.taxonomy().status === 'saving'"
            >
              {{ locale.locale() === 'ar' ? 'حفظ الوسم' : 'Save tag' }}
            </button>
          </form>
        </article>
      </div>

      <div class="taxonomy-grid taxonomy-lists">
        <article class="workflow-card">
          <h2>{{ locale.locale() === 'ar' ? 'التصنيفات الحالية' : 'Current categories' }}</h2>
          <ul class="plain-list">
            @for (category of store.taxonomy().categories; track category.id) {
              <li>
                <div>
                  <strong>{{ category.code }}</strong
                  ><span
                    >{{ localizedName(category.localizations, 'ar') }} /
                    {{ localizedName(category.localizations, 'en') }}</span
                  >
                  @if (!category.isActive) {
                    <span class="status-pill">{{
                      locale.locale() === 'ar' ? 'غير نشط' : 'Inactive'
                    }}</span>
                  }
                </div>
                <button class="secondary-button" type="button" (click)="editCategory(category)">
                  {{ locale.locale() === 'ar' ? 'تعديل' : 'Edit' }}
                </button>
              </li>
            }
          </ul>
        </article>
        <article class="workflow-card">
          <h2>{{ locale.locale() === 'ar' ? 'الوسوم الحالية' : 'Current tags' }}</h2>
          <ul class="plain-list">
            @for (tag of store.taxonomy().tags; track tag.id) {
              <li>
                <div>
                  <strong>{{ tag.code }}</strong
                  ><span
                    >{{ localizedName(tag.localizations, 'ar') }} /
                    {{ localizedName(tag.localizations, 'en') }}</span
                  >
                  @if (!tag.isActive) {
                    <span class="status-pill">{{
                      locale.locale() === 'ar' ? 'غير نشط' : 'Inactive'
                    }}</span>
                  }
                </div>
                <button class="secondary-button" type="button" (click)="editTag(tag)">
                  {{ locale.locale() === 'ar' ? 'تعديل' : 'Edit' }}
                </button>
              </li>
            }
          </ul>
        </article>
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaxonomyPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AdminPhase6Store);
  protected readonly categoryEditId = signal<string | null>(null);
  protected readonly tagEditId = signal<string | null>(null);
  protected readonly categoryForm = new FormGroup({
    code: codeControl(),
    parentId: new FormControl('', { nonNullable: true }),
    displayOrder: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    isActive: new FormControl(true, { nonNullable: true }),
    arName: nameControl(),
    enName: nameControl(),
  });
  protected readonly tagForm = new FormGroup({
    code: codeControl(),
    isActive: new FormControl(true, { nonNullable: true }),
    arName: nameControl(),
    enName: nameControl(),
  });

  constructor() {
    this.store.loadTaxonomy();
  }

  protected saveCategory(): void {
    this.categoryForm.markAllAsTouched();
    if (this.categoryForm.invalid) return;
    const value = this.categoryForm.getRawValue();
    this.store.saveCategory(this.categoryEditId(), {
      code: value.code.trim(),
      parentId: value.parentId.trim() || null,
      displayOrder: value.displayOrder,
      isActive: value.isActive,
      localizations: localizations(value.arName, value.enName),
    });
    this.resetCategory();
  }

  protected saveTag(): void {
    this.tagForm.markAllAsTouched();
    if (this.tagForm.invalid) return;
    const value = this.tagForm.getRawValue();
    this.store.saveTag(this.tagEditId(), {
      code: value.code.trim(),
      isActive: value.isActive,
      localizations: localizations(value.arName, value.enName),
    });
    this.resetTag();
  }

  protected editCategory(category: Category): void {
    this.categoryEditId.set(category.id);
    this.categoryForm.patchValue({
      code: category.code,
      parentId: category.parentId ?? '',
      displayOrder: category.displayOrder,
      isActive: category.isActive,
      arName: findLocalizedName(category.localizations, 'ar'),
      enName: findLocalizedName(category.localizations, 'en'),
    });
  }

  protected editTag(tag: Tag): void {
    this.tagEditId.set(tag.id);
    this.tagForm.patchValue({
      code: tag.code,
      isActive: tag.isActive,
      arName: findLocalizedName(tag.localizations, 'ar'),
      enName: findLocalizedName(tag.localizations, 'en'),
    });
  }

  protected resetCategory(): void {
    this.categoryEditId.set(null);
    this.categoryForm.reset({
      code: '',
      parentId: '',
      displayOrder: 0,
      isActive: true,
      arName: '',
      enName: '',
    });
  }

  protected resetTag(): void {
    this.tagEditId.set(null);
    this.tagForm.reset({ code: '', isActive: true, arName: '', enName: '' });
  }

  protected localizedName(
    items: readonly { locale: string; name: string }[],
    locale: ContentLocale,
  ): string {
    return findLocalizedName(items, locale);
  }
}

const codeControl = (): FormControl<string> =>
  new FormControl('', {
    nonNullable: true,
    validators: [
      requiredValidator,
      Validators.maxLength(80),
      Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/u),
    ],
  });

const nameControl = (): FormControl<string> =>
  new FormControl('', {
    nonNullable: true,
    validators: [requiredValidator, Validators.maxLength(200)],
  });

const localizations = (ar: string, en: string) => [
  { locale: 'ar' as ContentLocale, name: ar.trim() },
  { locale: 'en' as ContentLocale, name: en.trim() },
];

const findLocalizedName = (
  items: readonly { locale: string; name: string }[],
  locale: ContentLocale,
): string => items.find((item) => item.locale === locale)?.name ?? '';
