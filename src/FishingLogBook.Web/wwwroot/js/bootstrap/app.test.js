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

    it('captures a browser install prompt offered before any page asks for it', async () => {
        await import('./app.js');
        const event = new Event('beforeinstallprompt');
        event.prompt = () => Promise.resolve();
        event.userChoice = Promise.resolve({ outcome: 'accepted' });

        window.dispatchEvent(event);

        const { getInstallState } = await import('../browser/install.js');
        expect(getInstallState().canPrompt).toBe(true);
    });
});
