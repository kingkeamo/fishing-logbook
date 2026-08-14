const databaseName = 'FishingLogBook';
const storeName = 'testCatches';
const photographStoreName = 'testCatchPhotographs';
const version = 2;
const openTimeoutMs = 8000;

function emit(eventName, details) {
    const elapsedMilliseconds = details.elapsedMilliseconds;
    try {
        console.debug(`[FLB] ${eventName}`, { elapsedMilliseconds, ...details });
    } catch {
        // Console must never break IndexedDB.
    }
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

function openDatabase() {
    const started = performance.now();
    emit('OfflineDbOpenStarted', { storeName, elapsedMilliseconds: 0 });
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
            db.onversionchange = () => db.close();
            db.onclose = () => { };
            emit('OfflineDbOpenCompleted', { elapsedMilliseconds: Math.round(performance.now() - started) });
            resolve(db);
        };
        request.onerror = () => {
            emit('OfflineDbOpenFailed', { elapsedMilliseconds: Math.round(performance.now() - started), errorType: request.error?.name });
            reject(request.error);
        };
    }), openTimeoutMs, 'IndexedDB open').catch((error) => {
        const elapsedMilliseconds = Math.round(performance.now() - started);
        if (String(error?.message || '').includes('timed out')) {
            emit('OfflineDbOpenTimedOut', { elapsedMilliseconds });
        }
        throw error;
    });
}

function runWrite(objectStoreName, mutate) {
    const started = performance.now();
    return openDatabase().then((db) => withTimeout(new Promise((resolve, reject) => {
        const transaction = db.transaction(objectStoreName, 'readwrite');
        transaction.oncomplete = () => {
            emit('OfflineDbWriteCompleted', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started) });
            db.close();
            resolve();
        };
        transaction.onabort = () => {
            emit('OfflineDbTransactionAborted', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started), errorType: transaction.error?.name });
            db.close();
            reject(transaction.error || new Error('IndexedDB transaction aborted'));
        };
        transaction.onerror = () => {
            emit('OfflineDbWriteFailed', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started), errorType: transaction.error?.name });
            db.close();
            reject(transaction.error);
        };
        mutate(transaction.objectStore(objectStoreName));
    }), openTimeoutMs, 'IndexedDB write').catch((error) => {
        try { db.close(); } catch { /* already closed */ }
        throw error;
    }));
}

function runRead(objectStoreName, read) {
    const started = performance.now();
    return openDatabase().then((db) => withTimeout(new Promise((resolve, reject) => {
        const transaction = db.transaction(objectStoreName, 'readonly');
        const request = read(transaction.objectStore(objectStoreName));
        request.onsuccess = async () => {
            try {
                const value = await Promise.resolve(request.result);
                emit('OfflineDbReadCompleted', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started) });
                db.close();
                resolve(value);
            } catch (error) {
                emit('OfflineDbReadFailed', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started), errorType: error?.name });
                db.close();
                reject(error);
            }
        };
        request.onerror = () => {
            emit('OfflineDbReadFailed', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started), errorType: request.error?.name });
            db.close();
            reject(request.error);
        };
        transaction.onabort = () => {
            emit('OfflineDbTransactionAborted', { storeName: objectStoreName, elapsedMilliseconds: Math.round(performance.now() - started), errorType: transaction.error?.name });
            db.close();
            reject(transaction.error || new Error('IndexedDB transaction aborted'));
        };
    }), openTimeoutMs, 'IndexedDB read').catch((error) => {
        try { db.close(); } catch { /* already closed */ }
        throw error;
    }));
}

export async function putTestCatch(json) {
    const catchRecord = JSON.parse(json);
    await runWrite(storeName, (store) => store.put(catchRecord));
}

export async function getAllTestCatches() {
    const items = await runRead(storeName, (store) => store.getAll());
    return (items || []).map((item) => JSON.stringify(item));
}

export async function putTestCatchPhotograph(id, bytes, contentType) {
    const blob = new Blob([toUint8Array(bytes)], { type: contentType });
    await runWrite(photographStoreName, (store) => store.put({ id, blob, contentType }));
}

export async function getTestCatchPhotograph(id) {
    const item = await runRead(photographStoreName, (store) => store.get(id));
    if (!item) {
        return null;
    }

    const buffer = await item.blob.arrayBuffer();
    return { contentType: item.contentType, bytesBase64: uint8ToBase64(new Uint8Array(buffer)) };
}

function toUint8Array(bytes) {
    if (bytes instanceof Uint8Array) {
        return bytes;
    }

    return new Uint8Array(bytes);
}

function uint8ToBase64(bytes) {
    let binary = '';
    const chunkSize = 0x8000;
    for (let offset = 0; offset < bytes.length; offset += chunkSize) {
        binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
    }

    return btoa(binary);
}
