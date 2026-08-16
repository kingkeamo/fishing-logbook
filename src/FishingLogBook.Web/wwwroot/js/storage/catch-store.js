import {
    closeDatabase,
    elapsedSince,
    openDatabase,
    runTransaction
} from './indexed-db.js';
import { emit, emitStorageEstimate, emitTimedOut } from './offline-diagnostics.js';

export const CATCH_DATABASE_NAME = 'FishingLogBook';
export const CATCH_STORE_NAME = 'testCatches';
export const PHOTO_STORE_NAME = 'testCatchPhotographs';
export const CATCH_DATABASE_VERSION = 2;
export const openTimeoutMs = 8000;

const databaseName = CATCH_DATABASE_NAME;
const storeName = CATCH_STORE_NAME;
const photographStoreName = PHOTO_STORE_NAME;
const version = CATCH_DATABASE_VERSION;

export function openCatchDatabase() {
    const started = performance.now();
    emit('OfflineDbOpenStarted', { storeName, operation: 'open', elapsedMilliseconds: 0 });
    return openDatabase({
        databaseName,
        version,
        timeoutMs: openTimeoutMs,
        timeoutLabel: 'IndexedDB open',
        onUpgrade: (db) => {
            if (!db.objectStoreNames.contains(storeName)) {
                db.createObjectStore(storeName, { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains(photographStoreName)) {
                db.createObjectStore(photographStoreName, { keyPath: 'id' });
            }
        },
        onOpened: () => {
            emit('OfflineDbOpenCompleted', {
                operation: 'open',
                elapsedMilliseconds: elapsedSince(started)
            });
            void emitStorageEstimate();
        },
        onFailed: (error) => {
            emit('OfflineDbOpenFailed', {
                operation: 'open',
                elapsedMilliseconds: elapsedSince(started),
                errorType: error?.name
            });
        },
        onTimedOut: () => {
            emit('OfflineDbOpenTimedOut', {
                operation: 'open',
                elapsedMilliseconds: elapsedSince(started)
            });
        },
        onVersionChange: (db) => {
            closeDatabase(db);
            emit('OfflineDbClosed', {
                operation: 'versionchange',
                elapsedMilliseconds: elapsedSince(started)
            });
        }
    });
}

export function runCatchTransaction(objectStoreName, mode, operationName, execute) {
    const started = performance.now();
    return openCatchDatabase().then((db) => runTransaction(db, {
        storeName: objectStoreName,
        mode,
        timeoutMs: openTimeoutMs,
        timeoutLabel: `IndexedDB ${operationName}`,
        execute,
        closeWhenDone: true,
        onStarted: () => emit('OfflineDbTransactionStarted', {
            storeName: objectStoreName,
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onCompleted: () => emit('OfflineDbTransactionCompleted', {
            storeName: objectStoreName,
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onAborted: (error) => emit('OfflineDbTransactionAborted', {
            storeName: objectStoreName,
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started),
            errorType: error?.name
        }),
        onError: (error) => emit('OfflineDbTransactionError', {
            storeName: objectStoreName,
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started),
            errorType: error?.name
        }),
        onRequestSucceeded: () => emit('OfflineDbRequestSucceeded', {
            storeName: objectStoreName,
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onClosed: () => emit('OfflineDbClosed', {
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onTimedOut: (error) => emitTimedOut(mode, objectStoreName, started, error)
    }));
}

export async function putTestCatch(json) {
    const catchRecord = JSON.parse(json);
    await runCatchTransaction(storeName, 'readwrite', 'write', (store, succeed, fail) => {
        const request = store.put(catchRecord);
        request.onsuccess = () => succeed();
        request.onerror = () => fail(request.error);
    });
}

export async function getAllTestCatches() {
    return runCatchTransaction(storeName, 'readonly', 'read', (store, succeed, fail) => {
        const items = [];
        const request = store.openCursor();
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                succeed(items.map((item) => JSON.stringify(item)));
                return;
            }

            items.push(cursor.value);
            cursor.continue();
        };
        request.onerror = () => fail(request.error);
    });
}
