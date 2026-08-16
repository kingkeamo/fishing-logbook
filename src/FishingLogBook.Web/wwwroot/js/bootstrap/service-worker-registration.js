const epoch = '20260814-cache-first-unredirected';

export function listenForServiceWorkerErrors(targetWindow = window) {
    targetWindow.navigator.serviceWorker?.addEventListener('message', (event) => {
        if (event.data?.type === 'ServiceWorkerError') {
            targetWindow.console.error('[FLB] ServiceWorkerError', event.data.message);
        }
    });
}

export async function registerServiceWorker(targetWindow = window) {
    try {
        if (targetWindow.localStorage.getItem('flb-sw-epoch') !== epoch) {
            const registrations = await targetWindow.navigator.serviceWorker.getRegistrations();
            await Promise.all(registrations.map(registration => registration.unregister()));
            const keys = await targetWindow.caches.keys();
            await Promise.all(keys.map(key => targetWindow.caches.delete(key)));
            targetWindow.localStorage.setItem('flb-sw-epoch', epoch);
        }
    } catch { /* ignore */ }
    targetWindow.navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
}
