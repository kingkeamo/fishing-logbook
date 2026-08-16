import { describe, expect, it, vi } from 'vitest';
import { PHOTO_STORE_NAME, openCatchDatabase } from './catch-store.js';
import * as catchStore from './catch-store.js';
import * as indexedDb from './indexed-db.js';
import {
    getTestCatchPhotograph,
    putTestCatchPhotograph
} from './photo-store.js';

describe('Photo store', () => {
    it('stores and reads photograph bytes', async () => {
        const bytes = new Uint8Array([1, 2, 3, 4]);

        await putTestCatchPhotograph('photo-1', bytes, 'image/jpeg');
        const stored = await getTestCatchPhotograph('photo-1');

        expect(stored.contentType).toBe('image/jpeg');
        expect(stored.bytesBase64).toBe(btoa(String.fromCharCode(1, 2, 3, 4)));
    });

    it('preserves the content type', async () => {
        await putTestCatchPhotograph('photo-2', new Uint8Array([9]), 'image/png');

        const stored = await getTestCatchPhotograph('photo-2');

        expect(stored.contentType).toBe('image/png');
    });

    it('returns null when the photograph is missing', async () => {
        const stored = await getTestCatchPhotograph('missing');

        expect(stored).toBeNull();
    });

    it('rejects a photograph that has no id', async () => {
        await expect(putTestCatchPhotograph(undefined, new Uint8Array([1]), 'image/jpeg')).rejects.toBeTruthy();
    });

    it('stores ArrayBuffer photograph bytes', async () => {
        await putTestCatchPhotograph('photo-buffer', new Uint8Array([5, 6]).buffer, 'image/jpeg');

        const stored = await getTestCatchPhotograph('photo-buffer');

        expect(stored.bytesBase64).toBe(btoa(String.fromCharCode(5, 6)));
    });

    it('reads a legacy blob photograph', async () => {
        vi.spyOn(catchStore, 'openCatchDatabase').mockResolvedValue({
            close() { }
        });
        vi.spyOn(indexedDb, 'executeTransaction').mockResolvedValue({
            contentType: 'image/png',
            blob: {
                arrayBuffer: async () => new Uint8Array([7, 8]).buffer
            }
        });

        try {
            const stored = await getTestCatchPhotograph('photo-blob');

            expect(stored.contentType).toBe('image/png');
            expect(stored.bytesBase64).toBe(btoa(String.fromCharCode(7, 8)));
        } finally {
            vi.restoreAllMocks();
        }
    });

    it('returns null for a record with neither bytes nor a blob', async () => {
        const db = await openCatchDatabase();
        await new Promise((resolve, reject) => {
            const transaction = db.transaction(PHOTO_STORE_NAME, 'readwrite');
            transaction.objectStore(PHOTO_STORE_NAME).put({
                id: 'photo-empty',
                contentType: 'image/jpeg'
            });
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
        });
        db.close();

        await expect(getTestCatchPhotograph('photo-empty')).resolves.toBeNull();
    });

    it('rejects when the photograph read times out', async () => {
        vi.useFakeTimers();
        vi.spyOn(catchStore, 'openCatchDatabase').mockResolvedValue({
            close() { }
        });
        vi.spyOn(indexedDb, 'executeTransaction').mockReturnValue(new Promise(() => { }));

        try {
            const assertion = expect(getTestCatchPhotograph('photo-timeout'))
                .rejects.toThrow('IndexedDB photograph read timed out');
            await vi.advanceTimersByTimeAsync(8000);
            await assertion;
        } finally {
            vi.restoreAllMocks();
            vi.useRealTimers();
        }
    });

    it('rejects when the photograph request fails', async () => {
        let transaction;
        const db = {
            close() { },
            transaction() {
                transaction = {
                    objectStore() {
                        return {
                            get() {
                                const request = {
                                    error: new Error('get failed'),
                                    onsuccess: null,
                                    onerror: null
                                };
                                queueMicrotask(() => request.onerror?.());
                                return request;
                            }
                        };
                    },
                    abort() {
                        this.onabort?.();
                    },
                    oncomplete: null,
                    onabort: null,
                    onerror: null
                };
                return transaction;
            }
        };
        vi.spyOn(catchStore, 'openCatchDatabase').mockResolvedValue(db);

        try {
            await expect(getTestCatchPhotograph('photo-request-error')).rejects.toThrow('IndexedDB transaction aborted');
        } finally {
            vi.restoreAllMocks();
        }
    });

    it('rejects when the photograph transaction aborts', async () => {
        let transaction;
        const db = {
            close() { },
            transaction() {
                transaction = {
                    objectStore() {
                        return {
                            get() {
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
        };
        vi.spyOn(catchStore, 'openCatchDatabase').mockResolvedValue(db);

        try {
            const promise = getTestCatchPhotograph('photo-abort');
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

    it('rejects when the photograph transaction errors', async () => {
        let transaction;
        const db = {
            close() { },
            transaction() {
                transaction = {
                    objectStore() {
                        return {
                            get() {
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
        };
        vi.spyOn(catchStore, 'openCatchDatabase').mockResolvedValue(db);

        try {
            const promise = getTestCatchPhotograph('photo-error');
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
});
