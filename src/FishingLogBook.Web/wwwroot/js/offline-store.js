const databaseName = 'FishingLogBook';
const storeName = 'testCatches';
const version = 1;

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, version);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(storeName)) {
                db.createObjectStore(storeName, { keyPath: 'id' });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function putTestCatch(json) {
    const catchRecord = JSON.parse(json);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readwrite');
        transaction.oncomplete = () => {
            db.close();
            resolve();
        };
        transaction.onerror = () => {
            db.close();
            reject(transaction.error);
        };
        transaction.objectStore(storeName).put(catchRecord);
    });
}

export async function getAllTestCatches() {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readonly');
        const request = transaction.objectStore(storeName).getAll();
        request.onsuccess = () => {
            db.close();
            resolve(request.result.map((item) => JSON.stringify(item)));
        };
        request.onerror = () => {
            db.close();
            reject(request.error);
        };
    });
}
