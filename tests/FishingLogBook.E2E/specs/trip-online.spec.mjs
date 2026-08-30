import { test, expect } from '../support/fixtures.mjs';
import { testName } from '../support/catch-journey.mjs';
import {
    startTrip,
    reloadTrip,
    addTripNote,
    finishTrip
} from '../support/trip-journey.mjs';

test('solo Trip: catch and note persist through the authoritative round trip, and finishing survives reload', async ({ page }) => {
    // The Add Note time picker only carries minute precision and rejects a timestamp
    // before the Trip's exact (sub-minute) start instant, so adding a note in the same
    // minute the Trip started can require waiting for the real wall clock to roll into
    // the next minute (up to ~60s) before any value satisfies both bounds.
    test.setTimeout(240_000);
    const tripId = await startTrip(page);
    await expect(page.locator('#active-trip-status')).toBeVisible();

    await page.locator('#active-trip-record-catch').click();
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await expect(page.locator('#catch-trip-name')).toBeVisible();
    const png = Buffer.from(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
    await page.locator('#record-catch-method-Fly').click();
    await page.locator('#record-catch-species-BrownTrout').click();
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles({
        name: 'e2e-solo-trip-catch.png', mimeType: 'image/png', buffer: png
    });
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/catches')
            && response.request().method() === 'POST'
            && response.ok()),
        page.waitForResponse(response =>
            /\/api\/catches\/[0-9a-f-]+\/photographs$/i.test(new URL(response.url()).pathname)
            && response.request().method() === 'POST'
            && response.ok()),
        page.locator('#save-catch-button').click()
    ]);
    await expect(page.locator('#catch-saved')).toBeVisible();
    await page.locator('a[href="/catches"]').first().click();
    await expect(page.locator('#catch-list-loading')).toBeHidden();

    await page.goto(`/trips/${tripId}`);
    await expect(page.locator('#trip-loading')).toBeHidden();
    await expect(page.locator('#active-trip-catch-count')).toContainText('1');

    const noteText = testName('solo-trip-note');
    await addTripNote(page, noteText);
    await expect(page.locator('#active-trip-note-count')).toContainText('1');

    await reloadTrip(page);
    await expect(page.locator('#active-trip-catch-count')).toContainText('1');
    await expect(page.locator('#active-trip-note-count')).toContainText('1');
    await expect(page.locator('.trip-timeline-note-text')).toContainText(noteText);

    await finishTrip(page);
    await expect(page.locator('#active-trip-finish')).toHaveCount(0);

    await page.reload();
    await expect(page.locator('#trip-loading')).toBeHidden();
    await expect(page.locator('#active-trip-catch-count')).toContainText('1');
    await expect(page.locator('#active-trip-note-count')).toContainText('1');
    await expect(page.locator('.trip-timeline-note-text')).toContainText(noteText);
    await expect(page.locator('#active-trip-finish')).toHaveCount(0);
});

test('a blank Trip can be started and finished with no catches and renders a valid recap after reload', async ({ page }) => {
    const tripId = await startTrip(page);

    await finishTrip(page);
    await expect(page.locator('#active-trip-catch-count')).toHaveText('No catches yet');

    await page.goto(`/trips/${tripId}`);
    await expect(page.locator('#trip-loading')).toBeHidden();
    await expect(page.locator('#active-trip-card')).toBeVisible();
    await expect(page.locator('#active-trip-catch-count')).toHaveText('No catches yet');
    await expect(page.locator('#active-trip-finish')).toHaveCount(0);
});
