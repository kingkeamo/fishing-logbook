import { defineConfig, devices } from '@playwright/test';
import { resolve } from 'node:path';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5019';

export default defineConfig({
    testDir: './specs',
    outputDir: 'artifacts/test-results',
    globalSetup: './support/auth.setup.mjs',
    timeout: 45_000,
    expect: { timeout: 10_000 },
    fullyParallel: false,
    workers: 1,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 1 : 0,
    reporter: [['list'], ['html', { outputFolder: 'artifacts/report', open: 'never' }]],
    use: {
        ...devices['Desktop Chrome'],
        baseURL,
        ignoreHTTPSErrors: true,
        storageState: resolve('.auth/e2e-user.json'),
        screenshot: 'only-on-failure',
        // Authenticated traces contain network metadata and remain local-only.
        trace: process.env.CI ? 'off' : 'retain-on-failure',
        video: 'off'
    },
    projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
    webServer: process.env.E2E_EXTERNAL_STACK === 'true' ? undefined : {
        command: 'node support/start-stack.mjs',
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 180_000,
        stdout: 'pipe',
        stderr: 'pipe'
    }
});
