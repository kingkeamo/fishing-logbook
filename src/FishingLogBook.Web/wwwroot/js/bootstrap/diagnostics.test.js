import { describe, expect, it, vi } from 'vitest';
import { createDiagnosticsApi, inspectOfflineStartup, installDiagnostics } from './diagnostics.js';

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
                        waiting: null
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
});
