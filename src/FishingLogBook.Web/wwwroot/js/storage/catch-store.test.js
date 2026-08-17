import { describe, expect, it, vi } from 'vitest';
import {
    CATCH_DATABASE_NAME,
    CATCH_STORE_NAME,
    PHOTO_STORE_NAME,
    PRODUCTION_CATCH_STORE_NAME,
    PRODUCTION_PHOTO_STORE_NAME,
    getAllCatchesWithPhotographs,
    getAllTestCatches,
    openCatchDatabase,
    putCatchWithPhotographs,
    putTestCatch
} from './catch-store.js';
import * as indexedDb from './indexed-db.js';

describe('Catch store', () => {
    it('puts and reads a Catch', async () => {
        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'first' }));

        const items = await getAllTestCatches();

        expect(items).toHaveLength(1);
        expect(JSON.parse(items[0])).toMatchObject({ id: 'catch-1', notes: 'first' });
    });

    it('reads multiple records', async () => {
        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'one' }));
        await putTestCatch(JSON.stringify({ id: 'catch-2', notes: 'two' }));

        const items = await getAllTestCatches();
        const ids = items.map((item) => JSON.parse(item).id).sort();

        expect(ids).toEqual(['catch-1', 'catch-2']);
    });

    it('returns an empty list from an empty database', async () => {
        const items = await getAllTestCatches();

        expect(items).toEqual([]);
    });

    it('updates an existing Catch', async () => {
        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'before' }));
        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'after' }));

        const items = await getAllTestCatches();

        expect(items).toHaveLength(1);
        expect(JSON.parse(items[0]).notes).toBe('after');
    });

    it('adds the photograph store when upgrading an older Catch database', async () => {
        const first = await new Promise((resolve, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME, 1);
            request.onupgradeneeded = () => {
                request.result.createObjectStore(CATCH_STORE_NAME, { keyPath: 'id' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        first.close();

        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'upgraded' }));

        const upgraded = await new Promise((resolve, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        expect([...upgraded.objectStoreNames].sort()).toEqual(
            [
                PHOTO_STORE_NAME,
                CATCH_STORE_NAME,
                PRODUCTION_CATCH_STORE_NAME,
                PRODUCTION_PHOTO_STORE_NAME
            ].sort()
        );
        upgraded.close();

        const items = await getAllTestCatches();
        expect(JSON.parse(items[0]).notes).toBe('upgraded');
    });

    it('keeps existing Catch stores when upgrading a complete older database', async () => {
        const first = await new Promise((resolve, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME, 1);
            request.onupgradeneeded = () => {
                request.result.createObjectStore(CATCH_STORE_NAME, { keyPath: 'id' });
                request.result.createObjectStore(PHOTO_STORE_NAME, { keyPath: 'id' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        first.close();

        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'already-complete' }));

        const items = await getAllTestCatches();
        expect(JSON.parse(items[0]).notes).toBe('already-complete');
    });

    it('rejects a Catch that has no id', async () => {
        await expect(putTestCatch(JSON.stringify({ notes: 'no id' }))).rejects.toBeTruthy();
    });

    it('closes the connection when another version is opened', async () => {
        const first = await openCatchDatabase();

        const upgraded = await new Promise((resolve, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME, 4);
            request.onupgradeneeded = () => { };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        expect(() => first.transaction(CATCH_STORE_NAME, 'readonly')).toThrow();
        upgraded.close();
    });

    it('rejects when IndexedDB open fails', async () => {
        const originalOpen = indexedDB.open.bind(indexedDB);
        indexedDB.open = () => {
            const request = {
                error: new DOMException('blocked', 'UnknownError')
            };
            queueMicrotask(() => request.onerror?.());
            return request;
        };

        try {
            await expect(putTestCatch(JSON.stringify({ id: 'catch-1' }))).rejects.toBeTruthy();
        } finally {
            indexedDB.open = originalOpen;
        }
    });

    it('rejects when IndexedDB open times out', async () => {
        vi.useFakeTimers();
        const originalOpen = indexedDB.open.bind(indexedDB);
        indexedDB.open = () => ({
            onupgradeneeded: null,
            onsuccess: null,
            onerror: null
        });

        try {
            const assertion = expect(putTestCatch(JSON.stringify({ id: 'catch-1' })))
                .rejects.toThrow('IndexedDB open timed out');
            await vi.advanceTimersByTimeAsync(8000);
            await assertion;
        } finally {
            indexedDB.open = originalOpen;
            vi.useRealTimers();
        }
    });

    it('rejects when a Catch transaction aborts', async () => {
        let transaction;
        vi.spyOn(indexedDb, 'openDatabase').mockResolvedValue({
            close() { },
            transaction() {
                transaction = {
                    objectStore() {
                        return {
                            put() {
                                return { onsuccess: null, onerror: null };
                            }
                        };
                    },
                    oncomplete: null,
                    onabort: null,
                    onerror: null
                };
                return transaction;
            }
        });

        try {
            const promise = putTestCatch(JSON.stringify({ id: 'catch-1' }));
            await vi.waitFor(() => {
                expect(transaction).toBeTruthy();
            });
            transaction.error = Object.assign(new Error('aborted'), { name: 'AbortError' });
            transaction.onabort();
            await expect(promise).rejects.toThrow('aborted');
        } finally {
            vi.restoreAllMocks();
        }
    });

    it('rejects when a Catch transaction errors', async () => {
        let transaction;
        vi.spyOn(indexedDb, 'openDatabase').mockResolvedValue({
            close() { },
            transaction() {
                transaction = {
                    objectStore() {
                        return {
                            put() {
                                return { onsuccess: null, onerror: null };
                            }
                        };
                    },
                    oncomplete: null,
                    onabort: null,
                    onerror: null
                };
                return transaction;
            }
        });

        try {
            const promise = putTestCatch(JSON.stringify({ id: 'catch-1' }));
            await vi.waitFor(() => {
                expect(transaction).toBeTruthy();
            });
            transaction.error = Object.assign(new Error('failed'), { name: 'UnknownError' });
            transaction.onerror();
            await expect(promise).rejects.toThrow('failed');
        } finally {
            vi.restoreAllMocks();
        }
    });

    it('rejects when a Catch transaction times out', async () => {
        vi.useFakeTimers();
        vi.spyOn(indexedDb, 'openDatabase').mockResolvedValue({
            close() { },
            transaction() {
                return {
                    objectStore() {
                        return {
                            put() {
                                return { onsuccess: null, onerror: null };
                            }
                        };
                    },
                    oncomplete: null,
                    onabort: null,
                    onerror: null
                };
            }
        });

        try {
            const assertion = expect(putTestCatch(JSON.stringify({ id: 'catch-1' })))
                .rejects.toThrow('IndexedDB write timed out');
            await vi.advanceTimersByTimeAsync(8000);
            await assertion;
        } finally {
            vi.restoreAllMocks();
            vi.useRealTimers();
        }
    });
});

describe('Production Catch store', () => {
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
                        if (name === PRODUCTION_CATCH_STORE_NAME) {
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

    it('does not expose or adopt a legacy unowned Catch when another user signs in first', async () => {
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

        const firstSignedIn = await getAllCatchesWithPhotographs(otherUserId);
        const originalOwner = await getAllCatchesWithPhotographs(ownerUserId);
        const stored = await readRawProductionCatch(unscopedId);

        expect(firstSignedIn).toEqual([]);
        expect(originalOwner).toEqual([]);
        expect(JSON.stringify(firstSignedIn)).not.toContain('53.2707');
        expect(JSON.stringify(firstSignedIn)).not.toContain('unscoped-photo');
        expect(stored.id).toBe(unscopedId);
        expect(stored.userId).toBeUndefined();
        expect(stored.location.latitude).toBe(53.2707);
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

        expect(firstSignedIn).toEqual([]);
        expect(JSON.stringify(firstSignedIn)).not.toContain('empty-owner-photo');
    });
});

function readRawProductionCatch(id) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(CATCH_DATABASE_NAME);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            const db = request.result;
            const transaction = db.transaction(PRODUCTION_CATCH_STORE_NAME, 'readonly');
            const getRequest = transaction.objectStore(PRODUCTION_CATCH_STORE_NAME).get(id);
            getRequest.onerror = () => {
                db.close();
                reject(getRequest.error);
            };
            getRequest.onsuccess = () => {
                db.close();
                resolve(getRequest.result);
            };
        };
    });
}
