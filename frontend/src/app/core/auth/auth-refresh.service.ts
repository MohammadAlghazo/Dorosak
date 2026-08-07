import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, inject, Injectable, PLATFORM_ID } from '@angular/core';
import {
  finalize,
  firstValueFrom,
  from,
  map,
  type Observable,
  shareReplay,
  tap,
  throwError,
} from 'rxjs';
import { IdentityApiClient } from '../api/identity-api.client';
import type { AuthSession } from '../api/identity-api.types';
import { RuntimeConfigService } from '../api/runtime-config.service';
import { SessionStore } from './session.store';

@Injectable({ providedIn: 'root' })
export class AuthRefreshService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);
  private readonly identityApi = inject(IdentityApiClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly session = inject(SessionStore);
  private inFlight: Observable<string> | undefined;
  private readonly channel = this.createChannel();
  private lastBroadcast: { session: AuthSession; receivedAt: number } | undefined;

  constructor() {
    this.channel?.addEventListener('message', (event: MessageEvent<unknown>) => {
      const message = readAuthBroadcast(event.data);
      if (!message) return;
      if (message.type === 'logout') {
        this.session.markAnonymous();
        this.identityApi.resetCsrf();
        return;
      }

      this.lastBroadcast = { session: message.session, receivedAt: Date.now() };
      this.session.establish(message.session);
      this.identityApi.resetCsrf();
    });
    this.destroyRef.onDestroy(() => {
      this.channel?.close();
    });
  }

  refresh(): Observable<string> {
    if (!isPlatformBrowser(this.platformId) || !this.runtimeConfig.value().capabilities.identity) {
      return throwError(() => new Error('Identity refresh is not available.'));
    }
    if (this.inFlight) return this.inFlight;

    this.inFlight = from(this.refreshAcrossTabs()).pipe(
      tap((session) => {
        this.session.establish(session);
        this.identityApi.resetCsrf();
        this.channel?.postMessage({ type: 'session', session } satisfies AuthBroadcast);
      }),
      map((session) => session.accessToken),
      finalize(() => {
        this.inFlight = undefined;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.inFlight;
  }

  broadcastLogout(): void {
    this.channel?.postMessage({ type: 'logout' } satisfies AuthBroadcast);
  }

  private async refreshAcrossTabs(): Promise<AuthSession> {
    const freshBroadcast = this.getFreshBroadcast();
    if (freshBroadcast) return freshBroadcast;

    const locks = (globalThis.navigator as { locks?: LockManager }).locks;
    if (!locks) return firstValueFrom(this.identityApi.refreshSession());

    return locks.request('dorosak-session-refresh', async () => {
      const sessionFromOtherTab = this.getFreshBroadcast();
      return sessionFromOtherTab ?? firstValueFrom(this.identityApi.refreshSession());
    });
  }

  private getFreshBroadcast(): AuthSession | undefined {
    return this.lastBroadcast && Date.now() - this.lastBroadcast.receivedAt <= 5_000
      ? this.lastBroadcast.session
      : undefined;
  }

  private createChannel(): BroadcastChannel | undefined {
    return isPlatformBrowser(this.platformId) && typeof globalThis.BroadcastChannel === 'function'
      ? new BroadcastChannel('dorosak-auth-v1')
      : undefined;
  }
}

type AuthBroadcast = { type: 'session'; session: AuthSession } | { type: 'logout' };

const readAuthBroadcast = (value: unknown): AuthBroadcast | undefined => {
  if (!isRecord(value) || typeof value['type'] !== 'string') return undefined;
  if (value['type'] === 'logout') return { type: 'logout' };
  if (value['type'] !== 'session') return undefined;
  const session = value['session'];
  if (
    !isRecord(session) ||
    typeof session['accessToken'] !== 'string' ||
    typeof session['accessTokenExpiresAt'] !== 'string' ||
    !isIdentitySnapshot(session['identity'])
  ) {
    return undefined;
  }
  return { type: 'session', session: session as unknown as AuthSession };
};

const isIdentitySnapshot = (value: unknown): boolean =>
  isRecord(value) &&
  typeof value['userId'] === 'string' &&
  typeof value['sessionId'] === 'string' &&
  typeof value['displayName'] === 'string' &&
  typeof value['email'] === 'string' &&
  Array.isArray(value['roles']) &&
  Array.isArray(value['permissions']) &&
  Array.isArray(value['authenticationMethods']);

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
