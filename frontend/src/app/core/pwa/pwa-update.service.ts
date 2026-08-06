import { inject, Injectable, signal } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Injectable({ providedIn: 'root' })
export class PwaUpdateService {
  private readonly updates = inject(SwUpdate);
  private readonly updateReady = signal(false);

  readonly isUpdateReady = this.updateReady.asReadonly();

  constructor() {
    if (!this.updates.isEnabled) return;
    this.updates.versionUpdates
      .pipe(
        filter((event): event is VersionReadyEvent => event.type === 'VERSION_READY'),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.updateReady.set(true));
  }

  async activate(): Promise<void> {
    if (!this.updateReady()) return;
    await this.updates.activateUpdate();
    location.reload();
  }
}
