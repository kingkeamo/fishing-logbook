import { getPlatform } from '../browser/platform.js';

const sessionKey = 'flb-anonymous-session-id';
const lastErrorKey = 'flb-last-error';
const offlineAccessDatabaseName = 'FishingLogBookOfflineAccess';
const offlineAccessStoreName = 'deviceEntitlements';
const offlineAccessModulePath = './js/browser/offline-access.js';

export function createDiagnosticsApi(targetWindow) {
    return {
        getSessionId: () => {
            try { return targetWindow.localStorage.getItem(sessionKey); }
            catch { return null; }
        },
        setSessionId: (value) => {
            try { targetWindow.localStorage.setItem(sessionKey, value); } catch { /* ignore */ }
        },
        getPlatform: () => getPlatform(targetWindow.navigator),
        console: (level, eventName, message) => {
            const line = `[FLB] ${eventName}: ${message}`;
            if (level === 'Error' || level === 'Critical') {
                targetWindow.console.error(line);
            } else if (level === 'Warning') {
                targetWindow.console.warn(line);
            } else {
                targetWindow.console.debug(line);
            }
        },
        setLastError: (json) => {
            try { targetWindow.localStorage.setItem(lastErrorKey, json); } catch { }
        },
        getLastError: () => {
            try { return targetWindow.localStorage.getItem(lastErrorKey); } catch { return null; }
        },
        inspectOfflineStartup: () => inspectOfflineStartup(targetWindow)
    };
}

export function installDiagnostics(targetWindow) {
    targetWindow.fishingLogBookDiagnostics = createDiagnosticsApi(targetWindow);
}

export async function inspectOfflineStartup(targetWindow) {
    const currentLocation = targetWindow.location
        ? `${targetWindow.location.origin ?? ''}${targetWindow.location.pathname ?? ''}`
        : '';
    const lastError = readLastError(targetWindow);
    const result = {
        documentBaseUri: targetWindow.document?.baseURI ?? '',
        currentUrl: currentLocation,
        resolvedModuleUrl: new URL(offlineAccessModulePath, targetWindow.document?.baseURI ?? targetWindow.location?.href).href,
        serviceWorkerSupported: Boolean(targetWindow.navigator?.serviceWorker),
        controllerPresent: Boolean(targetWindow.navigator?.serviceWorker?.controller),
        controllerScriptUrl: targetWindow.navigator?.serviceWorker?.controller?.scriptURL ?? null,
        controllerCacheName: null,
        controllerManifestVersion: null,
        activeWorkerState: null,
        activeWorkerScriptUrl: null,
        waitingWorkerState: null,
        waitingWorkerScriptUrl: null,
        cacheNames: [],
        matchingCacheName: null,
        moduleCached: false,
        moduleContentType: null,
        moduleStatus: null,
        moduleRedirected: null,
        entitlementDatabaseState: 'inspection-unavailable',
        entitlementStorePresent: null,
        entitlementRecordCount: null,
        entitlementRecordStates: [],
        lastErrorSource: lastError?.source ?? null,
        lastErrorType: lastError?.errorType ?? null,
        lastErrorMessage: lastError?.message ?? null,
        failedStage: null,
        errorType: null,
        errorMessage: null
    };

    try {
        await inspectServiceWorker(targetWindow, result);
        await inspectCaches(targetWindow, result);
        await inspectEntitlementDatabase(targetWindow, result);
    } catch (error) {
        result.failedStage ??= 'offline-diagnostics';
        result.errorType = safeErrorType(error);
        result.errorMessage = safeErrorMessage(error);
    }

    return result;
}

function readLastError(targetWindow) {
    try {
        const value = targetWindow.localStorage?.getItem(lastErrorKey);
        return value ? JSON.parse(value) : null;
    } catch {
        return null;
    }
}

async function inspectServiceWorker(targetWindow, result) {
    if (!targetWindow.navigator?.serviceWorker?.getRegistration) return;

    try {
        const registration = await targetWindow.navigator.serviceWorker.getRegistration();
        result.activeWorkerState = registration?.active?.state ?? null;
        result.activeWorkerScriptUrl = registration?.active?.scriptURL ?? null;
        result.waitingWorkerState = registration?.waiting?.state ?? null;
        result.waitingWorkerScriptUrl = registration?.waiting?.scriptURL ?? null;
        if (targetWindow.navigator.serviceWorker.controller) {
            const controllerDetails = await inspectController(targetWindow, targetWindow.navigator.serviceWorker.controller);
            result.controllerCacheName = controllerDetails?.cacheName ?? null;
            result.controllerManifestVersion = controllerDetails?.manifestVersion ?? null;
        }
    } catch (error) {
        recordFailure(result, 'service-worker-registration', error);
    }
}

