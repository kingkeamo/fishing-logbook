import { test, expect, chromium, firefox, webkit } from '@playwright/test';
import { createAuthenticatedContext } from '../support/multi-user-auth.mjs';
import { ensureDisplayName } from '../support/angler-identity.mjs';
import {
    startTrip,
    inviteAngler,
    acceptInvitation,
    removeParticipant
} from '../support/trip-journey.mjs';

const anglerOneName = 'E2E Angler One';
const anglerTwoName = 'E2E Angler Two';
const anglerThreeName = 'E2E Angler Three';

test.describe('Trip collaboration security', () => {
    test.describe.configure({ mode: 'serial' });

    let owner;
    let participant;
    let unrelated;

    test.beforeAll(async () => {
        test.setTimeout(300_000);
        owner = await createAuthenticatedContext(chromium, 1);
        participant = await createAuthenticatedContext(firefox, 2);
        unrelated = await createAuthenticatedContext(webkit, 3);
        await ensureDisplayName(owner.page, anglerOneName);
        await ensureDisplayName(participant.page, anglerTwoName);
        await ensureDisplayName(unrelated.page, anglerThreeName);
    });

    test.afterAll(async () => {
        await owner?.browser.close();
        await participant?.browser.close();
        await unrelated?.browser.close();
    });

    test('an unrelated angler cannot open a shared Trip merely by navigating to its id', async () => {
        test.setTimeout(60_000);
        const tripId = await startTrip(owner.page);
        await inviteAngler(owner.page, tripId, anglerTwoName);
        await owner.page.locator('#trip-participants-close').click();
        await acceptInvitation(participant.page, tripId);

        await unrelated.page.goto(`/trips/${tripId}`);
        await expect(unrelated.page.locator('#trip-loading')).toBeHidden();
        await expect(unrelated.page.locator('#trip-not-found')).toBeVisible();
        await expect(unrelated.page.locator('#active-trip-card')).toHaveCount(0);
    });

    test('a removed participant loses shared Trip access and cannot contribute after authoritative refresh', async () => {
        test.setTimeout(90_000);
        const tripId = await startTrip(owner.page);
        const invitedUserId = await inviteAngler(owner.page, tripId, anglerTwoName);
        await owner.page.locator('#trip-participants-close').click();
        await acceptInvitation(participant.page, tripId);

        await participant.page.goto(`/trips/${tripId}`);
        await expect(participant.page.locator('#trip-loading')).toBeHidden();
        await expect(participant.page.locator('#active-trip-card')).toBeVisible();
        await expect(participant.page.locator('#active-trip-record-catch')).toBeVisible();

        const profileResponseWaiter = participant.page.waitForResponse(response =>
            response.url().endsWith('/api/profiles/me')
            && response.request().method() === 'GET'
            && response.ok());
        await participant.page.goto('/profile');
        const profileResponse = await profileResponseWaiter;
        const authorization = profileResponse.request().headers().authorization;
        const apiOrigin = new URL(profileResponse.url()).origin;

        await removeParticipant(owner.page, invitedUserId);

        await participant.page.goto(`/trips/${tripId}`);
        await expect(participant.page.locator('#trip-loading')).toBeHidden();
        await expect(participant.page.locator('#trip-not-found')).toBeVisible();
        await expect(participant.page.locator('#active-trip-card')).toHaveCount(0);

        const contributionAttempt = await participant.page.request.post(
            `${apiOrigin}/api/trips/${tripId}/notes`,
            {
                headers: { authorization },
                data: {
                    noteId: crypto.randomUUID(),
                    text: 'should not be accepted after removal',
                    recordedOn: new Date().toISOString()
                },
                failOnStatusCode: false
            });
        expect([401, 403, 404]).toContain(contributionAttempt.status());
    });
});
