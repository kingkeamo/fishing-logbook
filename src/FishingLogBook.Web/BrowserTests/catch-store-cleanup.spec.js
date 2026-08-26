import { expect, test } from '@playwright/test';

const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const now = new Date('2026-08-26T12:00:00Z');
const cutoffIso = new Date(now.getTime() - (24 * 60 * 60 * 1000)).toISOString();

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate((name) => new Promise((resolve) => {
        const request = indexedDB.deleteDatabase(name);
        request.onsuccess = () => resolve();
        request.onerror = () => resolve();
        request.onblocked = () => resolve();
    }), databaseName);
});

async function seedCatch(page, {
    catchId,
    userId,
    syncStatus,
    metadataSyncStatus,
    syncedAt,
    photographSyncStatus = 'synchronised'
}) {
    await page.evaluate(async ({ catchStoreModule, catchId, userId, syncStatus, metadataSyncStatus, syncedAt, photographSyncStatus }) => {
        const { putCatchWithPhotographs, updateCatchMetadata } = await import(catchStoreModule);
        const photographId = `${catchId.slice(0, 8)}-cccc-cccc-cccc-cccccccccccc`;
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId,
                caughtOn: '2020-01-01T08:00:00+00:00',
                syncStatus: 'savedLocally',
                metadataSyncStatus: 'savedLocally',
                photographs: [
                    { id: photographId, catchId, contentType: 'image/jpeg', syncStatus: 'savedLocally' }
                ]
            }),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array(64).fill(7) }]);

        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId,
            syncStatus,
            metadataSyncStatus,
            syncedAt,
            photographs: [{ id: photographId, catchId, syncStatus: photographSyncStatus }]
        }));

        return photographId;
    }, { catchStoreModule, catchId, userId, syncStatus, metadataSyncStatus, syncedAt, photographSyncStatus });
}

test('cleanup removes an eligible synced catch and its photograph without reading photograph bytes', async ({ page }) => {
    await seedCatch(page, {
        catchId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000001',
        userId: ownerUserId,
        syncStatus: 'synchronised',
        metadataSyncStatus: 'synchronised',
        syncedAt: new Date(now.getTime() - (25 * 60 * 60 * 1000)).toISOString()
    });

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId, cutoffIso }) => {
        const module = await import(catchStoreModule);
        const gets = [];
        const originalGet = IDBObjectStore.prototype.get;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            gets.push({ store: this.name, key });
            return originalGet.call(this, key);
        };

        try {
            const removed = await module.cleanupSyncedCatches(ownerUserId, cutoffIso);
            const remaining = await module.getCatchMetadata(ownerUserId);
            return {
                removed,
                remainingCount: remaining.length,
                photographGets: gets.filter((access) => access.store === module.PHOTO_STORE_NAME).length
            };
        } finally {
            IDBObjectStore.prototype.get = originalGet;
        }
    }, { catchStoreModule, ownerUserId, cutoffIso });

    expect(result.removed).toBe(1);
    expect(result.remainingCount).toBe(0);
    expect(result.photographGets).toBe(0);
});

test('cleanup retains a synced catch newer than the retention cutoff', async ({ page }) => {
    await seedCatch(page, {
        catchId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000002',
        userId: ownerUserId,
        syncStatus: 'synchronised',
        metadataSyncStatus: 'synchronised',
        syncedAt: new Date(now.getTime() - (1 * 60 * 60 * 1000)).toISOString()
    });

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId, cutoffIso }) => {
        const module = await import(catchStoreModule);
        const removed = await module.cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await module.getCatchMetadata(ownerUserId);
        return { removed, remainingCount: remaining.length };
    }, { catchStoreModule, ownerUserId, cutoffIso });

    expect(result.removed).toBe(0);
    expect(result.remainingCount).toBe(1);
});

test('cleanup retains a pending catch even when older than the retention cutoff', async ({ page }) => {
    await seedCatch(page, {
        catchId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000003',
        userId: ownerUserId,
        syncStatus: 'waitingToSynchronise',
        metadataSyncStatus: 'synchronised',
        syncedAt: new Date(now.getTime() - (48 * 60 * 60 * 1000)).toISOString(),
        photographSyncStatus: 'waitingToSynchronise'
    });

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId, cutoffIso }) => {
        const module = await import(catchStoreModule);
        const removed = await module.cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await module.getCatchMetadata(ownerUserId);
        return { removed, remainingCount: remaining.length };
    }, { catchStoreModule, ownerUserId, cutoffIso });

    expect(result.removed).toBe(0);
    expect(result.remainingCount).toBe(1);
});

test('cleanup never removes another owners eligible catch', async ({ page }) => {
    await seedCatch(page, {
        catchId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000004',
        userId: ownerUserId,
        syncStatus: 'synchronised',
        metadataSyncStatus: 'synchronised',
        syncedAt: new Date(now.getTime() - (48 * 60 * 60 * 1000)).toISOString()
    });
    await seedCatch(page, {
        catchId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000005',
        userId: otherUserId,
        syncStatus: 'synchronised',
        metadataSyncStatus: 'synchronised',
        syncedAt: new Date(now.getTime() - (48 * 60 * 60 * 1000)).toISOString()
    });

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId, otherUserId, cutoffIso }) => {
        const module = await import(catchStoreModule);
        const removed = await module.cleanupSyncedCatches(ownerUserId, cutoffIso);
        const ownerRemaining = await module.getCatchMetadata(ownerUserId);
        const otherRemaining = await module.getCatchMetadata(otherUserId);
        return { removed, ownerRemaining: ownerRemaining.length, otherRemaining: otherRemaining.length };
    }, { catchStoreModule, ownerUserId, otherUserId, cutoffIso });

    expect(result.removed).toBe(1);
    expect(result.ownerRemaining).toBe(0);
    expect(result.otherRemaining).toBe(1);
});

test('cleanup does not disturb a photograph that is still awaiting upload', async ({ page }) => {
    await seedCatch(page, {
        catchId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000006',
        userId: ownerUserId,
        syncStatus: 'synchronised',
        metadataSyncStatus: 'synchronised',
        syncedAt: new Date(now.getTime() - (48 * 60 * 60 * 1000)).toISOString(),
        photographSyncStatus: 'waitingToSynchronise'
    });

    const result = await page.evaluate(async ({ catchStoreModule, ownerUserId, cutoffIso }) => {
        const module = await import(catchStoreModule);
        const removed = await module.cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await module.getCatchMetadata(ownerUserId);
        return { removed, remainingCount: remaining.length };
    }, { catchStoreModule, ownerUserId, cutoffIso });

    expect(result.removed).toBe(0);
    expect(result.remainingCount).toBe(1);
});
