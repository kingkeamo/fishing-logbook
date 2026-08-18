import { describe, expect, it } from 'vitest';
import { fromDateTimeLocalValue, toDateTimeLocalValue } from './time.js';

const utcPlusFourOffsetMinutes = -240;

describe('time', () => {
    it('returns an empty string when the UTC instant is invalid', () => {
        expect(toDateTimeLocalValue('not-a-date', utcPlusFourOffsetMinutes)).toBe('');
    });

    it('returns null when the local value is missing or malformed', () => {
        expect(fromDateTimeLocalValue(null, utcPlusFourOffsetMinutes)).toBeNull();
        expect(fromDateTimeLocalValue('', utcPlusFourOffsetMinutes)).toBeNull();
        expect(fromDateTimeLocalValue('17/08/2026 14:00', utcPlusFourOffsetMinutes)).toBeNull();
        expect(fromDateTimeLocalValue('2026-08-17 14:00', utcPlusFourOffsetMinutes)).toBeNull();
    });

    it('formats a UTC instant as UTC+04 wall-clock time', () => {
        expect(toDateTimeLocalValue('2026-08-17T10:00:00.000Z', utcPlusFourOffsetMinutes))
            .toBe('2026-08-17T14:00');
    });

    it('converts a UTC+04 local correction to the matching UTC instant', () => {
        expect(fromDateTimeLocalValue('2026-08-17T15:00', utcPlusFourOffsetMinutes))
            .toBe('2026-08-17T11:00:00.000Z');
    });

    it('preserves the UTC instant when an unchanged UTC+04 value is converted back', () => {
        const utcIso = '2026-08-17T10:00:00.000Z';
        const localValue = toDateTimeLocalValue(utcIso, utcPlusFourOffsetMinutes);

        expect(localValue).toBe('2026-08-17T14:00');
        expect(fromDateTimeLocalValue(localValue, utcPlusFourOffsetMinutes)).toBe(utcIso);
    });

    it('shifts the calendar date when UTC+04 crosses midnight', () => {
        expect(toDateTimeLocalValue('2026-08-17T22:00:00.000Z', utcPlusFourOffsetMinutes))
            .toBe('2026-08-18T02:00');
        expect(fromDateTimeLocalValue('2026-08-18T02:00', utcPlusFourOffsetMinutes))
            .toBe('2026-08-17T22:00:00.000Z');
    });
});
