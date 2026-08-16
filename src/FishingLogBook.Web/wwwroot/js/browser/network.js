export function createNetworkApi(targetWindow) {
    return {
        isOnline: () => targetWindow.navigator.onLine,
        onOnline: (helper) => {
            targetWindow.addEventListener('online', () => {
                helper.invokeMethodAsync('OnBrowserOnline');
            });
        }
    };
}

export function installNetwork(targetWindow) {
    targetWindow.fishingLogBookNetwork = createNetworkApi(targetWindow);
}
