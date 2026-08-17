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

        expect(result.catchStores).toEqual(['catchPhotographs', 'catches', 'testCatchPhotographs', 'testCatches']);
        expect(result.diagnosticStores).toEqual(['diagnosticEvents']);
        expect(result.catches).toEqual([{ id: 'catch-only', notes: 'catch-db' }]);
    });
});

test.describe('Production Catch IndexedDB', () => {
    test('writes a Catch and photograph with stable ids', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.putAndGetProductionCatch());

        expect(records).toHaveLength(1);
        expect(JSON.parse(records[0].json).id).toBe('11111111-1111-1111-1111-111111111111');
        expect(records[0].photographs[0].id).toBe('22222222-2222-2222-2222-222222222222');
        expect(records[0].photographs[0].bytesBase64).toBe(btoa(String.fromCharCode(1, 2, 3)));
        expect(JSON.parse(records[0].json).location).toBeUndefined();
    });

    test('keeps Catch and photograph ids after close and reload', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');
        await page.evaluate(() => window.harness.putAndGetProductionCatch());

        await page.reload();
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.readProductionCatches());
        expect(JSON.parse(records[0].json).id).toBe('11111111-1111-1111-1111-111111111111');
        expect(records[0].photographs[0].id).toBe('22222222-2222-2222-2222-222222222222');
    });

    test('keeps three photographs and capture order after close and reload', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');
        await page.evaluate(() => window.harness.putAndGetProductionCatchWithThreePhotographs());

        await page.reload();
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.readProductionCatches());
        const catchRecord = JSON.parse(records[0].json);
        expect(catchRecord.id).toBe('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
        expect(records[0].photographs.map((photograph) => photograph.id)).toEqual([
            '11111111-1111-1111-1111-111111111111',
            '22222222-2222-2222-2222-222222222222',
            '00000000-0000-0000-0000-000000000003'
        ]);
        expect(records[0].photographs.map((photograph) => photograph.catchId)).toEqual([
            catchRecord.id,
            catchRecord.id,
            catchRecord.id
        ]);
        expect(records[0].photographs.map((photograph) => photograph.contentType)).toEqual([
            'image/jpeg',
            'image/png',
            'image/webp'
        ]);
        expect(records[0].photographs.map((photograph) => photograph.bytesBase64)).toEqual([
            btoa(String.fromCharCode(1, 1, 1)),
            btoa(String.fromCharCode(2, 2, 2)),
            btoa(String.fromCharCode(3, 3, 3))
        ]);
    });

    test('does not keep a Catch when photograph persistence fails', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const result = await page.evaluate(() => window.harness.putProductionCatchWithoutPhotographId());

        expect(result.threw).toBe(true);
        expect(result.items.map((item) => JSON.parse(item.json).id)).not.toContain('orphan-catch');
    });

    test('writes a Catch with location and keeps it after close and reload', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');
        await page.evaluate(() => window.harness.putAndGetProductionCatchWithLocation());

        await page.reload();
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.readProductionCatches());
        const catchRecord = JSON.parse(records[0].json);
        expect(catchRecord.id).toBe('11111111-1111-1111-1111-111111111111');
        expect(catchRecord.location).toEqual({
            latitude: 53.2707,
            longitude: -9.0568,
            accuracyMetres: 12,
            capturedOn: '2026-08-17T08:00:00+00:00',
            source: 'DeviceGps',
            visibility: 'Private',
            consentVersion: '1'
        });
        expect(records[0].photographs[0].id).toBe('22222222-2222-2222-2222-222222222222');
    });

    test('still reads a Catch stored without a location property', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const records = await page.evaluate(() => window.harness.putLegacyProductionCatchWithoutLocation());
        const catchRecord = JSON.parse(records[0].json);
        expect(catchRecord.id).toBe('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
        expect(catchRecord.location).toBeUndefined();
        expect(records[0].photographs[0].id).toBe('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
    });

    test('does not let the first signed-in user read or adopt a legacy unscoped Catch', async ({ page }) => {
        await page.goto(`${harness}/index.html`);
        await expect(page.locator('#status')).toHaveText('ready');

        const result = await page.evaluate(() => window.harness.putUnscopedProductionCatchThenReadAsFirstSigner());

        expect(result.firstSignerView).toEqual([]);
        expect(result.originalOwnerView).toEqual([]);
        expect(JSON.stringify(result.firstSignerView)).not.toContain('53.2707');
        expect(JSON.stringify(result.firstSignerView)).not.toContain('unscoped-photo');
        expect(result.stored.userId).toBeUndefined();
        expect(result.stored.location).toEqual({ latitude: 53.2707, longitude: -9.0568 });
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
