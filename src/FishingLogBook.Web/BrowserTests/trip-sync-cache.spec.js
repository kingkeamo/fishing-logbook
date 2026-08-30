import { expect, test } from '@playwright/test';

const tripStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-store.js';
const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const seededCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const startedOn = '2026-08-26T05:32:00+00:00';
const endedOn = '2026-08-26T12:15:00+00:00';
const longAgo = '2026-08-20T05:32:00+00:00';
const recently = '2026-08-26T05:00:00+00:00';
const cutoff = '2026-08-25T05:32:00+00:00';

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate((name) => new Promise((resolve) => {
        const request = indexedDB.deleteDatabase(name);
        request.onsuccess = () => resolve();
        request.onerror = () => resolve();
        request.onblocked = () => resolve();
    }), databaseName);
});

function trip(id, overrides = {}) {
    return {
        id,
        ownerUserId,
        status: 'Active',
        startedOn,
        endedOn: null,
        title: null,
        placeName: null,
        location: null,
        syncStatus: 'savedLocally',
        syncedAt: null,
        ...overrides
    };
}

function syncedTrip(id, overrides = {}) {
    return trip(id, {
        status: 'Completed',
        endedOn,
        syncStatus: 'synchronised',
        syncedAt: longAgo,
        ...overrides
    });
}

async function saveTrips(page, records) {
    await page.evaluate(async ({ tripStoreModule, records }) => {
        const { putTrip } = await import(tripStoreModule);
        for (const record of records) {
            await putTrip(JSON.stringify(record));
        }
    }, { tripStoreModule, records });
}

async function readPending(page, owner) {
    return page.evaluate(async ({ tripStoreModule, owner }) => {
        const { getPendingTrips } = await import(tripStoreModule);
        const pending = await getPendingTrips(owner);
        return pending.map(entry => JSON.parse(entry.json).id).sort();
    }, { tripStoreModule, owner });
}

async function cleanup(page, owner) {
    return page.evaluate(async ({ tripStoreModule, owner, cutoff }) => {
        const { cleanupSyncedTrips } = await import(tripStoreModule);
        return cleanupSyncedTrips(owner, cutoff);
    }, { tripStoreModule, owner, cutoff });
}

async function readAllTripIds(page, owner) {
    return page.evaluate(async ({ tripStoreModule, owner }) => {
        const { getTrips } = await import(tripStoreModule);
        const stored = await getTrips(owner);
        return stored.map(entry => JSON.parse(entry.json).id).sort();
    }, { tripStoreModule, owner });
}

async function seedCatch(page) {
    await page.evaluate(async ({ catchStoreModule, ownerUserId, seededCatchId }) => {
        const { putCatchWithPhotographs } = await import(catchStoreModule);
        await putCatchWithPhotographs(
            JSON.stringify({
                id: seededCatchId,
                userId: ownerUserId,
                caughtOn: '2026-06-14T09:48:00+00:00',
                notes: 'unrelated to any trip',
                photographs: [{ id: 'photo-1', catchId: seededCatchId, contentType: 'image/jpeg' }]
            }),
            [{
                id: 'photo-1',
                catchId: seededCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array(2048).fill(3)
            }]);
    }, { catchStoreModule, ownerUserId, seededCatchId });
}

test('the pending scan returns only this angler unsynchronised trips, excluding permanently failed ones', async ({ page }) => {
    await saveTrips(page, [
        trip('aaaaaaaa-0000-0000-0000-000000000001', { status: 'Completed', endedOn }),
        trip('aaaaaaaa-0000-0000-0000-000000000002', { syncStatus: 'failedToSynchronise' }),
        syncedTrip('aaaaaaaa-0000-0000-0000-000000000003'),
        trip('aaaaaaaa-0000-0000-0000-000000000004', { ownerUserId: otherUserId })
    ]);

    expect(await readPending(page, ownerUserId)).toEqual([
        'aaaaaaaa-0000-0000-0000-000000000001'
    ]);
});

test('pending work survives a full reload', async ({ page }) => {
    await saveTrips(page, [trip('aaaaaaaa-0000-0000-0000-000000000001')]);

    await page.reload();

    expect(await readPending(page, ownerUserId)).toEqual(['aaaaaaaa-0000-0000-0000-000000000001']);
});

test('retention cleanup removes only synced completed trips past the window', async ({ page }) => {
    await saveTrips(page, [
        syncedTrip('aaaaaaaa-0000-0000-0000-000000000001'),
        syncedTrip('aaaaaaaa-0000-0000-0000-000000000002', { syncedAt: recently }),
        trip('aaaaaaaa-0000-0000-0000-000000000003', { status: 'Completed', endedOn, syncedAt: longAgo }),
        trip('aaaaaaaa-0000-0000-0000-000000000004', { syncStatus: 'synchronised', syncedAt: longAgo })
    ]);

    expect(await cleanup(page, ownerUserId)).toBe(1);
    expect(await readAllTripIds(page, ownerUserId)).toEqual([
        'aaaaaaaa-0000-0000-0000-000000000002',
        'aaaaaaaa-0000-0000-0000-000000000003',
        'aaaaaaaa-0000-0000-0000-000000000004'
    ]);
});

test('retention cleanup never reaches another angler trips', async ({ page }) => {
    await saveTrips(page, [syncedTrip('aaaaaaaa-0000-0000-0000-000000000001', { ownerUserId: otherUserId })]);

    expect(await cleanup(page, ownerUserId)).toBe(0);
    expect(await readAllTripIds(page, otherUserId)).toEqual(['aaaaaaaa-0000-0000-0000-000000000001']);
});

test('cleaned up trips stay gone after a reload', async ({ page }) => {
    await saveTrips(page, [syncedTrip('aaaaaaaa-0000-0000-0000-000000000001')]);
    expect(await cleanup(page, ownerUserId)).toBe(1);

    await page.reload();

    expect(await readAllTripIds(page, ownerUserId)).toEqual([]);
});

test('retention cleanup leaves stored catches and photographs untouched', async ({ page }) => {
    await seedCatch(page);
    await saveTrips(page, [syncedTrip('aaaaaaaa-0000-0000-0000-000000000001')]);

    expect(await cleanup(page, ownerUserId)).toBe(1);

    const catches = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getAllCatchesWithPhotographs } = await import(catchStoreModule);
        const stored = await getAllCatchesWithPhotographs(ownerUserId);
        return stored.map(entry => ({
            notes: JSON.parse(entry.json).notes,
            photographs: entry.photographs.length
        }));
    }, { catchStoreModule, ownerUserId });

    expect(catches).toEqual([{ notes: 'unrelated to any trip', photographs: 1 }]);
});
