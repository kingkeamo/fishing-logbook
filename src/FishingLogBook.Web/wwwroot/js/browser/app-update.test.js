import { beforeEach, describe, expect, it, vi } from 'vitest';

function createWorker() {
    return { postMessage: vi.fn(), state: 'installed', addEventListener: vi.fn() };
}

function createRegistration({ waiting, installing } = {}) {
    const listeners = {};
    return {
        waiting,
        installing,
        update: vi.fn(async () => undefined),
        unregister: vi.fn(async () => true),
        addEventListener: (type, listener) => { listeners[type] = listener; },
        raise: (type) => listeners[type]?.()
    };
}

function createWindow({ controller = {}, onLine = true } = {}) {
    const controllerListeners = [];
    const documentListeners = {};
    return {
        navigator: {
            onLine,
            serviceWorker: {
                controller,
                addEventListener: (type, listener) => {
                    if (type === 'controllerchange') {
                        controllerListeners.push(listener);
                    }
                }
            }
        },
        document: {
            visibilityState: 'visible',
            addEventListener: (type, listener) => { documentListeners[type] = listener; }
        },
        location: { reload: vi.fn() },
        caches: { delete: vi.fn(), keys: vi.fn() },
        indexedDB: { deleteDatabase: vi.fn() },
        localStorage: { clear: vi.fn(), removeItem: vi.fn() },
        sessionStorage: { clear: vi.fn() },
        raiseControllerChange: () => controllerListeners.forEach(listener => listener()),
        raiseVisible: () => documentListeners.visibilitychange?.()
    };
}

let update;

beforeEach(async () => {
    vi.resetModules();
    update = await import('./app-update.js');
});

describe('app update detection', () => {
    it('reports no update when the service worker is not registered', () => {
        const targetWindow = createWindow();

        update.trackAppUpdates(targetWindow, undefined);

        expect(update.getUpdateState()).toEqual({ isUpdateReady: false });
    });

    it('reports no update when nothing is waiting', () => {
        const targetWindow = createWindow();

        update.trackAppUpdates(targetWindow, createRegistration());

        expect(getState(targetWindow)).toEqual({ isUpdateReady: false });
    });

    it('does not fabricate an update when the update check fails offline', async () => {
        const targetWindow = createWindow({ onLine: false });
        const registration = createRegistration();

        update.trackAppUpdates(targetWindow, registration);
        await update.checkForUpdate(targetWindow);

        expect(registration.update).not.toHaveBeenCalled();
        expect(getState(targetWindow)).toEqual({ isUpdateReady: false });
    });

    it('does not fabricate an update when the network rejects the update check', async () => {
        const targetWindow = createWindow();
        const registration = createRegistration();
        registration.update = vi.fn(async () => { throw new TypeError('Failed to fetch'); });

        update.trackAppUpdates(targetWindow, registration);
        await update.checkForUpdate(targetWindow);

        expect(getState(targetWindow)).toEqual({ isUpdateReady: false });
    });

    it('does not treat a first installation without a controller as an update', () => {
        const targetWindow = createWindow({ controller: null });

        update.trackAppUpdates(targetWindow, createRegistration({ waiting: createWorker() }));

        expect(getState(targetWindow)).toEqual({ isUpdateReady: false });
    });

    it('reports a worker that was already waiting when the app started', () => {
        const targetWindow = createWindow();

        update.trackAppUpdates(targetWindow, createRegistration({ waiting: createWorker() }));

        expect(getState(targetWindow)).toEqual({ isUpdateReady: true });
    });

    it('reports an update that finishes installing after startup', () => {
        const targetWindow = createWindow();
        const installing = createWorker();
        installing.state = 'installing';
        const registration = createRegistration({ installing });

        update.trackAppUpdates(targetWindow, registration);
        installing.state = 'installed';
        raiseStateChange(installing);

        expect(getState(targetWindow)).toEqual({ isUpdateReady: true });
    });

    it('checks for a new version when the app becomes visible again', () => {
        const targetWindow = createWindow();
        const registration = createRegistration();

        update.trackAppUpdates(targetWindow, registration);
        registration.update.mockClear();
        targetWindow.raiseVisible();

        expect(registration.update).toHaveBeenCalledOnce();
    });
});

