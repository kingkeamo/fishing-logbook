import { expect, test } from '@playwright/test';

const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const photographBytes = 1024 * 1024;

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate((name) => new Promise((resolve) => {
        const request = indexedDB.deleteDatabase(name);
        request.onsuccess = () => resolve();
        request.onerror = () => resolve();
        request.onblocked = () => resolve();
    }), databaseName);
});

async function seed(page, catches) {
    await page.evaluate(async ({ catchStoreModule, catches, photographBytes }) => {
        const { putCatchWithPhotographs } = await import(catchStoreModule);
        for (const item of catches) {
            const photographs = item.photographIds.map((photographId, index) => ({
                id: photographId,
                catchId: item.catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array(photographBytes).fill(index + 1)
            }));
            await putCatchWithPhotographs(
                JSON.stringify({
                    id: item.catchId,
                    userId: item.userId,
                    caughtOn: '2026-08-17T08:00:00+00:00',
                    photographs: photographs.map((photograph) => ({
                        id: photograph.id,
                        catchId: item.catchId,
                        contentType: photograph.contentType,
                        syncStatus: 'savedLocally'
                    }))
                }),
                photographs);
        }
    }, { catchStoreModule, catches, photographBytes });
}

function catchWithPhotographs(index, userId = ownerUserId, photographCount = 1) {
    const catchId = `aaaaaaaa-aaaa-aaaa-aaaa-00000000000${index}`;
    return {
        catchId,
        userId,
        photographIds: Array.from(
            { length: photographCount },
            (_, photograph) => `bbbbbbbb-bbbb-bbbb-bbbb-${index}0000000000${photograph}`)
    };
}

test('list metadata read transfers no photograph bytes and never opens the photograph store', async ({ page }) => {
    await seed(page, [
        catchWithPhotographs(1),
        catchWithPhotographs(2),
        catchWithPhotographs(3, ownerUserId, 2),
        catchWithPhotographs(4),
        catchWithPhotographs(5),
        catchWithPhotographs(6),
        catchWithPhotographs(7),
        catchWithPhotographs(8)
    ]);

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getCatchMetadata, PHOTO_STORE_NAME } = await import(catchStoreModule);
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
            const items = await getCatchMetadata(ownerUserId);
            return {
                count: items.length,
                transferredBytes: items.reduce(
                    (total, item) => total + item.photographs.reduce(
                        (bytes, photograph) => bytes + photograph.bytesBase64.length,
                        0),
                    0),
                photographStoreAccesses: stores.filter((access) => access.store === PHOTO_STORE_NAME).length
            };
        } finally {
            IDBObjectStore.prototype.get = originalGet;
            IDBObjectStore.prototype.openCursor = originalOpenCursor;
        }
    }, { catchStoreModule, ownerUserId });

    expect(result.count).toBe(8);
    expect(result.transferredBytes).toBe(0);
    expect(result.photographStoreAccesses).toBe(0);
});

test('single Catch read returns only the requested photograph blobs', async ({ page }) => {
    await seed(page, [
        catchWithPhotographs(1),
        catchWithPhotographs(2, ownerUserId, 2),
        catchWithPhotographs(3)
    ]);

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getCatchWithPhotographs, PHOTO_STORE_NAME } = await import(catchStoreModule);
        const keys = [];
        const originalGet = IDBObjectStore.prototype.get;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            keys.push({ store: this.name, key });
            return originalGet.call(this, key);
        };

        try {
            const item = await getCatchWithPhotographs(
                ownerUserId,
                'aaaaaaaa-aaaa-aaaa-aaaa-000000000002');
            return {
                catchId: JSON.parse(item.json).id,
                photographIds: item.photographs.map((photograph) => photograph.id),
                photographKeysRead: keys
                    .filter((access) => access.store === PHOTO_STORE_NAME)
                    .map((access) => access.key)
            };
        } finally {
            IDBObjectStore.prototype.get = originalGet;
        }
    }, { catchStoreModule, ownerUserId });

    expect(result.catchId).toBe('aaaaaaaa-aaaa-aaaa-aaaa-000000000002');
    expect(result.photographIds).toEqual([
        'bbbbbbbb-bbbb-bbbb-bbbb-200000000000',
        'bbbbbbbb-bbbb-bbbb-bbbb-200000000001'
    ]);
    expect(result.photographKeysRead).toEqual([
        'bbbbbbbb-bbbb-bbbb-bbbb-200000000000',
        'bbbbbbbb-bbbb-bbbb-bbbb-200000000001'
    ]);
});

test('single Catch read does not return another owner Catch or its blobs', async ({ page }) => {
    await seed(page, [catchWithPhotographs(9, otherUserId)]);

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getCatchWithPhotographs, PHOTO_STORE_NAME } = await import(catchStoreModule);
        const keys = [];
        const originalGet = IDBObjectStore.prototype.get;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            keys.push({ store: this.name, key });
            return originalGet.call(this, key);
        };

        try {
            const item = await getCatchWithPhotographs(
                ownerUserId,
                'aaaaaaaa-aaaa-aaaa-aaaa-000000000009');
            return {
                item,
                photographKeysRead: keys.filter((access) => access.store === PHOTO_STORE_NAME).length
            };
        } finally {
            IDBObjectStore.prototype.get = originalGet;
        }
    }, { catchStoreModule, ownerUserId });

    expect(result.item).toBeNull();
    expect(result.photographKeysRead).toBe(0);
});

test('offline logbook read still returns cached photograph bytes for every owned Catch', async ({ page }) => {
    await seed(page, [catchWithPhotographs(1), catchWithPhotographs(2)]);

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getAllCatchesWithPhotographs } = await import(catchStoreModule);
        const items = await getAllCatchesWithPhotographs(ownerUserId);
        return items.map((item) => item.photographs.map((photograph) => photograph.bytesBase64.length));
    }, { catchStoreModule, ownerUserId });

    expect(result).toHaveLength(2);
    expect(result.every((lengths) => lengths.every((length) => length > 0))).toBe(true);
});

test('repeated list reads do not accumulate photograph store work', async ({ page }) => {
    await seed(page, [
        catchWithPhotographs(1),
        catchWithPhotographs(2),
        catchWithPhotographs(3)
    ]);

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId }) => {
        const { getCatchMetadata, PHOTO_STORE_NAME } = await import(catchStoreModule);
        const accesses = [];
        const originalGet = IDBObjectStore.prototype.get;
        const originalOpenCursor = IDBObjectStore.prototype.openCursor;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            accesses.push(this.name);
            return originalGet.call(this, key);
        };
        IDBObjectStore.prototype.openCursor = function instrumentedOpenCursor(...args) {
            accesses.push(this.name);
            return originalOpenCursor.apply(this, args);
        };

        try {
            for (let read = 0; read < 5; read++) {
                await getCatchMetadata(ownerUserId);
            }

            return accesses.filter((name) => name === PHOTO_STORE_NAME).length;
        } finally {
            IDBObjectStore.prototype.get = originalGet;
            IDBObjectStore.prototype.openCursor = originalOpenCursor;
        }
    }, { catchStoreModule, ownerUserId });

    expect(result).toBe(0);
});
