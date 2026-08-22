import { describe, expect, it, vi } from 'vitest';
import {
    captureInstallEvents,
    detectInstallState,
    getInstallState,
    promptInstall,
    subscribeInstallState,
    unsubscribeInstallState
} from './install.js';

function capturablePrompt(outcome) {
    const event = new Event('beforeinstallprompt');
    event.prompt = () => Promise.resolve();
    event.userChoice = Promise.resolve({ outcome });
    return event;
}

describe('install platform detection', () => {
    it('falls back safely for an unknown browser', () => {
        const state = detectInstallState(
            { userAgent: 'Unknown', platform: 'Unknown', maxTouchPoints: 0 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Other');
        expect(state.canPrompt).toBe(false);
    });

    it('detects an iPhone without pretending a native prompt exists', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0)', platform: 'iPhone', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('iOS');
        expect(state.isSafari).toBe(false);
        expect(state.canPrompt).toBe(false);
    });

    it('detects an iPad reporting a desktop user agent', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1.15', platform: 'MacIntel', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('iOS');
        expect(state.isSafari).toBe(true);
    });

    it('recognises Safari on iPhone', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone) AppleWebKit/605.1.15 Safari/604.1', platform: 'iPhone', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.isSafari).toBe(true);
    });

    it.each([
        ['Mozilla/5.0 (iPhone) CriOS/140 Safari/604.1'],
        ['Mozilla/5.0 (iPhone) FxiOS/140 Safari/604.1'],
        ['Mozilla/5.0 (iPhone) EdgiOS/140 Safari/604.1'],
        ['Mozilla/5.0 (iPhone) Safari/604.1 FBAV/500']
    ])('does not treat an in-app or third-party iOS browser as Safari: %s', userAgent => {
        const state = detectInstallState(
            { userAgent, platform: 'iPhone', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('iOS');
        expect(state.isSafari).toBe(false);
    });

    it('detects Android rather than the Linux kernel in its user agent', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Linux; Android 15; SM-S928B) Chrome/140 Mobile Safari/537.36', platform: 'Linux armv8l', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Android');
    });

    it('detects Samsung Internet on Android', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Linux; Android 15) SamsungBrowser/27.0 Chrome/140 Mobile Safari/537.36', platform: 'Linux armv8l', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Android');
    });

    it.each([
        ['Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140', 'Win32'],
        ['Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Chrome/140', 'MacIntel'],
        ['Mozilla/5.0 (X11; Linux x86_64) Chrome/140', 'Linux x86_64'],
        ['Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) Chrome/140', 'Linux x86_64']
    ])('treats a desktop browser as a computer: %s', (userAgent, platform) => {
        const state = detectInstallState(
            { userAgent, platform, maxTouchPoints: 0 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Desktop');
    });

    it('does not treat a desktop MacIntel without touch as iOS', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1.15', platform: 'MacIntel', maxTouchPoints: 0 },
            () => ({ matches: false }),
            false);

        expect(state.platformFamily).toBe('Desktop');
    });

    it('detects a standalone display mode as installed', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Linux; Android 15) Chrome/140', platform: 'Linux armv8l', maxTouchPoints: 5 },
            () => ({ matches: true }),
            true);

        expect(state).toEqual({ isInstalled: true, canPrompt: false, platformFamily: 'Android', isSafari: false });
    });

    it('detects an iOS app launched from the Home Screen as installed', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone) Safari/604.1', platform: 'iPhone', maxTouchPoints: 5, standalone: true },
            () => ({ matches: false }),
            true);

        expect(state.isInstalled).toBe(true);
        expect(state.platformFamily).toBe('iOS');
        expect(state.canPrompt).toBe(false);
    });

    it('detects a captured native prompt on a computer', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (Windows NT 10.0) Chrome/140', platform: 'Win32', maxTouchPoints: 0 },
            () => ({ matches: false }),
            true);

        expect(state.platformFamily).toBe('Desktop');
        expect(state.canPrompt).toBe(true);
    });
});

