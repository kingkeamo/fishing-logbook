import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createDiagnosticsApi, inspectOfflineStartup, installDiagnostics } from './diagnostics.js';

const offlineDatabaseName = 'FishingLogBookOfflineAccess';
const offlineStoreName = 'deviceEntitlements';

beforeEach(async () => {
    await deleteOfflineDatabase();
});

function createTargetWindow({ storageThrows = false, platform = 'Win32', userAgent = 'Mozilla/5.0' } = {}) {
    const storage = {};
    return {
        localStorage: {
            getItem(key) {
                if (storageThrows) {
                    throw new Error('blocked');
                }

                return Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null;
            },
            setItem(key, value) {
                if (storageThrows) {
                    throw new Error('blocked');
                }

                storage[key] = value;
            }
        },
        navigator: {
            platform,
            userAgent
        },
        console: {
            error: vi.fn(),
            warn: vi.fn(),
            debug: vi.fn()
        }
    };
}

describe('diagnostics bootstrap', () => {
    it('stores and reads the anonymous session id', () => {
        const api = createDiagnosticsApi(createTargetWindow());

        expect(api.getSessionId()).toBeNull();
        api.setSessionId('session-1');
        expect(api.getSessionId()).toBe('session-1');
    });

    it('stores and reads the last error', () => {
        const api = createDiagnosticsApi(createTargetWindow());
        const json = JSON.stringify({ eventName: 'Crash' });

        expect(api.getLastError()).toBeNull();
        api.setLastError(json);
        expect(api.getLastError()).toBe(json);
    });

    it('returns null and ignores writes when localStorage is blocked', () => {
        const api = createDiagnosticsApi(createTargetWindow({ storageThrows: true }));

        expect(api.getSessionId()).toBeNull();
        expect(api.getLastError()).toBeNull();
        api.setSessionId('session-1');
        api.setLastError('{}');
        expect(api.getSessionId()).toBeNull();
        expect(api.getLastError()).toBeNull();
    });

    it('reports the browser platform', () => {
        const api = createDiagnosticsApi(createTargetWindow({
            platform: 'Linux',
            userAgent: 'Firefox'
        }));

        expect(api.getPlatform()).toBe('Linux Firefox');
    });

    it('writes Error and Critical lines to console.error', () => {
        const targetWindow = createTargetWindow();
        const api = createDiagnosticsApi(targetWindow);

        api.console('Error', 'OfflineFailed', 'put failed');
        api.console('Critical', 'Crash', 'unhandled');

        expect(targetWindow.console.error).toHaveBeenNthCalledWith(1, '[FLB] OfflineFailed: put failed');
        expect(targetWindow.console.error).toHaveBeenNthCalledWith(2, '[FLB] Crash: unhandled');
    });

    it('writes Warning lines to console.warn', () => {
        const targetWindow = createTargetWindow();
        const api = createDiagnosticsApi(targetWindow);

        api.console('Warning', 'SlowOpen', '8000ms');

        expect(targetWindow.console.warn).toHaveBeenCalledWith('[FLB] SlowOpen: 8000ms');
    });

    it('writes other levels to console.debug', () => {
        const targetWindow = createTargetWindow();
        const api = createDiagnosticsApi(targetWindow);

        api.console('Debug', 'Opened', 'ok');
        api.console('Information', 'Started', 'ok');

        expect(targetWindow.console.debug).toHaveBeenNthCalledWith(1, '[FLB] Opened: ok');
        expect(targetWindow.console.debug).toHaveBeenNthCalledWith(2, '[FLB] Started: ok');
    });

    it('installs the diagnostics API on the window', () => {
        const targetWindow = createTargetWindow();

        installDiagnostics(targetWindow);
        targetWindow.fishingLogBookDiagnostics.setSessionId('session-2');

        expect(targetWindow.fishingLogBookDiagnostics.getSessionId()).toBe('session-2');
    });

    it('reports the controlling worker and exact cached offline module', async () => {
        const response = {
            headers: { get: vi.fn().mockReturnValue('application/javascript') },
            status: 200,
            redirected: false
        };
        const targetWindow = {
            document: { baseURI: 'https://dev.test/' },
            location: { href: 'https://dev.test/' },
            navigator: {
                serviceWorker: {
                    controller: { scriptURL: 'https://dev.test/service-worker.js' },
                    getRegistration: vi.fn().mockResolvedValue({
                        active: { state: 'activated', scriptURL: 'https://dev.test/service-worker.js' },
                        waiting: null,
                        installing: { state: 'installing', scriptURL: 'https://dev.test/service-worker-next.js' }
                    })
                }
            },
            caches: {
                keys: vi.fn().mockResolvedValue(['offline-cache-v1']),
                open: vi.fn().mockResolvedValue({ match: vi.fn().mockResolvedValue(response) })
            },
            indexedDB: { databases: vi.fn().mockResolvedValue([]) }
        };

        const result = await inspectOfflineStartup(targetWindow);

        expect(result.resolvedModuleUrl).toBe('https://dev.test/js/browser/offline-access.js');
        expect(result.controllerPresent).toBe(true);
        expect(result.activeWorkerState).toBe('activated');
        expect(result.installingWorkerState).toBe('installing');
        expect(result.installingWorkerScriptUrl).toBe('https://dev.test/service-worker-next.js');
        expect(result.moduleCached).toBe(true);
        expect(result.matchingCacheName).toBe('offline-cache-v1');
        expect(result.moduleContentType).toBe('application/javascript');
        expect(result.entitlementDatabaseState).toBe('not-found');
    });

    it('reports a cache inspection failure without throwing', async () => {
        const targetWindow = {
            document: { baseURI: 'https://dev.test/' },
            location: { href: 'https://dev.test/' },
            navigator: {},
            caches: { keys: vi.fn().mockRejectedValue(new TypeError('cache unavailable')) },
            indexedDB: { databases: vi.fn().mockResolvedValue([]) }
        };

        const result = await inspectOfflineStartup(targetWindow);

        expect(result.failedStage).toBe('cache-storage-read');
        expect(result.errorType).toBe('TypeError');
        expect(result.errorMessage).toBe('cache unavailable');
    });

    it('does not create the entitlement database when a stale database listing reports it present', async () => {
        const targetWindow = diagnosticsTarget({
            databases: vi.fn().mockResolvedValue([{ name: offlineDatabaseName }]),
            open: indexedDB.open.bind(indexedDB)
        });

        await inspectOfflineStartup(targetWindow);

        expect(await offlineDatabaseExists()).toBe(false);
    });

    it('does not add the entitlement store to an existing database', async () => {
        await createOfflineDatabase();
        const before = await inspectOfflineDatabase();

        await inspectOfflineStartup(diagnosticsTarget(indexedDB));

        const after = await inspectOfflineDatabase();
        expect(after).toEqual(before);
        expect(after.storeNames).not.toContain(offlineStoreName);
    });

    it('leaves existing entitlement records unchanged', async () => {
        await createOfflineDatabase(database => database.createObjectStore(offlineStoreName, { keyPath: 'ownerKey' }));
        await writeEntitlement({
            ownerKey: 'owner-1',
            state: 'ready',
            ciphertext: new Uint8Array([1, 2, 3, 4])
        });
        const before = await readEntitlements();

        await inspectOfflineStartup(diagnosticsTarget(indexedDB));

        const after = await readEntitlements();
        expect(normaliseRecords(after)).toEqual(normaliseRecords(before));
    });

    it('leaves existing storage unchanged when entitlement inspection fails', async () => {
        await createOfflineDatabase(database => database.createObjectStore(offlineStoreName, { keyPath: 'ownerKey' }));
        await writeEntitlement({ ownerKey: 'owner-1', state: 'ready', ciphertext: new Uint8Array([9, 8, 7]) });
        const before = await readEntitlements();
        const targetWindow = diagnosticsTarget({
            databases: indexedDB.databases.bind(indexedDB),
            open: vi.fn(() => { throw new DOMException('Inspection blocked', 'UnknownError'); })
        });

        const result = await inspectOfflineStartup(targetWindow);

        const after = await readEntitlements();
        expect(result.entitlementDatabaseState).toBe('check-failed');
        expect(result.failedStage).toBe('entitlement-database-read');
        expect(normaliseRecords(after)).toEqual(normaliseRecords(before));
    });
});

