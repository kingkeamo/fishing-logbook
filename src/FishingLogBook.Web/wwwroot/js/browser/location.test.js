import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    getCurrent,
    isPromptDismissed,
    queryPermission,
    setPromptDismissed
} from './location.js';
import { withTimeout } from './timeout.js';

function mockNavigator({ geolocation, permissions } = {}) {
    vi.stubGlobal('navigator', {
        geolocation,
        permissions
    });
}

describe('location', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        localStorage.clear();
        vi.useRealTimers();
    });

    it('returns granted when permission is granted', async () => {
        mockNavigator({
            geolocation: {},
            permissions: {
                query: async () => ({ state: 'granted' })
            }
        });

        await expect(queryPermission()).resolves.toBe('granted');
    });

    it('returns denied from getCurrent when geolocation is denied', async () => {
        mockNavigator({
            geolocation: {
                getCurrentPosition(_success, error) {
                    error({ code: 1 });
                }
            }
        });

        await expect(getCurrent(1000)).resolves.toEqual({ error: 'denied' });
    });

    it('returns unavailable when geolocation is missing', async () => {
        mockNavigator({});

        await expect(queryPermission()).resolves.toBe('unavailable');
        await expect(getCurrent(1000)).resolves.toEqual({ error: 'unavailable' });
    });

    it('returns timeout from getCurrent when geolocation times out', async () => {
        mockNavigator({
            geolocation: {
                getCurrentPosition(_success, error) {
                    error({ code: 3 });
                }
            }
        });

        await expect(getCurrent(1000)).resolves.toEqual({ error: 'timeout' });
    });

    it('returns prompt when the permissions API is unavailable', async () => {
        mockNavigator({
            geolocation: {}
        });

        await expect(queryPermission()).resolves.toBe('prompt');
    });

    it('returns prompt when the permissions query rejects', async () => {
        mockNavigator({
            geolocation: {},
            permissions: {
                query: async () => {
                    throw new Error('denied by browser');
                }
            }
        });

        await expect(queryPermission()).resolves.toBe('prompt');
    });

    it('bounds a permissions query that never settles through withTimeout', async () => {
        vi.useFakeTimers();
        const never = new Promise(() => { });
        const assertion = expect(withTimeout(never, 30, 'location permission'))
            .rejects.toThrow('location permission timed out');
        await vi.advanceTimersByTimeAsync(30);
        await assertion;
    });

    it('stores prompt dismissal in localStorage', () => {
        expect(isPromptDismissed()).toBe(false);
        setPromptDismissed();
        expect(isPromptDismissed()).toBe(true);
    });

    it('treats a blocked localStorage read as not dismissed', () => {
        vi.stubGlobal('localStorage', {
            getItem() {
                throw new Error('blocked');
            },
            setItem() {
                throw new Error('blocked');
            }
        });

        expect(isPromptDismissed()).toBe(false);
        expect(() => setPromptDismissed()).not.toThrow();
    });

    it('returns coordinates when geolocation succeeds', async () => {
        mockNavigator({
            geolocation: {
                getCurrentPosition(success) {
                    success({
                        coords: {
                            latitude: 53.35,
                            longitude: -6.26,
                            accuracy: 12
                        },
                        timestamp: Date.UTC(2026, 0, 1)
                    });
                }
            }
        });

        await expect(getCurrent(1000)).resolves.toEqual({
            latitude: 53.35,
            longitude: -6.26,
            accuracy: 12,
            timestamp: '2026-01-01T00:00:00.000Z'
        });
    });

    it('returns unavailable for other geolocation error codes', async () => {
        mockNavigator({
            geolocation: {
                getCurrentPosition(_success, error) {
                    error({ code: 2 });
                }
            }
        });

        await expect(getCurrent(1000)).resolves.toEqual({ error: 'unavailable' });
    });
});
