export function createNetworkApi(targetWindow) {
    return {
        isOnline: () => targetWindow.navigator.onLine,
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
