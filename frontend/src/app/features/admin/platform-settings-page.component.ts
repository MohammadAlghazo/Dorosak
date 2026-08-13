import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocaleService } from '../../core/i18n/locale.service';
import { requiredValidator } from '../auth/auth-form.helpers';
import { AdministrationStore } from './administration.store';

@Component({
  selector: 'drs-platform-settings-page',
  imports: [ReactiveFormsModule, RouterLink],
  providers: [AdministrationStore],
  template: `
    <section class="workflow-page" aria-labelledby="platform-settings-title">
      <a class="back-link" [routerLink]="['../']">{{
        locale.locale() === 'ar' ? 'الإدارة' : 'Administration'
      }}</a>
      <header class="workflow-heading">
        <p class="identity-kicker">
          {{ locale.locale() === 'ar' ? 'إعدادات العرض' : 'Showcase configuration' }}
        </p>
        <h1 id="platform-settings-title">
          {{ locale.locale() === 'ar' ? 'إعدادات المنصة' : 'Platform settings' }}
        </h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'تتحكم في مساحة العرض العامة دون خلطها بإعدادات النشر.'
              : 'Control the public showcase without mixing it with deployment configuration.'
          }}
        </p>
      </header>
      @if (store.settings().status === 'loading') {
        <div class="workflow-state" role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل الإعدادات…' : 'Loading settings…' }}
        </div>
      }
      @if (store.settings().errorCode) {
        <div class="form-alert" role="alert">
          {{ message(store.settings().errorCode) }} <code>{{ store.settings().errorCode }}</code
          ><button class="text-button" type="button" (click)="store.loadSettings()">
            {{ locale.locale() === 'ar' ? 'إعادة التحميل' : 'Reload' }}
          </button>
        </div>
      }
      @if (store.settings().status === 'conflict') {
        <div class="conflict-panel" role="alert">
          <h2>{{ locale.locale() === 'ar' ? 'تعارض في النسخة' : 'Version conflict' }}</h2>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'احتفظنا بقيمك المحلية. أعد التحميل لمراجعة النسخة الجديدة قبل المحاولة.'
                : 'Your local values are preserved. Reload to review the new version before trying again.'
            }}
          </p>
          <button class="secondary-button" type="button" (click)="store.loadSettings()">
            {{ locale.locale() === 'ar' ? 'إعادة التحميل' : 'Reload authoritative values' }}
          </button>
        </div>
      }
      <form class="workflow-card workflow-form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="form-grid two-columns">
          <div>
            <label for="featured-limit">{{
              locale.locale() === 'ar' ? 'عدد المقررات المميزة' : 'Featured course limit'
            }}</label
            ><input
              id="featured-limit"
              type="number"
              min="1"
              max="12"
              formControlName="featuredCourseLimit"
            />
            <p class="field-help">1–12</p>
          </div>
          <label class="checkbox-label" for="show-notice"
            ><input id="show-notice" type="checkbox" formControlName="showPortfolioNotice" />{{
              locale.locale() === 'ar' ? 'عرض إشعار الديمو' : 'Show portfolio notice'
            }}</label
          >
          <div>
            <label for="notice-ar">الإشعار بالعربية</label
            ><textarea
              id="notice-ar"
              rows="4"
              maxlength="240"
              formControlName="noticeAr"
              dir="rtl"
            ></textarea>
          </div>
          <div>
            <label for="notice-en">English notice</label
            ><textarea
              id="notice-en"
              rows="4"
              maxlength="240"
              formControlName="noticeEn"
              dir="ltr"
            ></textarea>
          </div>
        </div>
        <label for="settings-audit-reason">{{
          locale.locale() === 'ar' ? 'سبب التدقيق' : 'Audit reason'
        }}</label
        ><input
          id="settings-audit-reason"
          formControlName="auditReason"
          minlength="8"
          maxlength="1000"
        />
        @if (noticeError()) {
          <p class="field-error" role="alert">
            {{
              locale.locale() === 'ar'
                ? 'اكتب الإشعار باللغتين عند تفعيله.'
                : 'Both notice translations are required when enabled.'
            }}
          </p>
        }
        <div class="action-row">
          <button
            class="primary-button"
            type="submit"
            [disabled]="store.settings().status === 'saving'"
          >
            {{ locale.locale() === 'ar' ? 'حفظ الإعدادات' : 'Save settings' }}</button
          ><span class="save-indicator">{{ versionLabel() }}</span>
        </div>
      </form>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlatformSettingsPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(AdministrationStore);
  protected readonly form = new FormGroup({
    featuredCourseLimit: new FormControl(3, {
      nonNullable: true,
      validators: [requiredValidator, Validators.min(1), Validators.max(12)],
    }),
    showPortfolioNotice: new FormControl(false, { nonNullable: true }),
    noticeAr: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(240)] }),
    noticeEn: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(240)] }),
    auditReason: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, Validators.minLength(8), Validators.maxLength(1000)],
    }),
  });
  protected readonly noticeError = signal(false);
  private patchedVersion: number | null = null;

  constructor() {
    this.store.loadSettings();
    effect(() => {
      const settings = this.store.settings().settings;
      if (!settings || settings.version === this.patchedVersion) return;
      this.patchedVersion = settings.version;
      this.form.patchValue(
        { ...settings, auditReason: this.form.controls.auditReason.value },
        { emitEvent: false },
      );
      this.noticeError.set(false);
    });
  }

  protected save(): void {
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    const noticeRequired =
      value.showPortfolioNotice && (!value.noticeAr.trim() || !value.noticeEn.trim());
    this.noticeError.set(noticeRequired);
    if (
      this.form.controls.auditReason.invalid ||
      this.form.controls.featuredCourseLimit.invalid ||
      noticeRequired
    )
      return;
    const settings = this.store.settings().settings;
    if (!settings) return;
    this.store.updateSettings(
      {
        featuredCourseLimit: value.featuredCourseLimit,
        showPortfolioNotice: value.showPortfolioNotice,
        noticeAr: value.noticeAr.trim(),
        noticeEn: value.noticeEn.trim(),
        expectedVersion: settings.version,
      },
      value.auditReason.trim(),
    );
  }

  protected versionLabel(): string {
    const settings = this.store.settings().settings;
    return settings
      ? `v${String(settings.version)} · ${new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium' }).format(new Date(settings.updatedAt))}`
      : '';
  }

  protected message(code: string | null): string {
    if (code === 'SETTINGS.VERSION_CONFLICT')
      return this.locale.locale() === 'ar'
        ? 'تغيرت الإعدادات قبل الحفظ.'
        : 'The settings changed before saving.';
    if (code === 'AUTH.FORBIDDEN')
      return this.locale.locale() === 'ar'
        ? 'تحتاج جلسة مدير حديثة وسبب تدقيق.'
        : 'A recent admin session and audit reason are required.';
    return this.locale.locale() === 'ar'
      ? 'تعذر تحميل الإعدادات.'
      : 'Settings could not be loaded.';
  }
}
