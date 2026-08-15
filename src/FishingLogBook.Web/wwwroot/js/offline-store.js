const databaseName = 'FishingLogBook';
const storeName = 'testCatches';
const photographStoreName = 'testCatchPhotographs';
const version = 2;
const openTimeoutMs = 8000;

function emit(eventName, details) {
    const safe = {
        elapsedMilliseconds: details.elapsedMilliseconds,
        operation: details.operation,
        storeName: details.storeName,
        errorType: details.errorType,
        quotaBytes: details.quotaBytes,
        usageBytes: details.usageBytes
    };
    try {
        console.debug(`[FLB] ${eventName}`, safe);
        globalThis.fishingLogBookDiagnostics?.console?.('Debug', eventName, JSON.stringify(safe));
    } catch {
        // Console must never break IndexedDB.
    }
}

function elapsedSince(started) {
    return Math.round(performance.now() - started);
}

function withTimeout(promise, milliseconds, operationName) {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error(`${operationName} timed out`)), milliseconds);
        promise.then(
            (value) => {
                clearTimeout(timer);
                resolve(value);
            },
            (error) => {
                clearTimeout(timer);
                reject(error);
            });
    });
}

function closeDatabase(db, started, operationName) {
    try {
        db.close();
    } catch {
        // Already closed.
    }

    emit('OfflineDbClosed', {
        operation: operationName,
        elapsedMilliseconds: elapsedSince(started)
    });
}

function emitTimedOut(mode, objectStoreName, started, error) {
    if (!String(error?.message || '').includes('timed out')) {
        return;
    }

    const eventName = mode === 'readwrite' ? 'OfflineDbWriteTimedOut' : 'OfflineDbReadTimedOut';
    emit(eventName, {
        storeName: objectStoreName,
        operation: mode === 'readwrite' ? 'write' : 'read',
        elapsedMilliseconds: elapsedSince(started)
    });
}

async function emitStorageEstimate() {
    if (!navigator.storage || typeof navigator.storage.estimate !== 'function') {
        return;
    }

    try {
        const estimate = await navigator.storage.estimate();
        emit('OfflineDbOpenCompleted', {
            operation: 'open',
            elapsedMilliseconds: 0,
            quotaBytes: Number.isFinite(estimate.quota) ? String(Math.trunc(estimate.quota)) : undefined,
            usageBytes: Number.isFinite(estimate.usage) ? String(Math.trunc(estimate.usage)) : undefined
        });
    } catch {
        // Estimate is debug-only and must never fail open.
    }
}

function openDatabase() {
    const started = performance.now();
    emit('OfflineDbOpenStarted', { storeName, operation: 'open', elapsedMilliseconds: 0 });
    return withTimeout(new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, version);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(storeName)) {
                db.createObjectStore(storeName, { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains(photographStoreName)) {
                db.createObjectStore(photographStoreName, { keyPath: 'id' });
            }
        };
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => closeDatabase(db, started, 'versionchange');
            db.onclose = () => { };
            emit('OfflineDbOpenCompleted', {
                operation: 'open',
                elapsedMilliseconds: elapsedSince(started)
            });
            void emitStorageEstimate();
            resolve(db);
        };
        request.onerror = () => {
            emit('OfflineDbOpenFailed', {
                operation: 'open',
                elapsedMilliseconds: elapsedSince(started),
                errorType: request.error?.name
            });
            reject(request.error);
        };
    }), openTimeoutMs, 'IndexedDB open').catch((error) => {
        if (String(error?.message || '').includes('timed out')) {
            emit('OfflineDbOpenTimedOut', {
                operation: 'open',
                elapsedMilliseconds: elapsedSince(started)
            });
        }
        throw error;
    });
}

