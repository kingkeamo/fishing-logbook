import { describe, expect, it } from 'vitest';
import { detectInstallState, getInstallState, promptInstall } from './install.js';

describe('install detection', () => {
    it('detects an installed standalone app', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0', platform: 'Win32', maxTouchPoints: 0 },
            () => ({ matches: true }),
            true);

        expect(state).toEqual({ isInstalled: true, canPrompt: false, platformFamily: 'Windows', isSafari: false });
    });

    it('provides iOS instructions without showing a fake prompt', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone)', platform: 'iPhone', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('iOS');
        expect(state.isSafari).toBe(false);
        expect(state.canPrompt).toBe(false);
    });

    it('detects an iOS app already running from the Home Screen', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone)', platform: 'iPhone', maxTouchPoints: 5, standalone: true },
            () => ({ matches: false }),
            true);

        expect(state.isInstalled).toBe(true);
        expect(state.platformFamily).toBe('iOS');
        expect(state.canPrompt).toBe(false);
    });

    it('provides Android instructions when no prompt was captured', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 Android', platform: 'Linux', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Android');
        expect(state.canPrompt).toBe(false);
    });

    it('recognises Safari on iPhone without pretending a native prompt exists', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone) AppleWebKit/605.1.15 Safari/604.1', platform: 'iPhone', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('iOS');
        expect(state.isSafari).toBe(true);
        expect(state.canPrompt).toBe(false);
    });

    it('detects Windows with a captured native prompt', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Windows NT 10.0) Chrome/140', platform: 'Win32', maxTouchPoints: 0 },
            () => ({ matches: false }),
            true);

        expect(state.platformFamily).toBe('Windows');
        expect(state.canPrompt).toBe(true);
    });

    it('falls back safely for an unknown browser', () => {
        const state = detectInstallState(
            { userAgent: 'Unknown', platform: 'Unknown', maxTouchPoints: 0 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Other');
        expect(state.canPrompt).toBe(false);
    });

    it('returns unavailable when no native prompt was captured', async () => {
        expect(await promptInstall()).toBe('unavailable');
    });

    it('returns accepted and consumes the captured native prompt', async () => {
        const event = new Event('beforeinstallprompt');
        event.prompt = () => Promise.resolve();
        event.userChoice = Promise.resolve({ outcome: 'accepted' });
        window.dispatchEvent(event);

        expect(getInstallState().canPrompt).toBe(true);
        expect(await promptInstall()).toBe('accepted');
        expect(await promptInstall()).toBe('unavailable');
    });

    it('returns dismissed without treating dismissal as an error', async () => {
        const event = new Event('beforeinstallprompt');
        event.prompt = () => Promise.resolve();
        event.userChoice = Promise.resolve({ outcome: 'dismissed' });
        window.dispatchEvent(event);

        expect(await promptInstall()).toBe('dismissed');
    });

    it('updates to installed when the browser reports app installation', () => {
        window.dispatchEvent(new Event('appinstalled'));

        expect(getInstallState().isInstalled).toBe(true);
        expect(getInstallState().canPrompt).toBe(false);
    });
});
