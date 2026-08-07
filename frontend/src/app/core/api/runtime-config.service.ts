import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, isDevMode, PLATFORM_ID, signal } from '@angular/core';

export interface RuntimeCapabilities {
  identity: boolean;
  learning: boolean;
  offline: boolean;
}

export interface RuntimeConfig {
  apiBasePath: string;
  release: string;
  defaultLocale: 'ar' | 'en';
  supportedLocales: readonly ('ar' | 'en')[];
  capabilities: RuntimeCapabilities;
}

const defaultConfig: RuntimeConfig = {
  apiBasePath: '/api/v1',
  release: 'development',
  defaultLocale: 'ar',
  supportedLocales: ['ar', 'en'],
  capabilities: { identity: isDevMode(), learning: false, offline: true },
};

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly runtimeConfig = signal<RuntimeConfig>(defaultConfig);

  readonly value = this.runtimeConfig.asReadonly();

  async load(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) return;

    const response = await fetch('/runtime-config.json', {
      cache: 'no-store',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      throw new Error(`Runtime configuration failed with HTTP ${String(response.status)}.`);
    }

    const config: unknown = await response.json();
    if (!this.isRuntimeConfig(config)) throw new Error('Runtime configuration is invalid.');
    this.runtimeConfig.set(config);
  }

  apiUrl(path: string): string {
    const normalized = path.replace(/^\/+/, '');
    return `${this.runtimeConfig().apiBasePath}/${normalized}`;
  }

  private isRuntimeConfig(value: unknown): value is RuntimeConfig {
    if (!value || typeof value !== 'object') return false;
    const candidate = value as Partial<RuntimeConfig>;
    return (
      typeof candidate.apiBasePath === 'string' &&
      candidate.apiBasePath.startsWith('/api/') &&
      typeof candidate.release === 'string' &&
      (candidate.defaultLocale === 'ar' || candidate.defaultLocale === 'en') &&
      Array.isArray(candidate.supportedLocales) &&
      Boolean(candidate.capabilities)
    );
  }
}