function diagnosticsTarget(databaseFactory) {
    return {
        document: { baseURI: 'https://dev.test/' },
        location: { href: 'https://dev.test/', origin: 'https://dev.test', pathname: '/' },
        navigator: {},
        caches: { keys: vi.fn().mockResolvedValue([]) },
        indexedDB: databaseFactory
    };
}

function deleteOfflineDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.deleteDatabase(offlineDatabaseName);
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error('IndexedDB deletion blocked'));
    });
}

function createOfflineDatabase(upgrade) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(offlineDatabaseName, 1);
        request.onupgradeneeded = () => upgrade?.(request.result);
        request.onsuccess = () => {
            request.result.close();
            resolve();
        };
        request.onerror = () => reject(request.error);
    });
}

async function offlineDatabaseExists() {
    const databases = await indexedDB.databases();
    return databases.some(database => database.name === offlineDatabaseName);
}

function inspectOfflineDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(offlineDatabaseName);
        request.onsuccess = () => {
            const database = request.result;
            resolve({ version: database.version, storeNames: Array.from(database.objectStoreNames) });
            database.close();
        };
        request.onerror = () => reject(request.error);
    });
}

function writeEntitlement(record) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(offlineDatabaseName);
        request.onsuccess = () => {
            const database = request.result;
            const transaction = database.transaction(offlineStoreName, 'readwrite');
            transaction.objectStore(offlineStoreName).put(record);
            transaction.oncomplete = () => {
                database.close();
                resolve();
            };
            transaction.onerror = () => reject(transaction.error);
        };
        request.onerror = () => reject(request.error);
    });
}

function readEntitlements() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(offlineDatabaseName);
        request.onsuccess = () => {
            const database = request.result;
            const transaction = database.transaction(offlineStoreName, 'readonly');
            const records = transaction.objectStore(offlineStoreName).getAll();
            records.onsuccess = () => resolve(records.result);
            records.onerror = () => reject(records.error);
            transaction.oncomplete = () => database.close();
        };
        request.onerror = () => reject(request.error);
    });
}

function normaliseRecords(records) {
    return records.map(record => ({
        ownerKey: record.ownerKey,
        state: record.state,
        ciphertext: Array.from(record.ciphertext)
    }));
}
