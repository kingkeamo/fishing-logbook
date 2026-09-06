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

    test('keeps a full twenty-photo batch distinct and clears every entry', async ({ page }) => {
        await page.goto(harness);
        await expect(page.locator('#status')).toHaveText('ready');

        const result = await page.evaluate(async () => {
            const registrations = [];
            for (let index = 0; index < 20; index += 1) {
                registrations.push(await window.importPhotoHarness.registerTestImage(index % 2 === 0));
            }

            window.importPhotoHarness.clear();
            const missing = [];
            for (const registration of registrations) {
                try {
                    await window.importPhotoHarness.readLength(registration.token);
                    missing.push(false);
                } catch {
                    missing.push(true);
                }
            }

            return { registrations, missing };
        });

        expect(new Set(result.registrations.map(registration => registration.token)).size).toBe(20);
        expect(new Set(result.registrations.map(registration => registration.thumbnailUrl)).size).toBe(20);
        expect(result.missing).toEqual(Array(20).fill(true));
    });
});
