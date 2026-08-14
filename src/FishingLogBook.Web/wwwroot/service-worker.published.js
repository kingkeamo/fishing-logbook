// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

async function onInstall(event) {
    console.info('Service worker: Install');

    const cache = await caches.open(cacheName);
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { cache: 'no-cache' }));

    await Promise.all(assetsRequests.map(request => cache.add(request).catch(() => undefined)));
    await cacheNavigationShell(cache);
    await self.skipWaiting();
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    await self.clients.claim();

    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    const url = new URL(event.request.url);
    if (url.pathname.endsWith('/service-worker.js') || url.pathname.endsWith('/service-worker-assets.js')) {
        return fetch(event.request);
    }

    const cache = await caches.open(cacheName);

    if (event.request.mode === 'navigate') {
        try {
            const networkResponse = await fetch(event.request);
            if (networkResponse.ok) {
                return networkResponse;
            }
        } catch {
            // Offline or unreachable: use the cached shell below.
        }

        const cachedIndex = await asNavigationResponse(await matchIndexHtml(cache));
        if (cachedIndex) {
            return cachedIndex;
        }

        return Response.error();
    }

    const cachedResponse = await cache.match(event.request, { ignoreSearch: true });
    if (cachedResponse && cachedResponse.ok && !cachedResponse.redirected) {
        return cachedResponse;
    }

    return fetch(event.request);
}

async function cacheNavigationShell(cache) {
    const response = await fetch(new Request('index.html', { cache: 'no-cache' }));
    if (!response.ok) {
        return;
    }

    await cache.put('index.html', await asNavigationResponse(response));
}

async function matchIndexHtml(cache) {
    const candidates = ['index.html', './index.html', '/index.html'];
    for (const candidate of candidates) {
        const matched = await cache.match(candidate, { ignoreSearch: true });
        if (matched) {
            return matched;
        }
    }

    const keys = await cache.keys();
    const indexKey = keys.find(key => {
        const path = new URL(key.url).pathname;
        return path.endsWith('/index.html') || path === '/';
    });

    return indexKey ? cache.match(indexKey) : undefined;
}

async function asNavigationResponse(response) {
    if (!response || !response.ok) {
        return undefined;
    }

    return new Response(await response.blob(), {
        status: 200,
        statusText: 'OK',
        headers: response.headers
    });
}
