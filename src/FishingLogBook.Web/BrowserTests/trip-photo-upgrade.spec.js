import { expect, test } from '@playwright/test';

const tripStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-store.js';
const tripPhotoStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-photo-store.js';
const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const logbookModule = '/src/FishingLogBook.Web/wwwroot/js/storage/logbook-database.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const seededCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const seededPhotographId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const tripPhotographId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
const startedOn = '2026-08-26T05:32:00+00:00';
const addedOn = '2026-08-26T06:00:00+00:00';

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate((name) => new Promise((resolve) => {
        const request = indexedDB.deleteDatabase(name);
        request.onsuccess = () => resolve();
        request.onerror = () => resolve();
        request.onblocked = () => resolve();
    }), databaseName);
});

async function seedVersionFive(page) {
    return page.evaluate(async (input) => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(input.databaseName, 5);
            request.onupgradeneeded = () => {
                const upgrading = request.result;
                upgrading.createObjectStore('catches', { keyPath: 'id' });
                upgrading.createObjectStore('catchPhotographs', { keyPath: 'id' });
                upgrading.createObjectStore('trips', { keyPath: 'id' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        await new Promise((resolve, reject) => {
            const transaction = db.transaction(['catches', 'catchPhotographs', 'trips'], 'readwrite');
            transaction.objectStore('catches').put({
                id: input.seededCatchId,
                userId: input.ownerUserId,
                caughtOn: '2026-06-14T09:48:00+00:00',
                notes: 'recorded before the trip photo upgrade',
                syncStatus: 'savedLocally',
                metadataSyncStatus: 'savedLocally',
                tripId: input.tripId,
                photographs: [{
                    id: input.seededPhotographId,
                    catchId: input.seededCatchId,
                    contentType: 'image/jpeg'
                }]
            });
            transaction.objectStore('catchPhotographs').put({
                id: input.seededPhotographId,
                catchId: input.seededCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array(64 * 1024).fill(7).buffer
            });
            transaction.objectStore('trips').put({
                id: input.tripId,
                ownerUserId: input.ownerUserId,
                status: 'Active',
                startedOn: input.startedOn,
                endedOn: null,
                title: 'before the upgrade',
                placeName: null,
                location: null,
                syncStatus: 'savedLocally',
                syncedAt: null
            });
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });

        const version = db.version;
        const stores = [...db.objectStoreNames].sort();
        db.close();
        return { version, stores };
    }, { databaseName, seededCatchId, seededPhotographId, ownerUserId, tripId, startedOn });
}

async function openThroughTheApp(page) {
    return page.evaluate(async ({ logbookModule }) => {
        const logbook = await import(logbookModule);
        const db = await logbook.openLogbookDatabase();
        const result = { version: db.version, stores: [...db.objectStoreNames].sort() };
        db.close();
        return result;
    }, { logbookModule });
}

async function readSeeded(page) {
    return page.evaluate(async (input) => {
        const catches = await import(input.catchStoreModule);
        const trips = await import(input.tripStoreModule);
        const stored = await catches.getAllCatchesWithPhotographs(input.ownerUserId);
        const trip = await trips.getTrip(input.ownerUserId, input.tripId);
        return {
            catches: stored.map(entry => ({
                notes: JSON.parse(entry.json).notes,
                tripId: JSON.parse(entry.json).tripId,
                photographs: entry.photographs.length,
                base64Length: entry.photographs[0]?.bytesBase64?.length ?? 0
            })),
            trip: trip === null ? null : JSON.parse(trip.json)
        };
    }, { catchStoreModule, tripStoreModule, ownerUserId, tripId });
}

async function addTripPhotograph(page, overrides = {}) {
    return page.evaluate(async (input) => {
        const store = await import(input.tripPhotoStoreModule);
        await store.putTripPhotograph(
            JSON.stringify({
                id: input.tripPhotographId,
                tripId: input.tripId,
                ownerUserId: input.ownerUserId,
                contentType: 'image/jpeg',
                addedOn: input.addedOn,
                capturedOn: input.capturedOn,
                objectKey: null,
                syncStatus: 'savedLocally',
                syncedAt: null,
                ...input.overrides
            }),
            new Uint8Array(32 * 1024).fill(3));
        return true;
    }, {
        tripPhotoStoreModule,
        tripPhotographId,
        tripId,
        ownerUserId,
        addedOn,
        capturedOn: null,
        overrides
    });
}

test('upgrading from v5 keeps every existing record and adds the trip photo store', async ({ page }) => {
    const seeded = await seedVersionFive(page);
    expect(seeded.version).toBe(5);
    expect(seeded.stores).toEqual(['catchPhotographs', 'catches', 'trips']);

    const upgraded = await openThroughTheApp(page);

    expect(upgraded.version).toBe(6);
    expect(upgraded.stores).toEqual(['catchPhotographs', 'catches', 'tripPhotographs', 'trips']);

    const stored = await readSeeded(page);
    expect(stored.catches).toHaveLength(1);
    expect(stored.catches[0].notes).toBe('recorded before the trip photo upgrade');
    expect(stored.catches[0].tripId).toBe(tripId);
    expect(stored.catches[0].photographs).toBe(1);
    expect(stored.catches[0].base64Length).toBeGreaterThan(0);
    expect(stored.trip.title).toBe('before the upgrade');
    expect(stored.trip.syncStatus).toBe('savedLocally');
});

test('a trip photograph added after the upgrade survives a full reload', async ({ page }) => {
    await seedVersionFive(page);
    await openThroughTheApp(page);
    await addTripPhotograph(page, { capturedOn: startedOn });

    await page.reload();

    const stored = await readSeeded(page);
    expect(stored.trip.photographs).toHaveLength(1);
    expect(stored.trip.photographs[0].id).toBe(tripPhotographId);
    expect(stored.trip.photographs[0].capturedOn).toBe(startedOn);

    const bytes = await page.evaluate(async (input) => {
        const store = await import(input.tripPhotoStoreModule);
        const value = await store.getTripPhotographBytes(
            input.ownerUserId,
            input.tripId,
            input.tripPhotographId);
        return value === null ? null : value.length ?? value.byteLength;
    }, { tripPhotoStoreModule, ownerUserId, tripId, tripPhotographId });
    expect(bytes).toBe(32 * 1024);
});

test('a trip metadata read never touches the photograph blob store', async ({ page }) => {
    await seedVersionFive(page);
    await openThroughTheApp(page);
    await addTripPhotograph(page);

    const observed = await page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        const opened = [];
        const originalTransaction = IDBDatabase.prototype.transaction;
        IDBDatabase.prototype.transaction = function patched(storeNames, ...rest) {
            const names = Array.isArray(storeNames) ? storeNames : [storeNames];
            opened.push(...names);
            return originalTransaction.call(this, storeNames, ...rest);
        };
        try {
            await trips.getTrips(input.ownerUserId);
            await trips.getActiveTrip(input.ownerUserId);
            await trips.getPendingTrips(input.ownerUserId);
        } finally {
            IDBDatabase.prototype.transaction = originalTransaction;
        }

        return opened;
    }, { tripStoreModule, ownerUserId });

    expect(observed).not.toContain('tripPhotographs');
    expect(observed).not.toContain('catchPhotographs');
    expect(observed).toContain('trips');
});

