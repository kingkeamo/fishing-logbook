import { describe, expect, it, vi } from 'vitest';
import {
    CATCH_STORE_NAME,
    PHOTO_STORE_NAME,
    cleanupSyncedCatches,
    getAllCatchesWithPhotographs,
    getCatchMetadata,
    getCatchMetadataById,
    getCatchPhotographBytes,
    getCatchWithPhotographs,
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

    it('lets the recorder see a Catch stored for another angler', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const anglerUserId = ownerUserId;
        const recorderUserId = otherUserId;
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: anglerUserId,
                anglerUserId,
                recordedByUserId: recorderUserId,
                caughtOn: '2026-08-17T08:00:00+00:00'
            }),
            [{
                id: 'recorded-for-another-photo',
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1])
            }]
        );

        const anglerView = await getAllCatchesWithPhotographs(anglerUserId);
        const recorderView = await getAllCatchesWithPhotographs(recorderUserId);
        const unrelatedView = await getAllCatchesWithPhotographs('33333333-3333-3333-3333-333333333333');

        expect(anglerView).toHaveLength(1);
        expect(recorderView).toHaveLength(1);
        expect(unrelatedView).toHaveLength(0);
        const fromRecorderView = JSON.parse(recorderView[0].json);
        expect(fromRecorderView.userId).toBe(anglerUserId);
        expect(fromRecorderView.recordedByUserId).toBe(recorderUserId);
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

    it('does not let a stale sync completion clear a newer metadata edit', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photographId = '11111111-1111-1111-1111-111111111111';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2026-08-20T08:15:00Z',
                syncStatus: 2,
                metadataSyncStatus: 2,
                photographs: [{ id: photographId, catchId, contentType: 'image/jpeg', syncStatus: 2 }]
            }),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1]) }]
        );
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2025-06-14T06:32:10Z',
                syncStatus: 1,
                metadataSyncStatus: 1,
                photographs: [{ id: photographId, catchId, contentType: 'image/jpeg', syncStatus: 2 }]
            }),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1]) }]
        );

        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            caughtOn: '2026-08-20T08:15:00Z',
            syncStatus: 3,
            metadataSyncStatus: 3,
            photographs: [{ id: photographId, catchId, contentType: 'image/jpeg', syncStatus: 3 }]
        }));

        const ownerView = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = JSON.parse(ownerView[0].json);
        expect(stored.caughtOn).toBe('2025-06-14T06:32:10Z');
        expect(stored.syncStatus).toBe(1);
        expect(stored.metadataSyncStatus).toBe(1);
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

    it('deletes a photograph blob that is dropped from a subsequent put for the same Catch', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const keptPhotoId = '11111111-1111-1111-1111-111111111111';
        const removedPhotoId = '22222222-2222-2222-2222-222222222222';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2026-08-17T08:00:00+00:00',
                photographs: [
                    { id: keptPhotoId, catchId, contentType: 'image/jpeg' },
                    { id: removedPhotoId, catchId, contentType: 'image/png' }
                ]
            }),
            [
                { id: keptPhotoId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1]) },
                { id: removedPhotoId, catchId, contentType: 'image/png', bytes: new Uint8Array([2]) }
            ]
        );

        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2026-08-17T08:00:00+00:00',
                photographs: [
                    { id: keptPhotoId, catchId, contentType: 'image/jpeg' }
                ]
            }),
            [
                { id: keptPhotoId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1]) }
            ]
        );

        const items = await getAllCatchesWithPhotographs(ownerUserId);
        const photoIds = items[0].photographs.map((photograph) => photograph.id);

        expect(photoIds).toEqual([keptPhotoId]);
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
                            openCursor() {
                                const request = { onsuccess: null, onerror: null, result: null };
                                queueMicrotask(() => request.onsuccess?.());
                                return request;
                            },
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

