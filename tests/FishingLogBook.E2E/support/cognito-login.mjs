export async function signIn(page, applicationOrigin, username, password, pauseAfterClickMs = 0) {
    await page.goto('/');
    await page.locator('#landing-sign-in').click();
    if (pauseAfterClickMs > 0) {
        await page.waitForTimeout(pauseAfterClickMs);
    }
    // Cognito may already hold a live SSO session for this account (e.g. it was just
    // signed in elsewhere in this run) and silently bounce straight back to the app's
    // callback instead of showing the hosted-UI form. Waiting only for "left
    // localhost" races that instant round trip, so watch for either outcome instead
    // of assuming the form will appear.
    await page.waitForURL(url =>
        !url.hostname.includes('localhost')
        || (url.origin === applicationOrigin && url.pathname.includes('/authentication/login-callback')));
    if (!new URL(page.url()).hostname.includes('localhost')) {
        const usernameField = page.locator('input[name="username"], #signInFormUsername');
        // The hosted UI's email field carries autofocus, which in WebKit can race a
        // programmatic .fill() and leave the field visually empty (its own focus
        // handling resets what was just typed). Re-fill if that happens rather than
        // trusting .fill() blindly.
        await usernameField.fill(username);
        if ((await usernameField.inputValue()) !== username) {
            await usernameField.click();
            await usernameField.fill(username);
        }
        await page.locator('input[name="password"], #signInFormPassword').fill(password);
        await page.locator('button[type="submit"], input[type="submit"]').first().click();
    }

    await page.waitForURL(url =>
        url.origin === applicationOrigin
        && !url.pathname.includes('/authentication/login-callback'), { timeout: 45_000 });
    await page.waitForURL(url =>
        url.origin === applicationOrigin
        && ['/catches', '/onboarding'].includes(url.pathname), { timeout: 90_000 });
    await completeOnboardingWhenRequired(page);
}

export async function completeOnboardingWhenRequired(page) {
    if (new URL(page.url()).pathname === '/catches') return;

    await page.locator('#onboarding-loading').waitFor({ state: 'hidden' });
    await page.locator('#onboarding-next').waitFor({ state: 'visible' });
    await page.locator('#onboarding-next').click();
    await page.locator('#onboarding-method-Fly').click();
    await page.locator('#onboarding-species-more-Fly').click();
    await page.locator('#catalogue-picker-modal-option-BrownTrout').click();
    await page.locator('#catalogue-picker-modal-save').click();
    await page.locator('#onboarding-next').click();
    await page.locator('#onboarding-skip-location').click();
    await page.locator('#onboarding-next').click();
    await page.locator('#onboarding-next').click();
    await page.locator('#onboarding-finish').click();
    await page.waitForURL(url => new URL(url).pathname === '/catches', { timeout: 60_000 });
}

export async function readSessionStorage(page) {
    return page.evaluate(() => Object.fromEntries(
        Array.from({ length: window.sessionStorage.length }, (_, index) => {
            const key = window.sessionStorage.key(index);
            return [key, window.sessionStorage.getItem(key)];
        })));
}
