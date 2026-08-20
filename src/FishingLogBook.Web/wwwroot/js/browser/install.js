let deferredInstallPrompt;

window.addEventListener('beforeinstallprompt', event => {
    event.preventDefault();
    deferredInstallPrompt = event;
});

export function detectInstallState(navigatorValue, matchMediaValue, canPrompt) {
    const userAgent = navigatorValue.userAgent || '';
    const platform = navigatorValue.platform || '';
    const isIos = /iPad|iPhone|iPod/.test(userAgent) ||
        (platform === 'MacIntel' && navigatorValue.maxTouchPoints > 1);
    const isAndroid = /Android/i.test(userAgent);
    const isInstalled = Boolean(navigatorValue.standalone) ||
        Boolean(matchMediaValue('(display-mode: standalone)').matches);
    return { isInstalled, canPrompt: canPrompt && !isInstalled, isIos, isAndroid };
}

export function getInstallState() {
    return detectInstallState(navigator, window.matchMedia.bind(window), Boolean(deferredInstallPrompt));
}

export async function promptInstall() {
    if (!deferredInstallPrompt) {
        return false;
    }

    const prompt = deferredInstallPrompt;
    deferredInstallPrompt = undefined;
    await prompt.prompt();
    const choice = await prompt.userChoice;
    return choice.outcome === 'accepted';
}
