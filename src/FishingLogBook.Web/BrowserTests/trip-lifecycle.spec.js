import { expect, test } from '@playwright/test';

const tripStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-store.js';
const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const seededCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const startedOn = '2026-08-26T05:32:00+00:00';
const endedOn = '2026-08-26T12:15:00+00:00';

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate((name) => new Promise((resolve) => {
        const request = indexedDB.deleteDatabase(name);
        request.onsuccess = () => resolve();
        request.onerror = () => resolve();
        request.onblocked = () => resolve();
    }), databaseName);
});

function trip(overrides = {}) {
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

async function readActiveTrip(page) {
    return page.evaluate(async ({ tripStoreModule, ownerUserId }) => {
        const { getActiveTrip } = await import(tripStoreModule);
        const stored = await getActiveTrip(ownerUserId);
        return stored === null ? null : JSON.parse(stored.json);
    }, { tripStoreModule, ownerUserId });
}

async function readTrip(page) {
    return page.evaluate(async ({ tripStoreModule, ownerUserId, tripId }) => {
        const { getTrip } = await import(tripStoreModule);
        const stored = await getTrip(ownerUserId, tripId);
        return stored === null ? null : JSON.parse(stored.json);
    }, { tripStoreModule, ownerUserId, tripId });
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

test('starting a trip writes an active record to IndexedDB', async ({ page }) => {
    const outcome = await saveTrip(page, trip());

    expect(outcome).toBe('saved');
    const active = await readActiveTrip(page);
    expect(active.id).toBe(tripId);
    expect(active.status).toBe('Active');
    expect(active.endedOn).toBeNull();
});

test('the active trip survives a full reload', async ({ page }) => {
    await saveTrip(page, trip());

    await page.reload();
    await page.waitForFunction(() => document.readyState === 'complete');

    const active = await readActiveTrip(page);
    expect(active.id).toBe(tripId);
    expect(active.startedOn).toBe(startedOn);
});

test('finishing marks the same record completed rather than creating another', async ({ page }) => {
    await saveTrip(page, trip());

    const outcome = await saveTrip(page, trip({ status: 'Completed', endedOn }));

    expect(outcome).toBe('saved');
    const stored = await readTrip(page);
    expect(stored.id).toBe(tripId);
    expect(stored.status).toBe('Completed');
    expect(stored.endedOn).toBe(endedOn);
    expect(stored.startedOn).toBe(startedOn);
});

test('no active trip remains after finishing, including across a reload', async ({ page }) => {
    await saveTrip(page, trip());
    await saveTrip(page, trip({ status: 'Completed', endedOn }));

    expect(await readActiveTrip(page)).toBeNull();

    await page.reload();
    await page.waitForFunction(() => document.readyState === 'complete');

    expect(await readActiveTrip(page)).toBeNull();
});

test('a new trip may start once the previous one is finished', async ({ page }) => {
    await saveTrip(page, trip());
    await saveTrip(page, trip({ status: 'Completed', endedOn }));

    const outcome = await saveTrip(page, trip({ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }));

    expect(outcome).toBe('saved');
    expect((await readActiveTrip(page)).id).toBe('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
});

test('the whole start and finish journey leaves stored catches untouched', async ({ page }) => {
    await seedCatch(page);

    await saveTrip(page, trip());
    await saveTrip(page, trip({ status: 'Completed', endedOn }));

    const catches = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getAllCatchesWithPhotographs } = await import(catchStoreModule);
        const stored = await getAllCatchesWithPhotographs(ownerUserId);
        return stored.map((item) => ({
            notes: JSON.parse(item.json).notes,
            photographs: item.photographs.length
        }));
    }, { catchStoreModule, ownerUserId });

    expect(catches).toHaveLength(1);
    expect(catches[0].notes).toBe('unrelated to any trip');
    expect(catches[0].photographs).toBe(1);
});

test('the local start and finish journey issues no network request', async ({ page }) => {
    const requests = [];
    page.on('request', (request) => {
        const url = request.url();
        if (url.includes('/api/')) {
            requests.push(url);
        }
    });

    await saveTrip(page, trip());
    await saveTrip(page, trip({ status: 'Completed', endedOn }));

    expect(requests).toEqual([]);
});
