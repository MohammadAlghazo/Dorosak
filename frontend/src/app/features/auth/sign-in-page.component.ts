import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionCoordinator } from '../../core/auth/session-coordinator.service';
import { LocaleService } from '../../core/i18n/locale.service';
import { localReturnUrl } from '../../core/routing/session.guard';
import { authErrorMessage, emailValidator, requiredValidator } from './auth-form.helpers';

@Component({
  selector: 'drs-sign-in-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="sign-in-title">
      <p class="identity-kicker">
        {{ locale.locale() === 'ar' ? 'مرحبًا بعودتك' : 'Welcome back' }}
      </p>
      <h1 id="sign-in-title">{{ locale.copy().signIn }}</h1>
      <p>
        {{
          locale.locale() === 'ar'
            ? 'تابع من حيث توقفت في مسارك التعليمي.'
            : 'Continue your learning from where you left off.'
        }}
      </p>

      @if (error()) {
        <div id="sign-in-error" class="form-alert" role="alert">{{ error() }}</div>
      }

      <form class="identity-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <label for="sign-in-email">{{
          locale.locale() === 'ar' ? 'البريد الإلكتروني' : 'Email address'
        }}</label>
        <input
          id="sign-in-email"
          type="email"
          formControlName="email"
          autocomplete="email"
          [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid"
          aria-describedby="sign-in-email-error"
        />
        @if (form.controls.email.touched && form.controls.email.invalid) {
          <p id="sign-in-email-error" class="field-error">
            {{ locale.locale() === 'ar' ? 'أدخل بريدًا صحيحًا.' : 'Enter a valid email address.' }}
          </p>
        }

        <div class="label-row">
          <label for="sign-in-password">{{
            locale.locale() === 'ar' ? 'كلمة المرور' : 'Password'
          }}</label>
          <a [routerLink]="['/', locale.locale(), 'auth', 'forgot-password']">{{
            locale.locale() === 'ar' ? 'نسيت كلمة المرور؟' : 'Forgot password?'
          }}</a>
        </div>
        <input
          id="sign-in-password"
          type="password"
          formControlName="password"
          autocomplete="current-password"
          [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid"
          aria-describedby="sign-in-password-error"
        />
        @if (form.controls.password.touched && form.controls.password.invalid) {
          <p id="sign-in-password-error" class="field-error">
            {{ locale.locale() === 'ar' ? 'كلمة المرور مطلوبة.' : 'Password is required.' }}
          </p>
        }

        <button type="submit" [disabled]="submitting()" [attr.aria-busy]="submitting()">
          {{
            submitting()
              ? locale.locale() === 'ar'
                ? 'جارٍ تسجيل الدخول…'
                : 'Signing in…'
              : locale.copy().signIn
          }}
        </button>
      </form>

      <p class="identity-alternative">
        {{ locale.locale() === 'ar' ? 'ليس لديك حساب؟' : 'New to Dorosak?' }}
        <a [routerLink]="['/', locale.locale(), 'auth', 'register']">{{
          locale.locale() === 'ar' ? 'أنشئ حسابًا' : 'Create an account'
        }}</a>
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly coordinator = inject(SessionCoordinator);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, emailValidator],
    }),
    password: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
  });

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;

    this.error.set(null);
    this.submitting.set(true);
    const returnUrl = localReturnUrl(
      this.route.snapshot.queryParamMap.get('returnUrl'),
      this.locale.locale(),
    );
    this.coordinator
      .signIn({
        email: this.form.controls.email.value.trim(),
        password: this.form.controls.password.value,
      })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          if (result.outcome === 'authenticated') {
            void this.router.navigateByUrl(returnUrl);
            return;
          }
          void this.router.navigate(['/', this.locale.locale(), 'auth', 'mfa'], {
            queryParams: { returnUrl },
          });
        },
        error: (requestError: unknown) => {
          this.submitting.set(false);
          this.error.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }
}
