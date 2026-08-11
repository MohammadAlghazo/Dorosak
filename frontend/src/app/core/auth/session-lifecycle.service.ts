import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class SessionLifecycleService {
  private readonly endingSource = new Subject<void>();

  readonly ending$ = this.endingSource.asObservable();

  endActiveSession(): void {
    this.endingSource.next();
  }
}
