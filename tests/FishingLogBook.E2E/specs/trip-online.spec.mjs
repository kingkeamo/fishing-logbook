import { test, expect } from '../support/fixtures.mjs';
import { testName } from '../support/catch-journey.mjs';
import {
    startTrip,
    reloadTrip,
    addTripNote,
    addTripPhotograph,
    finishTrip
} from '../support/trip-journey.mjs';

const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');

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

test('a Trip photograph persists and remains visible after server reload', async ({ page }) => {
    const tripId = await startTrip(page);

    await addTripPhotograph(page, {
        name: 'e2e-trip-photo.png', mimeType: 'image/png', buffer: png
    });
    await expect(page.locator('#active-trip-photograph-count')).toContainText('1');

    await page.reload();
    await expect(page.locator('#trip-loading')).toBeHidden();
    await expect(page.locator('#active-trip-photograph-count')).toContainText('1');
    await expect(page.locator('.trip-timeline-photograph')).toHaveCount(1);
    await expect(page.locator('.trip-timeline-photograph').first()).toHaveJSProperty('complete', true);

    await page.goto('/trips');
    await expect(page.locator('#trip-list-loading')).toBeHidden();
    await page.goto(`/trips/${tripId}`);
    await expect(page.locator('#trip-loading')).toBeHidden();
    await expect(page.locator('#active-trip-photograph-count')).toContainText('1');
});

test('navigating away from an active Trip and returning preserves it', async ({ page }) => {
    const tripId = await startTrip(page);

    await page.locator('#catch-logbook-nav-link').click();
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await expect(page.locator('#trip-update-link')).toBeVisible();

    await page.locator('#trip-update-link').click();
    await expect(page.locator('#trip-loading')).toBeHidden();
    await expect(page.locator('#active-trip-card')).toBeVisible();
    expect(new URL(page.url()).pathname).toBe(`/trips/${tripId}`);
});
