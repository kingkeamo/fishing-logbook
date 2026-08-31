import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { signIn, readSessionStorage } from './cognito-login.mjs';

const credentialCache = new Map();

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

async function authenticatedState(browser, baseURL, userNumber) {
    if (credentialCache.has(userNumber)) {
        return credentialCache.get(userNumber);
    }

    const applicationOrigin = new URL(baseURL).origin;

    // User 1 already has a live Cognito SSO session from global setup's own sign-in
    // (support/auth.setup.mjs). A second interactive login for the same real account
    // races Cognito's silent SSO bounce-back (the hosted UI skips the form and
    // redirects to the callback before our own navigation waits start watching for
    // it), so reuse the storage state global setup already captured instead of
    // logging in again.
    if (userNumber === 1) {
        const storageState = JSON.parse(await readFile(resolve('.auth/e2e-user.json'), 'utf8'));
        const sessionStorage = JSON.parse(await readFile(resolve('.auth/e2e-session.json'), 'utf8'));
        const state = { storageState, sessionStorage, origin: applicationOrigin };
        credentialCache.set(userNumber, state);
        return state;
    }

    const { username, password } = credentialsFor(userNumber);
    const context = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
    try {
        const page = await context.newPage();
        await signIn(page, applicationOrigin, username, password);
        const storageState = await context.storageState();
        const sessionStorage = await readSessionStorage(page);
        const state = { storageState, sessionStorage, origin: applicationOrigin };
        credentialCache.set(userNumber, state);
        return state;
    } finally {
        await context.close();
    }
}

/**
 * Creates an isolated, already-authenticated browser context/page for the given
 * E2E Cognito user (1, 2 or 3). The real Cognito sign-in only runs once per user per
 * worker process; subsequent calls replay the captured storage state into a fresh
 * context so each caller gets its own isolated cookies/localStorage/IndexedDB without
 * repeating a slow interactive login every time.
 */
export async function createAuthenticatedContext(browser, userNumber) {
    const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5019';
    const state = await authenticatedState(browser, baseURL, userNumber);
    const context = await browser.newContext({
        baseURL,
        ignoreHTTPSErrors: true,
        storageState: state.storageState
    });
    await context.addInitScript(payload => {
        if (window.location.origin === payload.origin) {
            for (const [key, value] of Object.entries(payload.values)) window.sessionStorage.setItem(key, value);
        }
    }, { origin: state.origin, values: state.sessionStorage });
    const page = await context.newPage();
    return { context, page };
}
