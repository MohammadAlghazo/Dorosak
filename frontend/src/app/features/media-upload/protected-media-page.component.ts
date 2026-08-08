import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { MediaApiClient } from '../../core/api/media-api.client';
import type { MediaStatus } from '../../core/api/media-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';

type ProtectedMediaState =
  | { status: 'loading' | 'offline'; media: null; errorCode: null }
  | { status: 'success'; media: MediaStatus; errorCode: null }
  | { status: 'error'; media: null; errorCode: string | null };

@Component({
  selector: 'drs-protected-media-page',
  template: `
    <section class="protected-media" aria-labelledby="protected-media-title">
      <p class="identity-kicker">
        {{ locale.locale() === 'ar' ? 'وسائط محمية' : 'Protected media' }}
      </p>
      <h1 id="protected-media-title">
        {{ locale.locale() === 'ar' ? 'وسائط الدرس' : 'Lesson media' }}
      </h1>

      <div class="media-stage">
        @switch (state().status) {
          @case ('loading') {
            <p role="status">
              {{ locale.locale() === 'ar' ? 'جارٍ تحميل الحالة…' : 'Loading media status…' }}
            </p>
          }
          @case ('offline') {
            <div role="alert">
              <strong>{{
                locale.locale() === 'ar'
                  ? 'الوسائط غير متاحة دون اتصال'
                  : 'Media is unavailable offline'
              }}</strong>
              <p>
                {{
                  locale.locale() === 'ar'
                    ? 'لا تُخزّن ملفات الدروس المحمية في ذاكرة التطبيق.'
                    : 'Protected lesson files are not stored in the application cache.'
                }}
              </p>
            </div>
          }
          @case ('error') {
            <div role="alert">
              <strong>{{
                locale.locale() === 'ar' ? 'تعذر تحميل الوسائط' : 'Media could not be loaded'
              }}</strong>
              @if (state().errorCode) {
                <code>{{ state().errorCode }}</code>
              }
            </div>
          }
          @case ('success') {
            @if (state().media; as media) {
              <div>
                <strong>{{ media.state }}</strong>
                <p>{{ media.contentType }}</p>
                @if (media.state === 'Ready') {
                  <button
                    class="primary-button"
                    type="button"
                    [disabled]="granting()"
                    (click)="openMedia()"
                  >
                    {{
                      granting()
                        ? locale.locale() === 'ar'
                          ? 'جارٍ منح الوصول…'
                          : 'Granting access…'
                        : locale.locale() === 'ar'
                          ? 'فتح الوسائط'
                          : 'Open media'
                    }}
                  </button>
                } @else {
                  <p role="status">
                    {{
                      locale.locale() === 'ar'
                        ? 'لم تجهز الوسائط بعد.'
                        : 'The media is not ready yet.'
                    }}
                  </p>
                }
              </div>
            }
          }
        }
      </div>
      @if (grantError()) {
        <p class="form-alert" role="alert">
          {{
            locale.locale() === 'ar'
              ? 'تعذر إنشاء رابط وصول جديد.'
              : 'A fresh access link could not be created.'
          }}
          <code>{{ grantError() }}</code>
        </p>
      }
      <button class="secondary-button" type="button" (click)="load()">
        {{ locale.locale() === 'ar' ? 'تحديث الحالة' : 'Refresh status' }}
      </button>
    </section>
  `,
  styles: `
    .protected-media {
      max-inline-size: 80rem;
      margin-inline: auto;
    }
    h1 {
      margin-block: var(--space-2) var(--space-5);
      font-size: clamp(2rem, 5vw, 4rem);
    }
    .media-stage {
      display: grid;
      place-items: center;
      min-block-size: 55dvh;
      padding: var(--space-7);
      color: #f8fafc;
      background: #0f1b2e;
      border: 1px solid #334155;
      text-align: center;
    }
    .media-stage p {
      color: #cbd5e1;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProtectedMediaPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly api = inject(MediaApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly assetId = this.route.snapshot.paramMap.get('assetId') ?? '';
  protected readonly state = signal<ProtectedMediaState>({
    status: 'loading',
    media: null,
    errorCode: null,
  });
  protected readonly granting = signal(false);
  protected readonly grantError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    if (!this.connectivity.isOnline()) {
      this.state.set({ status: 'offline', media: null, errorCode: null });
      return;
    }
    this.state.set({ status: 'loading', media: null, errorCode: null });
    this.api
      .getStatus(this.assetId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (media) => {
          this.state.set({ status: 'success', media, errorCode: null });
        },
        error: (error: unknown) => {
          this.state.set({
            status: 'error',
            media: null,
            errorCode: error instanceof ApiProblem ? error.code : null,
          });
        },
      });
  }

  protected openMedia(): void {
    if (!this.connectivity.isOnline() || this.granting()) return;
    this.granting.set(true);
    this.grantError.set(null);
    this.api
      .createDownloadGrant(this.assetId, { variantId: null, fileName: null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (grant) => {
          this.granting.set(false);
          if (!isPlatformBrowser(this.platformId)) return;
          const link = this.document.createElement('a');
          link.href = grant.url;
          link.rel = 'noopener';
          link.click();
        },
        error: (error: unknown) => {
          this.granting.set(false);
          this.grantError.set(error instanceof ApiProblem ? error.code : 'MEDIA.GRANT_FAILED');
        },
      });
  }
}
