import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    getCurrent,
    isPromptDismissed,
    queryPermission,
    setPromptDismissed
} from './location.js';

describe('location JSInterop shim', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        localStorage.clear();
    });

    it('re-exports permission and prompt helpers used by Blazor', async () => {
        vi.stubGlobal('navigator', {});

        await expect(queryPermission()).resolves.toBe('unavailable');
        expect(isPromptDismissed()).toBe(false);
        setPromptDismissed();
        expect(isPromptDismissed()).toBe(true);
    });

    it('re-exports getCurrent', async () => {
        vi.stubGlobal('navigator', {
            geolocation: {
                getCurrentPosition(_success, error) {
                    error({ code: 1 });
                }
            }
        });

        await expect(getCurrent(1000)).resolves.toEqual({ error: 'denied' });
    });
});
