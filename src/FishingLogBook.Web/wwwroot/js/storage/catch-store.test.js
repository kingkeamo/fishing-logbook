import { describe, expect, it, vi } from 'vitest';
import {
    CATCH_STORE_NAME,
    getAllCatchesWithPhotographs,
    openCatchDatabase,
    putCatchWithPhotographs,
    updateCatchMetadata
} from './catch-store.js';
import * as indexedDb from './indexed-db.js';

describe('Catch store', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';

    it('puts and reads a Catch with photograph bytes and stable ids', async () => {
        const catchId = '11111111-1111-1111-1111-111111111111';
        const photographId = '22222222-2222-2222-2222-222222222222';
        await putCatchWithPhotographs(
            JSON.stringify({ id: catchId, userId: ownerUserId, caughtOn: '2026-08-17T08:00:00+00:00' }),
            [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1, 2, 3])
            }]
        );

        const items = await getAllCatchesWithPhotographs(ownerUserId);

        expect(items).toHaveLength(1);
        expect(JSON.parse(items[0].json).id).toBe(catchId);
        expect(items[0].photographs).toHaveLength(1);
        expect(items[0].photographs[0].id).toBe(photographId);
        expect(items[0].photographs[0].catchId).toBe(catchId);
        expect(items[0].photographs[0].bytesBase64).toBe(btoa(String.fromCharCode(1, 2, 3)));
    });

    it('keeps owner and provenance ids after reopen', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                anglerUserId: ownerUserId,
                recordedByUserId: ownerUserId,
                caughtOn: '2026-08-17T08:00:00+00:00'
            }),
            [{
                id: 'provenance-photo',
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1])
            }]
        );

        const firstRead = await getAllCatchesWithPhotographs(ownerUserId);
        const reopened = await getAllCatchesWithPhotographs(ownerUserId);
        const catchRecord = JSON.parse(reopened[0].json);

        expect(JSON.parse(firstRead[0].json).anglerUserId).toBe(ownerUserId);
        expect(catchRecord.userId).toBe(ownerUserId);
        expect(catchRecord.anglerUserId).toBe(ownerUserId);
        expect(catchRecord.recordedByUserId).toBe(ownerUserId);
        expect(catchRecord.anglerUserId).toBe(catchRecord.userId);
        expect(catchRecord.recordedByUserId).toBe(catchRecord.userId);
    });

    it('still reads a Catch stored without provenance properties', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2026-08-17T08:00:00+00:00'
            }),
            [{
                id: 'legacy-photo',
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1])
            }]
        );

        const reopened = await getAllCatchesWithPhotographs(ownerUserId);
        const catchRecord = JSON.parse(reopened[0].json);

        expect(catchRecord.userId).toBe(ownerUserId);
        expect(catchRecord.anglerUserId).toBeUndefined();
        expect(catchRecord.recordedByUserId).toBeUndefined();
    });

    it('reads three photographs in metadata order after reopen', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photoA = '11111111-1111-1111-1111-111111111111';
        const photoB = '22222222-2222-2222-2222-222222222222';
        const photoC = '00000000-0000-0000-0000-000000000003';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2026-08-17T08:00:00+00:00',
                photographs: [
                    { id: photoA, catchId, contentType: 'image/jpeg' },
                    { id: photoB, catchId, contentType: 'image/png' },
                    { id: photoC, catchId, contentType: 'image/webp' }
                ]
            }),
            [
                { id: photoC, catchId, contentType: 'image/webp', bytes: new Uint8Array([3, 3, 3]) },
                { id: photoA, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1, 1, 1]) },
                { id: photoB, catchId, contentType: 'image/png', bytes: new Uint8Array([2, 2, 2]) }
            ]
        );

        const firstRead = await getAllCatchesWithPhotographs(ownerUserId);
        const reopened = await getAllCatchesWithPhotographs(ownerUserId);

        expect(JSON.parse(firstRead[0].json).id).toBe(catchId);
        expect(reopened[0].photographs.map((photograph) => photograph.id)).toEqual([photoA, photoB, photoC]);
        expect(reopened[0].photographs.map((photograph) => photograph.catchId)).toEqual([catchId, catchId, catchId]);
        expect(reopened[0].photographs.map((photograph) => photograph.contentType)).toEqual([
            'image/jpeg',
            'image/png',
            'image/webp'
        ]);
        expect(reopened[0].photographs.map((photograph) => photograph.bytesBase64)).toEqual([
            btoa(String.fromCharCode(1, 1, 1)),
            btoa(String.fromCharCode(2, 2, 2)),
            btoa(String.fromCharCode(3, 3, 3))
        ]);
    });

    it('persists sync transitions across reopen without replacing photograph bytes', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photographId = '11111111-1111-1111-1111-111111111111';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                syncStatus: 0,
                metadataSyncStatus: 0,
                photographs: [{
                    id: photographId,
                    catchId,
                    contentType: 'image/jpeg',
                    syncStatus: 0
                }]
            }),
            [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1, 2, 3])
            }]
        );

        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            syncStatus: 4,
            metadataSyncStatus: 3,
            photographs: [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                syncStatus: 3,
                objectKey: `catches/${ownerUserId}/${catchId}/${photographId}`
            }]
        }));

        const firstRead = await getAllCatchesWithPhotographs(ownerUserId);
        const reopened = await getAllCatchesWithPhotographs(ownerUserId);
        const metadata = JSON.parse(reopened[0].json);

        expect(JSON.parse(firstRead[0].json).syncStatus).toBe(4);
        expect(metadata.metadataSyncStatus).toBe(3);
        expect(metadata.photographs[0].syncStatus).toBe(3);
        expect(metadata.photographs[0].objectKey).toBe(
            `catches/${ownerUserId}/${catchId}/${photographId}`
        );
        expect(reopened[0].photographs[0].bytesBase64).toBe(
            btoa(String.fromCharCode(1, 2, 3))
        );
    });

    it('does not let another user overwrite sync state', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await putCatchWithPhotographs(
            JSON.stringify({ id: catchId, userId: ownerUserId, syncStatus: 0 }),
            [{
                id: 'photo-1',
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1])
            }]
        );

        await expect(updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId: otherUserId,
            syncStatus: 3
        }))).rejects.toThrow('Owned Catch was not found');

        const ownerView = await getAllCatchesWithPhotographs(ownerUserId);
        expect(JSON.parse(ownerView[0].json).syncStatus).toBe(0);
        expect(ownerView[0].photographs[0].bytesBase64).toBe(
            btoa(String.fromCharCode(1))
        );
    });

    it('accepts the server location privacy once local sync has settled', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photographId = '11111111-1111-1111-1111-111111111111';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                syncStatus: 3,
                metadataSyncStatus: 3,
                location: {
                    latitude: 53.2707,
                    longitude: -9.0568,
                    visibility: 'Private'
                },
                photographs: [{
                    id: photographId,
                    catchId,
                    contentType: 'image/jpeg',
                    syncStatus: 3
                }]
            }),
            [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1])
            }]
        );

        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            syncStatus: 3,
            metadataSyncStatus: 3,
            location: {
                latitude: 53.2707,
                longitude: -9.0568,
                visibility: 'Public'
            },
            photographs: [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                syncStatus: 3
            }]
        }));

        const ownerView = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = JSON.parse(ownerView[0].json);
        expect(stored.location.visibility).toBe('Public');
        expect(stored.syncStatus).toBe(3);
        expect(stored.photographs[0].syncStatus).toBe(3);
    });

    it('does not let a stale sync transition overwrite newer location privacy', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photographId = '11111111-1111-1111-1111-111111111111';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                syncStatus: 1,
                metadataSyncStatus: 1,
                location: {
                    latitude: 53.2707,
                    longitude: -9.0568,
                    visibility: 'Private'
                },
                photographs: [{
                    id: photographId,
                    catchId,
                    contentType: 'image/jpeg',
                    syncStatus: 1
                }]
            }),
            [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1])
            }]
        );

        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            syncStatus: 3,
            metadataSyncStatus: 3,
            location: {
                latitude: 53.2707,
                longitude: -9.0568,
                visibility: 'Public'
            },
            photographs: [{
                id: photographId,
                catchId,
                contentType: 'image/jpeg',
                syncStatus: 3
            }]
        }));

        const ownerView = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = JSON.parse(ownerView[0].json);
        expect(stored.location.visibility).toBe('Private');
        expect(stored.syncStatus).toBe(3);
        expect(stored.photographs[0].syncStatus).toBe(3);
    });

    it('keeps two separately saved catches on distinct ids', async () => {
        await putCatchWithPhotographs(
            JSON.stringify({ id: 'catch-a', userId: ownerUserId, caughtOn: '2026-08-17T08:00:00+00:00' }),
            [{ id: 'photo-a', catchId: 'catch-a', contentType: 'image/jpeg', bytes: new Uint8Array([1]) }]
        );
        await putCatchWithPhotographs(
            JSON.stringify({ id: 'catch-b', userId: ownerUserId, caughtOn: '2026-08-17T09:00:00+00:00' }),
            [{ id: 'photo-b', catchId: 'catch-b', contentType: 'image/png', bytes: new Uint8Array([2]) }]
        );

        const items = await getAllCatchesWithPhotographs(ownerUserId);
        const ids = items.map((item) => JSON.parse(item.json).id).sort();
        const photoIds = items.flatMap((item) => item.photographs.map((photograph) => photograph.id)).sort();

        expect(ids).toEqual(['catch-a', 'catch-b']);
        expect(photoIds).toEqual(['photo-a', 'photo-b']);
    });

    it('does not persist a Catch when a photograph has no id', async () => {
        await expect(putCatchWithPhotographs(
            JSON.stringify({ id: 'orphan-catch', caughtOn: '2026-08-17T08:00:00+00:00' }),
            [{ catchId: 'orphan-catch', contentType: 'image/jpeg', bytes: new Uint8Array([9]) }]
        )).rejects.toBeTruthy();

        const items = await getAllCatchesWithPhotographs(ownerUserId);
        const ids = items.map((item) => JSON.parse(item.json).id);

        expect(ids).not.toContain('orphan-catch');
    });

    it('rejects a Catch with zero photographs', async () => {
        await expect(putCatchWithPhotographs(
            JSON.stringify({ id: 'empty-photos', caughtOn: '2026-08-17T08:00:00+00:00' }),
            []
        )).rejects.toThrow('Catch requires at least one photograph');

        const items = await getAllCatchesWithPhotographs(ownerUserId);
        const ids = items.map((item) => JSON.parse(item.json).id);
        expect(ids).not.toContain('empty-photos');
    });

    it('does not persist a Catch when photograph put fails after the Catch write starts', async () => {
        vi.spyOn(indexedDb, 'openDatabase').mockResolvedValue({
            close() { },
            transaction() {
                const transaction = {
                    objectStore(name) {
                        if (name === CATCH_STORE_NAME) {
                            return {
                                put() {
                                    const request = { onsuccess: null, onerror: null };
                                    queueMicrotask(() => request.onsuccess?.());
                                    return request;
                                }
                            };
                        }

                        return {
                            put() {
                                const request = {
                                    onsuccess: null,
                                    onerror: null,
                                    error: Object.assign(new Error('photo failed'), { name: 'UnknownError' })
                                };
                                queueMicrotask(() => request.onerror?.());
                                return request;
                            }
                        };
                    },
                    oncomplete: null,
                    onabort: null,
                    onerror: null,
                    abort() {
                        queueMicrotask(() => transaction.onabort?.());
                    }
                };
                return transaction;
            }
        });

        try {
            await expect(putCatchWithPhotographs(
                JSON.stringify({ id: 'partial-catch', caughtOn: '2026-08-17T08:00:00+00:00' }),
                [{
                    id: 'photo-1',
                    catchId: 'partial-catch',
                    contentType: 'image/jpeg',
                    bytes: new Uint8Array([1])
                }]
            )).rejects.toBeTruthy();
        } finally {
            vi.restoreAllMocks();
        }

        const items = await getAllCatchesWithPhotographs(ownerUserId);
        const ids = items.map((item) => JSON.parse(item.json).id);
        expect(ids).not.toContain('partial-catch');
    });

    it('does not return another user’s Catch or photograph bytes', async () => {
        const ownerCatchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const otherCatchId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: ownerCatchId,
                userId: ownerUserId,
                caughtOn: '2026-08-17T08:00:00+00:00'
            }),
            [{
                id: 'owner-photo',
                catchId: ownerCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1, 2, 3])
            }]
        );
        await putCatchWithPhotographs(
            JSON.stringify({
                id: otherCatchId,
                userId: otherUserId,
                caughtOn: '2026-08-17T09:00:00+00:00',
                location: { latitude: 53.2707, longitude: -9.0568 }
            }),
            [{
                id: 'other-photo',
                catchId: otherCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([9, 9, 9])
            }]
        );

        const ownerView = await getAllCatchesWithPhotographs(ownerUserId);
        const otherView = await getAllCatchesWithPhotographs(otherUserId);

        expect(ownerView.map((item) => JSON.parse(item.json).id)).toEqual([ownerCatchId]);
        expect(ownerView[0].photographs.map((photograph) => photograph.id)).toEqual(['owner-photo']);
        expect(JSON.stringify(ownerView)).not.toContain('53.2707');
        expect(JSON.stringify(ownerView)).not.toContain('other-photo');
        expect(otherView.map((item) => JSON.parse(item.json).id)).toEqual([otherCatchId]);
        expect(otherView[0].photographs.map((photograph) => photograph.id)).toEqual(['other-photo']);
        expect(JSON.stringify(otherView)).not.toContain('owner-photo');
    });

    it('does not let the first signed-in user read or adopt a legacy unscoped Catch', async () => {
        const unscopedId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: unscopedId,
                caughtOn: '2026-08-17T08:00:00+00:00',
                location: { latitude: 53.2707, longitude: -9.0568 }
            }),
            [{
                id: 'unscoped-photo',
                catchId: unscopedId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([4, 5, 6])
            }]
        );

        const firstSignerView = await getAllCatchesWithPhotographs(otherUserId);
        const originalOwnerView = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = await readRawCatch(unscopedId);

        expect(firstSignerView).toEqual([]);
        expect(originalOwnerView).toEqual([]);
        expect(JSON.stringify(firstSignerView)).not.toContain('53.2707');
        expect(JSON.stringify(firstSignerView)).not.toContain('unscoped-photo');
        expect(stored.userId).toBeUndefined();
        expect(stored.location).toEqual({ latitude: 53.2707, longitude: -9.0568 });
    });

    it('does not expose unscoped Catches alongside owned records', async () => {
        const unscopedId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
        await putCatchWithPhotographs(
            JSON.stringify({ id: unscopedId, caughtOn: '2026-08-17T08:00:00+00:00' }),
            [{
                id: 'unscoped-photo',
                catchId: unscopedId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([4])
            }]
        );
        await putCatchWithPhotographs(
            JSON.stringify({
                id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
                userId: ownerUserId,
                caughtOn: '2026-08-17T09:00:00+00:00'
            }),
            [{
                id: 'owner-photo',
                catchId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
                contentType: 'image/jpeg',
                bytes: new Uint8Array([5])
            }]
        );

        const otherView = await getAllCatchesWithPhotographs(otherUserId);
        const ownerView = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = await readRawCatch(unscopedId);

        expect(otherView).toEqual([]);
        expect(ownerView.map((item) => JSON.parse(item.json).id)).toEqual([
            'dddddddd-dddd-dddd-dddd-dddddddddddd'
        ]);
        expect(JSON.stringify(ownerView)).not.toContain('unscoped-photo');
        expect(stored.userId).toBeUndefined();
    });

    it('does not treat an empty user id as an owner', async () => {
        const unscopedId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: unscopedId,
                userId: '00000000-0000-0000-0000-000000000000',
                caughtOn: '2026-08-17T08:00:00+00:00'
            }),
            [{
                id: 'empty-owner-photo',
                catchId: unscopedId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([7])
            }]
        );

        const firstSignedIn = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = await readRawCatch(unscopedId);

        expect(firstSignedIn).toEqual([]);
        expect(JSON.stringify(firstSignedIn)).not.toContain('empty-owner-photo');
        expect(stored.userId).toBe('00000000-0000-0000-0000-000000000000');
    });
});

function readRawCatch(id) {
    return openCatchDatabase().then((db) => new Promise((resolve, reject) => {
        const transaction = db.transaction(CATCH_STORE_NAME, 'readonly');
        const request = transaction.objectStore(CATCH_STORE_NAME).get(id);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
        transaction.oncomplete = () => db.close();
    }));
}
