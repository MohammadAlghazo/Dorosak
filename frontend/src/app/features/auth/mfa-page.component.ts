import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionCoordinator } from '../../core/auth/session-coordinator.service';
import { LocaleService } from '../../core/i18n/locale.service';
import { localReturnUrl } from '../../core/routing/session.guard';
import { authErrorMessage, requiredValidator } from './auth-form.helpers';

@Component({
  selector: 'drs-mfa-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="mfa-title">
      <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'حماية إضافية' : 'One more check' }}</p>
      <h1 id="mfa-title">{{
        recovery
          ? locale.locale() === 'ar'
            ? 'استخدم رمز الاسترداد'
            : 'Use a recovery code'
          : locale.locale() === 'ar'
            ? 'أدخل رمز التحقق'
            : 'Enter your verification code'
      }}</h1>

      @if (!coordinator.pendingMfaChallenge()) {
        <div class="form-alert" role="alert">{{
          locale.locale() === 'ar'
            ? 'انتهت محاولة تسجيل الدخول أو فُقدت بعد إعادة تحميل الصفحة.'
            : 'The sign-in attempt expired or was lost when the page reloaded.'
        }}</div>
        <a class="primary-link" [routerLink]="['/', locale.locale(), 'auth', 'sign-in']">{{
          locale.copy().signIn
        }}</a>
      } @else {
        <p>{{
          recovery
            ? locale.locale() === 'ar'
              ? 'أدخل أحد رموز الاسترداد غير المستخدمة.'
              : 'Enter one of your unused recovery codes.'
            : locale.locale() === 'ar'
              ? 'أدخل الرمز المكوّن من 6 أرقام من تطبيق المصادقة.'
              : 'Enter the 6-digit code from your authenticator app.'
        }}</p>
        @if (error()) {
          <div class="form-alert" role="alert">{{ error() }}</div>
        }
        <form class="identity-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label for="mfa-code">{{
            recovery
              ? locale.locale() === 'ar'
                ? 'رمز الاسترداد'
                : 'Recovery code'
              : locale.locale() === 'ar'
                ? 'رمز التحقق'
                : 'Verification code'
          }}</label>
          <input
            id="mfa-code"
            formControlName="code"
            [attr.inputmode]="recovery ? null : 'numeric'"
            [attr.autocomplete]="recovery ? 'off' : 'one-time-code'"
            [attr.aria-invalid]="form.controls.code.touched && form.controls.code.invalid"
            aria-describedby="mfa-code-error"
          />
          @if (form.controls.code.touched && form.controls.code.invalid) {
            <p id="mfa-code-error" class="field-error">{{
              recovery
                ? locale.locale() === 'ar'
                  ? 'أدخل رمز الاسترداد.'
                  : 'Enter a recovery code.'
                : locale.locale() === 'ar'
                  ? 'أدخل رمزًا من 6 أرقام.'
                  : 'Enter a 6-digit code.'
            }}</p>
          }
          <button type="submit" [disabled]="submitting()">{{
            locale.locale() === 'ar' ? 'متابعة' : 'Continue'
          }}</button>
        </form>

        <p class="identity-alternative">
          @if (recovery) {
            <a
              [routerLink]="['/', locale.locale(), 'auth', 'mfa']"
              [queryParams]="{ returnUrl: returnUrl }"
              >{{ locale.locale() === 'ar' ? 'استخدم تطبيق المصادقة' : 'Use authenticator app' }}</a
            >
          } @else {
            <a
              [routerLink]="['/', locale.locale(), 'auth', 'mfa', 'recovery']"
              [queryParams]="{ returnUrl: returnUrl }"
              >{{ locale.locale() === 'ar' ? 'استخدم رمز استرداد' : 'Use a recovery code' }}</a
            >
          }
        </p>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MfaPageComponent {
  protected readonly coordinator = inject(SessionCoordinator);
  protected readonly locale = inject(LocaleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly recovery = this.route.snapshot.data['recovery'] === true;
  protected readonly returnUrl = localReturnUrl(
    this.route.snapshot.queryParamMap.get('returnUrl'),
    this.locale.locale(),
  );
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: this.recovery
         ? [requiredValidator]
         : [requiredValidator, Validators.pattern(/^\d{6}$/u)],
    }),
  });

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting() || !this.coordinator.pendingMfaChallenge()) return;
    this.error.set(null);
    this.submitting.set(true);
    this.coordinator.completeMfa(this.form.controls.code.value.trim(), this.recovery).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.returnUrl);
      },
      error: (requestError: unknown) => {
        this.submitting.set(false);
        this.error.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }
}
