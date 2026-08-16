import { describe, expect, it } from 'vitest';
import { getPlatform } from './platform.js';

describe('platform', () => {
    it('prefers userAgentData platform over navigator.platform', () => {
        const platform = getPlatform({
            userAgentData: { platform: 'Windows' },
            platform: 'Win32',
            userAgent: 'Mozilla/5.0'
        });

        expect(platform).toBe('Windows Mozilla/5.0');
    });

    it('falls back to navigator.platform when userAgentData is missing', () => {
        const platform = getPlatform({
            platform: 'MacIntel',
            userAgent: 'Safari'
        });

        expect(platform).toBe('MacIntel Safari');
    });

    it('falls back to navigator.platform when userAgentData.platform is empty', () => {
        const platform = getPlatform({
            userAgentData: { platform: '' },
            platform: 'Win32',
            userAgent: 'Mozilla/5.0'
        });

        expect(platform).toBe('Win32 Mozilla/5.0');
    });

    it('returns an empty string when platform details are missing', () => {
        expect(getPlatform({})).toBe('');
    });

    it('trims the combined string to 120 characters', () => {
        const platform = getPlatform({
            platform: 'X'.repeat(80),
            userAgent: 'Y'.repeat(80)
        });

        expect(platform).toHaveLength(120);
        expect(platform.startsWith('X')).toBe(true);
    });
});
