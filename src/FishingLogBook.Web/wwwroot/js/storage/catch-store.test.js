import { describe, expect, it, vi } from 'vitest';
import {
    CATCH_DATABASE_NAME,
    CATCH_STORE_NAME,
    PHOTO_STORE_NAME,
    getAllTestCatches,
    openCatchDatabase,
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
            [PHOTO_STORE_NAME, CATCH_STORE_NAME].sort()
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
            const request = indexedDB.open(CATCH_DATABASE_NAME, 3);
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
