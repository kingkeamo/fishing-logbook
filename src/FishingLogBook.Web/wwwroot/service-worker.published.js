// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations
//
// Cloudflare Pages 308s /index.html to /. Chrome rejects redirected responses for
// navigations. Cache-first, then rebuild redirected responses (dotnet/aspnetcore#33872).

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
// Never intercept cross-origin requests (e.g. presigned R2 photograph URLs). Re-dispatching
// them via fetch(event.request) from inside the worker changes request semantics enough
// that R2 rejects the re-issued request even though the same URL loads fine directly (FLB#74).
self.addEventListener('fetch', event => {
    if (new URL(event.request.url).origin !== self.location.origin) {
        return;
    }

    event.respondWith(onFetch(event));
});
self.addEventListener('error', event => notifyServiceWorkerError(event.message || 'error'));
self.addEventListener('unhandledrejection', event => notifyServiceWorkerError(String(event.reason || 'unhandledrejection')));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/, /\/_redirects$/, /\/_headers$/, /\.test\.js$/ ];

const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    const cache = await caches.open(cacheName);
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { cache: 'no-cache' }));

    await Promise.all(assetsRequests.map(request => cacheUnredirected(cache, request).catch(() => undefined)));
    await cacheAppShell(cache);
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

    const shouldServeIndexHtml = event.request.mode === 'navigate'
        && !manifestUrlList.some(manifestUrl => manifestUrl === event.request.url);

    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    let cachedResponse = await cache.match(request, { ignoreSearch: true });

    if (!cachedResponse && shouldServeIndexHtml) {
        cachedResponse = await matchIndexHtml(cache);
    }

    if (cachedResponse) {
        cachedResponse = await withoutRedirect(cachedResponse);
    }

    return cachedResponse || fetch(event.request);
}

async function cacheAppShell(cache) {
    const response = await fetch(new Request('./', { cache: 'no-cache' }));
    if (!response.ok) {
        return;
    }

    const shell = await withoutRedirect(response);
    if (!shell) {
        return;
    }

    await cache.put('index.html', shell);
}

async function cacheUnredirected(cache, request) {
    const response = await fetch(request);
    if (!response.ok) {
        return;
    }

    const stored = await withoutRedirect(response);
    if (stored) {
        await cache.put(request, stored);
    }
}

async function matchIndexHtml(cache) {
    const candidates = ['index.html', './index.html', '/index.html', './', '/'];
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

async function withoutRedirect(response) {
    if (!response) {
        return undefined;
    }

    if (!response.redirected) {
        return response;
    }

    const clonedResponse = response.clone();
    return new Response(clonedResponse.body, {
        headers: clonedResponse.headers,
        status: clonedResponse.status,
        statusText: clonedResponse.statusText
    });
}

function notifyServiceWorkerError(message) {
    console.error('[FLB] ServiceWorkerError', message);
    self.clients.matchAll({ includeUncontrolled: true }).then((clients) => {
        clients.forEach((client) => client.postMessage({ type: 'ServiceWorkerError', message: String(message).slice(0, 200) }));
    }).catch(() => { });
}
