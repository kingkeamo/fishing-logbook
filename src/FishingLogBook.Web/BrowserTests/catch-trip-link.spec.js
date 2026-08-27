import { expect, test } from '@playwright/test';

const tripStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-store.js';
const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const catchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const legacyCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccd';
const tripId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
const startedOn = '2026-08-26T05:32:00+00:00';
const longAgo = '2026-08-20T05:32:00+00:00';
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

async function saveCatch(page, record, photographId = 'photo-1') {
    await page.evaluate(async ({ catchStoreModule, record, photographId }) => {
        const { putCatchWithPhotographs } = await import(catchStoreModule);
        await putCatchWithPhotographs(JSON.stringify(record), [{
            id: photographId,
            catchId: record.id,
            contentType: 'image/jpeg',
            bytes: new Uint8Array(4096).fill(9)
        }]);
    }, { catchStoreModule, record, photographId });
}

async function saveTrip(page, record) {
    await page.evaluate(async ({ tripStoreModule, record }) => {
        const { putTrip } = await import(tripStoreModule);
        await putTrip(JSON.stringify(record));
    }, { tripStoreModule, record });
}

async function readCatchMetadata(page, id) {
    return page.evaluate(async ({ catchStoreModule, ownerUserId, id }) => {
        const { getCatchMetadataById } = await import(catchStoreModule);
        const stored = await getCatchMetadataById(ownerUserId, id);
        return stored === null ? null : JSON.parse(stored.json);
    }, { catchStoreModule, ownerUserId, id });
}

async function cleanupTrips(page, retainedTripIds) {
    return page.evaluate(async ({ tripStoreModule, ownerUserId, cutoff, retainedTripIds }) => {
        const { cleanupSyncedTrips } = await import(tripStoreModule);
        return cleanupSyncedTrips(ownerUserId, cutoff, retainedTripIds);
    }, { tripStoreModule, ownerUserId, cutoff, retainedTripIds });
}

async function remainingTripIds(page) {
    return page.evaluate(async ({ tripStoreModule, ownerUserId }) => {
        const { getTrips } = await import(tripStoreModule);
        const stored = await getTrips(ownerUserId);
        return stored.map(entry => JSON.parse(entry.json).id).sort();
    }, { tripStoreModule, ownerUserId });
}

function catchRecord(overrides = {}) {
    return {
        id: catchId,
        userId: ownerUserId,
        caughtOn: '2026-08-26T08:00:00+00:00',
        speciesName: 'Pike',
        photographs: [{ id: 'photo-1', catchId, contentType: 'image/jpeg' }],
        syncStatus: 'savedLocally',
        metadataSyncStatus: 'savedLocally',
        tripId,
        ...overrides
    };
}

function syncedTrip() {
    return {
        id: tripId,
        ownerUserId,
        status: 'Completed',
        startedOn,
        endedOn: startedOn,
        title: null,
        placeName: null,
        location: null,
        syncStatus: 'synchronised',
        syncedAt: longAgo
    };
}

test('a catch keeps its trip across a full reload', async ({ page }) => {
    await saveCatch(page, catchRecord());

    await page.reload();

    expect((await readCatchMetadata(page, catchId)).tripId).toBe(tripId);
});

test('a pre-T5 catch without a trip still reads back', async ({ page }) => {
    const legacy = catchRecord({ id: legacyCatchId });
    delete legacy.tripId;
    legacy.photographs = [{ id: 'photo-2', catchId: legacyCatchId, contentType: 'image/jpeg' }];
    await saveCatch(page, legacy, 'photo-2');

    await page.reload();

    const stored = await readCatchMetadata(page, legacyCatchId);
    expect(stored.tripId).toBeUndefined();
    expect(stored.speciesName).toBe('Pike');
    expect(stored.photographs).toHaveLength(1);
});

test('the database version is unchanged by the trip link', async ({ page }) => {
    const version = await page.evaluate(async ({ catchStoreModule }) => {
        const { CATCH_DATABASE_VERSION } = await import(catchStoreModule);
        return CATCH_DATABASE_VERSION;
    }, { catchStoreModule });

    expect(version).toBe(5);
});

test('a pending linked catch keeps its trip out of retention cleanup', async ({ page }) => {
    await saveTrip(page, syncedTrip());
    await saveCatch(page, catchRecord());

    expect(await cleanupTrips(page, [tripId])).toBe(0);
    expect(await remainingTripIds(page)).toEqual([tripId]);
});

test('the trip is cleaned up once nothing references it', async ({ page }) => {
    await saveTrip(page, syncedTrip());
    await saveCatch(page, catchRecord({ syncStatus: 'synchronised', metadataSyncStatus: 'synchronised' }));

    expect(await cleanupTrips(page, [])).toBe(1);
    expect(await remainingTripIds(page)).toEqual([]);
});

test('retention cleanup leaves the linked catch and its photograph untouched', async ({ page }) => {
    await saveTrip(page, syncedTrip());
    await saveCatch(page, catchRecord({ syncStatus: 'synchronised', metadataSyncStatus: 'synchronised' }));

    expect(await cleanupTrips(page, [])).toBe(1);

    const stored = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getAllCatchesWithPhotographs } = await import(catchStoreModule);
        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        return catches.map(entry => ({
            tripId: JSON.parse(entry.json).tripId,
            photographs: entry.photographs.length
        }));
    }, { catchStoreModule, ownerUserId });

    expect(stored).toEqual([{ tripId, photographs: 1 }]);
});
