export function captureInstallEvents(targetWindow) {
    const state = store(targetWindow);
    if (state.capturing) {
        return;
    }

    state.capturing = true;
    targetWindow.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        state.prompt = event;
        publish(targetWindow);
    });
    targetWindow.addEventListener('appinstalled', () => {
        state.installed = true;
        state.prompt = undefined;
        publish(targetWindow);
    });

    const standalone = typeof targetWindow.matchMedia === 'function'
        ? targetWindow.matchMedia('(display-mode: standalone)')
        : undefined;
    if (typeof standalone?.addEventListener === 'function') {
        standalone.addEventListener('change', () => publish(targetWindow));
    }
}

export function detectInstallState(navigatorValue, matchMediaValue, canPrompt, appInstalled = false) {
    const userAgent = navigatorValue.userAgent || '';
    const platform = navigatorValue.platform || '';
    const isIos = /iPad|iPhone|iPod/.test(userAgent) ||
        (platform === 'MacIntel' && navigatorValue.maxTouchPoints > 1);
    const isAndroid = !isIos && /Android/i.test(userAgent);
    const isDesktop = !isIos && !isAndroid && isDesktopFamily(userAgent, platform);
    const isSafari = isIos && /Safari/i.test(userAgent) &&
        !/CriOS|FxiOS|EdgiOS|OPiOS|GSA|FBAN|FBAV|Instagram/i.test(userAgent);
    const isInstalled = appInstalled || Boolean(navigatorValue.standalone) ||
        Boolean(matchMediaValue('(display-mode: standalone)').matches);
    const platformFamily = isIos ? 'iOS' : isAndroid ? 'Android' : isDesktop ? 'Desktop' : 'Other';
    return { isInstalled, canPrompt: canPrompt && !isInstalled, platformFamily, isSafari };
}

export function getInstallState() {
    captureInstallEvents(window);
    return currentState(window);
}

export function subscribeInstallState(subscriber) {
    captureInstallEvents(window);
    const state = store(window);
    state.nextToken += 1;
    state.subscribers.set(state.nextToken, subscriber);
    return state.nextToken;
}

export function unsubscribeInstallState(token) {
    store(window).subscribers.delete(token);
}

export async function promptInstall() {
    const state = store(window);
    if (!state.prompt) {
        return 'unavailable';
    }

    const prompt = state.prompt;
    state.prompt = undefined;
    await prompt.prompt();
    const choice = await prompt.userChoice;
    publish(window);
    return choice.outcome === 'accepted' ? 'accepted' : 'dismissed';
}

function store(targetWindow) {
    if (!targetWindow.fishingLogBookInstall) {
        targetWindow.fishingLogBookInstall = {
            prompt: undefined,
            installed: false,
            capturing: false,
            nextToken: 0,
            subscribers: new Map()
        };
    }

    return targetWindow.fishingLogBookInstall;
}

function currentState(targetWindow) {
    const state = store(targetWindow);
    const matchMedia = typeof targetWindow.matchMedia === 'function'
        ? targetWindow.matchMedia.bind(targetWindow)
        : () => ({ matches: false });
    return detectInstallState(targetWindow.navigator, matchMedia, Boolean(state.prompt), state.installed);
}

function isDesktopFamily(userAgent, platform) {
    return /Windows|Macintosh|Mac OS X|CrOS|X11|Linux/i.test(userAgent) ||
        /^Win|^Mac|^Linux/i.test(platform);
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
        const pending = subscriber.invokeMethodAsync('OnInstallStateChanged', current);
        if (typeof pending?.catch === 'function') {
            pending.catch(() => state.subscribers.delete(token));
        }
    } catch {
        state.subscribers.delete(token);
    }
}
