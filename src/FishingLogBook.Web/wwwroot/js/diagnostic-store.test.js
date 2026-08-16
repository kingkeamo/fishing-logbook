import { describe, expect, it } from 'vitest';
import {
    deleteDiagnosticEvents,
    getDiagnosticQueueCount,
    getPendingDiagnosticEvents,
    getStorageEstimate,
    inspectExistingDiagnosticDatabase,
    putDiagnosticEvent
} from './diagnostic-store.js';

describe('diagnostic-store JSInterop shim', () => {
    it('re-exports diagnostic queue operations used by Blazor', async () => {
        await putDiagnosticEvent(JSON.stringify({
            id: 'keep',
            timestampUtc: '2026-01-01T00:00:00.000Z',
            eventName: 'kept'
        }), 10);
        await putDiagnosticEvent(JSON.stringify({
            id: 'drop',
            timestampUtc: '2026-01-02T00:00:00.000Z',
            eventName: 'dropped'
        }), 10);

        await deleteDiagnosticEvents(JSON.stringify(['drop']));

        const pending = await getPendingDiagnosticEvents(10);
        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0]).id).toBe('keep');
        await expect(getDiagnosticQueueCount()).resolves.toBe(1);
    });

    it('re-exports storage estimate', async () => {
        const estimate = await getStorageEstimate();

        expect(estimate).toHaveProperty('quota');
        expect(estimate).toHaveProperty('usage');
    });

    it('re-exports inspect of an existing diagnostic database', async () => {
        await putDiagnosticEvent(JSON.stringify({
            id: 'keep',
            timestampUtc: '2026-01-01T00:00:00.000Z',
            eventName: 'kept'
        }), 10);

        await expect(inspectExistingDiagnosticDatabase()).resolves.toEqual({
            exists: true,
            hasStore: true,
            count: 1
        });
    });
});
