export function createNetworkApi(targetWindow) {
    let monitoring;

    return {
        isOnline: () => targetWindow.navigator.onLine,
        startMonitoring: (helper) => {
            if (monitoring) return;

            const notifyConnectivity = () => helper.invokeMethodAsync(
                'OnBrowserConnectivityChanged',
                targetWindow.navigator.onLine);
            const notifyUsable = () => helper.invokeMethodAsync('OnBrowserUsable');
            const notifyVisible = () => {
                if (targetWindow.document?.visibilityState === 'visible') {
                    helper.invokeMethodAsync('OnBrowserUsable');
                }
            };
            targetWindow.addEventListener('online', notifyConnectivity);
            targetWindow.addEventListener('offline', notifyConnectivity);
            targetWindow.addEventListener('pageshow', notifyUsable);
            targetWindow.document?.addEventListener('visibilitychange', notifyVisible);
            monitoring = { notifyConnectivity, notifyUsable, notifyVisible };
        },
        stopMonitoring: () => {
            if (!monitoring) return;

            targetWindow.removeEventListener('online', monitoring.notifyConnectivity);
            targetWindow.removeEventListener('offline', monitoring.notifyConnectivity);
            targetWindow.removeEventListener('pageshow', monitoring.notifyUsable);
            targetWindow.document?.removeEventListener('visibilitychange', monitoring.notifyVisible);
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
