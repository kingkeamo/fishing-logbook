export function createNetworkApi(targetWindow) {
    let monitoring;

    return {
        isOnline: () => targetWindow.navigator.onLine,
        startMonitoring: (helper) => {
            if (monitoring) return;

            const notify = () => helper.invokeMethodAsync(
                'OnBrowserConnectivityChanged',
                targetWindow.navigator.onLine);
            targetWindow.addEventListener('online', notify);
            targetWindow.addEventListener('offline', notify);
            monitoring = { notify };
        },
        stopMonitoring: () => {
            if (!monitoring) return;

            targetWindow.removeEventListener('online', monitoring.notify);
            targetWindow.removeEventListener('offline', monitoring.notify);
            monitoring = undefined;
        },
        onOnline: (helper) => {
            targetWindow.addEventListener('online', () => {
                helper.invokeMethodAsync('OnBrowserOnline');
            });
        },
        onUsable: (helper) => {
            targetWindow.addEventListener('pageshow', () => {
                helper.invokeMethodAsync('OnBrowserUsable');
            });
            targetWindow.document?.addEventListener('visibilitychange', () => {
                if (targetWindow.document.visibilityState === 'visible') {
                    helper.invokeMethodAsync('OnBrowserUsable');
                }
            });
        }
    };
}

export function installNetwork(targetWindow) {
    targetWindow.fishingLogBookNetwork = createNetworkApi(targetWindow);
}
