import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    closeDatabase,
    executeTransaction,
    getStorageEstimate,
    openDatabase,
    runTransaction
} from './indexed-db.js';

function createMockDb() {
    let transaction;
    const db = {
        closed: false,
        close() {
            this.closed = true;
        },
        transaction() {
            transaction = {
                objectStore() {
                    return {};
                },
                oncomplete: null,
                onabort: null,
                onerror: null,
                abort() {
                    this.onabort?.();
                }
            };
            return transaction;
        }
    };
    return {
        db,
        getTransaction: () => transaction
    };
}

describe('IndexedDB helper', () => {
    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
    });

    it('opens a database', async () => {
        const db = await openDatabase({
            databaseName: 'HelperOpen',
            version: 1,
            timeoutMs: 1000,
            onUpgrade: (opened) => {
                opened.createObjectStore('items', { keyPath: 'id' });
            }
        });

        expect(db.name).toBe('HelperOpen');
        expect(db.objectStoreNames.contains('items')).toBe(true);
        closeDatabase(db);
    });

    it('upgrades a database', async () => {
        const first = await openDatabase({
            databaseName: 'HelperUpgrade',
            version: 1,
            timeoutMs: 1000,
            onUpgrade: (opened) => {
                opened.createObjectStore('items', { keyPath: 'id' });
            }
        });
        closeDatabase(first);

        const upgraded = await openDatabase({
            databaseName: 'HelperUpgrade',
            version: 2,
            timeoutMs: 1000,
            onUpgrade: (opened) => {
                opened.createObjectStore('more', { keyPath: 'id' });
            }
        });

        expect(upgraded.objectStoreNames.contains('items')).toBe(true);
        expect(upgraded.objectStoreNames.contains('more')).toBe(true);
        closeDatabase(upgraded);
    });

    it('resolves a transaction only on complete', async () => {
        const { db, getTransaction } = createMockDb();
        let settled = false;
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            execute: (_store, succeed) => succeed('saved')
        }).then((value) => {
            settled = true;
            return value;
        });

        await Promise.resolve();
        expect(settled).toBe(false);

        getTransaction().oncomplete();
        await expect(promise).resolves.toBe('saved');
        expect(settled).toBe(true);
        expect(db.closed).toBe(true);
    });

    it('does not complete a write on request success alone', async () => {
        const { db, getTransaction } = createMockDb();
        let settled = false;
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            execute: (_store, succeed) => succeed()
        }).then(() => {
            settled = true;
        });

        await Promise.resolve();
        expect(settled).toBe(false);
        getTransaction().oncomplete();
        await promise;
        expect(settled).toBe(true);
    });

    it('rejects when the transaction aborts', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            abortMessage: 'IndexedDB transaction aborted',
            execute: () => { }
        });

        await Promise.resolve();
        getTransaction().error = new Error('aborted');
        getTransaction().onabort();
        await expect(promise).rejects.toThrow('aborted');
    });

    it('rejects when the transaction errors', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            execute: () => { }
        });

        await Promise.resolve();
        getTransaction().error = new Error('failed');
        getTransaction().onerror();
        await expect(promise).rejects.toThrow('failed');
    });

    it('rejects when the transaction times out', async () => {
        vi.useFakeTimers();
        const { db } = createMockDb();
        const promise = runTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            timeoutMs: 40,
            timeoutLabel: 'IndexedDB write',
            execute: () => { }
        });
        const assertion = expect(promise).rejects.toThrow('IndexedDB write timed out');
        await vi.advanceTimersByTimeAsync(40);
        await assertion;
        vi.useRealTimers();
        expect(db.closed).toBe(true);
    });

    it('closes the connection', async () => {
        const db = await openDatabase({
            databaseName: 'HelperClose',
            version: 1,
            timeoutMs: 1000,
            onUpgrade: (opened) => {
                opened.createObjectStore('items', { keyPath: 'id' });
            }
        });

        closeDatabase(db);
        expect(() => db.transaction('items', 'readonly')).toThrow();
    });

    it('closes the connection on versionchange', async () => {
        let versionChanged = false;
        const first = await openDatabase({
            databaseName: 'HelperVersionChange',
            version: 1,
            timeoutMs: 1000,
            onUpgrade: (opened) => {
                opened.createObjectStore('items', { keyPath: 'id' });
            },
            onVersionChange: (opened) => {
                versionChanged = true;
                closeDatabase(opened);
            }
        });

        const second = await openDatabase({
            databaseName: 'HelperVersionChange',
            version: 2,
            timeoutMs: 1000,
            onUpgrade: () => { }
        });

        expect(versionChanged).toBe(true);
        expect(() => first.transaction('items', 'readonly')).toThrow();
        closeDatabase(second);
    });

    it('closes the previous connection when versionchange has no custom handler', async () => {
        const first = await openDatabase({
            databaseName: 'HelperDefaultVersionChange',
            version: 1,
            timeoutMs: 1000,
            onUpgrade: (opened) => {
                opened.createObjectStore('items', { keyPath: 'id' });
            }
        });

        const second = await openDatabase({
            databaseName: 'HelperDefaultVersionChange',
            version: 2,
            timeoutMs: 1000,
            onUpgrade: () => { }
        });

        expect(() => first.transaction('items', 'readonly')).toThrow();
        closeDatabase(second);
    });

    it('rejects when open fails', async () => {
        const originalOpen = indexedDB.open.bind(indexedDB);
        const onFailed = vi.fn();
        indexedDB.open = () => {
            const request = {
                error: new DOMException('blocked', 'UnknownError')
            };
            queueMicrotask(() => request.onerror?.());
            return request;
        };

        try {
            await expect(openDatabase({
                databaseName: 'HelperOpenFail',
                version: 1,
                timeoutMs: 1000,
                onFailed
            })).rejects.toBeTruthy();
            expect(onFailed).toHaveBeenCalled();
        } finally {
            indexedDB.open = originalOpen;
        }
    });

    it('calls onTimedOut when open never completes', async () => {
        vi.useFakeTimers();
        const originalOpen = indexedDB.open.bind(indexedDB);
        const onTimedOut = vi.fn();
        indexedDB.open = () => ({
            onupgradeneeded: null,
            onsuccess: null,
            onerror: null
        });

        try {
            const assertion = expect(openDatabase({
                databaseName: 'HelperOpenTimeout',
                version: 1,
                timeoutMs: 40,
                timeoutLabel: 'IndexedDB open',
                onTimedOut
            })).rejects.toThrow('IndexedDB open timed out');
            await vi.advanceTimersByTimeAsync(40);
            await assertion;
            expect(onTimedOut).toHaveBeenCalled();
        } finally {
            indexedDB.open = originalOpen;
        }
    });

    it('still rejects on open timeout when onTimedOut is omitted', async () => {
        vi.useFakeTimers();
        const originalOpen = indexedDB.open.bind(indexedDB);
        indexedDB.open = () => ({
            onupgradeneeded: null,
            onsuccess: null,
            onerror: null
        });

        try {
            const assertion = expect(openDatabase({
                databaseName: 'HelperOpenTimeoutNoCallback',
                version: 1,
                timeoutMs: 40,
                timeoutLabel: 'IndexedDB open'
            })).rejects.toThrow('IndexedDB open timed out');
            await vi.advanceTimersByTimeAsync(40);
            await assertion;
        } finally {
            indexedDB.open = originalOpen;
        }
    });

    it('does not close the database when closeWhenDone is false', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readonly',
            closeWhenDone: false,
            execute: (_store, succeed) => succeed('kept-open')
        });

        await Promise.resolve();
        getTransaction().oncomplete();
        await expect(promise).resolves.toBe('kept-open');
        expect(db.closed).toBe(false);
    });

    it('rejects with the abort message when the transaction has no error', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            abortMessage: 'IndexedDB transaction aborted',
            execute: () => { }
        });

        await Promise.resolve();
        getTransaction().onabort();
        await expect(promise).rejects.toThrow('IndexedDB transaction aborted');
    });

    it('rejects from fail even when abort has already happened', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            execute: (_store, _succeed, fail) => {
                getTransaction().abort = () => {
                    throw new Error('already aborted');
                };
                fail(new Error('request failed'));
            }
        });

        await expect(promise).rejects.toThrow('request failed');
    });

    it('rejects with a fallback when fail is called without an error', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            execute: (_store, _succeed, fail) => {
                getTransaction().abort = () => {
                    throw new Error('already aborted');
                };
                fail();
            }
        });

        await expect(promise).rejects.toThrow('IndexedDB request failed');
    });

    it('ignores a second settle after the transaction has finished', async () => {
        const { db, getTransaction } = createMockDb();
        const promise = executeTransaction(db, {
            storeName: 'items',
            mode: 'readwrite',
            execute: (_store, succeed) => succeed('first')
        });

        await Promise.resolve();
        getTransaction().oncomplete();
        getTransaction().onerror();
        await expect(promise).resolves.toBe('first');
    });

    it('leaves the connection open on timeout when closeWhenDone is false', async () => {
        vi.useFakeTimers();
        const { db } = createMockDb();
        const onTimedOut = vi.fn();
        const promise = runTransaction(db, {
            storeName: 'items',
            mode: 'readonly',
            timeoutMs: 40,
            timeoutLabel: 'IndexedDB read',
            closeWhenDone: false,
            onTimedOut,
            execute: () => { }
        });
        const assertion = expect(promise).rejects.toThrow('IndexedDB read timed out');
        await vi.advanceTimersByTimeAsync(40);
        await assertion;
        expect(onTimedOut).toHaveBeenCalled();
        expect(db.closed).toBe(false);
    });

    it('swallows close errors when the connection is already closed', () => {
        expect(() => closeDatabase({
            close() {
                throw new Error('already closed');
            }
        })).not.toThrow();
    });

    it('returns null quota and usage when storage estimate is missing', async () => {
        vi.stubGlobal('navigator', {});

        await expect(getStorageEstimate()).resolves.toEqual({ quota: null, usage: null });
    });

    it('returns finite quota and usage from the storage estimate', async () => {
        vi.stubGlobal('navigator', {
            storage: {
                estimate: async () => ({ quota: 2048, usage: 128 })
            }
        });

        await expect(getStorageEstimate()).resolves.toEqual({ quota: 2048, usage: 128 });
    });

    it('returns null quota and usage when the estimate is not finite', async () => {
        vi.stubGlobal('navigator', {
            storage: {
                estimate: async () => ({ quota: Number.POSITIVE_INFINITY, usage: Number.NaN })
            }
        });

        await expect(getStorageEstimate()).resolves.toEqual({ quota: null, usage: null });
    });

    it('returns null quota and usage when estimate throws', async () => {
        vi.stubGlobal('navigator', {
            storage: {
                estimate: async () => {
                    throw new Error('estimate blocked');
                }
            }
        });

        await expect(getStorageEstimate()).resolves.toEqual({ quota: null, usage: null });
    });
});

describe('blocked upgrades', () => {
    it('reports a blocked upgrade instead of failing silently', async () => {
        const databaseName = `blocked-${Math.floor(performance.now() * 1000)}`;
        const holder = await new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, 1);
            request.onupgradeneeded = () => request.result.createObjectStore('items', { keyPath: 'id' });
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        let blocked = false;
        const opening = openDatabase({
            databaseName,
            version: 2,
            timeoutMs: 2000,
            onUpgrade: () => { },
            onBlocked: () => {
                blocked = true;
                holder.close();
            }
        });
        const upgraded = await opening;

        expect(blocked).toBe(true);
        expect(upgraded.version).toBe(2);
        upgraded.close();
    });
});
