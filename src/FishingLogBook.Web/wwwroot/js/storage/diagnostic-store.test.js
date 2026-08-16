import { describe, expect, it } from 'vitest';
import {
    DIAGNOSTIC_DATABASE_NAME,
    DIAGNOSTIC_STORE_NAME,
    deleteDiagnosticEvents,
    getDiagnosticQueueCount,
    getPendingDiagnosticEvents,
    getStorageEstimate,
    putDiagnosticEvent
} from './diagnostic-store.js';

function eventJson(id, timestampUtc) {
    return JSON.stringify({
        id,
        timestampUtc,
        eventName: `event-${id}`
    });
}

describe('Diagnostic store', () => {
    it('creates the diagnostic schema and timestampUtc index', async () => {
        await putDiagnosticEvent(eventJson('a', '2026-01-01T00:00:00.000Z'), 10);

        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(DIAGNOSTIC_DATABASE_NAME);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        expect(db.objectStoreNames.contains(DIAGNOSTIC_STORE_NAME)).toBe(true);
        const transaction = db.transaction(DIAGNOSTIC_STORE_NAME, 'readonly');
        const store = transaction.objectStore(DIAGNOSTIC_STORE_NAME);
        expect(store.indexNames.contains('timestampUtc')).toBe(true);
        db.close();
    });

    it('writes and reads the queue in timestamp order', async () => {
        await putDiagnosticEvent(eventJson('later', '2026-01-02T00:00:00.000Z'), 10);
        await putDiagnosticEvent(eventJson('earlier', '2026-01-01T00:00:00.000Z'), 10);

        const pending = await getPendingDiagnosticEvents(10);
        const ids = pending.map((item) => JSON.parse(item).id);

        expect(ids).toEqual(['earlier', 'later']);
    });

    it('counts queued events', async () => {
        await putDiagnosticEvent(eventJson('a', '2026-01-01T00:00:00.000Z'), 10);
        await putDiagnosticEvent(eventJson('b', '2026-01-02T00:00:00.000Z'), 10);

        await expect(getDiagnosticQueueCount()).resolves.toBe(2);
    });

    it('deletes the oldest records when the queue is bounded', async () => {
        await putDiagnosticEvent(eventJson('oldest', '2026-01-01T00:00:00.000Z'), 2);
        await putDiagnosticEvent(eventJson('middle', '2026-01-02T00:00:00.000Z'), 2);
        await putDiagnosticEvent(eventJson('newest', '2026-01-03T00:00:00.000Z'), 2);

        const pending = await getPendingDiagnosticEvents(10);
        const ids = pending.map((item) => JSON.parse(item).id);

        expect(await getDiagnosticQueueCount()).toBe(2);
        expect(ids).toEqual(['middle', 'newest']);
    });

    it('deletes uploaded events by id', async () => {
        await putDiagnosticEvent(eventJson('keep', '2026-01-01T00:00:00.000Z'), 10);
        await putDiagnosticEvent(eventJson('drop', '2026-01-02T00:00:00.000Z'), 10);

        await deleteDiagnosticEvents(JSON.stringify(['drop']));

        const pending = await getPendingDiagnosticEvents(10);
        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0]).id).toBe('keep');
    });

    it('treats a missing ids payload as an empty delete', async () => {
        await putDiagnosticEvent(eventJson('keep', '2026-01-01T00:00:00.000Z'), 10);

        await deleteDiagnosticEvents();

        const pending = await getPendingDiagnosticEvents(10);
        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0]).id).toBe('keep');
    });

    it('returns a storage estimate', async () => {
        const estimate = await getStorageEstimate();

        expect(estimate).toHaveProperty('quota');
        expect(estimate).toHaveProperty('usage');
    });
});