function inspectController(targetWindow, controller) {
    return new Promise(resolve => {
        if (typeof targetWindow.MessageChannel !== 'function') {
            resolve(null);
            return;
        }

        const channel = new targetWindow.MessageChannel();
        const timeout = targetWindow.setTimeout(() => resolve(null), 1000);
        channel.port1.onmessage = event => {
            targetWindow.clearTimeout(timeout);
            resolve(event.data ?? null);
        };
        controller.postMessage({ type: 'InspectOfflineCache' }, [channel.port2]);
    });
}

async function inspectCaches(targetWindow, result) {
    if (!targetWindow.caches?.keys) {
        recordFailure(result, 'cache-storage-unavailable', new Error('Cache Storage unavailable'));
        return;
    }

    try {
        result.cacheNames = await targetWindow.caches.keys();
        for (const cacheName of result.cacheNames) {
            const cache = await targetWindow.caches.open(cacheName);
            const response = await cache.match(result.resolvedModuleUrl, { ignoreSearch: true });
            if (!response) continue;

            result.matchingCacheName = cacheName;
            result.moduleCached = true;
            result.moduleContentType = response.headers?.get?.('content-type') ?? null;
            result.moduleStatus = response.status ?? null;
            result.moduleRedirected = response.redirected ?? null;
            return;
        }
    } catch (error) {
        recordFailure(result, 'cache-storage-read', error);
    }
}

async function inspectEntitlementDatabase(targetWindow, result) {
    if (typeof targetWindow.indexedDB?.databases !== 'function') return;

    try {
        const databases = await targetWindow.indexedDB.databases();
        if (!databases.some(database => database.name === offlineAccessDatabaseName)) {
            result.entitlementDatabaseState = 'not-found';
            result.entitlementStorePresent = false;
            result.entitlementRecordCount = 0;
            return;
        }

        const inspection = await readExistingEntitlementDatabase(targetWindow.indexedDB);
        Object.assign(result, inspection);
    } catch (error) {
        result.entitlementDatabaseState = 'check-failed';
        recordFailure(result, 'entitlement-database-read', error);
    }
}

function readExistingEntitlementDatabase(indexedDB) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(offlineAccessDatabaseName);
        request.onupgradeneeded = event => {
            event.target.transaction.abort();
        };
        request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'));
        request.onsuccess = () => {
            const database = request.result;
            if (!database.objectStoreNames.contains(offlineAccessStoreName)) {
                database.close();
                resolve({
                    entitlementDatabaseState: 'found',
                    entitlementStorePresent: false,
                    entitlementRecordCount: 0,
                    entitlementRecordStates: []
                });
                return;
            }

            const transaction = database.transaction(offlineAccessStoreName, 'readonly');
            const recordsRequest = transaction.objectStore(offlineAccessStoreName).getAll();
            recordsRequest.onerror = () => reject(recordsRequest.error ?? new Error('IndexedDB read failed'));
            recordsRequest.onsuccess = () => {
                const records = recordsRequest.result ?? [];
                resolve({
                    entitlementDatabaseState: 'found',
                    entitlementStorePresent: true,
                    entitlementRecordCount: records.length,
                    entitlementRecordStates: records.map(record => String(record?.state ?? 'unknown'))
                });
            };
            transaction.oncomplete = () => database.close();
            transaction.onerror = () => {
                database.close();
                reject(transaction.error ?? new Error('IndexedDB transaction failed'));
            };
        };
    });
}

function recordFailure(result, stage, error) {
    if (result.failedStage) return;
    result.failedStage = stage;
    result.errorType = safeErrorType(error);
    result.errorMessage = safeErrorMessage(error);
}

function safeErrorType(error) {
    return String(error?.name ?? 'Error').slice(0, 80);
}

function safeErrorMessage(error) {
    return String(error?.message ?? 'Operation failed').slice(0, 200);
}
