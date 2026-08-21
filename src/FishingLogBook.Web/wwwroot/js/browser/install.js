let deferredInstallPrompt;
let appInstalled = false;

window.addEventListener('beforeinstallprompt', event => {
    event.preventDefault();
    deferredInstallPrompt = event;
});

window.addEventListener('appinstalled', () => {
    appInstalled = true;
    deferredInstallPrompt = undefined;
});

export function detectInstallState(navigatorValue, matchMediaValue, canPrompt) {
    const userAgent = navigatorValue.userAgent || '';
    const platform = navigatorValue.platform || '';
    const isIos = /iPad|iPhone|iPod/.test(userAgent) ||
        (platform === 'MacIntel' && navigatorValue.maxTouchPoints > 1);
    const isAndroid = /Android/i.test(userAgent);
    const isWindows = /Windows/i.test(userAgent) || /^Win/i.test(platform);
    const isSafari = isIos && /Safari/i.test(userAgent) &&
        !/CriOS|FxiOS|EdgiOS|OPiOS/i.test(userAgent);
    const isInstalled = appInstalled || Boolean(navigatorValue.standalone) ||
        Boolean(matchMediaValue('(display-mode: standalone)').matches);
    const platformFamily = isIos ? 'iOS' : isAndroid ? 'Android' : isWindows ? 'Windows' : 'Other';
    return { isInstalled, canPrompt: canPrompt && !isInstalled, platformFamily, isSafari };
}

export function getInstallState() {
    const matchMedia = typeof window.matchMedia === 'function'
        ? window.matchMedia.bind(window)
        : () => ({ matches: false });
    return detectInstallState(navigator, matchMedia, Boolean(deferredInstallPrompt));
}

export async function promptInstall() {
    if (!deferredInstallPrompt) {
        return 'unavailable';
    }

    const prompt = deferredInstallPrompt;
    deferredInstallPrompt = undefined;
    await prompt.prompt();
    const choice = await prompt.userChoice;
    return choice.outcome === 'accepted' ? 'accepted' : 'dismissed';
}