describe('getCatchPhotographBytes', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';

    function catchJson(id, userId, photographIds) {
        return JSON.stringify({
            id,
            userId,
            caughtOn: '2026-08-17T08:00:00+00:00',
            photographs: photographIds.map((photographId) => ({
                id: photographId,
                catchId: id,
                contentType: 'image/jpeg',
                syncStatus: 'savedLocally'
            }))
        });
    }

    it('returns a Uint8Array Blazor can marshal as byte[], not the stored ArrayBuffer', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photographId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await putCatchWithPhotographs(
            catchJson(catchId, ownerUserId, [photographId]),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1, 2, 3]) }]
        );

        const bytes = await getCatchPhotographBytes(ownerUserId, catchId, photographId);

        expect(bytes).toBeInstanceOf(Uint8Array);
        expect(Array.from(bytes)).toEqual([1, 2, 3]);
    });

    it('returns null for a photograph that belongs to another owner\'s Catch', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const photographId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await putCatchWithPhotographs(
            catchJson(catchId, ownerUserId, [photographId]),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1, 2, 3]) }]
        );

        const bytes = await getCatchPhotographBytes(otherUserId, catchId, photographId);

        expect(bytes).toBeNull();
    });

    it('returns null when the photograph does not belong to the given Catch', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const otherCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
        const photographId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        const unrelatedPhotographId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
        await putCatchWithPhotographs(
            catchJson(catchId, ownerUserId, [photographId]),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1, 2, 3]) }]
        );
        await putCatchWithPhotographs(
            catchJson(otherCatchId, ownerUserId, [unrelatedPhotographId]),
            [{
                id: unrelatedPhotographId,
                catchId: otherCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([9])
            }]
        );

        const bytes = await getCatchPhotographBytes(ownerUserId, catchId, unrelatedPhotographId);

        expect(bytes).toBeNull();
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