describe('install event capture', () => {
    it('reports no native prompt before the browser offers one', async () => {
        expect(getInstallState().canPrompt).toBe(false);
        expect(await promptInstall()).toBe('unavailable');
    });

    it('registers its window listeners once for the lifetime of the page', () => {
        const targetWindow = { addEventListener: vi.fn() };

        captureInstallEvents(targetWindow);
        captureInstallEvents(targetWindow);

        expect(targetWindow.addEventListener.mock.calls.map(call => call[0]))
            .toEqual(['beforeinstallprompt', 'appinstalled']);
    });

    it('keeps the captured prompt where every module instance can reach it', async () => {
        const another = await import('./install.js?instance=shared');
        window.dispatchEvent(capturablePrompt('dismissed'));

        expect(another.getInstallState().canPrompt).toBe(true);
        expect(await another.promptInstall()).toBe('dismissed');
        expect(getInstallState().canPrompt).toBe(false);
    });

    it('keeps a captured prompt available until it is used', async () => {
        window.dispatchEvent(capturablePrompt('accepted'));

        expect(getInstallState().canPrompt).toBe(true);
        expect(getInstallState().canPrompt).toBe(true);
        expect(await promptInstall()).toBe('accepted');
        expect(await promptInstall()).toBe('unavailable');
        expect(getInstallState().canPrompt).toBe(false);
    });

    it('treats dismissal as a normal outcome', async () => {
        window.dispatchEvent(capturablePrompt('dismissed'));

        expect(await promptInstall()).toBe('dismissed');
        expect(getInstallState().canPrompt).toBe(false);
    });
});

describe('install state subscriptions', () => {
    it('publishes a prompt that arrives after the first state read', () => {
        const subscriber = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const token = subscribeInstallState(subscriber);

        window.dispatchEvent(capturablePrompt('accepted'));

        expect(subscriber.invokeMethodAsync).toHaveBeenCalledTimes(1);
        const [method, state] = subscriber.invokeMethodAsync.mock.calls[0];
        expect(method).toBe('OnInstallStateChanged');
        expect(state.canPrompt).toBe(true);
        unsubscribeInstallState(token);
    });

    it('publishes to every current subscriber', async () => {
        const first = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const second = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const firstToken = subscribeInstallState(first);
        const secondToken = subscribeInstallState(second);

        expect(await promptInstall()).toBe('accepted');

        expect(first.invokeMethodAsync).toHaveBeenCalledTimes(1);
        expect(second.invokeMethodAsync).toHaveBeenCalledTimes(1);
        unsubscribeInstallState(firstToken);
        unsubscribeInstallState(secondToken);
    });

    it('stops publishing to a subscriber that has been removed', () => {
        const subscriber = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const token = subscribeInstallState(subscriber);
        unsubscribeInstallState(token);

        window.dispatchEvent(capturablePrompt('accepted'));

        expect(subscriber.invokeMethodAsync).not.toHaveBeenCalled();
    });

    it('drops a subscriber whose callback can no longer be reached', async () => {
        const subscriber = {
            invokeMethodAsync: vi.fn(() => Promise.reject(new Error('disposed')))
        };
        const token = subscribeInstallState(subscriber);

        window.dispatchEvent(capturablePrompt('accepted'));
        await Promise.resolve();
        window.dispatchEvent(capturablePrompt('accepted'));

        expect(subscriber.invokeMethodAsync).toHaveBeenCalledTimes(1);
        unsubscribeInstallState(token);
    });

    it('publishes an installed state when the browser reports installation', () => {
        const subscriber = { invokeMethodAsync: vi.fn(() => Promise.resolve()) };
        const token = subscribeInstallState(subscriber);

        window.dispatchEvent(new Event('appinstalled'));

        expect(subscriber.invokeMethodAsync).toHaveBeenCalledTimes(1);
        expect(subscriber.invokeMethodAsync.mock.calls[0][1].isInstalled).toBe(true);
        expect(getInstallState().isInstalled).toBe(true);
        expect(getInstallState().canPrompt).toBe(false);
        unsubscribeInstallState(token);
    });
});
