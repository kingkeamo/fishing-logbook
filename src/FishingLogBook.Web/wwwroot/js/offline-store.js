const databaseName = 'FishingLogBook';
const storeName = 'testCatches';
const photographStoreName = 'testCatchPhotographs';
const version = 2;

function openDatabase() {
    return new Promise((resolve, reject) => {
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

export async function putTestCatchPhotograph(id, bytes, contentType) {
    const db = await openDatabase();
    const blob = new Blob([toUint8Array(bytes)], { type: contentType });
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(photographStoreName, 'readwrite');
        transaction.oncomplete = () => {
            db.close();
            resolve();
        };
        transaction.onerror = () => {
            db.close();
            reject(transaction.error);
        };
        transaction.objectStore(photographStoreName).put({ id, blob, contentType });
    });
}

export async function getTestCatchPhotograph(id) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(photographStoreName, 'readonly');
        const request = transaction.objectStore(photographStoreName).get(id);
        request.onsuccess = async () => {
            db.close();
            const item = request.result;
            if (!item) {
                resolve(null);
                return;
            }
            const buffer = await item.blob.arrayBuffer();
            resolve({ contentType: item.contentType, bytesBase64: uint8ToBase64(new Uint8Array(buffer)) });
        };
        request.onerror = () => {
            db.close();
            reject(request.error);
        };
    });
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
