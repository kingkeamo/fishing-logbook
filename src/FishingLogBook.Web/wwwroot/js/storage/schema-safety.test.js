import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    CATCH_DATABASE_NAME,
    CATCH_STORE_NAME,
    PHOTO_STORE_NAME,
    CATCH_DATABASE_VERSION,
    getAllCatchesWithPhotographs,
    putCatchWithPhotographs
} from './catch-store.js';
import { TRIP_STORE_NAME } from './trip-store.js';
import { TRIP_PHOTO_STORE_NAME } from './trip-photo-store.js';
import {
    DIAGNOSTIC_DATABASE_NAME,
    DIAGNOSTIC_STORE_NAME,
    putDiagnosticEvent
} from './diagnostic-store.js';
import {
    PREFERENCE_DATABASE_NAME,
    PREFERENCE_STORE_NAME,
    putFishingPreferences
} from './preference-store.js';

const storageDir = dirname(fileURLToPath(import.meta.url));
const ownerUserId = '11111111-1111-1111-1111-111111111111';

function readJs(fileName) {
    return readFileSync(resolve(storageDir, fileName), 'utf8');
}

function putCatch(id) {
    return putCatchWithPhotographs(
        JSON.stringify({ id, userId: ownerUserId, notes: 'keep' }),
        [{ id: `${id}-photo`, catchId: id, contentType: 'image/jpeg', bytes: new Uint8Array([1]) }]);
}

describe('schema safety', () => {
    it('does not mix Catch and diagnostic schema names across store modules', () => {
        const catchSource = readJs('catch-store.js');
        const diagnosticSource = readJs('diagnostic-store.js');
        const indexedDbSource = readJs('indexed-db.js');

        expect(catchSource).not.toContain('FishingLogBookDiagnostics');
        expect(catchSource).not.toContain('diagnosticEvents');
        expect(diagnosticSource).not.toContain(CATCH_STORE_NAME);
        expect(diagnosticSource).not.toContain(PHOTO_STORE_NAME);
        expect(indexedDbSource).not.toContain('FishingLogBookDiagnostics');
        expect(indexedDbSource).not.toContain(CATCH_DATABASE_NAME);
    });

    it('does not mix Catch and preference schema names across store modules', () => {
        const catchSource = readJs('catch-store.js');
        const preferenceSource = readJs('preference-store.js');
        const indexedDbSource = readJs('indexed-db.js');

        expect(catchSource).not.toContain(PREFERENCE_DATABASE_NAME);
        expect(catchSource).not.toContain(PREFERENCE_STORE_NAME);
        expect(catchSource).not.toContain('fishingPreferences');
        expect(preferenceSource).not.toContain(`'${CATCH_DATABASE_NAME}'`);
        expect(preferenceSource).not.toContain(CATCH_STORE_NAME);
        expect(preferenceSource).not.toContain(PHOTO_STORE_NAME);
        expect(indexedDbSource).not.toContain(PREFERENCE_DATABASE_NAME);
    });

    it('keeps the released logbook database version unchanged', () => {
        expect(CATCH_DATABASE_VERSION).toBe(6);
    });

    it('does not mix Trip and other feature schema names across store modules', () => {
        const tripSource = readJs('trip-store.js');
        const diagnosticSource = readJs('diagnostic-store.js');
        const preferenceSource = readJs('preference-store.js');

        expect(tripSource).not.toContain('FishingLogBookDiagnostics');
        expect(tripSource).not.toContain('FishingLogBookPreferences');
        expect(tripSource).not.toContain('diagnosticEvents');
        expect(tripSource).not.toContain('fishingPreferences');
        expect(diagnosticSource).not.toContain(TRIP_STORE_NAME);
        expect(preferenceSource).not.toContain(TRIP_STORE_NAME);
        expect(diagnosticSource).not.toContain(TRIP_PHOTO_STORE_NAME);
        expect(preferenceSource).not.toContain(TRIP_PHOTO_STORE_NAME);
    });

    it('owns the logbook database name in exactly one module', () => {
        const logbookSource = readJs('logbook-database.js');
        const catchSource = readJs('catch-store.js');
        const tripSource = readJs('trip-store.js');

        expect(logbookSource).toContain(`'${CATCH_DATABASE_NAME}'`);
        expect(catchSource).not.toContain(`'${CATCH_DATABASE_NAME}'`);
        expect(tripSource).not.toContain(`'${CATCH_DATABASE_NAME}'`);
    });

    it('does not let the preference store modify the Catch schema', async () => {
        await putCatch('catch-1');
        await putFishingPreferences(
            '11111111-1111-1111-1111-111111111111',
            JSON.stringify({ weightUnit: 1 }));

        const catchDb = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });
        const preferenceDb = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(PREFERENCE_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });

        expect(catchDb.version).toBe(CATCH_DATABASE_VERSION);
        expect([...catchDb.objectStoreNames].sort())
            .toEqual([CATCH_STORE_NAME, PHOTO_STORE_NAME, TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME].sort());
        expect(catchDb.objectStoreNames.contains(PREFERENCE_STORE_NAME)).toBe(false);
        expect([...preferenceDb.objectStoreNames]).toEqual([PREFERENCE_STORE_NAME]);
        expect(preferenceDb.objectStoreNames.contains(CATCH_STORE_NAME)).toBe(false);
        catchDb.close();
        preferenceDb.close();

        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        expect(JSON.parse(catches[0].json).notes).toBe('keep');
    });

    it('does not let the diagnostic store modify the Catch schema', async () => {
        await putCatch('catch-1');
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
        expect(catchDb.objectStoreNames.contains(DIAGNOSTIC_STORE_NAME)).toBe(false);
        expect(diagnosticDb.objectStoreNames.contains(CATCH_STORE_NAME)).toBe(false);
        expect(diagnosticDb.objectStoreNames.contains(DIAGNOSTIC_STORE_NAME)).toBe(true);

        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        expect(JSON.parse(catches[0].json).notes).toBe('keep');
        catchDb.close();
        diagnosticDb.close();
    });

    it('creates a deterministic Catch schema on a fresh database', async () => {
        await putCatch('catch-1');
        const db = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });

        expect([...db.objectStoreNames].sort())
            .toEqual([CATCH_STORE_NAME, PHOTO_STORE_NAME, TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME].sort());
        db.close();
    });

    it('removes the obsolete TestCatch stores when upgrading an existing v3 database', async () => {
        const legacy = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME, 3);
            request.onupgradeneeded = () => {
                const db = request.result;
                db.createObjectStore('testCatches', { keyPath: 'id' });
                db.createObjectStore('testCatchPhotographs', { keyPath: 'id' });
                db.createObjectStore(CATCH_STORE_NAME, { keyPath: 'id' });
                db.createObjectStore(PHOTO_STORE_NAME, { keyPath: 'id' });
            };
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });
        legacy.close();

        await putCatch('catch-1');

        const upgraded = await new Promise((resolveOpen, reject) => {
            const request = indexedDB.open(CATCH_DATABASE_NAME);
            request.onsuccess = () => resolveOpen(request.result);
            request.onerror = () => reject(request.error);
        });

        expect(upgraded.version).toBe(CATCH_DATABASE_VERSION);
        expect([...upgraded.objectStoreNames].sort())
            .toEqual([CATCH_STORE_NAME, PHOTO_STORE_NAME, TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME].sort());
        expect(upgraded.objectStoreNames.contains('testCatches')).toBe(false);
        expect(upgraded.objectStoreNames.contains('testCatchPhotographs')).toBe(false);
        upgraded.close();

        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        expect(JSON.parse(catches[0].json).notes).toBe('keep');
    });
});
