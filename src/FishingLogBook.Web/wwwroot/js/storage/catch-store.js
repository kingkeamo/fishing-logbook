import {
    CATCH_STORE_NAME as LOGBOOK_CATCH_STORE_NAME,
    LOGBOOK_DATABASE_NAME,
    LOGBOOK_DATABASE_VERSION,
    PHOTO_STORE_NAME as LOGBOOK_PHOTO_STORE_NAME,
    normalisedOwnerId,
    openLogbookDatabase,
    openTimeoutMs as logbookOpenTimeoutMs,
    runLogbookMultiStoreTransaction,
    runLogbookTransaction
} from './logbook-database.js';

export const CATCH_DATABASE_NAME = LOGBOOK_DATABASE_NAME;
export const CATCH_STORE_NAME = LOGBOOK_CATCH_STORE_NAME;
export const PHOTO_STORE_NAME = LOGBOOK_PHOTO_STORE_NAME;
export const CATCH_DATABASE_VERSION = LOGBOOK_DATABASE_VERSION;
export const openTimeoutMs = logbookOpenTimeoutMs;

export const openCatchDatabase = openLogbookDatabase;

export function runCatchTransaction(objectStoreName, mode, operationName, execute) {
    return runLogbookTransaction(objectStoreName, mode, operationName, execute);
}

function runCatchWithPhotographsTransaction(mode, operationName, execute) {
    return runLogbookMultiStoreTransaction(
        [CATCH_STORE_NAME, PHOTO_STORE_NAME],
        mode,
        operationName,
        execute);
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
        const existingRequest = catchStore.get(catchRecord.id);
        existingRequest.onerror = () => fail(existingRequest.error);
        existingRequest.onsuccess = () => {
            const existingPhotographs = existingRequest.result?.photographs;
            const catchRequest = catchStore.put(catchRecord);
            catchRequest.onerror = () => fail(catchRequest.error);

            if (!existingRequest.result || Array.isArray(existingPhotographs)) {
                for (const photograph of existingPhotographs ?? []) {
                    if (!incomingIds.has(photograph.id)) {
                        const deleteRequest = photoStore.delete(photograph.id);
                        deleteRequest.onerror = () => fail(deleteRequest.error);
                    }
                }

                writePhotographs(photoStore, photos, succeed, fail);
                return;
            }

            // Records written before photograph metadata was embedded in the Catch
            // require one compatibility scan. Current writes use direct photograph keys.
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
    if (!catchRecord?.id || !normalisedOwnerId(catchRecord.userId)) {
        throw new Error('Owned Catch id is required');
    }

    await runCatchTransaction(CATCH_STORE_NAME, 'readwrite', 'sync-state-write', (store, succeed, fail) => {
        const existingRequest = store.get(catchRecord.id);
        existingRequest.onerror = () => fail(existingRequest.error);
        existingRequest.onsuccess = () => {
            const existing = existingRequest.result;
            if (!existing || normalisedOwnerId(existing.userId) !== normalisedOwnerId(catchRecord.userId)) {
                fail(new Error('Owned Catch was not found'));
                return;
            }

            const incomingPhotographs = new Map(
                (catchRecord.photographs || []).map((photograph) => [photograph.id, photograph])
            );
            const metadataChangedWhileSynchronising = hasMetadataDifference(existing, catchRecord);
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
                syncStatus: metadataChangedWhileSynchronising
                    ? existing.syncStatus
                    : catchRecord.syncStatus,
                metadataSyncStatus: metadataChangedWhileSynchronising
                    ? existing.metadataSyncStatus
                    : catchRecord.metadataSyncStatus,
                syncedAt: metadataChangedWhileSynchronising
                    ? existing.syncedAt
                    : catchRecord.syncedAt,
                location,
                photographs
            });
            updateRequest.onsuccess = () => succeed();
            updateRequest.onerror = () => fail(updateRequest.error);
        };
    });
}

export async function updateCatchTrip(json) {
    const request = JSON.parse(json);
    const owner = normalisedOwnerId(request?.userId);
    if (!request?.id || !owner) {
        throw new Error('Owned Catch id is required');
    }

    await runCatchTransaction(CATCH_STORE_NAME, 'readwrite', 'trip-write', (store, succeed, fail) => {
        const existingRequest = store.get(request.id);
        existingRequest.onerror = () => fail(existingRequest.error);
        existingRequest.onsuccess = () => {
            const existing = existingRequest.result;
            if (!existing || normalisedOwnerId(existing.userId) !== owner) {
                fail(new Error('Owned Catch was not found'));
                return;
            }

            const updateRequest = store.put({
                ...existing,
                tripId: request.tripId ?? null,
                metadataSyncStatus: request.metadataSyncStatus
            });
            updateRequest.onsuccess = () => succeed();
            updateRequest.onerror = () => fail(updateRequest.error);
        };
    });
}

function hasMetadataDifference(existing, incoming) {
    const metadataFields = [
        'caughtOn',
        'speciesName',
        'anglerUserId',
        'recordedByUserId',
        'weight',
        'length',
        'method',
        'baitOrLure',
        'notes'
    ];
    return metadataFields.some((field) =>
        Object.hasOwn(incoming, field)
        && JSON.stringify(existing[field] ?? null) !== JSON.stringify(incoming[field] ?? null));
}

