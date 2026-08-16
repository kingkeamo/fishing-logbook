import { afterEach, describe, expect, it, vi } from 'vitest';
import { emit, emitStorageEstimate, emitTimedOut } from './offline-diagnostics.js';

describe('offline diagnostics', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
        delete globalThis.fishingLogBookDiagnostics;
    });

    it('writes a debug line and forwards it to the diagnostics API', () => {
        const consoleFn = vi.fn();
        globalThis.fishingLogBookDiagnostics = { console: consoleFn };
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        emit('OfflineDbOpenStarted', {
            elapsedMilliseconds: 12,
            operation: 'open',
            storeName: 'testCatches',
            errorType: undefined,
            extra: 'must-not-leak'
        });

        expect(debug).toHaveBeenCalledWith('[FLB] OfflineDbOpenStarted', {
            elapsedMilliseconds: 12,
            operation: 'open',
            storeName: 'testCatches',
            errorType: undefined,
            quotaBytes: undefined,
            usageBytes: undefined
        });
        expect(consoleFn).toHaveBeenCalledWith(
            'Debug',
            'OfflineDbOpenStarted',
            JSON.stringify({
                elapsedMilliseconds: 12,
                operation: 'open',
                storeName: 'testCatches'
            })
        );
    });

    it('does not throw when the diagnostics console fails', () => {
        globalThis.fishingLogBookDiagnostics = {
            console() {
                throw new Error('console blocked');
            }
        };
        vi.spyOn(console, 'debug').mockImplementation(() => {
            throw new Error('debug blocked');
        });

        expect(() => emit('OfflineDbOpenStarted', { elapsedMilliseconds: 0, operation: 'open' })).not.toThrow();
    });

    it('emits a write timeout event', () => {
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        emitTimedOut('readwrite', 'testCatches', performance.now() - 20, new Error('IndexedDB write timed out'));

        expect(debug).toHaveBeenCalledWith(
            '[FLB] OfflineDbWriteTimedOut',
            expect.objectContaining({
                storeName: 'testCatches',
                operation: 'write'
            })
        );
    });

    it('emits a read timeout event', () => {
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        emitTimedOut('readonly', 'testCatchPhotographs', performance.now() - 20, new Error('IndexedDB photograph read timed out'));

        expect(debug).toHaveBeenCalledWith(
            '[FLB] OfflineDbReadTimedOut',
            expect.objectContaining({
                storeName: 'testCatchPhotographs',
                operation: 'read'
            })
        );
    });

    it('ignores errors that are not timeouts', () => {
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        emitTimedOut('readwrite', 'testCatches', performance.now(), new Error('quota exceeded'));
        emitTimedOut('readonly', 'testCatches', performance.now(), undefined);
        emitTimedOut('readonly', 'testCatches', performance.now(), {});

        expect(debug).not.toHaveBeenCalled();
    });

    it('does nothing when storage estimate is unavailable', async () => {
        vi.stubGlobal('navigator', { storage: undefined });
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        await emitStorageEstimate();

        expect(debug).not.toHaveBeenCalled();
    });

    it('emits quota and usage when estimate succeeds', async () => {
        vi.stubGlobal('navigator', {
            storage: {
                estimate: async () => ({ quota: 1024.9, usage: 12.2 })
            }
        });
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        await emitStorageEstimate();

        expect(debug).toHaveBeenCalledWith(
            '[FLB] OfflineDbOpenCompleted',
            expect.objectContaining({
                operation: 'open',
                quotaBytes: '1024',
                usageBytes: '12'
            })
        );
    });

    it('omits non-finite quota and usage', async () => {
        vi.stubGlobal('navigator', {
            storage: {
                estimate: async () => ({ quota: Number.POSITIVE_INFINITY, usage: Number.NaN })
            }
        });
        const debug = vi.spyOn(console, 'debug').mockImplementation(() => { });

        await emitStorageEstimate();

        expect(debug).toHaveBeenCalledWith(
            '[FLB] OfflineDbOpenCompleted',
            expect.objectContaining({
                quotaBytes: undefined,
                usageBytes: undefined
            })
        );
    });

    it('does not throw when estimate fails', async () => {
        vi.stubGlobal('navigator', {
            storage: {
                estimate: async () => {
                    throw new Error('estimate blocked');
                }
            }
        });

        await expect(emitStorageEstimate()).resolves.toBeUndefined();
    });
});
