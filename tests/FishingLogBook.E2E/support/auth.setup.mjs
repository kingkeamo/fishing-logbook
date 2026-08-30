import { chromium } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { signIn, readSessionStorage } from './cognito-login.mjs';

export default async function authenticate(config) {
    const username = required('E2E_COGNITO_USERNAME');
    const password = required('E2E_COGNITO_PASSWORD');
    const debugging = process.env.PWDEBUG === '1';
    const browser = await chromium.launch({
        headless: !debugging,
        slowMo: debugging ? 100 : 0
    });
    const context = await browser.newContext({
        baseURL: config.projects[0].use.baseURL,
        ignoreHTTPSErrors: true,
        recordVideo: undefined
    });

    try {
        const page = await context.newPage();
        page.on('console', message => {
            if (message.type() === 'error' && message.text().startsWith('[FLB]')) {
                console.error(`[E2E browser] ${message.text()}`);
            }
        });
        page.on('pageerror', error => {
            console.error(`[E2E browser page error] ${error.message}`);
        });
        const applicationOrigin = new URL(config.projects[0].use.baseURL).origin;
        await signIn(page, applicationOrigin, username, password);

        await mkdir(resolve('.auth'), { recursive: true });
        await context.storageState({ path: resolve('.auth/e2e-user.json') });
        const sessionStorage = await readSessionStorage(page);
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