export async function getAllCatchesWithPhotographs(ownerUserId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
        return [];
    }

    return runCatchWithPhotographsTransaction('readonly', 'read', (transaction, succeed, fail) => {
        const catchStore = transaction.objectStore(CATCH_STORE_NAME);
        const photoStore = transaction.objectStore(PHOTO_STORE_NAME);
        const catches = [];
        const catchRequest = catchStore.openCursor();
        catchRequest.onerror = () => fail(catchRequest.error);
        catchRequest.onsuccess = () => {
            const cursor = catchRequest.result;
            if (cursor) {
                if (normalisedOwnerId(cursor.value?.userId) === owner) {
                    catches.push(cursor.value);
                }

                cursor.continue();
                return;
            }

            const visibleIds = new Set(catches.map((item) => item.id));
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

                succeed(catches.map((item) => toStoredResult(item, photographs)));
            };
        };
    });
}

export async function getCatchMetadata(ownerUserId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
        return [];
    }

    return runCatchTransaction(CATCH_STORE_NAME, 'readonly', 'metadata-read', (store, succeed, fail) => {
        const catches = [];
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (cursor) {
                if (normalisedOwnerId(cursor.value?.userId) === owner) {
                    catches.push({ json: JSON.stringify(cursor.value), photographs: [] });
                }

                cursor.continue();
                return;
            }

            succeed(catches);
        };
    });
}

export async function getCatchMetadataById(ownerUserId, catchId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || typeof catchId !== 'string' || !catchId) {
        return null;
    }

    return runCatchTransaction(CATCH_STORE_NAME, 'readonly', 'single-metadata-read', (store, succeed, fail) => {
        const request = store.get(catchId);
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const record = request.result;
            succeed(record && normalisedOwnerId(record.userId) === owner
                ? { json: JSON.stringify(record), photographs: [] }
                : null);
        };
    });
}

export async function getCatchWithPhotographs(ownerUserId, catchId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || typeof catchId !== 'string' || !catchId) {
        return null;
    }

    return runCatchWithPhotographsTransaction('readonly', 'single-read', (transaction, succeed, fail) => {
        const catchStore = transaction.objectStore(CATCH_STORE_NAME);
        const photoStore = transaction.objectStore(PHOTO_STORE_NAME);
        const catchRequest = catchStore.get(catchId);
        catchRequest.onerror = () => fail(catchRequest.error);
        catchRequest.onsuccess = () => {
            const record = catchRequest.result;
            if (!record || normalisedOwnerId(record.userId) !== owner) {
                succeed(null);
                return;
            }

            const photographIds = (Array.isArray(record.photographs) ? record.photographs : [])
                .map((photograph) => photograph?.id)
                .filter((id) => typeof id === 'string' && id.length > 0);
            if (photographIds.length === 0) {
                readPhotographsByCatchId(photoStore, record, succeed, fail);
                return;
            }

            readPhotographsByIds(photoStore, record, photographIds, succeed, fail);
        };
    });
}

function readPhotographsByIds(photoStore, record, photographIds, succeed, fail) {
    const photographs = [];
    let remaining = photographIds.length;
    for (const photographId of photographIds) {
        const request = photoStore.get(photographId);
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            if (request.result && request.result.catchId === record.id) {
                photographs.push(request.result);
            }

            remaining -= 1;
            if (remaining === 0) {
                succeed(toStoredResult(record, photographs));
            }
        };
    }
}

function readPhotographsByCatchId(photoStore, record, succeed, fail) {
    const photographs = [];
    const request = photoStore.openCursor();
    request.onerror = () => fail(request.error);
    request.onsuccess = () => {
        const cursor = request.result;
        if (cursor) {
            if (cursor.value?.catchId === record.id) {
                photographs.push(cursor.value);
            }

            cursor.continue();
            return;
        }

        succeed(toStoredResult(record, photographs));
    };
}

function toStoredResult(record, photographs) {
    return {
        json: JSON.stringify(record),
        photographs: orderPhotographs(record, photographs).map((photograph) => ({
            id: photograph.id,
            catchId: photograph.catchId,
            contentType: photograph.contentType,
            bytesBase64: uint8ToBase64(toUint8Array(photograph.bytes))
        }))
    };
}

export async function cleanupSyncedCatches(ownerUserId, olderThanIso) {
    const owner = normalisedOwnerId(ownerUserId);
    const cutoff = Date.parse(olderThanIso);
    if (!owner || Number.isNaN(cutoff)) {
        return 0;
    }

    return runCatchWithPhotographsTransaction('readwrite', 'cleanup', (transaction, succeed, fail) => {
        const catchStore = transaction.objectStore(CATCH_STORE_NAME);
        const photoStore = transaction.objectStore(PHOTO_STORE_NAME);
        let removed = 0;
        const request = catchStore.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                succeed(removed);
                return;
            }

            const record = cursor.value;
            if (normalisedOwnerId(record?.userId) === owner && isEligibleForCleanup(record, cutoff)) {
                for (const photograph of record.photographs || []) {
                    photoStore.delete(photograph.id);
                }

                cursor.delete();
                removed += 1;
            }

            cursor.continue();
        };
    });
}

function isEligibleForCleanup(record, cutoff) {
    if (!isSynchronisedStatus(record.syncStatus) || !isSynchronisedStatus(record.metadataSyncStatus)) {
        return false;
    }

    const photographs = Array.isArray(record.photographs) ? record.photographs : [];
    if (!photographs.every((photograph) => isSynchronisedStatus(photograph.syncStatus))) {
        return false;
    }

    const syncedAt = Date.parse(record.syncedAt);
    return !Number.isNaN(syncedAt) && syncedAt <= cutoff;
}

function isSynchronisedStatus(status) {
    return status === 'synchronised' || status === 3;
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
