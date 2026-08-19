import {
    closeDatabase,
    openDatabase,
    runTransaction
} from './indexed-db.js';

export const PREFERENCE_DATABASE_NAME = 'FishingLogBookPreferences';
export const PREFERENCE_STORE_NAME = 'anglerPreferences';
export const PREFERENCE_DATABASE_VERSION = 1;

const databaseName = PREFERENCE_DATABASE_NAME;
const storeName = PREFERENCE_STORE_NAME;
const version = PREFERENCE_DATABASE_VERSION;
const openTimeoutMs = 8000;

function openPreferenceDatabase() {
    return openDatabase({
        databaseName,
        version,
        timeoutMs: openTimeoutMs,
        timeoutLabel: 'preference open',
        onUpgrade: (db) => {
            if (!db.objectStoreNames.contains(storeName)) {
                db.createObjectStore(storeName, { keyPath: 'userId' });
            }
        }
    });
}

async function withPreferenceTransaction(mode, timeoutLabel, abortMessage, execute) {
    const db = await openPreferenceDatabase();
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

export async function putFishingPreferences(userId, json) {
    if (!userId) {
        throw new Error('A fishing preference cache entry requires an owner.');
    }

    await withPreferenceTransaction(
        'readwrite',
        'preference write',
        'preference transaction aborted',
        (store, succeed, fail) => {
            const request = store.put({ userId, json });
            request.onerror = () => fail(request.error);
            request.onsuccess = () => succeed(undefined);
        });
}

export async function getFishingPreferences(userId) {
    if (!userId) {
        return null;
    }

    return withPreferenceTransaction(
        'readonly',
        'preference read',
        'preference transaction aborted',
        (store, succeed, fail) => {
            const request = store.get(userId);
            request.onerror = () => fail(request.error);
            request.onsuccess = () => {
                const record = request.result;
                if (!record || record.userId !== userId) {
                    succeed(null);
                    return;
                }

                succeed(record.json ?? null);
            };
        });
}

export async function clearFishingPreferences() {
    await withPreferenceTransaction(
        'readwrite',
        'preference clear',
        'preference transaction aborted',
        (store, succeed, fail) => {
            const request = store.clear();
            request.onerror = () => fail(request.error);
            request.onsuccess = () => succeed(undefined);
        });
}
