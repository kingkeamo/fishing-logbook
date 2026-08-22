let trackedWindow;

export function trackAppUpdates(targetWindow, registration) {
    trackedWindow = targetWindow;
    const state = store(targetWindow);
    if (!registration) {
        return;
    }

    state.registration = registration;
    observeController(targetWindow);
    observeVisibility(targetWindow);
    adopt(targetWindow, registration.waiting);
    registration.addEventListener?.('updatefound', () => observeInstalling(targetWindow, registration));
    observeInstalling(targetWindow, registration);
    checkForUpdate(targetWindow);
}

export function getUpdateState() {
    return currentState(activeWindow());
}

export function subscribeUpdateState(subscriber) {
    const state = store(activeWindow());
    state.nextToken += 1;
    state.subscribers.set(state.nextToken, subscriber);
    return state.nextToken;
}

export function unsubscribeUpdateState(token) {
    store(activeWindow()).subscribers.delete(token);
}

export async function applyUpdate() {
    const targetWindow = activeWindow();
    const state = store(targetWindow);
    if (state.applying || !state.waiting) {
        return false;
    }

    state.applying = true;
    try {
        state.waiting.postMessage({ type: 'SkipWaiting' });
        return true;
    } catch {
        state.applying = false;
        return false;
    }
}

export async function checkForUpdate(targetWindow = activeWindow()) {
    const state = store(targetWindow);
    if (!state.registration || targetWindow.navigator?.onLine === false) {
        return;
    }

    try {
        await state.registration.update();
    } catch { /* an unreachable network must never look like an update */ }
}

function activeWindow() {
    return trackedWindow ?? window;
}

function store(targetWindow) {
    if (!targetWindow.fishingLogBookUpdate) {
        targetWindow.fishingLogBookUpdate = {
            registration: undefined,
            waiting: undefined,
            applying: false,
            reloaded: false,
            observingController: false,
            observingVisibility: false,
            nextToken: 0,
            subscribers: new Map()
        };
    }

    return targetWindow.fishingLogBookUpdate;
}

function currentState(targetWindow) {
    return { isUpdateReady: Boolean(store(targetWindow).waiting) };
}

function observeInstalling(targetWindow, registration) {
    const installing = registration.installing;
    if (!installing) {
        return;
    }

    installing.addEventListener?.('statechange', () => {
        if (installing.state === 'installed') {
            adopt(targetWindow, registration.waiting || installing);
        }
    });
}

function adopt(targetWindow, worker) {
    const state = store(targetWindow);
    if (!worker || state.waiting === worker) {
        return;
    }

    if (!targetWindow.navigator?.serviceWorker?.controller) {
        return;
    }

    state.waiting = worker;
    publish(targetWindow);
}

function observeController(targetWindow) {
    const state = store(targetWindow);
    if (state.observingController) {
        return;
    }

    state.observingController = true;
    targetWindow.navigator.serviceWorker.addEventListener?.('controllerchange', () => {
        if (!state.applying || state.reloaded) {
            return;
        }

        state.reloaded = true;
        targetWindow.location.reload();
    });
}

function observeVisibility(targetWindow) {
    const state = store(targetWindow);
    if (state.observingVisibility || !targetWindow.document?.addEventListener) {
        return;
    }

    state.observingVisibility = true;
    targetWindow.document.addEventListener('visibilitychange', () => {
        if (targetWindow.document.visibilityState === 'visible') {
            checkForUpdate(targetWindow);
        }
    });
}

function publish(targetWindow) {
    const state = store(targetWindow);
    if (state.subscribers.size === 0) {
        return;
    }

    const current = currentState(targetWindow);
    for (const [token, subscriber] of [...state.subscribers]) {
        notify(state, token, subscriber, current);
    }
}

function notify(state, token, subscriber, current) {
    try {
        const pending = subscriber.invokeMethodAsync('OnUpdateStateChanged', current);
        if (typeof pending?.catch === 'function') {
            pending.catch(() => state.subscribers.delete(token));
        }
    } catch {
        state.subscribers.delete(token);
    }
}
