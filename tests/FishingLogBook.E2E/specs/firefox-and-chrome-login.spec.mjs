import { test } from '@playwright/test';
import { firefox, chromium } from '@playwright/test';
import { signIn } from '../support/cognito-login.mjs';

function required(name) {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is required. Set it in the same terminal running this test.`);
    return value;
}

async function clearBrowserState(context, page, baseURL) {
    await page.goto(baseURL);
    await page.evaluate(() => {
        window.localStorage.clear();
        window.sessionStorage.clear();
    });
    await context.clearCookies();
}

test('firefox and chrome login', async () => {
    test.setTimeout(120_000);
    const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5019';
    const applicationOrigin = new URL(baseURL).origin;

    const firefoxBrowser = await firefox.launch();
    const chromiumBrowser = await chromium.launch({ headless: false });
    try {
        const firefoxContext = await firefoxBrowser.newContext({ baseURL, ignoreHTTPSErrors: true });
        const firefoxPage = await firefoxContext.newPage();
        await clearBrowserState(firefoxContext, firefoxPage, baseURL);
        await signIn(
            firefoxPage,
            applicationOrigin,
            required('E2E_COGNITO_USERNAME_3'),
            required('E2E_COGNITO_PASSWORD_3'));

        const chromiumContext = await chromiumBrowser.newContext({ baseURL, ignoreHTTPSErrors: true });
        const chromiumPage = await chromiumContext.newPage();
        await clearBrowserState(chromiumContext, chromiumPage, baseURL);
        await signIn(
            chromiumPage,
            applicationOrigin,
            required('E2E_COGNITO_USERNAME_2'),
            required('E2E_COGNITO_PASSWORD_2'));
    } finally {
        await firefoxBrowser.close();
        await chromiumBrowser.close();
    }
});
