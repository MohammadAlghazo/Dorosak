import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import {
  authErrorMessage,
  emailValidator,
  matchingFields,
  requiredValidator,
} from './auth-form.helpers';

@Component({
  selector: 'drs-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="register-title">
      <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'ابدأ بهدوء' : 'Start clearly' }}</p>
      <h1 id="register-title">
        {{ locale.locale() === 'ar' ? 'أنشئ حسابك' : 'Create your account' }}
      </h1>

      @if (accepted()) {
        <div class="form-success" role="status">
          {{
            locale.locale() === 'ar'
              ? 'تم استلام الطلب. تحقق من بريدك الإلكتروني لإكمال التفعيل.'
              : 'Request accepted. Check your email to finish verification.'
          }}
        </div>
        <a class="primary-link" [routerLink]="['/', locale.locale(), 'auth', 'verify-email']">{{
          locale.locale() === 'ar' ? 'إعادة إرسال رسالة التحقق' : 'Resend verification email'
        }}</a>
      } @else {
        @if (error()) {
          <div class="form-alert" role="alert">{{ error() }}</div>
        }
        <form class="identity-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label for="register-name">{{
            locale.locale() === 'ar' ? 'الاسم المعروض' : 'Display name'
          }}</label>
          <input
            id="register-name"
            formControlName="displayName"
            autocomplete="name"
            [attr.aria-invalid]="
              form.controls.displayName.touched && form.controls.displayName.invalid
            "
            aria-describedby="register-name-error"
          />
          @if (form.controls.displayName.touched && form.controls.displayName.invalid) {
            <p id="register-name-error" class="field-error">
              {{ locale.locale() === 'ar' ? 'الاسم مطلوب.' : 'Display name is required.' }}
            </p>
          }

          <label for="register-email">{{
            locale.locale() === 'ar' ? 'البريد الإلكتروني' : 'Email address'
          }}</label>
          <input
            id="register-email"
            type="email"
            formControlName="email"
            autocomplete="email"
            [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid"
            aria-describedby="register-email-error"
          />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <p id="register-email-error" class="field-error">
              {{
                locale.locale() === 'ar' ? 'أدخل بريدًا صحيحًا.' : 'Enter a valid email address.'
              }}
            </p>
          }

          <label for="register-password">{{
            locale.locale() === 'ar' ? 'كلمة المرور' : 'Password'
          }}</label>
          <input
            id="register-password"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid"
            aria-describedby="register-password-help register-password-error"
          />
          <p id="register-password-help" class="field-help">
            {{
              locale.locale() === 'ar'
                ? 'استخدم كلمة مرور طويلة وفريدة.'
                : 'Use a long, unique password.'
            }}
          </p>
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <p id="register-password-error" class="field-error">
              {{ locale.locale() === 'ar' ? 'كلمة المرور مطلوبة.' : 'Password is required.' }}
            </p>
          }

          <label for="register-confirm">{{
            locale.locale() === 'ar' ? 'تأكيد كلمة المرور' : 'Confirm password'
          }}</label>
          <input
            id="register-confirm"
            type="password"
            formControlName="confirmPassword"
            autocomplete="new-password"
            [attr.aria-invalid]="
              form.controls.confirmPassword.touched && form.hasError('fieldsMismatch')
            "
            aria-describedby="register-confirm-error"
          />
          @if (form.controls.confirmPassword.touched && form.hasError('fieldsMismatch')) {
            <p id="register-confirm-error" class="field-error">
              {{
                locale.locale() === 'ar' ? 'كلمتا المرور غير متطابقتين.' : 'Passwords do not match.'
              }}
            </p>
          }

          <button type="submit" [disabled]="submitting()" [attr.aria-busy]="submitting()">
            {{
              submitting()
                ? locale.locale() === 'ar'
                  ? 'جارٍ إنشاء الحساب…'
                  : 'Creating account…'
                : locale.locale() === 'ar'
                  ? 'إنشاء الحساب'
                  : 'Create account'
            }}
          </button>
        </form>
      }

      <p class="identity-alternative">
        {{ locale.locale() === 'ar' ? 'لديك حساب؟' : 'Already have an account?' }}
        <a [routerLink]="['/', locale.locale(), 'auth', 'sign-in']">{{ locale.copy().signIn }}</a>
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  protected readonly accepted = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly form = new FormGroup(
    {
      displayName: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
      email: new FormControl('', {
        nonNullable: true,
        validators: [requiredValidator, emailValidator],
      }),
      password: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
      confirmPassword: new FormControl('', {
        nonNullable: true,
        validators: [requiredValidator],
      }),
    },
    { validators: [matchingFields('password', 'confirmPassword')] },
  );

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    this.error.set(null);
    this.submitting.set(true);
    this.identityApi
      .register({
        displayName: this.form.controls.displayName.value.trim(),
        email: this.form.controls.email.value.trim(),
        password: this.form.controls.password.value,
      })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          this.accepted.set(result.accepted);
        },
        error: (requestError: unknown) => {
          this.submitting.set(false);
          this.error.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }
}
