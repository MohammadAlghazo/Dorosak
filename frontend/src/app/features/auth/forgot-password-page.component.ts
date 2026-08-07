import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { authErrorMessage, emailValidator, requiredValidator } from './auth-form.helpers';

@Component({
  selector: 'drs-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="forgot-title">
      <p class="identity-kicker">
        {{ locale.locale() === 'ar' ? 'استعادة آمنة' : 'Secure recovery' }}
      </p>
      <h1 id="forgot-title">
        {{ locale.locale() === 'ar' ? 'نسيت كلمة المرور؟' : 'Forgot your password?' }}
      </h1>
      <p>
        {{
          locale.locale() === 'ar'
            ? 'سنرسل تعليمات الاستعادة إذا كان البريد مسجلًا.'
            : 'We will send recovery instructions if the address is registered.'
        }}
      </p>

      @if (accepted()) {
        <div class="form-success" role="status">
          {{
            locale.locale() === 'ar'
              ? 'تحقق من بريدك الإلكتروني للمتابعة.'
              : 'Check your email to continue.'
          }}
        </div>
      } @else {
        @if (error()) {
          <div class="form-alert" role="alert">{{ error() }}</div>
        }
        <form class="identity-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label for="forgot-email">{{
            locale.locale() === 'ar' ? 'البريد الإلكتروني' : 'Email address'
          }}</label>
          <input
            id="forgot-email"
            type="email"
            formControlName="email"
            autocomplete="email"
            [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid"
            aria-describedby="forgot-email-error"
          />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <p id="forgot-email-error" class="field-error">
              {{
                locale.locale() === 'ar' ? 'أدخل بريدًا صحيحًا.' : 'Enter a valid email address.'
              }}
            </p>
          }
          <button type="submit" [disabled]="submitting()">
            {{ locale.locale() === 'ar' ? 'إرسال رابط الاستعادة' : 'Send recovery link' }}
          </button>
        </form>
      }
      <p class="identity-alternative">
        <a [routerLink]="['/', locale.locale(), 'auth', 'sign-in']">{{
          locale.locale() === 'ar' ? 'العودة إلى تسجيل الدخول' : 'Back to sign in'
        }}</a>
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForgotPasswordPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  protected readonly accepted = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, emailValidator],
    }),
  });

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;
    this.error.set(null);
    this.submitting.set(true);
    this.identityApi
      .requestPasswordReset(this.form.controls.email.value.trim(), this.locale.locale())
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
