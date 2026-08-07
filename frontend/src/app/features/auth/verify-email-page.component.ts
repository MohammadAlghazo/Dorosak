import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  type OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { authErrorMessage, emailValidator, requiredValidator } from './auth-form.helpers';

type VerificationStatus = 'idle' | 'confirming' | 'confirmed' | 'failed';

@Component({
  selector: 'drs-verify-email-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="verify-title">
      <p class="identity-kicker">
        {{ locale.locale() === 'ar' ? 'تحقق من هويتك' : 'Confirm it is you' }}
      </p>
      <h1 id="verify-title">
        {{ locale.locale() === 'ar' ? 'تأكيد البريد الإلكتروني' : 'Verify your email' }}
      </h1>

      @switch (status()) {
        @case ('confirming') {
          <p role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ التحقق من الرابط…' : 'Verifying your link…' }}
          </p>
        }
        @case ('confirmed') {
          <div class="form-success" role="status">
            {{
              locale.locale() === 'ar'
                ? 'تم تأكيد بريدك الإلكتروني.'
                : 'Your email address is verified.'
            }}
          </div>
        }
        @case ('failed') {
          <div class="form-alert" role="alert">{{ confirmationError() }}</div>
        }
      }

      <div class="identity-divider" aria-hidden="true"></div>
      <h2>
        {{
          locale.locale() === 'ar' ? 'هل تحتاج إلى رسالة جديدة؟' : 'Need a new verification email?'
        }}
      </h2>
      <p>
        {{
          locale.locale() === 'ar'
            ? 'أدخل بريدك وسنرسل رسالة إذا كان الحساب مؤهلًا.'
            : 'Enter your address and we will send a message if the account is eligible.'
        }}
      </p>

      @if (resent()) {
        <div class="form-success" role="status">
          {{
            locale.locale() === 'ar'
              ? 'تم استلام الطلب. تحقق من بريدك.'
              : 'Request accepted. Check your inbox.'
          }}
        </div>
      } @else {
        @if (resendError()) {
          <div class="form-alert" role="alert">{{ resendError() }}</div>
        }
        <form class="identity-form" [formGroup]="form" (ngSubmit)="resend()" novalidate>
          <label for="verification-email">{{
            locale.locale() === 'ar' ? 'البريد الإلكتروني' : 'Email address'
          }}</label>
          <input
            id="verification-email"
            type="email"
            formControlName="email"
            autocomplete="email"
            [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid"
            aria-describedby="verification-email-error"
          />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <p id="verification-email-error" class="field-error">
              {{
                locale.locale() === 'ar' ? 'أدخل بريدًا صحيحًا.' : 'Enter a valid email address.'
              }}
            </p>
          }
          <button type="submit" [disabled]="sending()">
            {{ locale.locale() === 'ar' ? 'إرسال رسالة التحقق' : 'Send verification email' }}
          </button>
        </form>
      }

      <p class="identity-alternative">
        <a [routerLink]="['/', locale.locale(), 'auth', 'sign-in']">{{ locale.copy().signIn }}</a>
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyEmailPageComponent implements OnInit {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  protected readonly status = signal<VerificationStatus>('idle');
  protected readonly confirmationError = signal<string | null>(null);
  protected readonly resendError = signal<string | null>(null);
  protected readonly resent = signal(false);
  protected readonly sending = signal(false);
  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [requiredValidator, emailValidator],
    }),
  });

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!isPlatformBrowser(this.platformId) || !userId || !token) return;

    globalThis.history.replaceState(null, '', globalThis.location.pathname);

    this.status.set('confirming');
    this.identityApi.confirmEmailVerification({ userId, token }).subscribe({
      next: (result) => {
        this.status.set(result.completed ? 'confirmed' : 'failed');
      },
      error: (requestError: unknown) => {
        this.status.set('failed');
        this.confirmationError.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }

  protected resend(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.sending()) return;
    this.resendError.set(null);
    this.sending.set(true);
    this.identityApi
      .sendEmailVerification(this.form.controls.email.value.trim(), this.locale.locale())
      .subscribe({
        next: (result) => {
          this.sending.set(false);
          this.resent.set(result.accepted);
        },
        error: (requestError: unknown) => {
          this.sending.set(false);
          this.resendError.set(authErrorMessage(requestError, this.locale.locale()));
        },
      });
  }
}
