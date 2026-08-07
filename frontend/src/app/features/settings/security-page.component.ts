import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { SessionStore } from '../../core/auth/session.store';
import {
  authErrorMessage,
  emailValidator,
  matchingFields,
  requiredValidator,
} from '../auth/auth-form.helpers';

@Component({
  selector: 'drs-security-page',
  imports: [ReactiveFormsModule],
  template: `
    <section class="settings-page" aria-labelledby="security-title">
      <div class="settings-heading">
        <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'حسابك' : 'Your account' }}</p>
        <h1 id="security-title">{{ locale.locale() === 'ar' ? 'الأمان' : 'Security' }}</h1>
        <p>{{
          locale.locale() === 'ar'
            ? 'أدر كلمة المرور والتحقق بخطوتين من مكان واحد.'
            : 'Manage your password and two-step verification in one place.'
        }}</p>
      </div>

      <article class="settings-card">
        <h2>{{ locale.locale() === 'ar' ? 'تغيير البريد الإلكتروني' : 'Change email address' }}</h2>
        <p>{{
          locale.locale() === 'ar'
            ? 'سيبقى بريدك الحالي فعالًا حتى تؤكد العنوان الجديد.'
            : 'Your current address remains active until the new one is verified.'
        }}</p>
        <p><strong>{{ session.identity()?.email }}</strong></p>
        @if (emailMessage()) {
          <div class="form-success" role="status">{{ emailMessage() }}</div>
        }
        @if (emailError()) {
          <div class="form-alert" role="alert">{{ emailError() }}</div>
        }
        <form class="identity-form" [formGroup]="emailForm" (ngSubmit)="requestEmailChange()" novalidate>
          <label for="new-email">{{ locale.locale() === 'ar' ? 'البريد الجديد' : 'New email address' }}</label>
          <input
            id="new-email"
            type="email"
            formControlName="newEmail"
            autocomplete="email"
            [attr.aria-invalid]="emailForm.controls.newEmail.touched && emailForm.controls.newEmail.invalid"
          />
          <label for="email-change-password">{{
            locale.locale() === 'ar' ? 'كلمة المرور الحالية' : 'Current password'
          }}</label>
          <input
            id="email-change-password"
            type="password"
            formControlName="currentPassword"
            autocomplete="current-password"
            [attr.aria-invalid]="emailForm.controls.currentPassword.touched && emailForm.controls.currentPassword.invalid"
          />
          <button type="submit" [disabled]="requestingEmailChange()">{{
            locale.locale() === 'ar' ? 'إرسال رابط التأكيد' : 'Send confirmation link'
          }}</button>
        </form>
      </article>

      <article class="settings-card">
        <h2>{{ locale.locale() === 'ar' ? 'تغيير كلمة المرور' : 'Change password' }}</h2>
        @if (passwordMessage()) {
          <div class="form-success" role="status">{{ passwordMessage() }}</div>
        }
        @if (passwordError()) {
          <div class="form-alert" role="alert">{{ passwordError() }}</div>
        }
        <form class="identity-form" [formGroup]="passwordForm" (ngSubmit)="changePassword()" novalidate>
          <label for="current-password">{{
            locale.locale() === 'ar' ? 'كلمة المرور الحالية' : 'Current password'
          }}</label>
          <input
            id="current-password"
            type="password"
            formControlName="currentPassword"
            autocomplete="current-password"
            [attr.aria-invalid]="passwordForm.controls.currentPassword.touched && passwordForm.controls.currentPassword.invalid"
            aria-describedby="current-password-error"
          />
          @if (passwordForm.controls.currentPassword.touched && passwordForm.controls.currentPassword.invalid) {
            <p id="current-password-error" class="field-error">{{
              locale.locale() === 'ar' ? 'أدخل كلمة المرور الحالية.' : 'Enter your current password.'
            }}</p>
          }

          <label for="new-password">{{
            locale.locale() === 'ar' ? 'كلمة المرور الجديدة' : 'New password'
          }}</label>
          <input
            id="new-password"
            type="password"
            formControlName="newPassword"
            autocomplete="new-password"
            [attr.aria-invalid]="passwordForm.controls.newPassword.touched && passwordForm.controls.newPassword.invalid"
            aria-describedby="new-password-error"
          />
          @if (passwordForm.controls.newPassword.touched && passwordForm.controls.newPassword.invalid) {
            <p id="new-password-error" class="field-error">{{
              locale.locale() === 'ar' ? 'أدخل كلمة مرور جديدة.' : 'Enter a new password.'
            }}</p>
          }

          <label for="new-password-confirm">{{
            locale.locale() === 'ar' ? 'تأكيد كلمة المرور الجديدة' : 'Confirm new password'
          }}</label>
          <input
            id="new-password-confirm"
            type="password"
            formControlName="confirmPassword"
            autocomplete="new-password"
            [attr.aria-invalid]="passwordForm.controls.confirmPassword.touched && passwordForm.hasError('fieldsMismatch')"
            aria-describedby="new-password-confirm-error"
          />
          @if (passwordForm.controls.confirmPassword.touched && passwordForm.hasError('fieldsMismatch')) {
            <p id="new-password-confirm-error" class="field-error">{{
              locale.locale() === 'ar' ? 'كلمتا المرور غير متطابقتين.' : 'Passwords do not match.'
            }}</p>
          }
          <button type="submit" [disabled]="changingPassword()">{{
            locale.locale() === 'ar' ? 'تغيير كلمة المرور' : 'Change password'
          }}</button>
        </form>
      </article>

      <article class="settings-card">
        <div class="settings-card-heading">
          <div>
            <h2>{{ locale.locale() === 'ar' ? 'التحقق بخطوتين' : 'Two-step verification' }}</h2>
            <p>{{
              mfaEnabled()
                ? locale.locale() === 'ar'
                  ? 'مفعّل لحسابك.'
                  : 'Enabled for your account.'
                : locale.locale() === 'ar'
                  ? 'أضف طبقة حماية باستخدام تطبيق مصادقة.'
                  : 'Add another layer with an authenticator app.'
            }}</p>
          </div>
          <span class="status-chip" [class.status-chip-active]="mfaEnabled()">{{
            mfaEnabled()
              ? locale.locale() === 'ar'
                ? 'مفعّل'
                : 'Enabled'
              : locale.locale() === 'ar'
                ? 'غير مفعّل'
                : 'Not enabled'
          }}</span>
        </div>

        @if (mfaError()) {
          <div class="form-alert" role="alert">{{ mfaError() }}</div>
        }
        @if (mfaMessage()) {
          <div class="form-success" role="status">{{ mfaMessage() }}</div>
        }

        @if (!mfaEnabled() && !totpSetup()) {
          <button class="secondary-button" type="button" [disabled]="settingUpMfa()" (click)="setupMfa()">
            {{ locale.locale() === 'ar' ? 'بدء إعداد التطبيق' : 'Start app setup' }}
          </button>
        }

        @if (totpSetup()) {
          <div class="totp-details">
            <h3>{{ locale.locale() === 'ar' ? 'أكمل إعداد التطبيق' : 'Finish setting up your app' }}</h3>
            <p>{{
              locale.locale() === 'ar'
                ? 'أضف هذا المفتاح إلى تطبيق المصادقة، ثم أدخل الرمز الظاهر فيه.'
                : 'Add this secret to your authenticator app, then enter the code it shows.'
            }}</p>
            <dl>
              <div><dt>{{ locale.locale() === 'ar' ? 'المفتاح' : 'Secret' }}</dt><dd><code>{{ totpSetup()?.secret }}</code></dd></div>
              <div><dt>{{ locale.locale() === 'ar' ? 'رابط التطبيق' : 'App URI' }}</dt><dd><code class="breakable">{{ totpSetup()?.otpAuthUri }}</code></dd></div>
            </dl>
            <form class="identity-form" [formGroup]="mfaConfirmForm" (ngSubmit)="confirmMfa()" novalidate>
              <label for="totp-code">{{ locale.locale() === 'ar' ? 'رمز التطبيق' : 'Authenticator code' }}</label>
              <input
                id="totp-code"
                inputmode="numeric"
                autocomplete="one-time-code"
                formControlName="code"
                [attr.aria-invalid]="mfaConfirmForm.controls.code.touched && mfaConfirmForm.controls.code.invalid"
                aria-describedby="totp-code-error"
              />
              @if (mfaConfirmForm.controls.code.touched && mfaConfirmForm.controls.code.invalid) {
                <p id="totp-code-error" class="field-error">{{
                  locale.locale() === 'ar' ? 'أدخل رمزًا من 6 أرقام.' : 'Enter a 6-digit code.'
                }}</p>
              }
              <button type="submit" [disabled]="confirmingMfa()">{{
                locale.locale() === 'ar' ? 'تأكيد التفعيل' : 'Confirm setup'
              }}</button>
            </form>
          </div>
        }

        @if (recoveryCodes().length > 0) {
          <div class="recovery-panel" role="region" aria-labelledby="recovery-title">
            <h3 id="recovery-title">{{
              locale.locale() === 'ar' ? 'احفظ رموز الاسترداد' : 'Save your recovery codes'
            }}</h3>
            <p>{{
              locale.locale() === 'ar'
                ? 'تظهر هذه الرموز الآن فقط. احفظها في مكان آمن.'
                : 'These codes are shown only now. Store them somewhere safe.'
            }}</p>
            <ol>
              @for (code of recoveryCodes(); track code) {
                <li><code>{{ code }}</code></li>
              }
            </ol>
          </div>
        }

        @if (mfaEnabled()) {
          <form class="identity-form compact-form" [formGroup]="disableMfaForm" (ngSubmit)="disableMfa()" novalidate>
            <label for="disable-mfa-password">{{
              locale.locale() === 'ar' ? 'كلمة المرور لتعطيل التحقق' : 'Password to disable verification'
            }}</label>
            <input
              id="disable-mfa-password"
              type="password"
              formControlName="currentPassword"
              autocomplete="current-password"
              [attr.aria-invalid]="disableMfaForm.controls.currentPassword.touched && disableMfaForm.controls.currentPassword.invalid"
              aria-describedby="disable-mfa-error"
            />
            @if (disableMfaForm.controls.currentPassword.touched && disableMfaForm.controls.currentPassword.invalid) {
              <p id="disable-mfa-error" class="field-error">{{
                locale.locale() === 'ar' ? 'أدخل كلمة المرور.' : 'Enter your password.'
              }}</p>
            }
            <button class="danger-button" type="submit" [disabled]="disablingMfa()">{{
              locale.locale() === 'ar' ? 'تعطيل التحقق بخطوتين' : 'Disable two-step verification'
            }}</button>
          </form>
        }
      </article>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SecurityPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  protected readonly session = inject(SessionStore);
  protected readonly mfaEnabled = computed(() => this.session.identity()?.mfaEnabled ?? false);
  protected readonly emailMessage = signal<string | null>(null);
  protected readonly emailError = signal<string | null>(null);
  protected readonly passwordMessage = signal<string | null>(null);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly mfaMessage = signal<string | null>(null);
  protected readonly mfaError = signal<string | null>(null);
  protected readonly changingPassword = signal(false);
  protected readonly requestingEmailChange = signal(false);
  protected readonly settingUpMfa = signal(false);
  protected readonly confirmingMfa = signal(false);
  protected readonly disablingMfa = signal(false);
  protected readonly totpSetup = signal<{ secret: string; otpAuthUri: string } | null>(null);
  protected readonly recoveryCodes = signal<readonly string[]>([]);

  protected readonly emailForm = new FormGroup({
    newEmail: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, emailValidator],
    }),
    currentPassword: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator],
    }),
  });

  protected readonly passwordForm = new FormGroup(
    {
       currentPassword: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
       newPassword: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
       confirmPassword: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
    },
    { validators: [matchingFields('newPassword', 'confirmPassword')] },
  );
  protected readonly mfaConfirmForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
       validators: [requiredValidator, Validators.pattern(/^\d{6}$/u)],
    }),
  });
  protected readonly disableMfaForm = new FormGroup({
     currentPassword: new FormControl('', { nonNullable: true, validators: [requiredValidator] }),
  });

  protected requestEmailChange(): void {
    this.emailForm.markAllAsTouched();
    if (this.emailForm.invalid || this.requestingEmailChange()) return;
    this.emailError.set(null);
    this.emailMessage.set(null);
    this.requestingEmailChange.set(true);
    this.identityApi
      .requestEmailChange({
        newEmail: this.emailForm.controls.newEmail.value.trim(),
        currentPassword: this.emailForm.controls.currentPassword.value,
        locale: this.locale.locale(),
      })
      .subscribe({
        next: () => {
          this.requestingEmailChange.set(false);
          this.emailForm.reset();
          this.emailMessage.set(
            this.locale.locale() === 'ar'
              ? 'تم إرسال رابط إلى البريد الجديد.'
              : 'A confirmation link was sent to the new address.',
          );
        },
        error: (requestError: unknown) => {
          this.requestingEmailChange.set(false);
          this.emailError.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }

  protected changePassword(): void {
    this.passwordForm.markAllAsTouched();
    if (this.passwordForm.invalid || this.changingPassword()) return;
    this.passwordMessage.set(null);
    this.passwordError.set(null);
    this.changingPassword.set(true);
    this.identityApi
      .changePassword({
        currentPassword: this.passwordForm.controls.currentPassword.value,
        newPassword: this.passwordForm.controls.newPassword.value,
      })
      .subscribe({
        next: () => {
          this.changingPassword.set(false);
          this.passwordForm.reset();
          this.passwordMessage.set(
            this.locale.locale() === 'ar' ? 'تم تغيير كلمة المرور.' : 'Password changed.',
          );
        },
        error: (requestError: unknown) => {
          this.changingPassword.set(false);
          this.passwordError.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }

  protected setupMfa(): void {
    if (this.settingUpMfa()) return;
    this.mfaError.set(null);
    this.mfaMessage.set(null);
    this.settingUpMfa.set(true);
    this.identityApi.setupMfa().subscribe({
      next: (setup) => {
        this.settingUpMfa.set(false);
        this.totpSetup.set(setup);
      },
      error: (requestError: unknown) => {
        this.settingUpMfa.set(false);
        this.mfaError.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }

  protected confirmMfa(): void {
    this.mfaConfirmForm.markAllAsTouched();
    if (this.mfaConfirmForm.invalid || this.confirmingMfa()) return;
    this.mfaError.set(null);
    this.confirmingMfa.set(true);
    this.identityApi.confirmMfa(this.mfaConfirmForm.controls.code.value).subscribe({
      next: (result) => {
        this.confirmingMfa.set(false);
        this.totpSetup.set(null);
        this.recoveryCodes.set(result.recoveryCodes);
        this.mfaConfirmForm.reset();
        this.refreshIdentity();
        this.mfaMessage.set(
          this.locale.locale() === 'ar' ? 'تم تفعيل التحقق بخطوتين.' : 'Two-step verification is enabled.',
        );
      },
      error: (requestError: unknown) => {
        this.confirmingMfa.set(false);
        this.mfaError.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }

  protected disableMfa(): void {
    this.disableMfaForm.markAllAsTouched();
    if (this.disableMfaForm.invalid || this.disablingMfa()) return;
    this.mfaError.set(null);
    this.mfaMessage.set(null);
    this.disablingMfa.set(true);
    this.identityApi
      .disableMfa(this.disableMfaForm.controls.currentPassword.value)
      .subscribe({
        next: () => {
          this.disablingMfa.set(false);
          this.disableMfaForm.reset();
          this.recoveryCodes.set([]);
          this.refreshIdentity();
          this.mfaMessage.set(
            this.locale.locale() === 'ar'
              ? 'تم تعطيل التحقق بخطوتين.'
              : 'Two-step verification is disabled.',
          );
        },
        error: (requestError: unknown) => {
          this.disablingMfa.set(false);
          this.mfaError.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }

  private refreshIdentity(): void {
    this.identityApi.getProfile().subscribe({
      next: (identity) => {
        this.session.updateIdentity(identity);
      },
    });
  }
}