describe('Catch store read granularity', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';

    function recordStoreAccess() {
        const accesses = { get: [], openCursor: [] };
        const originalGet = IDBObjectStore.prototype.get;
        const originalOpenCursor = IDBObjectStore.prototype.openCursor;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            accesses.get.push({ store: this.name, key });
            return originalGet.call(this, key);
        };
        IDBObjectStore.prototype.openCursor = function instrumentedOpenCursor(...args) {
            accesses.openCursor.push({ store: this.name });
            return originalOpenCursor.apply(this, args);
        };
        accesses.restore = () => {
            IDBObjectStore.prototype.get = originalGet;
            IDBObjectStore.prototype.openCursor = originalOpenCursor;
        };
        return accesses;
    }

    function largePhotograph(id, catchId, seed) {
        return {
            id,
            catchId,
            contentType: 'image/jpeg',
            bytes: new Uint8Array(256 * 1024).fill(seed)
        };
    }

    async function seedCatch(catchId, photographs, userId = ownerUserId) {
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId,
                caughtOn: '2026-08-17T08:00:00+00:00',
                photographs: photographs.map((photograph) => ({
                    id: photograph.id,
                    catchId,
                    contentType: photograph.contentType,
                    syncStatus: 'savedLocally'
                }))
            }),
            photographs);
    }

    it('reads list metadata without touching the photograph store', async () => {
        const first = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const second = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await seedCatch(first, [largePhotograph('photo-a', first, 1)]);
        await seedCatch(second, [largePhotograph('photo-b', second, 2)]);
        const accesses = recordStoreAccess();

        try {
            const items = await getCatchMetadata(ownerUserId);

            expect(items).toHaveLength(2);
            expect(items.every((item) => item.photographs.length === 0)).toBe(true);
            expect(items.some((item) => JSON.parse(item.json).id === first)).toBe(true);
            expect(accesses.openCursor.map((access) => access.store)).toEqual([CATCH_STORE_NAME]);
            expect(accesses.get.filter((access) => access.store === PHOTO_STORE_NAME)).toHaveLength(0);
        } finally {
            accesses.restore();
        }
    });

    it('updates a current Catch without scanning unrelated photographs', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await seedCatch(catchId, [largePhotograph('old-photo', catchId, 1)]);
        const accesses = recordStoreAccess();

        try {
            await seedCatch(catchId, [largePhotograph('new-photo', catchId, 2)]);

            expect(accesses.get).toContainEqual({ store: CATCH_STORE_NAME, key: catchId });
            expect(accesses.openCursor).toHaveLength(0);
        } finally {
            accesses.restore();
        }

        const item = await getCatchWithPhotographs(ownerUserId, catchId);
        expect(item.photographs.map((photograph) => photograph.id)).toEqual(['new-photo']);
    });

    it('writes a new Catch without scanning existing photographs', async () => {
        const existingId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const newId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await seedCatch(existingId, [largePhotograph('existing-photo', existingId, 1)]);
        const accesses = recordStoreAccess();

        try {
            await seedCatch(newId, [largePhotograph('new-photo', newId, 2)]);

            expect(accesses.get).toContainEqual({ store: CATCH_STORE_NAME, key: newId });
            expect(accesses.openCursor).toHaveLength(0);
        } finally {
            accesses.restore();
        }
    });

    it('does not return metadata belonging to another owner', async () => {
        const owned = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const foreign = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
        await seedCatch(owned, [largePhotograph('photo-a', owned, 1)]);
        await seedCatch(foreign, [largePhotograph('photo-c', foreign, 3)], otherUserId);

        const items = await getCatchMetadata(ownerUserId);

        expect(items).toHaveLength(1);
        expect(JSON.parse(items[0].json).id).toBe(owned);
    });

    it('reads one Catch metadata record by key without touching photograph blobs', async () => {
        const wanted = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const unrelated = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await seedCatch(wanted, [largePhotograph('photo-a', wanted, 1)]);
        await seedCatch(unrelated, [largePhotograph('photo-b', unrelated, 2)]);
        const accesses = recordStoreAccess();

        try {
            const item = await getCatchMetadataById(ownerUserId, wanted);

            expect(JSON.parse(item.json).id).toBe(wanted);
            expect(item.photographs).toEqual([]);
            expect(accesses.get).toEqual([{ store: CATCH_STORE_NAME, key: wanted }]);
            expect(accesses.openCursor).toHaveLength(0);
        } finally {
            accesses.restore();
        }
    });

    it('treats an empty owner as no owner for metadata', async () => {
        const owned = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await seedCatch(owned, [largePhotograph('photo-a', owned, 1)]);

        const items = await getCatchMetadata('00000000-0000-0000-0000-000000000000');

        expect(items).toEqual([]);
    });

    it('reads one Catch by key and only its own photograph blobs', async () => {
        const wanted = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const unrelated = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await seedCatch(wanted, [largePhotograph('photo-a', wanted, 1)]);
        await seedCatch(unrelated, [
            largePhotograph('photo-b', unrelated, 2),
            largePhotograph('photo-c', unrelated, 3)
        ]);
        const accesses = recordStoreAccess();

        try {
            const item = await getCatchWithPhotographs(ownerUserId, wanted);

            expect(JSON.parse(item.json).id).toBe(wanted);
            expect(item.photographs).toHaveLength(1);
            expect(item.photographs[0].id).toBe('photo-a');
            expect(accesses.get).toEqual([
                { store: CATCH_STORE_NAME, key: wanted },
                { store: PHOTO_STORE_NAME, key: 'photo-a' }
            ]);
            expect(accesses.openCursor).toHaveLength(0);
        } finally {
            accesses.restore();
        }
    });

    it('reads every photograph belonging to the requested Catch in metadata order', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await seedCatch(catchId, [
            largePhotograph('photo-1', catchId, 1),
            largePhotograph('photo-2', catchId, 2),
            largePhotograph('photo-3', catchId, 3)
        ]);

        const item = await getCatchWithPhotographs(ownerUserId, catchId);

        expect(item.photographs.map((photograph) => photograph.id))
            .toEqual(['photo-1', 'photo-2', 'photo-3']);
        expect(item.photographs.every((photograph) => photograph.bytesBase64.length > 0)).toBe(true);
    });

    it('does not read a Catch belonging to another owner by id', async () => {
        const foreign = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
        await seedCatch(foreign, [largePhotograph('photo-c', foreign, 3)], otherUserId);
        const accesses = recordStoreAccess();

        try {
            const item = await getCatchWithPhotographs(ownerUserId, foreign);

            expect(item).toBeNull();
            expect(accesses.get.filter((access) => access.store === PHOTO_STORE_NAME)).toHaveLength(0);
        } finally {
            accesses.restore();
        }
    });

    it('returns null for a Catch that is not stored', async () => {
        const item = await getCatchWithPhotographs(ownerUserId, 'dddddddd-dddd-dddd-dddd-dddddddddddd');

        expect(item).toBeNull();
    });

    it('falls back to a filtered scan for a legacy Catch with no photograph metadata', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        const unrelated = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await putCatchWithPhotographs(
            JSON.stringify({ id: catchId, userId: ownerUserId, caughtOn: '2026-08-17T08:00:00+00:00' }),
            [largePhotograph('legacy-photo', catchId, 1)]);
        await seedCatch(unrelated, [largePhotograph('photo-b', unrelated, 2)]);

        const item = await getCatchWithPhotographs(ownerUserId, catchId);

        expect(item.photographs).toHaveLength(1);
        expect(item.photographs[0].id).toBe('legacy-photo');
    });
});

