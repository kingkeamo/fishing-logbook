import { describe, expect, it, vi } from 'vitest';
import { createDiagnosticsApi, installDiagnostics } from './diagnostics.js';

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
});
