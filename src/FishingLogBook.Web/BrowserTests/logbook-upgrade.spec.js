import { expect, test } from '@playwright/test';

const tripStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-store.js';
const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const logbookModule = '/src/FishingLogBook.Web/wwwroot/js/storage/logbook-database.js';
const databaseName = 'FishingLogBook';
const entitlementDatabaseName = 'FishingLogBookOfflineAccess';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const seededCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const seededPhotographId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const otherTripId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const startedOn = '2026-08-26T05:32:00+00:00';

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate(async (names) => {
        for (const name of names) {
            await new Promise((resolve) => {
                const request = indexedDB.deleteDatabase(name);
                request.onsuccess = () => resolve();
                request.onerror = () => resolve();
                request.onblocked = () => resolve();
            });
        }
    }, [databaseName, entitlementDatabaseName]);
});

async function seedVersionFour(page) {
    return page.evaluate(async ({ databaseName, seededCatchId, seededPhotographId, ownerUserId }) => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, 4);
            request.onupgradeneeded = () => {
                const upgrading = request.result;
                upgrading.createObjectStore('catches', { keyPath: 'id' });
                upgrading.createObjectStore('catchPhotographs', { keyPath: 'id' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        await new Promise((resolve, reject) => {
            const transaction = db.transaction(['catches', 'catchPhotographs'], 'readwrite');
            transaction.objectStore('catches').put({
                id: seededCatchId,
                userId: ownerUserId,
                caughtOn: '2026-06-14T09:48:00+00:00',
                notes: 'the one from before the upgrade',
                syncStatus: 'savedLocally',
                photographs: [{ id: seededPhotographId, catchId: seededCatchId, contentType: 'image/jpeg' }]
            });
            transaction.objectStore('catchPhotographs').put({
                id: seededPhotographId,
                catchId: seededCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array(64 * 1024).fill(7).buffer
            });
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });

        const version = db.version;
        const stores = [...db.objectStoreNames].sort();
        db.close();
        return { version, stores };
    }, { databaseName, seededCatchId, seededPhotographId, ownerUserId });
}

async function seedEntitlementDatabase(page) {
    return page.evaluate(async (name) => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(name, 1);
            request.onupgradeneeded = () => {
                request.result.createObjectStore('deviceEntitlements', { keyPath: 'ownerKey' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        await new Promise((resolve, reject) => {
            const transaction = db.transaction('deviceEntitlements', 'readwrite');
            transaction.objectStore('deviceEntitlements').put({
                ownerKey: 'owner-1',
                state: 'ready',
                ciphertext: [1, 2, 3, 4]
            });
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
        });

        const version = db.version;
        db.close();
        return version;
    }, entitlementDatabaseName);
}

function newTrip(overrides = {}) {
    return {
        id: tripId,
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

async function saveTrip(page, record) {
    return page.evaluate(async ({ tripStoreModule, record }) => {
        const { putTrip } = await import(tripStoreModule);
        return putTrip(JSON.stringify(record));
    }, { tripStoreModule, record });
}

async function readTrip(page, owner, id) {
    return page.evaluate(async ({ tripStoreModule, owner, id }) => {
        const { getTrip } = await import(tripStoreModule);
        const stored = await getTrip(owner, id);
        return stored === null ? null : JSON.parse(stored.json);
    }, { tripStoreModule, owner, id });
}

async function readActiveTrip(page, owner) {
    return page.evaluate(async ({ tripStoreModule, owner }) => {
        const { getActiveTrip } = await import(tripStoreModule);
        const stored = await getActiveTrip(owner);
        return stored === null ? null : JSON.parse(stored.json);
    }, { tripStoreModule, owner });
}

async function inspectDatabase(page) {
    return page.evaluate(async (name) => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(name);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        const result = { version: db.version, stores: [...db.objectStoreNames].sort() };
        db.close();
        return result;
    }, databaseName);
}

test('an existing version 4 database upgrades to the current version and keeps every Catch record and blob', async ({ page }) => {
    const seeded = await seedVersionFour(page);
    expect(seeded.version).toBe(4);
    expect(seeded.stores).toEqual(['catchPhotographs', 'catches']);

    const survivors = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getAllCatchesWithPhotographs } = await import(catchStoreModule);
        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        return catches.map((item) => ({
            notes: JSON.parse(item.json).notes,
            photographCount: item.photographs.length,
            base64Length: item.photographs[0]?.bytesBase64.length ?? 0
        }));
    }, { catchStoreModule, ownerUserId });

    const upgraded = await inspectDatabase(page);

    expect(upgraded.version).toBe(6);
    expect(upgraded.stores).toEqual(['catchPhotographs', 'catches', 'tripPhotographs', 'trips']);
    expect(survivors).toHaveLength(1);
    expect(survivors[0].notes).toBe('the one from before the upgrade');
    expect(survivors[0].photographCount).toBe(1);
    expect(survivors[0].base64Length).toBeGreaterThan(0);
});

test('the upgrade adds the Trip store without deleting any Catch data', async ({ page }) => {
    await seedVersionFour(page);

    await saveTrip(page, newTrip());

    const stored = await page.evaluate(async ({ catchStoreModule, ownerUserId, seededCatchId }) => {
        const { getCatchMetadataById } = await import(catchStoreModule);
        const record = await getCatchMetadataById(ownerUserId, seededCatchId);
        return record === null ? null : JSON.parse(record.json).notes;
    }, { catchStoreModule, ownerUserId, seededCatchId });

    expect(stored).toBe('the one from before the upgrade');
    expect((await inspectDatabase(page)).stores).toContain('trips');
});

test('the upgrade leaves the separate offline entitlement database untouched', async ({ page }) => {
    const entitlementVersion = await seedEntitlementDatabase(page);
    await seedVersionFour(page);

    await saveTrip(page, newTrip());

    const entitlements = await page.evaluate(async (name) => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(name);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        const records = await new Promise((resolve, reject) => {
            const request = db.transaction('deviceEntitlements', 'readonly')
                .objectStore('deviceEntitlements')
                .getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        const result = { version: db.version, stores: [...db.objectStoreNames], records };
        db.close();
        return result;
    }, entitlementDatabaseName);

    expect(entitlements.version).toBe(entitlementVersion);
    expect(entitlements.stores).toEqual(['deviceEntitlements']);
    expect(entitlements.records).toHaveLength(1);
    expect(entitlements.records[0].state).toBe('ready');
});

test('a blank Trip round-trips through a real upgrade', async ({ page }) => {
    await seedVersionFour(page);

    await saveTrip(page, newTrip());
    const stored = await readTrip(page, ownerUserId, tripId);

    expect(stored.status).toBe('Active');
    expect(stored.startedOn).toBe(startedOn);
    expect(stored.title).toBeNull();
    expect(stored.placeName).toBeNull();
    expect(stored.location).toBeNull();
    expect(stored.endedOn).toBeNull();
});

test('a completed Trip with a full location round-trips without losing provenance', async ({ page }) => {
    const location = {
        latitude: 53.4419,
        longitude: -9.2531,
        accuracyMetres: 8,
        capturedOn: startedOn,
        source: 'DeviceGps',
        visibility: 'Private',
        consentVersion: '1'
    };

    await saveTrip(page, newTrip({
        status: 'Completed',
        endedOn: '2026-08-26T11:15:00+00:00',
        title: 'Day with Dad',
        placeName: 'Lough Corrib',
        location
    }));

    const stored = await readTrip(page, ownerUserId, tripId);

    expect(stored.status).toBe('Completed');
    expect(stored.endedOn).toBe('2026-08-26T11:15:00+00:00');
    expect(stored.title).toBe('Day with Dad');
    expect(stored.placeName).toBe('Lough Corrib');
    expect(stored.location).toEqual(location);
});

test('a single Trip read touches only the Trip store', async ({ page }) => {
    await seedVersionFour(page);
    await saveTrip(page, newTrip());

    const accesses = await page.evaluate(async ({ tripStoreModule, owner, id }) => {
        const { getTrip } = await import(tripStoreModule);
        const stores = [];
        const originalGet = IDBObjectStore.prototype.get;
        const originalOpenCursor = IDBObjectStore.prototype.openCursor;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            stores.push({ store: this.name, operation: 'get' });
            return originalGet.call(this, key);
        };
        IDBObjectStore.prototype.openCursor = function instrumentedOpenCursor(...args) {
            stores.push({ store: this.name, operation: 'openCursor' });
            return originalOpenCursor.apply(this, args);
        };

        try {
            await getTrip(owner, id);
            return stores;
        } finally {
            IDBObjectStore.prototype.get = originalGet;
            IDBObjectStore.prototype.openCursor = originalOpenCursor;
        }
    }, { tripStoreModule, owner: ownerUserId, id: tripId });

    expect(accesses).toEqual([{ store: 'trips', operation: 'get' }]);
});

test('a Trip list read is owner scoped', async ({ page }) => {
    await saveTrip(page, newTrip({ placeName: 'Lough Corrib' }));
    await saveTrip(page, newTrip({ id: otherTripId, ownerUserId: otherUserId, placeName: 'Lough Mask' }));

    const trips = await page.evaluate(async ({ tripStoreModule, owner }) => {
        const { getTrips } = await import(tripStoreModule);
        const stored = await getTrips(owner);
        return stored.map((item) => JSON.parse(item.json).placeName);
    }, { tripStoreModule, owner: ownerUserId });

    expect(trips).toEqual(['Lough Corrib']);
});

test('an active Trip read is owner scoped', async ({ page }) => {
    await saveTrip(page, newTrip({ id: otherTripId, ownerUserId: otherUserId }));

    expect(await readActiveTrip(page, ownerUserId)).toBeNull();
    expect((await readActiveTrip(page, otherUserId)).id).toBe(otherTripId);
});

test('a second distinct active Trip is rejected for the same owner', async ({ page }) => {
    await saveTrip(page, newTrip());

    const outcome = await saveTrip(page, newTrip({ id: otherTripId }));

    expect(outcome).toBe('activeConflict');
    expect((await readActiveTrip(page, ownerUserId)).id).toBe(tripId);
    expect(await readTrip(page, ownerUserId, otherTripId)).toBeNull();
});

test('updating the same active Trip succeeds', async ({ page }) => {
    await saveTrip(page, newTrip());

    const outcome = await saveTrip(page, newTrip({ placeName: 'Lough Corrib' }));

    expect(outcome).toBe('saved');
    expect((await readActiveTrip(page, ownerUserId)).placeName).toBe('Lough Corrib');
});

test('each owner may hold their own active Trip', async ({ page }) => {
    await saveTrip(page, newTrip());

    const outcome = await saveTrip(page, newTrip({ id: otherTripId, ownerUserId: otherUserId }));

    expect(outcome).toBe('saved');
    expect((await readActiveTrip(page, ownerUserId)).id).toBe(tripId);
    expect((await readActiveTrip(page, otherUserId)).id).toBe(otherTripId);
});

test('the active Trip is still discoverable after a full page reload', async ({ page }) => {
    await saveTrip(page, newTrip({ placeName: 'Lough Corrib' }));

    await page.reload();
    await page.waitForFunction(() => document.readyState === 'complete');

    const recovered = await readActiveTrip(page, ownerUserId);

    expect(recovered.id).toBe(tripId);
    expect(recovered.placeName).toBe('Lough Corrib');
    expect(recovered.status).toBe('Active');
});

test('the logbook database reports one owning module for its name and version', async ({ page }) => {
    const schema = await page.evaluate(async (module) => {
        const logbook = await import(module);
        return {
            name: logbook.LOGBOOK_DATABASE_NAME,
            version: logbook.LOGBOOK_DATABASE_VERSION,
            tripStore: logbook.TRIP_STORE_NAME
        };
    }, logbookModule);

    expect(schema.name).toBe(databaseName);
    expect(schema.version).toBe(6);
    expect(schema.tripStore).toBe('trips');
});
