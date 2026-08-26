import {
    closeDatabase,
    elapsedSince,
    openDatabase,
    runMultiStoreTransaction,
    runTransaction
} from './indexed-db.js';
import { emit, emitStorageEstimate, emitTimedOut } from './offline-diagnostics.js';

export const LOGBOOK_DATABASE_NAME = 'FishingLogBook';
export const LOGBOOK_DATABASE_VERSION = 5;
export const CATCH_STORE_NAME = 'catches';
export const PHOTO_STORE_NAME = 'catchPhotographs';
export const TRIP_STORE_NAME = 'trips';
export const openTimeoutMs = 8000;

const LEGACY_TEST_CATCH_STORE_NAME = 'testCatches';
const LEGACY_TEST_CATCH_PHOTO_STORE_NAME = 'testCatchPhotographs';

export function openLogbookDatabase() {
    const started = performance.now();
    emit('OfflineDbOpenStarted', {
        storeName: CATCH_STORE_NAME,
        operation: 'open',
        elapsedMilliseconds: 0
    });
    return openDatabase({
        databaseName: LOGBOOK_DATABASE_NAME,
        version: LOGBOOK_DATABASE_VERSION,
        timeoutMs: openTimeoutMs,
        timeoutLabel: 'IndexedDB open',
        onUpgrade: (db) => {
            if (!db.objectStoreNames.contains(CATCH_STORE_NAME)) {
                db.createObjectStore(CATCH_STORE_NAME, { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains(PHOTO_STORE_NAME)) {
                db.createObjectStore(PHOTO_STORE_NAME, { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains(TRIP_STORE_NAME)) {
                db.createObjectStore(TRIP_STORE_NAME, { keyPath: 'id' });
            }
            if (db.objectStoreNames.contains(LEGACY_TEST_CATCH_STORE_NAME)) {
                db.deleteObjectStore(LEGACY_TEST_CATCH_STORE_NAME);
            }
            if (db.objectStoreNames.contains(LEGACY_TEST_CATCH_PHOTO_STORE_NAME)) {
                db.deleteObjectStore(LEGACY_TEST_CATCH_PHOTO_STORE_NAME);
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

export function runLogbookTransaction(objectStoreName, mode, operationName, execute) {
    const started = performance.now();
    return openLogbookDatabase().then((db) => runTransaction(db, {
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

export function runLogbookMultiStoreTransaction(storeNames, mode, operationName, execute) {
    const started = performance.now();
    return openLogbookDatabase().then((db) => runMultiStoreTransaction(db, {
        storeNames,
        mode,
        timeoutMs: openTimeoutMs,
        timeoutLabel: `IndexedDB ${operationName}`,
        execute,
        closeWhenDone: true,
        onStarted: () => emit('OfflineDbTransactionStarted', {
            storeName: storeNames.join(','),
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onCompleted: () => emit('OfflineDbTransactionCompleted', {
            storeName: storeNames.join(','),
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onAborted: (error) => emit('OfflineDbTransactionAborted', {
            storeName: storeNames.join(','),
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started),
            errorType: error?.name
        }),
        onError: (error) => emit('OfflineDbTransactionError', {
            storeName: storeNames.join(','),
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started),
            errorType: error?.name
        }),
        onRequestSucceeded: () => emit('OfflineDbRequestSucceeded', {
            storeName: storeNames.join(','),
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onClosed: () => emit('OfflineDbClosed', {
            operation: operationName,
            elapsedMilliseconds: elapsedSince(started)
        }),
        onTimedOut: (error) => emitTimedOut(mode, storeNames.join(','), started, error)
    }));
}

export function normalisedOwnerId(value) {
    if (typeof value !== 'string') {
        return '';
    }

    const normalised = value.trim().toLowerCase();
    if (!normalised || normalised === '00000000-0000-0000-0000-000000000000') {
        return '';
    }

    return normalised;
}
