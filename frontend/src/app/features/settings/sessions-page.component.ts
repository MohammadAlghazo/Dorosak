import { ChangeDetectionStrategy, Component, inject, type OnInit, signal } from '@angular/core';
import { IdentityApiClient } from '../../core/api/identity-api.client';
import type { SessionSummary } from '../../core/api/identity-api.types';
import { SessionCoordinator } from '../../core/auth/session-coordinator.service';
import { LocaleService } from '../../core/i18n/locale.service';
import { authErrorMessage } from '../auth/auth-form.helpers';

@Component({
  selector: 'drs-sessions-page',
  template: `
    <section class="settings-page" aria-labelledby="sessions-title">
      <div class="settings-heading">
        <p class="identity-kicker">{{ locale.locale() === 'ar' ? 'الوصول' : 'Access' }}</p>
        <h1 id="sessions-title">{{ locale.locale() === 'ar' ? 'جلساتك' : 'Your sessions' }}</h1>
        <p>
          {{
            locale.locale() === 'ar'
              ? 'راجع الأجهزة التي تستخدم حسابك وأنهِ الجلسات غير المعروفة.'
              : 'Review devices using your account and end sessions you do not recognize.'
          }}
        </p>
      </div>

      @if (error()) {
        <div class="form-alert" role="alert">{{ error() }}</div>
      }
      @if (loading()) {
        <p role="status">
          {{ locale.locale() === 'ar' ? 'جارٍ تحميل الجلسات…' : 'Loading sessions…' }}
        </p>
      } @else if (sessions().length === 0) {
        <p class="empty-state">
          {{ locale.locale() === 'ar' ? 'لا توجد جلسات.' : 'No sessions found.' }}
        </p>
      } @else {
        <div class="session-list" role="list">
          @for (session of sessions(); track session.sessionId) {
            <article class="session-card" role="listitem">
              <div>
                <h2>
                  {{
                    session.deviceName ||
                      (locale.locale() === 'ar' ? 'جهاز غير معروف' : 'Unknown device')
                  }}
                </h2>
                <p>
                  @if (session.isCurrent) {
                    <span class="status-chip status-chip-active">{{
                      locale.locale() === 'ar' ? 'الجلسة الحالية' : 'Current session'
                    }}</span>
                  }
                </p>
                <dl class="session-details">
                  <div>
                    <dt>{{ locale.locale() === 'ar' ? 'آخر استخدام' : 'Last used' }}</dt>
                    <dd>
                      <time [attr.datetime]="session.lastUsedAt">{{
                        formatDate(session.lastUsedAt)
                      }}</time>
                    </dd>
                  </div>
                  <div>
                    <dt>{{ locale.locale() === 'ar' ? 'أُنشئت' : 'Created' }}</dt>
                    <dd>
                      <time [attr.datetime]="session.createdAt">{{
                        formatDate(session.createdAt)
                      }}</time>
                    </dd>
                  </div>
                  <div>
                    <dt>{{ locale.locale() === 'ar' ? 'تنتهي نهائيًا' : 'Absolute expiry' }}</dt>
                    <dd>
                      <time [attr.datetime]="session.absoluteExpiresAt">{{
                        formatDate(session.absoluteExpiresAt)
                      }}</time>
                    </dd>
                  </div>
                </dl>
              </div>
              <button
                class="danger-button"
                type="button"
                [disabled]="revokingId() === session.sessionId"
                (click)="revoke(session)"
              >
                {{ locale.locale() === 'ar' ? 'إنهاء الجلسة' : 'End session' }}
              </button>
            </article>
          }
        </div>
        <button
          class="secondary-button"
          type="button"
          [disabled]="revokingAll()"
          (click)="revokeAll()"
        >
          {{ locale.locale() === 'ar' ? 'إنهاء كل الجلسات' : 'End all sessions' }}
        </button>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionsPageComponent implements OnInit {
  protected readonly locale = inject(LocaleService);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly coordinator = inject(SessionCoordinator);
  protected readonly sessions = signal<readonly SessionSummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly revokingId = signal<string | null>(null);
  protected readonly revokingAll = signal(false);

  ngOnInit(): void {
    this.load();
  }

  protected formatDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.valueOf())) return value;
    return new Intl.DateTimeFormat(this.locale.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(date);
  }

  protected revoke(session: SessionSummary): void {
    if (this.revokingId() || this.revokingAll()) return;
    this.error.set(null);
    this.revokingId.set(session.sessionId);
    this.identityApi.revokeSession(session.sessionId).subscribe({
      next: () => {
        this.revokingId.set(null);
        if (session.isCurrent) {
          this.coordinator.endLocalSession().subscribe(() => undefined);
          return;
        }
        this.sessions.update((items) =>
          items.filter((item) => item.sessionId !== session.sessionId),
        );
      },
      error: (requestError: unknown) => {
        this.revokingId.set(null);
        this.error.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }

  protected revokeAll(): void {
    if (this.revokingAll() || this.revokingId()) return;
    this.error.set(null);
    this.revokingAll.set(true);
    this.identityApi.revokeAllSessions().subscribe({
      next: () => {
        this.revokingAll.set(false);
        this.load();
      },
      error: (requestError: unknown) => {
        this.revokingAll.set(false);
        this.error.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }

  private load(): void {
    this.error.set(null);
    this.loading.set(true);
    this.identityApi.getSessions().subscribe({
      next: (sessions) => {
        this.loading.set(false);
        this.sessions.set(sessions);
      },
      error: (requestError: unknown) => {
        this.loading.set(false);
        this.error.set(authErrorMessage(requestError, this.locale.locale()));
      },
    });
  }
}
