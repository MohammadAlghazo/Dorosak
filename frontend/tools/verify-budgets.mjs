import { access, readFile, readdir, stat } from 'node:fs/promises';
import { extname, join, relative } from 'node:path';
import { gzipSync } from 'node:zlib';

const browserDirectory = join(import.meta.dirname, '..', 'dist', 'dorosak-web', 'browser');
const indexPath = await firstExisting([
  join(browserDirectory, 'index.html'),
  join(browserDirectory, 'index.csr.html'),
]);
const index = await readFile(indexPath, 'utf8');
const initialFiles = new Set(
  [...index.matchAll(/(?:src|href)="([^"?]+\.(?:js|css))(?:\?[^\"]*)?"/g)].map((match) =>
    match[1].replace(/^\//, ''),
  ),
);

const files = [];
const walk = async (directory) => {
  for (const entry of await readdir(directory)) {
    const path = join(directory, entry);
    const details = await stat(path);
    if (details.isDirectory()) {
      await walk(path);
    } else {
      files.push(path);
    }
  }
};
await walk(browserDirectory);

const kibibytes = (bytes) => bytes / 1024;
const gzipSize = async (path) => gzipSync(await readFile(path), { level: 9 }).byteLength;
const initialJavaScript = files.filter(
  (path) =>
    extname(path) === '.js' &&
    initialFiles.has(relative(browserDirectory, path).replaceAll('\\', '/')),
);
const initialCss = files.filter(
  (path) =>
    extname(path) === '.css' &&
    initialFiles.has(relative(browserDirectory, path).replaceAll('\\', '/')),
);
const lazyJavaScript = files.filter(
  (path) =>
    extname(path) === '.js' &&
    !initialFiles.has(relative(browserDirectory, path).replaceAll('\\', '/')),
);
const fonts = files.filter((path) => extname(path) === '.woff2');

const initialJavaScriptSize = (await Promise.all(initialJavaScript.map(gzipSize))).reduce(
  (sum, size) => sum + size,
  0,
);
const initialCssSize = (await Promise.all(initialCss.map(gzipSize))).reduce(
  (sum, size) => sum + size,
  0,
);
const fontSize = (await Promise.all(fonts.map((path) => stat(path)))).reduce(
  (sum, details) => sum + details.size,
  0,
);
const failures = [];

if (initialJavaScriptSize > 200 * 1024)
  failures.push(`Initial JavaScript is ${kibibytes(initialJavaScriptSize).toFixed(1)} KiB gzip.`);
if (initialCssSize > 40 * 1024)
  failures.push(`Initial CSS is ${kibibytes(initialCssSize).toFixed(1)} KiB gzip.`);
if (fontSize > 120 * 1024)
  failures.push(`Critical fonts are ${kibibytes(fontSize).toFixed(1)} KiB.`);

for (const path of lazyJavaScript) {
  const size = await gzipSize(path);
  const name = relative(browserDirectory, path).replaceAll('\\', '/');
  const limit = /(learning|editor|player)/u.test(name) ? 250 : 120;
  if (size > limit * 1024)
    failures.push(`${name} is ${kibibytes(size).toFixed(1)} KiB gzip (limit ${limit} KiB).`);
}

console.log(`Initial JS: ${kibibytes(initialJavaScriptSize).toFixed(1)} KiB gzip`);
console.log(`Initial CSS: ${kibibytes(initialCssSize).toFixed(1)} KiB gzip`);
console.log(`Fonts: ${kibibytes(fontSize).toFixed(1)} KiB`);
if (failures.length > 0) {
  throw new Error(failures.join('\n'));
}

async function firstExisting(paths) {
  for (const path of paths) {
    try {
      await access(path);
      return path;
    } catch {
      // Continue to the next known Angular output filename.
    }
  }
  throw new Error('Angular browser index output was not found.');
}
