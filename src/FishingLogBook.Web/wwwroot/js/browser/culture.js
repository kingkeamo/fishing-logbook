const cultureKey = 'fishinglogbook-culture';

export function createCultureApi(targetWindow) {
    return {
        get: () => {
            try { return targetWindow.localStorage.getItem(cultureKey); }
            catch { return null; }
        },
        set: (value) => {
            try { targetWindow.localStorage.setItem(cultureKey, value); } catch { /* ignore */ }
            targetWindow.document.documentElement.lang = value;
        },
        browser: () => targetWindow.navigator.language || 'en-GB',
        reload: () => {
            targetWindow.location.replace(`${targetWindow.location.origin}${targetWindow.location.pathname || '/'}`);
        }
    };
}

export function installCulture(targetWindow) {
    targetWindow.fishingLogBookCulture = createCultureApi(targetWindow);
}

export function applyStoredCulture(targetWindow) {
    const storedCulture = targetWindow.fishingLogBookCulture.get();
    if (storedCulture) {
        targetWindow.document.documentElement.lang = storedCulture;
    }
}
