import { Injectable, isDevMode } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TelemetryService {
  recordRequest(method: string, status: number, elapsedMilliseconds: number): void {
    if (isDevMode()) {
      console.debug('HTTP request completed', { method, status, elapsedMilliseconds });
    }
  }

  recordUnhandledError(errorName: string): void {
    if (isDevMode()) console.error('Unhandled application error', { errorName });
  }
}
