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