test('trip photographs are scoped to the owning angler', async ({ page }) => {
    await seedVersionFive(page);
    await openThroughTheApp(page);
    await addTripPhotograph(page);

    const readings = await page.evaluate(async (input) => {
        const store = await import(input.tripPhotoStoreModule);
        const mine = await store.getTripPhotographBytes(
            input.ownerUserId,
            input.tripId,
            input.tripPhotographId);
        const theirs = await store.getTripPhotographBytes(
            input.otherUserId,
            input.tripId,
            input.tripPhotographId);
        return {
            mine: mine === null ? null : mine.length ?? mine.byteLength,
            theirs,
            theirPending: await store.getPendingTripPhotographs(input.otherUserId)
        };
    }, { tripPhotoStoreModule, ownerUserId, otherUserId, tripId, tripPhotographId });

    expect(readings.mine).toBe(32 * 1024);
    expect(readings.theirs).toBeNull();
    expect(readings.theirPending).toEqual([]);
});

test('removing a trip photograph leaves the seeded catch and its blob untouched', async ({ page }) => {
    await seedVersionFive(page);
    await openThroughTheApp(page);
    await addTripPhotograph(page);

    const removed = await page.evaluate(async (input) => {
        const store = await import(input.tripPhotoStoreModule);
        return store.deleteTripPhotograph(input.ownerUserId, input.tripId, input.tripPhotographId);
    }, { tripPhotoStoreModule, ownerUserId, tripId, tripPhotographId });

    expect(removed).toBe(true);

    const stored = await readSeeded(page);
    expect(stored.trip.photographs).toEqual([]);
    expect(stored.catches[0].photographs).toBe(1);
    expect(stored.catches[0].base64Length).toBeGreaterThan(0);
});

test('a pending trip photograph holds its trip back from retention cleanup', async ({ page }) => {
    await seedVersionFive(page);
    await openThroughTheApp(page);
    await page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        await trips.putTrip(JSON.stringify({
            id: input.tripId,
            ownerUserId: input.ownerUserId,
            status: 'Completed',
            startedOn: input.startedOn,
            endedOn: input.startedOn,
            title: 'before the upgrade',
            placeName: null,
            location: null,
            syncStatus: 'synchronised',
            syncedAt: '2026-08-20T05:32:00+00:00',
            photographs: []
        }));
    }, { tripStoreModule, tripId, ownerUserId, startedOn });
    await addTripPhotograph(page);

    const outcome = await page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        const photographs = await import(input.tripPhotoStoreModule);
        const retained = await photographs.getTripsWithPendingPhotographs(input.ownerUserId);
        const removed = await trips.cleanupSyncedTrips(
            input.ownerUserId,
            '2026-08-25T05:32:00+00:00',
            retained);
        return { retained, removed, remaining: (await trips.getTrips(input.ownerUserId)).length };
    }, { tripStoreModule, tripPhotoStoreModule, ownerUserId });

    expect(outcome.retained).toEqual([tripId]);
    expect(outcome.removed).toBe(0);
    expect(outcome.remaining).toBe(1);
});
