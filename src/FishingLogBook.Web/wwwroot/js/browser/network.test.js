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

    it('installs the network API on the window', () => {
        const targetWindow = {
            navigator: { onLine: true },
            addEventListener() { }
        };

        installNetwork(targetWindow);

        expect(targetWindow.fishingLogBookNetwork.isOnline()).toBe(true);
    });
});
