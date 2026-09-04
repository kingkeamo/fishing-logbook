import { expect, test } from '@playwright/test';

const harness = '/src/FishingLogBook.Web/BrowserTests/harness/import-photo-blob-registry.html';

test.describe('Import photo blob registry', () => {
    test('stores sanitised bytes and releases thumbnail object URLs', async ({ page }) => {
        await page.goto(harness);
        await expect(page.locator('#status')).toHaveText('ready');

        const result = await page.evaluate(async () => {
            const registration = await window.importPhotoHarness.registerTestImage(true);
            const response = await fetch(registration.thumbnailUrl);
            const thumbnailType = response.headers.get('content-type');
            const storedLength = await window.importPhotoHarness.readLength(registration.token);
            const removed = window.importPhotoHarness.remove(registration.token);
            let missingAfterRemoval = false;
            try {
                await window.importPhotoHarness.readLength(registration.token);
            } catch {
                missingAfterRemoval = true;
            }

            return { registration, thumbnailType, storedLength, removed, missingAfterRemoval };
        });

        expect(result.registration.token).not.toBe('photo.png');
        expect(result.registration.thumbnailUrl).toMatch(/^blob:/);
        expect(result.thumbnailType).toBe('image/jpeg');
        expect(result.storedLength).toBeGreaterThan(0);
        expect(result.removed).toBe(true);
        expect(result.missingAfterRemoval).toBe(true);
    });

    test('keeps sequential registrations distinct and clears all entries', async ({ page }) => {
        await page.goto(harness);
        await expect(page.locator('#status')).toHaveText('ready');

        const result = await page.evaluate(async () => {
            const first = await window.importPhotoHarness.registerTestImage(true);
            const second = await window.importPhotoHarness.registerTestImage(false);
            window.importPhotoHarness.clear();
            const missing = [];
            for (const registration of [first, second]) {
                try {
                    await window.importPhotoHarness.readLength(registration.token);
                    missing.push(false);
                } catch {
                    missing.push(true);
                }
            }

            return { first, second, missing };
        });

        expect(result.first.token).not.toBe(result.second.token);
        expect(result.first.thumbnailUrl).not.toBe(result.second.thumbnailUrl);
        expect(result.missing).toEqual([true, true]);
    });
});
