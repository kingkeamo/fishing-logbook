import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    CATCH_DATABASE_NAME,
    CATCH_STORE_NAME,
    PHOTO_STORE_NAME,
    PRODUCTION_CATCH_STORE_NAME,
    PRODUCTION_PHOTO_STORE_NAME,
    getAllTestCatches,
    putTestCatch
} from './catch-store.js';
import {
    DIAGNOSTIC_DATABASE_NAME,
    DIAGNOSTIC_STORE_NAME,
    putDiagnosticEvent
} from './diagnostic-store.js';

const storageDir = dirname(fileURLToPath(import.meta.url));

function readJs(fileName) {
    return readFileSync(resolve(storageDir, fileName), 'utf8');
}

describe('schema safety', () => {
    it('does not mix Catch and diagnostic schema names across store modules', () => {
        const catchSource = readJs('catch-store.js');
        const diagnosticSource = readJs('diagnostic-store.js');
        const indexedDbSource = readJs('indexed-db.js');

        expect(catchSource).not.toContain('FishingLogBookDiagnostics');
        expect(catchSource).not.toContain('diagnosticEvents');
        expect(diagnosticSource).not.toContain('testCatches');
        expect(diagnosticSource).not.toContain('testCatchPhotographs');
        expect(indexedDbSource).not.toContain('FishingLogBookDiagnostics');
        expect(indexedDbSource).not.toContain(CATCH_DATABASE_NAME);
    });

    it('does not let the diagnostic store modify the Catch schema', async () => {
        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'keep' }));
        await putDiagnosticEvent(JSON.stringify({
            id: 'diag-1',
            timestampUtc: '2026-01-01T00:00:00.000Z'
        }), 10);

        const catchDb = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });
        const diagnosticDb = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(DIAGNOSTIC_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });

        expect(catchDb.name).toBe(CATCH_DATABASE_NAME);
        expect(diagnosticDb.name).toBe(DIAGNOSTIC_DATABASE_NAME);
        expect(catchDb.objectStoreNames.contains(CATCH_STORE_NAME)).toBe(true);
        expect(catchDb.objectStoreNames.contains(PHOTO_STORE_NAME)).toBe(true);
        expect(catchDb.objectStoreNames.contains(PRODUCTION_CATCH_STORE_NAME)).toBe(true);
        expect(catchDb.objectStoreNames.contains(PRODUCTION_PHOTO_STORE_NAME)).toBe(true);
        expect(catchDb.objectStoreNames.contains(DIAGNOSTIC_STORE_NAME)).toBe(false);
        expect(diagnosticDb.objectStoreNames.contains(CATCH_STORE_NAME)).toBe(false);
        expect(diagnosticDb.objectStoreNames.contains(DIAGNOSTIC_STORE_NAME)).toBe(true);

        const catches = await getAllTestCatches();
        expect(JSON.parse(catches[0]).notes).toBe('keep');
        catchDb.close();
        diagnosticDb.close();
    });

    it('creates a deterministic Catch schema on a fresh database', async () => {
        await putTestCatch(JSON.stringify({ id: 'catch-1' }));
        const db = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });

        expect([...db.objectStoreNames].sort()).toEqual([
            PHOTO_STORE_NAME,
            CATCH_STORE_NAME,
            PRODUCTION_CATCH_STORE_NAME,
            PRODUCTION_PHOTO_STORE_NAME
        ].sort());
        db.close();
    });
});
