import { defineConfig, devices } from '@playwright/test';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const testsRoot = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(testsRoot, '../../..');

export default defineConfig({
    testDir: '.',
    testMatch: '**/*.spec.js',
    outputDir: resolve(repoRoot, 'artifacts/javascript-tests/playwright/test-results'),
    timeout: 30000,
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 1 : 0,
    reporter: [
        ['list'],
        ['html', {
            open: 'never',
            outputFolder: resolve(repoRoot, 'artifacts/javascript-tests/playwright/report')
        }]
    ],
    webServer: {
        command: 'node server.mjs',
        cwd: testsRoot,
        url: 'http://127.0.0.1:4173/src/FishingLogBook.Web/BrowserTests/harness/index.html',
        reuseExistingServer: !process.env.CI,
        timeout: 120000
    },
    use: {
        baseURL: 'http://127.0.0.1:4173',
        trace: 'on-first-retry'
    },
    projects: [
        { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
        { name: 'webkit', use: { ...devices['Desktop Safari'] } }
    ]
});
