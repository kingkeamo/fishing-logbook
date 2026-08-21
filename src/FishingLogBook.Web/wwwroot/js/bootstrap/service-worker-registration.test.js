import { describe, expect, it, vi } from 'vitest';
import {
    listenForServiceWorkerErrors,
    registerServiceWorker
} from './service-worker-registration.js';

const currentEpoch = '20260821-atomic-app-shell';

function createTargetWindow({ epoch, localStorageThrows = false, registrationThrows = false } = {}) {
    const storage = {};
    if (epoch) {
        storage['flb-sw-epoch'] = epoch;
    }

    const unregister = vi.fn(async () => true);
    const register = vi.fn(async () => {
        if (registrationThrows) {
            throw new Error('registration failed');
        }

        return {};
    });
    const cacheDelete = vi.fn(async () => true);

    return {
        localStorage: {
            getItem(key) {
                if (localStorageThrows) {
                    throw new Error('blocked');
                }

                return Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null;
            },
            setItem(key, value) {
                storage[key] = value;
            }
        },
        navigator: {
            serviceWorker: {
                getRegistrations: vi.fn(async () => [{ unregister }]),
                register,
                addEventListener: vi.fn()
            }
        },
        caches: {
            keys: vi.fn(async () => ['cache-v1']),
            delete: cacheDelete
        },
        console: {
            error: vi.fn()
        },
        unregister,
        register,
        cacheDelete
    };
}

describe('service worker registration', () => {
    it('registers the replacement without deleting the last complete cache when the epoch changes', async () => {
        const targetWindow = createTargetWindow({ epoch: 'old-epoch' });

        await registerServiceWorker(targetWindow);

        expect(targetWindow.unregister).not.toHaveBeenCalled();
        expect(targetWindow.cacheDelete).not.toHaveBeenCalled();
        expect(targetWindow.localStorage.getItem('flb-sw-epoch')).toBe(currentEpoch);
        expect(targetWindow.register).toHaveBeenCalledWith('service-worker.js', { updateViaCache: 'none' });
    });

    it('registers normally when the current epoch is already stored', async () => {
        const targetWindow = createTargetWindow({ epoch: currentEpoch });

        await registerServiceWorker(targetWindow);

        expect(targetWindow.navigator.serviceWorker.getRegistrations).not.toHaveBeenCalled();
        expect(targetWindow.register).toHaveBeenCalledWith('service-worker.js', { updateViaCache: 'none' });
    });

    it('still registers after localStorage failures', async () => {
        const targetWindow = createTargetWindow({ localStorageThrows: true });

        await registerServiceWorker(targetWindow);

        expect(targetWindow.register).toHaveBeenCalledWith('service-worker.js', { updateViaCache: 'none' });
    });

    it('does not block application startup when service worker registration fails', async () => {
        const targetWindow = createTargetWindow({
            epoch: currentEpoch,
            registrationThrows: true
        });

        await expect(registerServiceWorker(targetWindow)).resolves.toBeUndefined();

        expect(targetWindow.console.error).toHaveBeenCalledWith(
            '[FLB] ServiceWorkerRegistrationError',
            expect.any(Error));
    });

    it('logs ServiceWorkerError messages and ignores other worker messages', () => {
        const targetWindow = createTargetWindow();
        let handler;
        targetWindow.navigator.serviceWorker.addEventListener = (type, callback) => {
            expect(type).toBe('message');
            handler = callback;
        };

        listenForServiceWorkerErrors(targetWindow);
        handler({ data: { type: 'ServiceWorkerError', message: 'cache failed' } });
        handler({ data: { type: 'SomethingElse', message: 'ignored' } });
        handler({ data: null });

        expect(targetWindow.console.error).toHaveBeenCalledTimes(1);
        expect(targetWindow.console.error).toHaveBeenCalledWith('[FLB] ServiceWorkerError', 'cache failed');
    });

    it('does nothing when the browser has no service worker API', () => {
        expect(() => listenForServiceWorkerErrors({ navigator: {} })).not.toThrow();
    });
});
