import { TestBed } from '@angular/core/testing';
import { RuntimeConfigService } from './runtime-config.service';

describe('RuntimeConfigService', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('loads public runtime configuration without secrets', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            apiBasePath: '/api/v1',
            release: 'test-release',
            defaultLocale: 'ar',
            supportedLocales: ['ar', 'en'],
            capabilities: { identity: false, learning: false, offline: true },
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        ),
      ),
    );
    const service = TestBed.inject(RuntimeConfigService);

    await service.load();

    expect(service.value().release).toBe('test-release');
    expect(service.apiUrl('/system/status')).toBe('/api/v1/system/status');
  });

  it('fails closed for malformed public configuration', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ apiBasePath: 'https://untrusted.example' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    );
    const service = TestBed.inject(RuntimeConfigService);

    await expect(service.load()).rejects.toThrow('Runtime configuration is invalid.');
  });
});