describe('Catch store cleanup', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';
    const now = Date.parse('2026-08-26T12:00:00Z');
    const cutoffIso = new Date(now - (24 * 60 * 60 * 1000)).toISOString();

    async function seedSyncedCatch(catchId, {
        userId = ownerUserId,
        syncStatus = 'synchronised',
        metadataSyncStatus = 'synchronised',
        photographSyncStatus = 'synchronised',
        syncedAt
    } = {}) {
        const photographId = `${catchId.slice(0, 8)}-cccc-cccc-cccc-cccccccccccc`;
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId,
                caughtOn: '2020-01-01T08:00:00+00:00',
                syncStatus: 'savedLocally',
                metadataSyncStatus: 'savedLocally',
                photographs: [{ id: photographId, catchId, contentType: 'image/jpeg', syncStatus: 'savedLocally' }]
            }),
            [{ id: photographId, catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1, 2, 3]) }]);
        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId,
            syncStatus,
            metadataSyncStatus,
            syncedAt,
            photographs: [{ id: photographId, catchId, syncStatus: photographSyncStatus }]
        }));
        return photographId;
    }

    function writeRawCatch(catchId, {
        userId = ownerUserId,
        syncStatus = 'synchronised',
        metadataSyncStatus = 'synchronised',
        photographs = [],
        syncedAt
    } = {}) {
        return openCatchDatabase().then((db) => new Promise((resolve, reject) => {
            const transaction = db.transaction(CATCH_STORE_NAME, 'readwrite');
            const request = transaction.objectStore(CATCH_STORE_NAME).put({
                id: catchId,
                userId,
                caughtOn: '2020-01-01T08:00:00+00:00',
                syncStatus,
                metadataSyncStatus,
                syncedAt,
                photographs
            });
            request.onsuccess = () => resolve();
            request.onerror = () => reject(request.error);
            transaction.oncomplete = () => db.close();
        }));
    }

    it('removes a fully synced Catch with no photographs older than the retention cutoff', async () => {
        const catchId = 'aaaaaaaa-1111-1111-1111-111111111111';
        await writeRawCatch(catchId, { syncedAt: new Date(now - (25 * 60 * 60 * 1000)).toISOString() });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await getCatchMetadata(ownerUserId);

        expect(removed).toBe(1);
        expect(remaining).toHaveLength(0);
    });

    it('retains a fully synced Catch with no photographs newer than the retention cutoff', async () => {
        const catchId = 'bbbbbbbb-1111-1111-1111-111111111111';
        await writeRawCatch(catchId, { syncedAt: new Date(now - (1 * 60 * 60 * 1000)).toISOString() });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await getCatchMetadata(ownerUserId);

        expect(removed).toBe(0);
        expect(remaining).toHaveLength(1);
    });

    it('removes an eligible synced Catch and its photograph without reading photograph bytes', async () => {
        const catchId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
        await seedSyncedCatch(catchId, {
            syncedAt: new Date(now - (25 * 60 * 60 * 1000)).toISOString()
        });
        const accesses = recordCleanupStoreAccess();

        try {
            const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);
            const remaining = await getCatchMetadata(ownerUserId);

            expect(removed).toBe(1);
            expect(remaining).toHaveLength(0);
            expect(accesses.get.filter((access) => access.store === PHOTO_STORE_NAME)).toHaveLength(0);
            expect(accesses.get.filter((access) => access.store === CATCH_STORE_NAME)).toHaveLength(0);
        } finally {
            accesses.restore();
        }
    });

    it('retains a synced Catch newer than the retention cutoff', async () => {
        const catchId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
        await seedSyncedCatch(catchId, { syncedAt: new Date(now - (1 * 60 * 60 * 1000)).toISOString() });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await getCatchMetadata(ownerUserId);

        expect(removed).toBe(0);
        expect(remaining).toHaveLength(1);
    });

    it('treats the exact retention boundary as eligible', async () => {
        const catchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
        await seedSyncedCatch(catchId, { syncedAt: cutoffIso });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);

        expect(removed).toBe(1);
    });

    it('never cleans up while offline-derived pending state is current, regardless of syncedAt age', async () => {
        const catchId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
        await seedSyncedCatch(catchId, {
            syncStatus: 'waitingToSynchronise',
            metadataSyncStatus: 'waitingToSynchronise',
            photographSyncStatus: 'waitingToSynchronise',
            syncedAt: new Date(now - (240 * 60 * 60 * 1000)).toISOString()
        });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);
        const remaining = await getCatchMetadata(ownerUserId);

        expect(removed).toBe(0);
        expect(remaining).toHaveLength(1);
    });

    it('retains a failed/recoverable Catch regardless of age', async () => {
        const catchId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
        await seedSyncedCatch(catchId, {
            syncStatus: 'failedToSynchronise',
            metadataSyncStatus: 'failedToSynchronise',
            photographSyncStatus: 'failedToSynchronise',
            syncedAt: new Date(now - (240 * 60 * 60 * 1000)).toISOString()
        });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);

        expect(removed).toBe(0);
    });

    it('retains a Catch whose photograph is still awaiting upload', async () => {
        const catchId = 'ffffffff-ffff-ffff-ffff-ffffffffffff';
        await seedSyncedCatch(catchId, {
            photographSyncStatus: 'waitingToSynchronise',
            syncedAt: new Date(now - (48 * 60 * 60 * 1000)).toISOString()
        });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);

        expect(removed).toBe(0);
    });

    it('never removes another owner Catch, even when eligible', async () => {
        const ownerCatchId = '11111111-2222-3333-4444-555555555555';
        const otherCatchId = '99999999-8888-7777-6666-555555555555';
        await seedSyncedCatch(ownerCatchId, { syncedAt: new Date(now - (48 * 60 * 60 * 1000)).toISOString() });
        await seedSyncedCatch(otherCatchId, {
            userId: otherUserId,
            syncedAt: new Date(now - (48 * 60 * 60 * 1000)).toISOString()
        });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);
        const ownerRemaining = await getCatchMetadata(ownerUserId);
        const otherRemaining = await getCatchMetadata(otherUserId);

        expect(removed).toBe(1);
        expect(ownerRemaining).toHaveLength(0);
        expect(otherRemaining).toHaveLength(1);
    });

    it('ignores an old CaughtOn when the Catch synced recently', async () => {
        const catchId = '22222222-3333-4444-5555-666666666666';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: catchId,
                userId: ownerUserId,
                caughtOn: '2015-01-01T08:00:00+00:00',
                syncStatus: 'savedLocally',
                metadataSyncStatus: 'savedLocally',
                photographs: [{ id: 'old-catch-photo', catchId, contentType: 'image/jpeg', syncStatus: 'savedLocally' }]
            }),
            [{ id: 'old-catch-photo', catchId, contentType: 'image/jpeg', bytes: new Uint8Array([1]) }]);
        await updateCatchMetadata(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            syncStatus: 'synchronised',
            metadataSyncStatus: 'synchronised',
            syncedAt: new Date(now - (1 * 60 * 60 * 1000)).toISOString(),
            photographs: [{ id: 'old-catch-photo', catchId, syncStatus: 'synchronised' }]
        }));

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);

        expect(removed).toBe(0);
    });

    it('does not treat a Catch with no recorded syncedAt as eligible', async () => {
        const catchId = '33333333-4444-5555-6666-777777777777';
        await seedSyncedCatch(catchId, { syncedAt: undefined });

        const removed = await cleanupSyncedCatches(ownerUserId, cutoffIso);

        expect(removed).toBe(0);
    });

    it('returns zero for an unknown owner without throwing', async () => {
        const removed = await cleanupSyncedCatches('', cutoffIso);

        expect(removed).toBe(0);
    });

    function recordCleanupStoreAccess() {
        const accesses = { get: [] };
        const originalGet = IDBObjectStore.prototype.get;
        IDBObjectStore.prototype.get = function instrumentedGet(key) {
            accesses.get.push({ store: this.name, key });
            return originalGet.call(this, key);
        };
        accesses.restore = () => {
            IDBObjectStore.prototype.get = originalGet;
        };
        return accesses;
    }
});
