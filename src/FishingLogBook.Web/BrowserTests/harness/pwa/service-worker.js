const cacheName = 'playwright-shell-v1';
const shell = '<!DOCTYPE html><html><body><p id="shell">FishingLogBook app shell</p></body></html>';

self.addEventListener('install', (event) => {
    event.waitUntil((async () => {
        const cache = await caches.open(cacheName);
        const response = new Response(shell, {
            headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
        await cache.put(new URL('./index.html', self.location).href, response.clone());
        await cache.put(new URL('./', self.location).href, response);
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', (event) => {
    event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', (event) => {
    if (event.request.mode !== 'navigate') {
        return;
    }

    event.respondWith((async () => {
        const cache = await caches.open(cacheName);
        return (await cache.match(new URL('./index.html', self.location).href))
            || (await cache.match(new URL('./', self.location).href))
            || fetch(event.request);
    })());
});
