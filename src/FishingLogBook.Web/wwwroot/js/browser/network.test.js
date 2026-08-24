import { describe, expect, it, vi } from 'vitest';
import { createNetworkApi, installNetwork } from './network.js';

describe('network', () => {
    it('reports the online state', () => {
        const api = createNetworkApi({
            navigator: { onLine: true },
            addEventListener() { }
        });

        expect(api.isOnline()).toBe(true);

        const offline = createNetworkApi({
            navigator: { onLine: false },
            addEventListener() { }
        });
        expect(offline.isOnline()).toBe(false);
    });

    it('invokes the helper when the browser fires online', () => {
        let handler;
        const helper = { invokeMethodAsync: vi.fn() };
        const api = createNetworkApi({
            navigator: { onLine: false },
            addEventListener(name, callback) {
                expect(name).toBe('online');
                handler = callback;
            }
        });

        api.onOnline(helper);
        handler();

        expect(helper.invokeMethodAsync).toHaveBeenCalledWith('OnBrowserOnline');
    });

    it('reports online and offline changes until monitoring stops', () => {
        const handlers = {};
        const helper = { invokeMethodAsync: vi.fn() };
        const targetWindow = {
            navigator: { onLine: true },
            addEventListener(name, callback) {
                handlers[name] = callback;
            },
            removeEventListener: vi.fn()
        };
        const api = createNetworkApi(targetWindow);
        api.startMonitoring(helper);

        targetWindow.navigator.onLine = false;
        handlers.offline();
        targetWindow.navigator.onLine = true;
        handlers.online();
        api.stopMonitoring();

        expect(helper.invokeMethodAsync).toHaveBeenNthCalledWith(1, 'OnBrowserConnectivityChanged', false);
        expect(helper.invokeMethodAsync).toHaveBeenNthCalledWith(2, 'OnBrowserConnectivityChanged', true);
        expect(targetWindow.removeEventListener).toHaveBeenCalledWith('online', handlers.online);
        expect(targetWindow.removeEventListener).toHaveBeenCalledWith('offline', handlers.offline);
    });

    it('registers only one connectivity monitor', () => {
        const handlers = {};
        const helper = { invokeMethodAsync: vi.fn() };
        const targetWindow = {
            navigator: { onLine: true },
            addEventListener: vi.fn((name, callback) => { handlers[name] = callback; }),
            removeEventListener() { }
        };
        const api = createNetworkApi(targetWindow);

        api.startMonitoring(helper);
        api.startMonitoring(helper);

        expect(targetWindow.addEventListener).toHaveBeenCalledTimes(2);
    });

    it('invokes the helper when the page resumes or becomes visible', () => {
        const windowHandlers = {};
        const documentHandlers = {};
        const helper = { invokeMethodAsync: vi.fn() };
        const targetWindow = {
            navigator: { onLine: true },
            document: {
                visibilityState: 'hidden',
                addEventListener(name, callback) {
                    documentHandlers[name] = callback;
                }
            },
            addEventListener(name, callback) {
                windowHandlers[name] = callback;
            }
        };
        const api = createNetworkApi(targetWindow);

        api.onUsable(helper);
        windowHandlers.pageshow();
        targetWindow.document.visibilityState = 'visible';
        documentHandlers.visibilitychange();

        expect(helper.invokeMethodAsync).toHaveBeenCalledTimes(2);
        expect(helper.invokeMethodAsync).toHaveBeenNthCalledWith(1, 'OnBrowserUsable');
        expect(helper.invokeMethodAsync).toHaveBeenNthCalledWith(2, 'OnBrowserUsable');
    });

    it('installs the network API on the window', () => {
        const targetWindow = {
            navigator: { onLine: true },
            addEventListener() { }
        };

        installNetwork(targetWindow);

        expect(targetWindow.fishingLogBookNetwork.isOnline()).toBe(true);
    });
});
