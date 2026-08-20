import {
    closeDatabase,
    elapsedSince,
    openDatabase,
    runMultiStoreTransaction,
    runTransaction
} from './indexed-db.js';
import { emit, emitStorageEstimate, emitTimedOut } from './offline-diagnostics.js';

export const CATCH_DATABASE_NAME = 'FishingLogBook';
export const CATCH_STORE_NAME = 'catches';
export const PHOTO_STORE_NAME = 'catchPhotographs';
export const CATCH_DATABASE_VERSION = 4;
export const openTimeoutMs = 8000;

const LEGACY_TEST_CATCH_STORE_NAME = 'testCatches';
const LEGACY_TEST_CATCH_PHOTO_STORE_NAME = 'testCatchPhotographs';

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

function runCatchWithPhotographsTransaction(mode, operationName, execute) {
    const started = performance.now();
    const storeNames = [CATCH_STORE_NAME, PHOTO_STORE_NAME];
    return openCatchDatabase().then((db) => runMultiStoreTransaction(db, {
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

export async function putCatchWithPhotographs(json, photographs) {
    const catchRecord = JSON.parse(json);
    const photos = Array.isArray(photographs) ? photographs : [];
    if (!catchRecord?.id) {
        throw new Error('Catch id is required');
    }

    if (photos.length === 0) {
        throw new Error('Catch requires at least one photograph');
    }

    await runCatchWithPhotographsTransaction('readwrite', 'write', (transaction, succeed, fail) => {
        for (const photograph of photos) {
            if (!photograph?.id) {
                fail(new Error('Photograph id is required'));
                return;
            }
        }

        const catchStore = transaction.objectStore(CATCH_STORE_NAME);
        const photoStore = transaction.objectStore(PHOTO_STORE_NAME);
        const incomingIds = new Set(photos.map((photograph) => photograph.id));
        const catchRequest = catchStore.put(catchRecord);
        catchRequest.onerror = () => fail(catchRequest.error);
        catchRequest.onsuccess = () => {
            const cursorRequest = photoStore.openCursor();
            cursorRequest.onerror = () => fail(cursorRequest.error);
            cursorRequest.onsuccess = () => {
                const cursor = cursorRequest.result;
                if (cursor) {
                    if (cursor.value.catchId === catchRecord.id && !incomingIds.has(cursor.value.id)) {
                        cursor.delete();
                    }

                    cursor.continue();
                    return;
                }

                writePhotographs(photoStore, photos, succeed, fail);
            };
        };
    });
}

function writePhotographs(photoStore, photos, succeed, fail) {
    let remaining = photos.length;
    for (const photograph of photos) {
        const request = photoStore.put({
            id: photograph.id,
            catchId: photograph.catchId,
            contentType: photograph.contentType,
            bytes: toStoredBytes(photograph.bytes)
        });
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            remaining -= 1;
            if (remaining === 0) {
                succeed();
            }
        };
    }
}

export async function updateCatchMetadata(json) {
    const catchRecord = JSON.parse(json);
    if (!catchRecord?.id || !normalisedUserId(catchRecord.userId)) {
        throw new Error('Owned Catch id is required');
    }

    await runCatchTransaction(CATCH_STORE_NAME, 'readwrite', 'sync-state-write', (store, succeed, fail) => {
        const existingRequest = store.get(catchRecord.id);
        existingRequest.onerror = () => fail(existingRequest.error);
        existingRequest.onsuccess = () => {
            const existing = existingRequest.result;
            if (!existing || normalisedUserId(existing.userId) !== normalisedUserId(catchRecord.userId)) {
                fail(new Error('Owned Catch was not found'));
                return;
            }

            const incomingPhotographs = new Map(
                (catchRecord.photographs || []).map((photograph) => [photograph.id, photograph])
            );
            const photographs = (existing.photographs || []).map((photograph) => {
                const incoming = incomingPhotographs.get(photograph.id);
                if (!incoming) {
                    return photograph;
                }

                return {
                    ...photograph,
                    syncStatus: incoming.syncStatus,
                    objectKey: incoming.objectKey
                };
            });
            const hasPendingLocalSyncState = (status) => typeof status === 'number' && status !== 3;
            const location = hasPendingLocalSyncState(existing.syncStatus)
                || hasPendingLocalSyncState(existing.metadataSyncStatus)
                ? (existing.location ?? catchRecord.location)
                : (catchRecord.location ?? existing.location);

            const updateRequest = store.put({
                ...existing,
                syncStatus: catchRecord.syncStatus,
                metadataSyncStatus: catchRecord.metadataSyncStatus,
                location,
                photographs
            });
            updateRequest.onsuccess = () => succeed();
            updateRequest.onerror = () => fail(updateRequest.error);
        };
    });
}

export async function getAllCatchesWithPhotographs(ownerUserId) {
    return runCatchWithPhotographsTransaction('readonly', 'read', (transaction, succeed, fail) => {
        const catchStore = transaction.objectStore(CATCH_STORE_NAME);
        const photoStore = transaction.objectStore(PHOTO_STORE_NAME);
        const catches = [];
        const catchRequest = catchStore.openCursor();
        catchRequest.onerror = () => fail(catchRequest.error);
        catchRequest.onsuccess = () => {
            const cursor = catchRequest.result;
            if (cursor) {
                catches.push(cursor.value);
                cursor.continue();
                return;
            }

            const visible = visibleCatchesForOwner(catches, ownerUserId);
            const visibleIds = new Set(visible.map((item) => item.id));
            const photographs = [];
            const photoRequest = photoStore.openCursor();
            photoRequest.onerror = () => fail(photoRequest.error);
            photoRequest.onsuccess = () => {
                const photoCursor = photoRequest.result;
                if (photoCursor) {
                    if (visibleIds.has(photoCursor.value.catchId)) {
                        photographs.push(photoCursor.value);
                    }
                    photoCursor.continue();
                    return;
                }

                succeed(visible.map((item) => ({
                    json: JSON.stringify(item),
                    photographs: orderPhotographs(item, photographs)
                        .map((photograph) => ({
                            id: photograph.id,
                            catchId: photograph.catchId,
                            contentType: photograph.contentType,
                            bytesBase64: uint8ToBase64(toUint8Array(photograph.bytes))
                        }))
                })));
            };
        };
    });
}

function visibleCatchesForOwner(catches, ownerUserId) {
    const owner = normalisedUserId(ownerUserId);
    if (!owner) {
        return [];
    }

    return catches.filter((item) => normalisedUserId(item?.userId) === owner);
}

function normalisedUserId(value) {
    if (typeof value !== 'string') {
        return '';
    }

    const normalised = value.trim().toLowerCase();
    if (!normalised || normalised === '00000000-0000-0000-0000-000000000000') {
        return '';
    }

    return normalised;
}

function orderPhotographs(catchRecord, photographs) {
    const related = photographs.filter((photograph) => photograph.catchId === catchRecord.id);
    const byId = new Map(related.map((photograph) => [photograph.id, photograph]));
    const ordered = [];
    const metadataPhotographs = Array.isArray(catchRecord.photographs) ? catchRecord.photographs : [];
    for (const metadata of metadataPhotographs) {
        const photograph = byId.get(metadata.id);
        if (photograph) {
            ordered.push(photograph);
            byId.delete(metadata.id);
        }
    }

    for (const photograph of byId.values()) {
        ordered.push(photograph);
    }

    return ordered;
}

function toUint8Array(bytes) {
    if (bytes instanceof Uint8Array) {
        return bytes;
    }

    if (!bytes) {
        return new Uint8Array();
    }

    return new Uint8Array(bytes);
}

function toStoredBytes(bytes) {
    const view = toUint8Array(bytes);
    return view.buffer.slice(view.byteOffset, view.byteOffset + view.byteLength);
}

function uint8ToBase64(bytes) {
    let binary = '';
    const chunkSize = 0x8000;
    for (let offset = 0; offset < bytes.length; offset += chunkSize) {
        binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
    }

    return btoa(binary);
}

