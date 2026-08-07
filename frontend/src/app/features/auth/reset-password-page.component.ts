import { afterNextRender, ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { authErrorMessage, matchingFields, requiredValidator } from './auth-form.helpers';

@Component({
  selector: 'drs-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="reset-title">
      <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'خطوة أخيرة' : 'One last step' }}</p>
      <h1 id="reset-title">{{
        locale.locale() === 'ar' ? 'عيّن كلمة مرور جديدة' : 'Set a new password'
      }}</h1>

      @if (!hasResetLink) {
        <div class="form-alert" role="alert">{{
          locale.locale() === 'ar'
            ? 'رابط الاستعادة غير مكتمل.'
            : 'The recovery link is incomplete.'
        }}</div>
      } @else if (completed()) {
        <div class="form-success" role="status">{{
          locale.locale() === 'ar'
            ? 'تم تغيير كلمة المرور. يمكنك تسجيل الدخول الآن.'
            : 'Your password was changed. You can sign in now.'
        }}</div>
      } @else {
        @if (error()) {
          <div class="form-alert" role="alert">{{ error() }}</div>
        }
        <form class="identity-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label for="reset-password">{{
            locale.locale() === 'ar' ? 'كلمة المرور الجديدة' : 'New password'
          }}</label>
          <input
            id="reset-password"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid"
            aria-describedby="reset-password-error"
          />
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <p id="reset-password-error" class="field-error">{{
              locale.locale() === 'ar' ? 'كلمة المرور مطلوبة.' : 'Password is required.'
            }}</p>
          }

          <label for="reset-confirm">{{
            locale.locale() === 'ar' ? 'تأكيد كلمة المرور' : 'Confirm password'
          }}</label>
          <input
            id="reset-confirm"
            type="password"
            formControlName="confirmPassword"
            autocomplete="new-password"
            [attr.aria-invalid]="form.controls.confirmPassword.touched && form.hasError('fieldsMismatch')"
            aria-describedby="reset-confirm-error"
          />
          @if (form.controls.confirmPassword.touched && form.hasError('fieldsMismatch')) {
            <p id="reset-confirm-error" class="field-error">{{
              locale.locale() === 'ar' ? 'كلمتا المرور غير متطابقتين.' : 'Passwords do not match.'
            }}</p>
          }
          <button type="submit" [disabled]="submitting()">{{
            locale.locale() === 'ar' ? 'حفظ كلمة المرور' : 'Save password'
          }}</button>
        </form>
      }

      <p class="identity-alternative">
        <a [routerLink]="['/', locale.locale(), 'auth', 'sign-in']">{{ locale.copy().signIn }}</a>
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetPasswordPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly route = inject(ActivatedRoute);
  private readonly userId = this.route.snapshot.queryParamMap.get('userId');
  private readonly token = this.route.snapshot.queryParamMap.get('token');
  protected readonly hasResetLink = Boolean(this.userId && this.token);
  protected readonly completed = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly form = new FormGroup(
    {
       password: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
      confirmPassword: new FormControl('', {
        nonNullable: true,
         validators: [requiredValidator],
      }),
    },
    { validators: [matchingFields('password', 'confirmPassword')] },
  );

  constructor() {
    afterNextRender(() => {
      if (this.hasResetLink) {
        globalThis.history.replaceState(null, '', globalThis.location.pathname);
      }
    });
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting() || !this.userId || !this.token) return;
    this.error.set(null);
    this.submitting.set(true);
    this.identityApi
      .resetPassword({
        userId: this.userId,
        token: this.token,
        newPassword: this.form.controls.password.value,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.completed.set(true);
          this.form.reset();
        },
        error: (requestError: unknown) => {
          this.submitting.set(false);
          this.error.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }
}
