import { describe, expect, it } from 'vitest';
import { detectInstallState } from './install.js';

describe('install detection', () => {
    it('detects an installed standalone app', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0', platform: 'Win32', maxTouchPoints: 0 },
            () => ({ matches: true }),
            true);

        expect(state).toEqual({ isInstalled: true, canPrompt: false, isIos: false, isAndroid: false });
    });

    it('provides iOS instructions without showing a fake prompt', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 (iPhone)', platform: 'iPhone', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.isIos).toBe(true);
        expect(state.canPrompt).toBe(false);
    });

    it('provides Android instructions when no prompt was captured', () => {
        const state = detectInstallState(
            { userAgent: 'Mozilla/5.0 Android', platform: 'Linux', maxTouchPoints: 5 },
            () => ({ matches: false }),
            false);

        expect(state.isAndroid).toBe(true);
        expect(state.canPrompt).toBe(false);
    });
});
