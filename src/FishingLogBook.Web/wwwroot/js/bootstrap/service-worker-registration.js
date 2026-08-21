const epoch = '20260821-pre-onboarding-startup';

export function listenForServiceWorkerErrors(targetWindow = window) {
    targetWindow.navigator.serviceWorker?.addEventListener('message', (event) => {
        if (event.data?.type === 'ServiceWorkerError') {
            targetWindow.console.error('[FLB] ServiceWorkerError', event.data.message);
        }
    });
}

export async function registerServiceWorker(targetWindow = window) {
    let epochChanged = false;
    try {
        epochChanged = targetWindow.localStorage.getItem('flb-sw-epoch') !== epoch;
    } catch { /* ignore */ }

    try {
        await targetWindow.navigator.serviceWorker.register(
            'service-worker.js',
            { updateViaCache: 'none' });

        if (epochChanged) {
            targetWindow.localStorage.setItem('flb-sw-epoch', epoch);
        }
    } catch (error) {
        targetWindow.console.error('[FLB] ServiceWorkerRegistrationError', error);
    }
}
