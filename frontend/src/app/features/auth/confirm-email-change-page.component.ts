import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  type OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import { LocaleService } from '../../core/i18n/locale.service';
import { authErrorMessage } from './auth-form.helpers';

type ConfirmationStatus = 'confirming' | 'confirmed' | 'failed';

@Component({
  selector: 'drs-confirm-email-change-page',
  imports: [RouterLink],
  template: `
    <section class="identity-page" aria-labelledby="email-change-title">
      <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'عنوان جديد' : 'New address' }}</p>
      <h1 id="email-change-title">{{
        locale.locale() === 'ar' ? 'تأكيد البريد الجديد' : 'Confirm your new email'
      }}</h1>

      @if (status() === 'confirming') {
        <p role="status">{{ locale.locale() === 'ar' ? 'جارٍ تأكيد التغيير…' : 'Confirming the change…' }}</p>
      } @else if (status() === 'confirmed') {
        <div class="form-success" role="status">{{
          locale.locale() === 'ar'
            ? 'تم تغيير البريد وإنهاء جلساتك. سجل الدخول بالبريد الجديد.'
            : 'Your email changed and your sessions ended. Sign in with the new address.'
        }}</div>
      } @else {
        <div class="form-alert" role="alert">{{ error() }}</div>
      }

      <p class="identity-alternative">
        <a [routerLink]="['/', locale.locale(), 'auth', 'sign-in']">{{ locale.copy().signIn }}</a>
      </p>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmEmailChangePageComponent implements OnInit {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  protected readonly status = signal<ConfirmationStatus>('confirming');
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!isPlatformBrowser(this.platformId) || !userId || !token) {
      this.status.set('failed');
      this.error.set(
        this.locale.locale() === 'ar' ? 'رابط تغيير البريد غير صالح.' : 'The email change link is invalid.',
      );
      return;
    }

    globalThis.history.replaceState(null, '', globalThis.location.pathname);
    this.identityApi.confirmEmailChange({ userId, token }).subscribe({
      next: () => {
        this.status.set('confirmed');
      },
      error: (requestError: unknown) => {
        this.status.set('failed');
        this.error.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }
}
