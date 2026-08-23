import { expect, test } from '@playwright/test';

test.describe('Published offline application shell', () => {
    test('renders Landing promptly after a cold offline reload without auth, API, or automatic WebAuthn', async ({ context, page }) => {
        const apiRequests = [];
        let credentialGetCalls = 0;
        page.on('request', request => {
            if (new URL(request.url()).pathname.startsWith('/api/')) apiRequests.push(request.url());
        });
        await page.addInitScript(() => {
            const credentials = navigator.credentials;
            if (!credentials || typeof credentials.get !== 'function') return;
            const prototype = Object.getPrototypeOf(credentials);
            const original = prototype.get;
            prototype.get = function (...args) {
                window.__credentialGetCalls = (window.__credentialGetCalls ?? 0) + 1;
                return original.apply(this, args);
            };
        });

        await page.goto('/');
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 30000 });
        await page.evaluate(async () => navigator.serviceWorker?.ready);
        await page.reload();
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 30000 });

        await context.setOffline(true);
        const started = Date.now();
        await page.reload({ waitUntil: 'domcontentloaded' });
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 15000 });
        const elapsed = Date.now() - started;
        credentialGetCalls = await page.evaluate(() => window.__credentialGetCalls ?? 0);

        expect(elapsed).toBeLessThan(15000);
        expect(page.getByText('Authorizing...')).toHaveCount(0);
        expect(credentialGetCalls).toBe(0);
        expect(apiRequests).toEqual([]);
    });
});
