const databaseName = 'FishingLogBookDiagnostics';
const storeName = 'diagnosticEvents';
const version = 1;
const openTimeoutMs = 8000;

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
    return withTimeout(new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, version);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(storeName)) {
                const store = db.createObjectStore(storeName, { keyPath: 'id' });
                store.createIndex('timestampUtc', 'timestampUtc');
            }
        };
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => db.close();
            db.onclose = () => { };
            resolve(db);
        };
        request.onerror = () => reject(request.error);
    }), openTimeoutMs, 'diagnostic open');
}

export async function putDiagnosticEvent(json, maxQueueSize) {
    const record = JSON.parse(json);
    const db = await openDatabase();
    try {
        await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onabort = () => reject(transaction.error || new Error('diagnostic transaction aborted'));
            transaction.onerror = () => reject(transaction.error);
            const store = transaction.objectStore(storeName);
            store.put(record);
            const countRequest = store.count();
            countRequest.onsuccess = () => {
                const overflow = (countRequest.result || 0) - maxQueueSize;
                if (overflow <= 0) {
                    return;
                }

                const index = store.index('timestampUtc');
                const cursorRequest = index.openCursor();
                let remaining = overflow;
                cursorRequest.onsuccess = () => {
                    const cursor = cursorRequest.result;
                    if (!cursor || remaining <= 0) {
                        return;
                    }

                    cursor.delete();
                    remaining -= 1;
                    cursor.continue();
                };
            };
        }), openTimeoutMs, 'diagnostic write');
    } finally {
        db.close();
    }
}

export async function getPendingDiagnosticEvents(maxCount) {
    const db = await openDatabase();
    try {
        return await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const results = [];
            transaction.oncomplete = () => resolve(results.map((item) => JSON.stringify(item)));
            transaction.onabort = () => reject(transaction.error || new Error('diagnostic transaction aborted'));
            transaction.onerror = () => reject(transaction.error);
            const index = transaction.objectStore(storeName).index('timestampUtc');
            const request = index.openCursor();
            request.onsuccess = () => {
                const cursor = request.result;
                if (!cursor || results.length >= maxCount) {
                    return;
                }

                results.push(cursor.value);
                cursor.continue();
            };
        }), openTimeoutMs, 'diagnostic read');
    } finally {
        db.close();
    }
}

export async function deleteDiagnosticEvents(idsJson) {
    const keys = JSON.parse(idsJson || '[]').map((id) => String(id));
    const db = await openDatabase();
    try {
        await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onabort = () => reject(transaction.error || new Error('diagnostic transaction aborted'));
            transaction.onerror = () => reject(transaction.error);
            const store = transaction.objectStore(storeName);
            for (const key of keys) {
                store.delete(key);
            }
        }), openTimeoutMs, 'diagnostic delete');
    } finally {
        db.close();
    }
}

export async function getDiagnosticQueueCount() {
    const db = await openDatabase();
    try {
        return await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const request = transaction.objectStore(storeName).count();
            request.onsuccess = () => resolve(request.result || 0);
            request.onerror = () => reject(request.error);
            transaction.onabort = () => reject(transaction.error || new Error('diagnostic transaction aborted'));
        }), openTimeoutMs, 'diagnostic count');
    } finally {
        db.close();
    }
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
