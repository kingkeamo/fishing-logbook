import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import vm from 'node:vm';
import { describe, expect, it, vi } from 'vitest';

const workerScript = readFileSync(
    resolve(import.meta.dirname, '../../service-worker.published.js'),
    'utf8');

class TestRequest {
    constructor(input, options = {}) {
        this.url = new URL(typeof input === 'string' ? input : input.url, 'https://app.test/').href;
        this.method = options.method ?? input?.method ?? 'GET';
        this.mode = options.mode ?? input?.mode ?? 'cors';
        this.cache = options.cache;
    }
}

class TestResponse {
    constructor(body, options = {}) {
        this.body = body;
        this.headers = options.headers ?? {};
        this.status = options.status ?? 200;
        this.statusText = options.statusText ?? 'OK';
        this.ok = this.status >= 200 && this.status < 300;
        this.redirected = options.redirected ?? false;
    }

    clone() {
        return new TestResponse(this.body, {
            headers: this.headers,
            status: this.status,
            statusText: this.statusText,
            redirected: this.redirected
        });
    }

    static error() {
        return new TestResponse(undefined, { status: 0 });
    }
}

function createWorker({ match, fetch: fetchImplementation } = {}) {
    const listeners = {};
    const cache = {
        match: vi.fn(match ?? (async () => undefined)),
        keys: vi.fn(async () => []),
        put: vi.fn(async () => undefined)
    };
    const fetch = vi.fn(fetchImplementation ?? (async () => new TestResponse('network')));
    const serviceWorker = {
        origin: 'https://app.test',
        location: new URL('https://app.test/service-worker.js'),
        assetsManifest: { version: 'test', assets: [] },
        importScripts: vi.fn(),
        addEventListener: (type, listener) => { listeners[type] = listener; },
        skipWaiting: vi.fn(async () => undefined),
        clients: {
            claim: vi.fn(async () => undefined),
            matchAll: vi.fn(async () => [])
        }
    };

    vm.runInNewContext(workerScript, {
        self: serviceWorker,
        caches: {
            open: vi.fn(async () => cache),
            keys: vi.fn(async () => []),
            delete: vi.fn(async () => true)
        },
        fetch,
        Request: TestRequest,
        Response: TestResponse,
        URL,
        console
    });

    function dispatchFetch(url, { mode = 'navigate' } = {}) {
        let responsePromise;
        const request = new TestRequest(url, { mode });
        listeners.fetch({
            request,
            respondWith: response => { responsePromise = Promise.resolve(response); }
        });

        return {
            wasIntercepted: responsePromise !== undefined,
            response: responsePromise
        };
    }

    return { cache, dispatchFetch, fetch };
}

describe('published service worker', () => {
    it('serves the cached app shell for a route navigation without using the network', async () => {
        const shell = new TestResponse('cached shell');
        const worker = createWorker({
            match: async request => request === 'index.html' ? shell : undefined
        });

        const result = worker.dispatchFetch('https://app.test/catches');

        expect(result.wasIntercepted).toBe(true);
        expect(await result.response).toBe(shell);
        expect(worker.fetch).not.toHaveBeenCalled();
    });

    it('fetches and caches the root app shell when a navigation cache entry is missing', async () => {
        const shell = new TestResponse('network shell');
        const worker = createWorker({ fetch: async () => shell });

        const response = await worker.dispatchFetch('https://app.test/catches').response;

        expect(response).toBe(shell);
        expect(worker.fetch).toHaveBeenCalledOnce();
        expect(worker.fetch.mock.calls[0][0].url).toBe('https://app.test/');
        expect(worker.fetch.mock.calls[0][0].url).not.toContain('/catches');
        expect(worker.cache.put).toHaveBeenCalledWith('index.html', expect.any(TestResponse));
    });

    it('normalizes a redirected network app shell before caching and returning it', async () => {
        const worker = createWorker({
            fetch: async () => new TestResponse('redirected shell', { redirected: true })
        });

        const response = await worker.dispatchFetch('https://app.test/catches').response;

        expect(response.redirected).toBe(false);
        expect(response.body).toBe('redirected shell');
        expect(worker.cache.put.mock.calls[0][1].redirected).toBe(false);
    });

    it('serves an alternate cached shell while offline', async () => {
        const shell = new TestResponse('offline shell');
        const worker = createWorker({
            match: async request => request === '/' ? shell : undefined,
            fetch: async () => { throw new TypeError('Failed to fetch'); }
        });

        const response = await worker.dispatchFetch('https://app.test/catches').response;

        expect(response).toBe(shell);
        expect(worker.fetch).not.toHaveBeenCalled();
    });

    it('does not intercept cross-origin requests', () => {
        const worker = createWorker();

        const result = worker.dispatchFetch('https://photos.example/catch.jpg', { mode: 'cors' });

        expect(result.wasIntercepted).toBe(false);
        expect(worker.fetch).not.toHaveBeenCalled();
    });

    it('always fetches service-worker scripts from the network', async () => {
        const worker = createWorker({
            match: async () => new TestResponse('stale worker')
        });

        await worker.dispatchFetch('https://app.test/service-worker.js', { mode: 'same-origin' }).response;

        expect(worker.fetch).toHaveBeenCalledOnce();
        expect(worker.cache.match).not.toHaveBeenCalled();
    });
});
