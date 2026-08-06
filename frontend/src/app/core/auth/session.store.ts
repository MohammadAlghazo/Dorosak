import { computed, Injectable, signal } from '@angular/core';

export interface SessionIdentity {
  userId: string;
  displayName: string;
  permissions: readonly string[];
}

interface SessionState {
  accessToken: string | null;
  identity: SessionIdentity | null;
}

@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly state = signal<SessionState>({ accessToken: null, identity: null });

  readonly accessToken = computed(() => this.state().accessToken);
  readonly identity = computed(() => this.state().identity);
  readonly isAuthenticated = computed(() => this.state().accessToken !== null);

  establish(accessToken: string, identity: SessionIdentity | null = null): void {
    this.state.set({ accessToken, identity });
  }

  clear(): void {
    this.state.set({ accessToken: null, identity: null });
  }
}
