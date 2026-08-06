import type { ErrorHandler } from '@angular/core';
import { inject, Injectable } from '@angular/core';
import { TelemetryService } from '../telemetry/telemetry.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly telemetry = inject(TelemetryService);

  handleError(error: unknown): void {
    const name = error instanceof Error ? error.name : 'UnknownError';
    this.telemetry.recordUnhandledError(name);
  }
}
