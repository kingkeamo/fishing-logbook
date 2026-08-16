import { expect, test } from '@playwright/test';

const harness = '/src/FishingLogBook.Web/BrowserTests/harness';

test.describe('Catch and diagnostic IndexedDB', () => {
    test('writes and reads a Catch', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.putAndGetCatch());

        expect(records).toEqual([{ id: 'harness-catch', notes: 'persisted' }]);
    });

    test('reads a Catch after close and reload', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');
        await page.evaluate(() => window.harness.putAndGetCatch());

        await page.reload();
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.readCatches());
        expect(records).toEqual([{ id: 'harness-catch', notes: 'persisted' }]);
    });

    test('persists a photograph', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const photo = await page.evaluate(() => window.harness.putAndGetPhoto());

        expect(photo.contentType).toBe('image/jpeg');
        expect(photo.bytesBase64).toBe(btoa(String.fromCharCode(7, 8, 9)));
    });

    test('keeps diagnostic storage isolated from Catch storage', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const result = await page.evaluate(() => window.harness.writeIsolatedRecords());

        expect(result.catchStores).toEqual(['testCatchPhotographs', 'testCatches']);
        expect(result.diagnosticStores).toEqual(['diagnosticEvents']);
        expect(result.catches).toEqual([{ id: 'catch-only', notes: 'catch-db' }]);
    });
});

test.describe('service worker application shell', () => {
    test('activates the application shell worker', async ({ page }) => {
        await page.goto(`${harness}/pwa/index.html`);
        await expect(page.locator('#shell')).toHaveText('FishingLogBook app shell');
        await expect(page.locator('#sw-status')).toHaveText('active', { timeout: 15000 });
    });

    test('serves the cached shell while offline', async ({ page, context, browserName }) => {
        test.skip(
            browserName === 'webkit',
            'Playwright WebKit cannot navigate while offline. That is not a substitute for real iPhone testing.');

        await page.goto(`${harness}/pwa/index.html`);
        await expect(page.locator('#sw-status')).toHaveText('active', { timeout: 15000 });

        await context.setOffline(true);
        await page.goto(`${harness}/pwa/`, { waitUntil: 'domcontentloaded' });
        await expect(page.locator('#shell')).toHaveText('FishingLogBook app shell');
    });
});
