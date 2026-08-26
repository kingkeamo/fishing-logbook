import { afterEach, describe, expect, it, vi } from 'vitest';
import { TimeoutError, withTimeout } from './timeout.js';

describe('withTimeout', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it('resolves the original value when the promise settles first', async () => {
        await expect(withTimeout(Promise.resolve('ok'), 1000, 'work')).resolves.toBe('ok');
    });

    it('rejects with the original error when the promise fails first', async () => {
        await expect(withTimeout(Promise.reject(new Error('boom')), 1000, 'work'))
            .rejects.toThrow('boom');
    });

    it('rejects with a timeout error when the promise never settles', async () => {
        vi.useFakeTimers();
        const assertion = expect(withTimeout(new Promise(() => { }), 40, 'work'))
            .rejects.toSatisfy((error) =>
                error instanceof TimeoutError && error.message === 'work timed out');
        await vi.advanceTimersByTimeAsync(40);
        await assertion;
    });
});
