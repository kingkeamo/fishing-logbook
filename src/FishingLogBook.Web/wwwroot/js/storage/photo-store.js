import {
    closeDatabase,
    elapsedSince,
    executeTransaction,
    withTimeout
} from './indexed-db.js';
import { emit, emitTimedOut } from './offline-diagnostics.js';
import {
    PHOTO_STORE_NAME,
    openCatchDatabase,
    openTimeoutMs,
    runCatchTransaction
} from './catch-store.js';

const photographStoreName = PHOTO_STORE_NAME;

export async function putTestCatchPhotograph(id, bytes, contentType) {
    const storedBytes = toStoredBytes(bytes);
    await runCatchTransaction(photographStoreName, 'readwrite', 'write', (store, succeed, fail) => {
        const request = store.put({ id, bytes: storedBytes, contentType });
        request.onsuccess = () => succeed();
        request.onerror = () => fail(request.error);
    });
}

export async function getTestCatchPhotograph(id) {
    const started = performance.now();
    const db = await openCatchDatabase();
    try {
        return await withTimeout(readPhotograph(db, id, started), openTimeoutMs, 'IndexedDB photograph read');
    } catch (error) {
        emitTimedOut('readonly', photographStoreName, started, error);
        throw error;
    } finally {
        closeDatabase(db);
        emit('OfflineDbClosed', {
            operation: 'read',
            elapsedMilliseconds: elapsedSince(started)
        });
    }
}

function readPhotograph(db, id, started) {
    return executeTransaction(db, {
        storeName: photographStoreName,
        mode: 'readonly',
        closeWhenDone: false,
        onStarted: () => emit('OfflineDbTransactionStarted', {
            storeName: photographStoreName,
            operation: 'read',
            elapsedMilliseconds: elapsedSince(started)
        }),
        onCompleted: () => emit('OfflineDbTransactionCompleted', {
            storeName: photographStoreName,
            operation: 'read',
            elapsedMilliseconds: elapsedSince(started)
        }),
        onAborted: (error) => emit('OfflineDbTransactionAborted', {
            storeName: photographStoreName,
            operation: 'read',
            elapsedMilliseconds: elapsedSince(started),
            errorType: error?.name
        }),
        onError: (error) => emit('OfflineDbTransactionError', {
            storeName: photographStoreName,
            operation: 'read',
            elapsedMilliseconds: elapsedSince(started),
            errorType: error?.name
        }),
        execute: (store, succeed, fail) => {
            const request = store.get(id);
            request.onsuccess = () => {
                emit('OfflineDbRequestSucceeded', {
                    storeName: photographStoreName,
                    operation: 'read',
                    elapsedMilliseconds: elapsedSince(started)
                });
                const item = photographFromRecord(request.result);
                succeed(item);
            };
            request.onerror = () => fail(request.error);
        }
    }).then((record) => completePhotograph(record));
}

function photographFromRecord(record) {
    if (!record) {
        return null;
    }

    if (record.bytes != null) {
        return {
            contentType: record.contentType,
            bytesBase64: uint8ToBase64(toUint8Array(record.bytes))
        };
    }

    return record;
}

function completePhotograph(record) {
    if (!record) {
        return null;
    }

    if (record.bytesBase64) {
        return { contentType: record.contentType, bytesBase64: record.bytesBase64 };
    }

    if (!record.blob) {
        return null;
    }

    return record.blob.arrayBuffer().then((buffer) => ({
        contentType: record.contentType,
        bytesBase64: uint8ToBase64(new Uint8Array(buffer))
    }));
}

function toUint8Array(bytes) {
    if (bytes instanceof Uint8Array) {
        return bytes;
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
