import {
    closeDatabase,
    getStorageEstimate,
    openDatabase,
    runTransaction,
    withTimeout
} from './indexed-db.js';

export const DIAGNOSTIC_DATABASE_NAME = 'FishingLogBookDiagnostics';
export const DIAGNOSTIC_STORE_NAME = 'diagnosticEvents';
export const DIAGNOSTIC_DATABASE_VERSION = 1;

const databaseName = DIAGNOSTIC_DATABASE_NAME;
const storeName = DIAGNOSTIC_STORE_NAME;
const version = DIAGNOSTIC_DATABASE_VERSION;
const openTimeoutMs = 8000;

function openDiagnosticDatabase() {
    return openDatabase({
        databaseName,
        version,
        timeoutMs: openTimeoutMs,
        timeoutLabel: 'diagnostic open',
        onUpgrade: (db) => {
            if (!db.objectStoreNames.contains(storeName)) {
                const store = db.createObjectStore(storeName, { keyPath: 'id' });
                store.createIndex('timestampUtc', 'timestampUtc');
            }
        }
    });
}

async function withDiagnosticTransaction(mode, timeoutLabel, abortMessage, execute) {
    const db = await openDiagnosticDatabase();
    try {
        return await runTransaction(db, {
            storeName,
            mode,
            timeoutMs: openTimeoutMs,
            timeoutLabel,
            abortMessage,
            closeWhenDone: false,
            execute
        });
    } finally {
        closeDatabase(db);
    }
}

export async function putDiagnosticEvent(json, maxQueueSize) {
    const record = JSON.parse(json);
    await withDiagnosticTransaction('readwrite', 'diagnostic write', 'diagnostic transaction aborted', (store, succeed, fail) => {
        store.put(record);
        const countRequest = store.count();
        countRequest.onsuccess = () => {
            const overflow = (countRequest.result || 0) - maxQueueSize;
            if (overflow <= 0) {
                succeed();
                return;
            }

            const index = store.index('timestampUtc');
            const cursorRequest = index.openCursor();
            let remaining = overflow;
            cursorRequest.onsuccess = () => {
                const cursor = cursorRequest.result;
                if (!cursor || remaining <= 0) {
                    succeed();
                    return;
                }

                cursor.delete();
                remaining -= 1;
                cursor.continue();
            };
            cursorRequest.onerror = () => fail(cursorRequest.error);
        };
        countRequest.onerror = () => fail(countRequest.error);
    });
}

export async function getPendingDiagnosticEvents(maxCount) {
    return withDiagnosticTransaction('readonly', 'diagnostic read', 'diagnostic transaction aborted', (store, succeed, fail) => {
        const results = [];
        const index = store.index('timestampUtc');
        const request = index.openCursor();
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor || results.length >= maxCount) {
                succeed(results.map((item) => JSON.stringify(item)));
                return;
            }

            results.push(cursor.value);
            cursor.continue();
        };
        request.onerror = () => fail(request.error);
    });
}

export async function deleteDiagnosticEvents(idsJson) {
    const keys = JSON.parse(idsJson || '[]').map((id) => String(id));
    await withDiagnosticTransaction('readwrite', 'diagnostic delete', 'diagnostic transaction aborted', (store, succeed) => {
        for (const key of keys) {
            store.delete(key);
        }

        succeed();
    });
}

export async function getDiagnosticQueueCount() {
    return withDiagnosticTransaction('readonly', 'diagnostic count', 'diagnostic transaction aborted', (store, succeed, fail) => {
        const request = store.count();
        request.onsuccess = () => succeed(request.result || 0);
        request.onerror = () => fail(request.error);
    });
}

export async function inspectExistingDiagnosticDatabase() {
    const exists = await productionDatabaseExists();
    if (!exists) {
        return { exists: false, hasStore: false, count: 0 };
    }

    const db = await openExistingDatabase();
    try {
        const hasStore = db.objectStoreNames.contains(storeName);
        if (!hasStore) {
            return { exists: true, hasStore: false, count: 0 };
        }

        const count = await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const request = transaction.objectStore(storeName).count();
            request.onsuccess = () => resolve(request.result || 0);
            request.onerror = () => reject(request.error);
            transaction.onabort = () => reject(transaction.error || new Error('diagnostic transaction aborted'));
        }), openTimeoutMs, 'diagnostic inspect count');
        return { exists: true, hasStore: true, count };
    } finally {
        db.close();
    }
}

async function productionDatabaseExists() {
    if (typeof indexedDB.databases === 'function') {
        try {
            const databases = await indexedDB.databases();
            if (Array.isArray(databases) && databases.some((item) => item.name === databaseName)) {
                return true;
            }
        } catch {
        }
    }

    return existsByOpenAbort();
}

function existsByOpenAbort() {
    return withTimeout(new Promise((resolve, reject) => {
        let createdFromEmpty = false;
        const request = indexedDB.open(databaseName);
        request.onupgradeneeded = (event) => {
            if (event.oldVersion === 0) {
                createdFromEmpty = true;
                event.target.transaction.abort();
            }
        };
        request.onsuccess = () => {
            const db = request.result;
            db.close();
            if (createdFromEmpty) {
                indexedDB.deleteDatabase(databaseName);
                resolve(false);
                return;
            }

            resolve(true);
        };
        request.onerror = () => {
            if (createdFromEmpty) {
                resolve(false);
                return;
            }

            reject(request.error);
        };
    }), openTimeoutMs, 'diagnostic exists');
}

function openExistingDatabase() {
    return withTimeout(new Promise((resolve, reject) => {
        let createdFromEmpty = false;
        const request = indexedDB.open(databaseName);
        request.onupgradeneeded = (event) => {
            if (event.oldVersion === 0) {
                createdFromEmpty = true;
                event.target.transaction.abort();
            }
        };
        request.onsuccess = () => {
            const db = request.result;
            if (createdFromEmpty) {
                db.close();
                indexedDB.deleteDatabase(databaseName);
                reject(new Error('not-initialised'));
                return;
            }

            db.onversionchange = () => db.close();
            db.onclose = () => { };
            resolve(db);
        };
        request.onerror = () => {
            if (createdFromEmpty || (request.error && request.error.name === 'AbortError')) {
                reject(new Error('not-initialised'));
                return;
            }

            reject(request.error);
        };
    }), openTimeoutMs, 'diagnostic inspect open');
}

export { getStorageEstimate };