describe('app update activation', () => {
    it('does nothing when no update is waiting', async () => {
        const targetWindow = createWindow();
        update.trackAppUpdates(targetWindow, createRegistration());

        await expect(update.applyUpdate()).resolves.toBe(false);

        expect(targetWindow.location.reload).not.toHaveBeenCalled();
    });

    it('asks the waiting worker to take over', async () => {
        const targetWindow = createWindow();
        const waiting = createWorker();
        update.trackAppUpdates(targetWindow, createRegistration({ waiting }));

        await expect(update.applyUpdate()).resolves.toBe(true);

        expect(waiting.postMessage).toHaveBeenCalledOnce();
        expect(waiting.postMessage).toHaveBeenCalledWith({ type: 'SkipWaiting' });
        expect(targetWindow.location.reload).not.toHaveBeenCalled();
    });

    it('reloads once when the new worker takes control', async () => {
        const targetWindow = createWindow();
        update.trackAppUpdates(targetWindow, createRegistration({ waiting: createWorker() }));
        await update.applyUpdate();

        targetWindow.raiseControllerChange();
        targetWindow.raiseControllerChange();
        targetWindow.raiseControllerChange();

        expect(targetWindow.location.reload).toHaveBeenCalledOnce();
    });

    it('does not reload on a controller change the app did not request', () => {
        const targetWindow = createWindow();
        update.trackAppUpdates(targetWindow, createRegistration({ waiting: createWorker() }));

        targetWindow.raiseControllerChange();

        expect(targetWindow.location.reload).not.toHaveBeenCalled();
    });

    it('ignores repeated activation requests', async () => {
        const targetWindow = createWindow();
        const waiting = createWorker();
        update.trackAppUpdates(targetWindow, createRegistration({ waiting }));

        await update.applyUpdate();
        await expect(update.applyUpdate()).resolves.toBe(false);
        await expect(update.applyUpdate()).resolves.toBe(false);

        expect(waiting.postMessage).toHaveBeenCalledOnce();
    });

    it('reports failure when the waiting worker cannot be reached', async () => {
        const targetWindow = createWindow();
        const waiting = createWorker();
        waiting.postMessage = vi.fn(() => { throw new Error('worker is gone'); });
        update.trackAppUpdates(targetWindow, createRegistration({ waiting }));

        await expect(update.applyUpdate()).resolves.toBe(false);

        expect(targetWindow.location.reload).not.toHaveBeenCalled();
    });

    it('never deletes application data as part of updating', async () => {
        const targetWindow = createWindow();
        update.trackAppUpdates(targetWindow, createRegistration({ waiting: createWorker() }));

        await update.applyUpdate();
        targetWindow.raiseControllerChange();

        expect(targetWindow.caches.delete).not.toHaveBeenCalled();
        expect(targetWindow.caches.keys).not.toHaveBeenCalled();
        expect(targetWindow.indexedDB.deleteDatabase).not.toHaveBeenCalled();
        expect(targetWindow.localStorage.clear).not.toHaveBeenCalled();
        expect(targetWindow.localStorage.removeItem).not.toHaveBeenCalled();
        expect(targetWindow.sessionStorage.clear).not.toHaveBeenCalled();
    });

    it('never unregisters the service worker as part of updating', async () => {
        const targetWindow = createWindow();
        const registration = createRegistration({ waiting: createWorker() });
        update.trackAppUpdates(targetWindow, registration);

        await update.applyUpdate();
        targetWindow.raiseControllerChange();

        expect(registration.unregister).not.toHaveBeenCalled();
    });
});

describe('app update subscriptions', () => {
    it('publishes when an update becomes ready after the first state read', () => {
        const targetWindow = createWindow();
        const installing = createWorker();
        installing.state = 'installing';
        const registration = createRegistration({ installing });
        update.trackAppUpdates(targetWindow, registration);
        const subscriber = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const token = update.subscribeUpdateState(subscriber);

        installing.state = 'installed';
        raiseStateChange(installing);

        expect(subscriber.invokeMethodAsync).toHaveBeenCalledOnce();
        expect(subscriber.invokeMethodAsync).toHaveBeenCalledWith(
            'OnUpdateStateChanged',
            { isUpdateReady: true });
        update.unsubscribeUpdateState(token);
    });

    it('publishes the same update only once', () => {
        const targetWindow = createWindow();
        const waiting = createWorker();
        const registration = createRegistration({ installing: waiting });
        update.trackAppUpdates(targetWindow, registration);
        const subscriber = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const token = update.subscribeUpdateState(subscriber);

        raiseStateChange(waiting);
        raiseStateChange(waiting);
        registration.raise('updatefound');
        raiseStateChange(waiting);

        expect(subscriber.invokeMethodAsync).toHaveBeenCalledOnce();
        update.unsubscribeUpdateState(token);
    });

    it('stops publishing to a subscriber that has been removed', () => {
        const targetWindow = createWindow();
        const installing = createWorker();
        installing.state = 'installing';
        update.trackAppUpdates(targetWindow, createRegistration({ installing }));
        const subscriber = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const token = update.subscribeUpdateState(subscriber);
        update.unsubscribeUpdateState(token);

        installing.state = 'installed';
        raiseStateChange(installing);

        expect(subscriber.invokeMethodAsync).not.toHaveBeenCalled();
    });

    it('drops a subscriber whose callback can no longer be reached', async () => {
        const targetWindow = createWindow();
        const installing = createWorker();
        installing.state = 'installing';
        update.trackAppUpdates(targetWindow, createRegistration({ installing }));
        const subscriber = {
            invokeMethodAsync: vi.fn(() => Promise.reject(new Error('disposed')))
        };
        update.subscribeUpdateState(subscriber);

        installing.state = 'installed';
        raiseStateChange(installing);
        await Promise.resolve();

        expect(subscriber.invokeMethodAsync).toHaveBeenCalledOnce();
        expect(store(targetWindow).subscribers.size).toBe(0);
    });
});

function raiseStateChange(worker) {
    worker.addEventListener.mock.calls
        .filter(call => call[0] === 'statechange')
        .forEach(call => call[1]());
}

function store(targetWindow) {
    return targetWindow.fishingLogBookUpdate;
}

function getState(targetWindow) {
    return { isUpdateReady: Boolean(store(targetWindow).waiting) };
}
