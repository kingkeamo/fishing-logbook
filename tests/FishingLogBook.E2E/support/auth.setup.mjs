import { chromium } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

export default async function authenticate(config) {
    const username = required('E2E_COGNITO_USERNAME');
    const password = required('E2E_COGNITO_PASSWORD');
    const browser = await chromium.launch();
    const context = await browser.newContext({
        baseURL: config.projects[0].use.baseURL,
        ignoreHTTPSErrors: true,
        recordVideo: undefined
    });

    try {
        const page = await context.newPage();
        await page.goto('/');
        await page.locator('#landing-sign-in').click();
        await page.waitForURL(url => !url.hostname.includes('localhost'));
        await page.locator('input[name="username"], #signInFormUsername').fill(username);
        await page.locator('input[name="password"], #signInFormPassword').fill(password);
        await page.locator('button[type="submit"], input[type="submit"]').first().click();
        await page.waitForURL(/localhost:5019\/(catches|onboarding)/, { timeout: 45_000 });
        if (page.url().includes('/onboarding')) {
            throw new Error('The dedicated E2E Cognito user must complete onboarding before running E2E tests.');
        }

        await mkdir(resolve('.auth'), { recursive: true });
        await context.storageState({ path: resolve('.auth/e2e-user.json') });
        const sessionStorage = await page.evaluate(() => Object.fromEntries(
            Array.from({ length: window.sessionStorage.length }, (_, index) => {
                const key = window.sessionStorage.key(index);
                return [key, window.sessionStorage.getItem(key)];
            })));
        await writeFile(resolve('.auth/e2e-session.json'), JSON.stringify(sessionStorage), { mode: 0o600 });
    } finally {
        await context.close();
        await browser.close();
    }
}

function required(name) {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is required. See tests/FishingLogBook.E2E/README.md.`);
    return value;
}
