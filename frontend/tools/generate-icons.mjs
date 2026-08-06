import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';
import sharp from 'sharp';

const outputDirectory = join(import.meta.dirname, '..', 'public', 'icons');
await mkdir(outputDirectory, { recursive: true });

const createSource = (safeArea) =>
  Buffer.from(`
  <svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
    <rect width="512" height="512" rx="${safeArea ? 0 : 88}" fill="#0f766e"/>
    <path d="M${safeArea ? 132 : 96} 154h280v56H${safeArea ? 132 : 96}z" fill="#ccfbf1"/>
    <path d="M${safeArea ? 132 : 96} 238h214v56H${safeArea ? 132 : 96}z" fill="#fff"/>
    <path d="M${safeArea ? 132 : 96} 322h156v56H${safeArea ? 132 : 96}z" fill="#99f6e4"/>
  </svg>
`);

for (const size of [192, 512]) {
  await sharp(createSource(false))
    .resize(size, size)
    .png()
    .toFile(join(outputDirectory, `icon-${size}.png`));
  await sharp(createSource(true))
    .resize(size, size)
    .png()
    .toFile(join(outputDirectory, `icon-maskable-${size}.png`));
}
