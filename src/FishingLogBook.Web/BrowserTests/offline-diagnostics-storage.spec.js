import { expect, test } from '@playwright/test';

const databaseName = 'FishingLogBookOfflineAccess';
const storeName = 'deviceEntitlements';
const diagnosticsModule = '/src/FishingLogBook.Web/wwwroot/js/bootstrap/diagnostics.js';

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await deleteDatabase(page);
});

test('diagnostics does not create an absent entitlement database', async ({ page }) => {
    const result = await page.evaluate(async ({ databaseName, diagnosticsModule }) => {
        const { inspectOfflineStartup } = await import(diagnosticsModule);
        const databaseFactory = {
            databases: async () => [{ name: databaseName }],
            open: indexedDB.open.bind(indexedDB)
        };
        await inspectOfflineStartup({ document, location, navigator, caches, indexedDB: databaseFactory });
        return (await indexedDB.databases()).some(database => database.name === databaseName);
    }, { databaseName, diagnosticsModule });

    expect(result).toBe(false);
});

test('diagnostics does not add a store to an existing entitlement database', async ({ page }) => {
    await createDatabase(page);
    const before = await inspectDatabase(page);

    await runDiagnostics(page);

    const after = await inspectDatabase(page);
    expect(after).toEqual(before);
    expect(after.storeNames).not.toContain(storeName);
});

test('diagnostics leaves existing entitlement records unchanged', async ({ page }) => {
    await createDatabase(page, true);
    await writeRecord(page, {
        ownerKey: 'owner-1',
        state: 'ready',
        ciphertext: [1, 2, 3, 4]
    });
    const before = await readRecords(page);

    await runDiagnostics(page);

    const after = await readRecords(page);
    expect(after).toEqual(before);
});

test('failed diagnostics inspection leaves entitlement storage unchanged', async ({ page }) => {
    await createDatabase(page, true);
    await writeRecord(page, {
        ownerKey: 'owner-1',
        state: 'ready',
        ciphertext: [9, 8, 7]
    });
    const before = await readRecords(page);

    const result = await page.evaluate(async ({ diagnosticsModule }) => {
        const { inspectOfflineStartup } = await import(diagnosticsModule);
        const databaseFactory = {
            databases: indexedDB.databases.bind(indexedDB),
            open: () => { throw new DOMException('Inspection blocked', 'UnknownError'); }
        };
        return inspectOfflineStartup({ document, location, navigator, caches, indexedDB: databaseFactory });
    }, { diagnosticsModule });

    const after = await readRecords(page);
    expect(result.entitlementDatabaseState).toBe('check-failed');
    expect(result.failedStage).toBe('entitlement-database-read');
    expect(after).toEqual(before);
});

async function runDiagnostics(page) {
    await page.evaluate(async diagnosticsModule => {
        const { inspectOfflineStartup } = await import(diagnosticsModule);
        await inspectOfflineStartup(window);
    }, diagnosticsModule);
}

async function deleteDatabase(page) {
    await page.evaluate(databaseName => new Promise((resolve, reject) => {
        const request = indexedDB.deleteDatabase(databaseName);
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error('IndexedDB deletion blocked'));
    }), databaseName);
}

async function createDatabase(page, createStore = false) {
    await page.evaluate(({ createStore, databaseName, storeName }) => new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => {
            if (createStore) request.result.createObjectStore(storeName, { keyPath: 'ownerKey' });
        };
        request.onsuccess = () => {
            request.result.close();
            resolve();
        };
        request.onerror = () => reject(request.error);
    }), { createStore, databaseName, storeName });
}

async function inspectDatabase(page) {
    return page.evaluate(databaseName => new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName);
        request.onsuccess = () => {
            const database = request.result;
            resolve({ version: database.version, storeNames: Array.from(database.objectStoreNames) });
            database.close();
        };
        request.onerror = () => reject(request.error);
    }), databaseName);
}

async function writeRecord(page, record) {
    await page.evaluate(({ databaseName, record, storeName }) => new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName);
        request.onsuccess = () => {
            const database = request.result;
            const transaction = database.transaction(storeName, 'readwrite');
            transaction.objectStore(storeName).put({
                ...record,
                ciphertext: new Uint8Array(record.ciphertext)
            });
            transaction.oncomplete = () => {
                database.close();
                resolve();
            };
            transaction.onerror = () => reject(transaction.error);
        };
        request.onerror = () => reject(request.error);
    }), { databaseName, record, storeName });
}

async function readRecords(page) {
    return page.evaluate(({ databaseName, storeName }) => new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName);
        request.onsuccess = () => {
            const database = request.result;
            const transaction = database.transaction(storeName, 'readonly');
            const records = transaction.objectStore(storeName).getAll();
            records.onsuccess = () => resolve(records.result.map(record => ({
                ownerKey: record.ownerKey,
                state: record.state,
                ciphertext: Array.from(record.ciphertext)
            })));
            records.onerror = () => reject(records.error);
            transaction.oncomplete = () => database.close();
        };
        request.onerror = () => reject(request.error);
    }), { databaseName, storeName });
}
