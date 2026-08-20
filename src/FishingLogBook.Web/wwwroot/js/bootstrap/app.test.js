import { describe, expect, it } from 'vitest';

describe('app bootstrap', () => {
    it('installs Blazor JSInterop globals on the window', async () => {
        localStorage.setItem('fishinglogbook-culture', 'ga');

        await import('./app.js');

        expect(typeof window.fishingLogBookCulture.get).toBe('function');
        expect(typeof window.fishingLogBookNetwork.isOnline).toBe('function');
        expect(typeof window.fishingLogBookDiagnostics.getSessionId).toBe('function');
        expect(typeof window.fishingLogBookAuthentication.logout).toBe('function');
        expect(document.documentElement.lang).toBe('ga');
        expect(window.fishingLogBookCulture.browser()).toBeTruthy();
        expect(typeof window.fishingLogBookNetwork.isOnline()).toBe('boolean');
    });
});
