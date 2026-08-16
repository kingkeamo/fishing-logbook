const version = 1;

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

function openDatabase(databaseName, storeName, timeoutMs) {
    return withTimeout(new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, version);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(storeName)) {
                db.createObjectStore(storeName, { keyPath: 'id' });
            }
        };
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => db.close();
            db.onclose = () => { };
            resolve(db);
        };
        request.onerror = () => reject(request.error);
    }), timeoutMs, 'probe open');
}

export async function openProbeDatabase(databaseName, storeName, timeoutMs) {
    const db = await openDatabase(databaseName, storeName, timeoutMs);
    db.close();
}

export async function writeProbeRecord(databaseName, storeName, timeoutMs) {
    const db = await openDatabase(databaseName, storeName, timeoutMs);
    try {
        await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            transaction.oncomplete = () => resolve();
            transaction.onabort = () => reject(transaction.error || new Error('probe transaction aborted'));
            transaction.onerror = () => reject(transaction.error);
            transaction.objectStore(storeName).put({
                id: 'probe',
                timestampUtc: new Date().toISOString()
            });
        }), timeoutMs, 'probe write');
    } finally {
        db.close();
    }
}

export async function countProbeRecords(databaseName, storeName, timeoutMs) {
    const db = await openDatabase(databaseName, storeName, timeoutMs);
    try {
        return await withTimeout(new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const request = transaction.objectStore(storeName).count();
            request.onsuccess = () => resolve(request.result || 0);
            request.onerror = () => reject(request.error);
            transaction.onabort = () => reject(transaction.error || new Error('probe transaction aborted'));
        }), timeoutMs, 'probe count');
    } finally {
        db.close();
    }
}
