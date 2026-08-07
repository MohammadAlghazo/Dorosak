import { defineConfig, devices } from '@playwright/test';

const e2ePort = process.env['DOROSAK_E2E_PORT'] ?? '4000';
const baseURL = `http://127.0.0.1:${e2ePort}`;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: Boolean(process.env['CI']),
  retries: process.env['CI'] ? 2 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm run serve:ssr',
    url: `${baseURL}/health`,
    env: {
      PORT: e2ePort,
      DOROSAK_ALLOWED_HOSTS: `127.0.0.1:${e2ePort},127.0.0.1,localhost`,
    },
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile-chromium',
      use: { ...devices['Pixel 7'] },
    },
  ],
});
