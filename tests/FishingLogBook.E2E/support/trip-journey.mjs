import { expect } from '@playwright/test';

export async function startTrip(page) {
    await page.goto('/catches');
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await expect(page.locator('#trip-action-loading')).toBeHidden();
    // A user can only have one active Trip at a time, so #trip-start-link is only
    // rendered when there isn't one already; #trip-update-link appears instead. When a
    // context is reused across multiple journeys in the same file, finish whatever is
    // still active first so this always starts a genuinely new Trip.
    if (await page.locator('#trip-update-link').count() > 0) {
        await page.locator('#trip-update-link').click();
        await expect(page.locator('#trip-loading')).toBeHidden();
        if (await page.locator('#active-trip-finish').count() > 0) {
            await finishTrip(page);
        }
        await page.goto('/catches');
        await expect(page.locator('#trip-action-loading')).toBeHidden();
    }

    await page.locator('#trip-start-link').click();
    await page.waitForURL(url => /\/trips\/[0-9a-f-]+$/i.test(new URL(url).pathname));
    await expect(page.locator('#active-trip-card')).toBeVisible();
    return tripIdFromUrl(page);
}

export function tripIdFromUrl(page) {
    const match = new URL(page.url()).pathname.match(/\/trips\/([0-9a-f-]+)$/i);
    if (!match) throw new Error(`Not on a Trip page: ${page.url()}`);
    return match[1];
}

export async function openTrip(page, tripId) {
    await page.goto(`/trips/${tripId}`);
    await expect(page.locator('#trip-loading')).toBeHidden();
}

export async function reloadTrip(page) {
    await page.reload();
    await expect(page.locator('#trip-loading')).toBeHidden();
}

export async function addTripNote(page, text) {
    // The time input only carries minute precision, and the picker rejects a note
    // timestamped before the Trip's actual (sub-minute) start instant, so a note added
    // in the same minute the Trip started needs the wall clock to roll into the next
    // minute before any HH:MM value can be both >= start and <= now. Reopening the
    // modal recomputes its "now" default, so retrying the open/fill/check cycle is a
    // real, observable-state wait rather than a fixed sleep.
    await expect.poll(async () => {
        await page.locator('#trip-note-start').click();
        await expect(page.locator('#trip-note-modal')).toBeVisible();
        await page.locator('#trip-note-text').fill(text);
        const invalid = await page.locator('#trip-note-recorded-on-invalid').count();
        if (invalid > 0) {
            await page.locator('#trip-note-cancel').click();
            await expect(page.locator('#trip-note-modal')).toBeHidden();
        }
        return invalid;
    }, { timeout: 90_000, intervals: [2_000] }).toBe(0);
    await expect(page.locator('#trip-note-save')).toBeEnabled();
    await page.locator('#trip-note-save').click();
    await expect(page.locator('#trip-note-modal')).toBeHidden();
}

export async function addTripPhotograph(page, file) {
    const existingCount = await page.locator('.trip-timeline-photograph').count();
    await page.locator('#active-trip-add-photo').click();
    await page.locator('#trip-photo-gallery input, #trip-photo-gallery').setInputFiles(file);
    await expect(page.locator('.trip-timeline-photograph')).toHaveCount(existingCount + 1);
}

export async function finishTrip(page) {
    await page.locator('#active-trip-finish').click();
    await expect(page.locator('#active-trip-status')).toContainText(await finishedStatusText(page));
}

async function finishedStatusText(page) {
    return page.evaluate(() => document.querySelector('#active-trip-status')?.textContent ?? '');
}

export async function openParticipants(page) {
    await page.locator('#active-trip-participants').click();
    await expect(page.locator('#trip-participants-modal')).toBeVisible();
    await expect(page.locator('#trip-participants-loading')).toBeHidden();
}

export async function inviteAngler(page, tripId, displayName) {
    await openParticipants(page);
    await page.locator('#trip-participants-invite').click();
    await expect(page.locator('#invite-angler-modal')).toBeVisible();
    await page.locator('#invite-angler-search').fill(displayName);
    const search = page.waitForResponse(response =>
        new URL(response.url()).pathname === '/api/profiles/lookup'
        && response.request().method() === 'GET'
        && response.ok());
    await search;
    await expect(page.locator('#invite-angler-searching')).toBeHidden();
    await expect(page.locator('#invite-angler-results')).toBeVisible();
    const result = page.locator('#invite-angler-results .invite-angler-result').first();
    const userId = await result.getAttribute('id').then(id => id.replace('invite-angler-result-', ''));
    await Promise.all([
        page.waitForResponse(response =>
            /\/api\/trips\/[0-9a-f-]+\/participants$/i.test(new URL(response.url()).pathname)
            && response.request().method() === 'POST'
            && response.ok()),
        page.locator(`#invite-angler-invite-${userId}`).click()
    ]);
    await expect(page.locator('#invite-angler-modal')).toBeHidden();
    return userId;
}

export async function acceptInvitation(page, tripId) {
    await page.goto('/trips');
    await expect(page.locator(`#trip-invitation-accept-${tripId}`)).toBeVisible();
    await Promise.all([
        page.waitForResponse(response =>
            /\/api\/trips\/[0-9a-f-]+\/invitation\/accept$/i.test(new URL(response.url()).pathname)
            && response.request().method() === 'POST'
            && response.ok()),
        page.locator(`#trip-invitation-accept-${tripId}`).click()
    ]);
}

export async function removeParticipant(page, participantUserId) {
    await openParticipants(page);
    await page.locator(`#trip-participant-remove-${participantUserId}`).click();
    await expect(page.locator(`#trip-participant-${participantUserId}`)).toHaveCount(0);
    await page.locator('#trip-participants-close').click();
}

export async function recordCatchForAngler(page, anglerUserId, options = {}) {
    const png = Buffer.from(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator(`#record-catch-method-${options.methodCode ?? 'Fly'}`).click();
    await page.locator(`#record-catch-species-${options.speciesCode ?? 'BrownTrout'}`).click();
    if (anglerUserId) {
        await page.locator(`#record-catch-angler-${anglerUserId}`).click();
    }
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles({
        name: 'e2e-catch.png', mimeType: 'image/png', buffer: png
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
    await page.locator('#catch-view-catches').click();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
}

export async function openCatchEditFromList(page, catchId) {
    if (new URL(page.url()).pathname !== '/catches') {
        await page.goto('/catches');
        await expect(page.locator('#catch-list-loading')).toBeHidden();
    }
    await page.locator(`#catch-card-menu-${catchId}`).click();
    await page.locator(`#catch-card-edit-${catchId}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
}

export async function correctCaughtBy(page, newAnglerUserId) {
    await expect(page.locator('#catch-provenance-editor')).toBeVisible();
    await page.locator(`#catch-provenance-angler-${newAnglerUserId}`).click();
    await Promise.all([
        page.waitForResponse(response =>
            /\/api\/catches\/[0-9a-f-]+\/angler$/i.test(new URL(response.url()).pathname)
            && response.request().method() === 'PATCH'
            && response.ok()),
        page.locator('#catch-provenance-update').click()
    ]);
}
