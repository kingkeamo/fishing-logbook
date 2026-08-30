import { test, expect, chromium } from '@playwright/test';
import { createAuthenticatedContext } from '../support/multi-user-auth.mjs';
import { ensureDisplayName } from '../support/angler-identity.mjs';
import { testName } from '../support/catch-journey.mjs';
import {
    startTrip,
    reloadTrip,
    inviteAngler,
    acceptInvitation,
    addTripNote,
    recordCatchForAngler,
    openCatchEditFromList,
    correctCaughtBy
} from '../support/trip-journey.mjs';

const anglerOneName = 'E2E Angler One';
const anglerTwoName = 'E2E Angler Two';

test.describe('Trip collaboration', () => {
    test.describe.configure({ mode: 'serial' });

    /** @type {import('@playwright/test').Browser} */
    let browser;
    let owner;
    let participant;

    test.beforeAll(async () => {
        browser = await chromium.launch();
        owner = await createAuthenticatedContext(browser, 1);
        participant = await createAuthenticatedContext(browser, 2);
        await ensureDisplayName(owner.page, anglerOneName);
        await ensureDisplayName(participant.page, anglerTwoName);
    });

    test.afterAll(async () => {
        await owner.context.close();
        await participant.context.close();
        await browser.close();
    });

    test('owner invites, participant accepts, and both contexts see the shared Trip after reload', async () => {
        test.setTimeout(120_000);
        const tripId = await startTrip(owner.page);

        const invitedUserId = await inviteAngler(owner.page, tripId, anglerTwoName);
        await expect(owner.page.locator(`#trip-participant-${invitedUserId}`)).toBeVisible();
        await owner.page.locator('#trip-participants-close').click();

        await acceptInvitation(participant.page, tripId);
        await participant.page.reload();
        await expect(participant.page.locator(`#trip-invitation-accept-${tripId}`)).toHaveCount(0);
        await expect(participant.page.locator(`#trip-list-item-${tripId}`)).toBeVisible();
        await expect(participant.page.locator(`#trip-list-shared-${tripId}`)).toBeVisible();

        await participant.page.locator(`#trip-list-view-${tripId}`).click();
        await expect(participant.page.locator('#trip-loading')).toBeHidden();
        await expect(participant.page.locator('#active-trip-card')).toBeVisible();

        await owner.page.reload();
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        await expect(owner.page.locator(`#trip-participant-status-${invitedUserId}`)).not.toHaveText('');
    });

    test('recorder records a Catch for the participant, and Caught By/Recorded By survive reload', async () => {
        test.setTimeout(120_000);
        const tripId = await startTrip(owner.page);
        const invitedUserId = await inviteAngler(owner.page, tripId, anglerTwoName);
        await owner.page.locator('#trip-participants-close').click();
        await acceptInvitation(participant.page, tripId);

        await owner.page.goto(`/trips/${tripId}`);
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        await recordCatchForAngler(owner.page, invitedUserId);

        await owner.page.goto(`/trips/${tripId}`);
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        await expect(owner.page.locator('.trip-timeline-catch-recorder')).toContainText(anglerOneName);

        const catchId = await owner.page.locator('.trip-timeline-catch').first()
            .locator('[id$="-link"]').getAttribute('id')
            .then(id => id.replace('trip-timeline-catch-', '').replace('-link', ''));
        await openCatchEditFromList(owner.page, catchId);
        await expect(owner.page.locator('#catch-provenance-editor')).toBeVisible();
        await expect(owner.page.locator('#catch-provenance-recorder-name')).toHaveText(anglerOneName);
        await expect(owner.page.locator(`#catch-provenance-angler-${invitedUserId}.mud-chip-filled.mud-chip-color-primary`)).toBeVisible();

        await owner.page.reload();
        await expect(owner.page.locator('#catch-edit-loading')).toBeHidden();
        await expect(owner.page.locator('#catch-provenance-recorder-name')).toHaveText(anglerOneName);
        await expect(owner.page.locator(`#catch-provenance-angler-${invitedUserId}.mud-chip-filled.mud-chip-color-primary`)).toBeVisible();

        await owner.page.goto('/catches');
        await expect(owner.page.locator('#catch-list-loading')).toBeHidden();
        await expect(owner.page.locator(`#catch-card-${catchId}`)).toBeVisible();
    });

    test('recorder corrects Caught By, and the correction survives a full reload including the photograph', async () => {
        test.setTimeout(120_000);
        const tripId = await startTrip(owner.page);
        const invitedUserId = await inviteAngler(owner.page, tripId, anglerTwoName);
        await owner.page.locator('#trip-participants-close').click();
        await acceptInvitation(participant.page, tripId);

        await owner.page.goto(`/trips/${tripId}`);
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        await recordCatchForAngler(owner.page, null);

        await owner.page.goto(`/trips/${tripId}`);
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        const catchId = await owner.page.locator('.trip-timeline-catch').first()
            .locator('[id$="-link"]').getAttribute('id')
            .then(id => id.replace('trip-timeline-catch-', '').replace('-link', ''));

        await openCatchEditFromList(owner.page, catchId);
        await expect(owner.page.locator('#catch-provenance-editor')).toBeVisible();
        const photoBeforeSrc = await owner.page.locator('#catch-edit-photos-panel img').first().getAttribute('src');
        expect(photoBeforeSrc).toBeTruthy();

        await correctCaughtBy(owner.page, invitedUserId);

        await owner.page.reload();
        await expect(owner.page.locator('#catch-edit-loading')).toBeHidden();
        await expect(owner.page.locator(`#catch-provenance-angler-${invitedUserId}.mud-chip-filled.mud-chip-color-primary`)).toBeVisible();
        await expect(owner.page.locator('#catch-provenance-recorder-name')).toHaveText(anglerOneName);
        const photoAfterSrc = await owner.page.locator('#catch-edit-photos-panel img').first().getAttribute('src');
        expect(photoAfterSrc).toBeTruthy();

        await owner.page.goto('/catches');
        await expect(owner.page.locator('#catch-list-loading')).toBeHidden();
        await expect(owner.page.locator(`#catch-card-${catchId}`)).toBeVisible();

        await owner.page.goto(`/trips/${tripId}`);
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        await expect(owner.page.locator('.trip-timeline-catch-recorder')).toContainText(anglerOneName);
    });

    test('accepted participant adds a Trip note visible to both users after authoritative reload', async () => {
        test.setTimeout(240_000);
        const tripId = await startTrip(owner.page);
        await inviteAngler(owner.page, tripId, anglerTwoName);
        await owner.page.locator('#trip-participants-close').click();
        await acceptInvitation(participant.page, tripId);

        await participant.page.locator(`#trip-list-view-${tripId}`).click();
        await expect(participant.page.locator('#trip-loading')).toBeHidden();
        const noteText = testName('participant-note');
        await addTripNote(participant.page, noteText);

        await reloadTrip(participant.page);
        await expect(participant.page.locator('.trip-timeline-note-text')).toContainText(noteText);

        await owner.page.goto(`/trips/${tripId}`);
        await expect(owner.page.locator('#trip-loading')).toBeHidden();
        await expect(owner.page.locator('.trip-timeline-note-text')).toContainText(noteText);
        await expect(owner.page.locator('.trip-timeline-contributor')).toContainText(anglerTwoName);
    });
});
