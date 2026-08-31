import { signIn } from './cognito-login.mjs';

function credentialsFor(userNumber) {
    const suffix = userNumber === 1 ? '' : `_${userNumber}`;
    const username = required(`E2E_COGNITO_USERNAME${suffix}`);
    const password = required(`E2E_COGNITO_PASSWORD${suffix}`);
    return { username, password };
}

function required(name) {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is required. See tests/FishingLogBook.E2E/README.md.`);
    return value;
}

// A fresh Playwright context should already have no storage, but the app can still
// silently auto-sign-in against unexpected leftover state (verified against
// specs/firefox-and-chrome-login.spec.mjs, which reliably reaches the real Cognito
// form once storage is explicitly cleared first). Clearing defensively before every
// live login avoids that silent bounce.
async function clearBrowserState(context, page, baseURL) {
    await page.goto(baseURL);
    await page.evaluate(() => {
        window.localStorage.clear();
        window.sessionStorage.clear();
    });
    await context.clearCookies();
}

/**
 * Launches the given browser engine (import chromium/firefox/webkit from
 * '@playwright/test' in the calling spec - never re-imported here, so there is no risk
 * of resolving a second, mismatched @playwright/test install alongside the test
 * runner's own) and signs in for real as the given E2E Cognito user (1, 2 or 3).
 * Returns { browser, context, page } - the caller owns closing `browser` when done.
 */
export async function createAuthenticatedContext(engine, userNumber) {
    const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5019';
    const applicationOrigin = new URL(baseURL).origin;
    const { username, password } = credentialsFor(userNumber);

    const browser = await engine.launch();
    const context = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
    const page = await context.newPage();
    await clearBrowserState(context, page, baseURL);
    await signIn(page, applicationOrigin, username, password);
    return { browser, context, page };
}
