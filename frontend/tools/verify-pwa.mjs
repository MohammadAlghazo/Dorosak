import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

const root = join(import.meta.dirname, '..');
const manifest = JSON.parse(await readFile(join(root, 'public', 'manifest.webmanifest'), 'utf8'));
const serviceWorker = JSON.parse(await readFile(join(root, 'ngsw-config.json'), 'utf8'));

if (manifest.start_url !== '/ar' || manifest.scope !== '/' || manifest.display !== 'standalone') {
  throw new Error('The PWA manifest does not satisfy the Dorosak navigation contract.');
}

const iconPurposes = new Set(manifest.icons.map((icon) => icon.purpose));
if (!iconPurposes.has('any') || !iconPurposes.has('maskable')) {
  throw new Error('The PWA manifest requires standard and maskable icons.');
}

const exclusions = new Set(serviceWorker.navigationUrls.filter((url) => url.startsWith('!')));
for (const path of ['!/api/**', '!/hubs/**', '!/integrations/**', '!/media/signed/**']) {
  if (!exclusions.has(path)) throw new Error(`Service worker navigation must exclude ${path}.`);
}

if (serviceWorker.dataGroups?.length) {
  throw new Error('Phase 4 must not cache API responses in Angular Service Worker data groups.');
}

console.log('PWA manifest and cache exclusions are valid.');
