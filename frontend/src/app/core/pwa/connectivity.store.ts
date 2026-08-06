import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ConnectivityStore {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);
  private readonly connected = signal(true);

  readonly isOnline = this.connected.asReadonly();

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;
    this.connected.set(navigator.onLine);
    const online = () => this.connected.set(true);
    const offline = () => this.connected.set(false);
    addEventListener('online', online);
    addEventListener('offline', offline);
    this.destroyRef.onDestroy(() => {
      removeEventListener('online', online);
      removeEventListener('offline', offline);
    });
  }
}
