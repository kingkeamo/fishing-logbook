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

    const { username, password } = credentialsFor(userNumber);
    const applicationOrigin = new URL(baseURL).origin;
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
