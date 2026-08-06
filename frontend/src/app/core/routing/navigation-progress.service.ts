import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  Router,
} from '@angular/router';

@Injectable({ providedIn: 'root' })
export class NavigationProgressService {
  private readonly visible = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;

  readonly isVisible = this.visible.asReadonly();

  constructor() {
    const destroyRef = inject(DestroyRef);
    inject(Router)
      .events.pipe(takeUntilDestroyed(destroyRef))
      .subscribe((event) => {
        if (event instanceof NavigationStart) {
          this.timer = setTimeout(() => this.visible.set(true), 150);
        }
        if (
          event instanceof NavigationEnd ||
          event instanceof NavigationCancel ||
          event instanceof NavigationError
        ) {
          if (this.timer) clearTimeout(this.timer);
          this.timer = undefined;
          this.visible.set(false);
        }
      });
    destroyRef.onDestroy(() => {
      if (this.timer) clearTimeout(this.timer);
    });
  }
}