function runTransaction(objectStoreName, mode, operationName, execute) {
    const started = performance.now();
    return openDatabase().then((db) => {
        const work = new Promise((resolve, reject) => {
            const transaction = db.transaction(objectStoreName, mode);
            emit('OfflineDbTransactionStarted', {
                storeName: objectStoreName,
                operation: operationName,
                elapsedMilliseconds: elapsedSince(started)
            });

            let result;
            transaction.oncomplete = () => {
                emit('OfflineDbTransactionCompleted', {
                    storeName: objectStoreName,
                    operation: operationName,
                    elapsedMilliseconds: elapsedSince(started)
                });
                closeDatabase(db, started, operationName);
                resolve(result);
            };
            transaction.onabort = () => {
                emit('OfflineDbTransactionAborted', {
                    storeName: objectStoreName,
                    operation: operationName,
                    elapsedMilliseconds: elapsedSince(started),
                    errorType: transaction.error?.name
                });
                closeDatabase(db, started, operationName);
                reject(transaction.error || new Error('IndexedDB transaction aborted'));
            };
            transaction.onerror = () => {
                emit('OfflineDbTransactionError', {
                    storeName: objectStoreName,
                    operation: operationName,
                    elapsedMilliseconds: elapsedSince(started),
                    errorType: transaction.error?.name
                });
                closeDatabase(db, started, operationName);
                reject(transaction.error);
            };

            execute(transaction.objectStore(objectStoreName), (value) => {
                result = value;
                emit('OfflineDbRequestSucceeded', {
                    storeName: objectStoreName,
                    operation: operationName,
                    elapsedMilliseconds: elapsedSince(started)
                });
            }, (error) => {
                emit('OfflineDbTransactionError', {
                    storeName: objectStoreName,
                    operation: operationName,
                    elapsedMilliseconds: elapsedSince(started),
                    errorType: error?.name
                });
                try {
                    transaction.abort();
                } catch {
                    // Already aborted.
                }
                reject(error || new Error('IndexedDB request failed'));
            });
        });

        return withTimeout(work, openTimeoutMs, `IndexedDB ${operationName}`).catch((error) => {
            emitTimedOut(mode, objectStoreName, started, error);
            try {
                db.close();
            } catch {
                // Already closed.
            }
            throw error;
        });
    });
}

export async function putTestCatch(json) {
    const catchRecord = JSON.parse(json);
    await runTransaction(storeName, 'readwrite', 'write', (store, succeed, fail) => {
        const request = store.put(catchRecord);
        request.onsuccess = () => succeed();
        request.onerror = () => fail(request.error);
    });
}

export async function getAllTestCatches() {
    return runTransaction(storeName, 'readonly', 'read', (store, succeed, fail) => {
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

export async function putTestCatchPhotograph(id, bytes, contentType) {
    const storedBytes = toStoredBytes(bytes);
    await runTransaction(photographStoreName, 'readwrite', 'write', (store, succeed, fail) => {
        const request = store.put({ id, bytes: storedBytes, contentType });
        request.onsuccess = () => succeed();
        request.onerror = () => fail(request.error);
    });
}

export async function getTestCatchPhotograph(id) {
    const started = performance.now();
    const db = await openDatabase();
    try {
        return await withTimeout(readPhotograph(db, id, started), openTimeoutMs, 'IndexedDB photograph read');
    } catch (error) {
        emitTimedOut('readonly', photographStoreName, started, error);
        throw error;
    } finally {
        closeDatabase(db, started, 'read');
    }
}

function readPhotograph(db, id, started) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(photographStoreName, 'readonly');
        emit('OfflineDbTransactionStarted', {
            storeName: photographStoreName,
            operation: 'read',
            elapsedMilliseconds: elapsedSince(started)
        });

        let item;
        transaction.oncomplete = () => {
            emit('OfflineDbTransactionCompleted', {
                storeName: photographStoreName,
                operation: 'read',
                elapsedMilliseconds: elapsedSince(started)
            });
            resolve(item);
        };
        transaction.onabort = () => {
            emit('OfflineDbTransactionAborted', {
                storeName: photographStoreName,
                operation: 'read',
                elapsedMilliseconds: elapsedSince(started),
                errorType: transaction.error?.name
            });
            reject(transaction.error || new Error('IndexedDB transaction aborted'));
        };
        transaction.onerror = () => {
            emit('OfflineDbTransactionError', {
                storeName: photographStoreName,
                operation: 'read',
                elapsedMilliseconds: elapsedSince(started),
                errorType: transaction.error?.name
            });
            reject(transaction.error);
        };

        const request = transaction.objectStore(photographStoreName).get(id);
        request.onsuccess = () => {
            emit('OfflineDbRequestSucceeded', {
                storeName: photographStoreName,
                operation: 'read',
                elapsedMilliseconds: elapsedSince(started)
            });
            item = photographFromRecord(request.result);
        };
        request.onerror = () => reject(request.error);
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

export async function getStorageEstimate() {
    if (!navigator.storage || typeof navigator.storage.estimate !== 'function') {
        return { quota: null, usage: null };
    }

    try {
        const estimate = await navigator.storage.estimate();
        return {
            quota: Number.isFinite(estimate.quota) ? estimate.quota : null,
            usage: Number.isFinite(estimate.usage) ? estimate.usage : null
        };
    } catch {
        return { quota: null, usage: null };
    }
}
