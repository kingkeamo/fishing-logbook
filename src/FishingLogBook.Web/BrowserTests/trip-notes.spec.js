import { expect, test } from '@playwright/test';

const tripStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-store.js';
const tripNoteStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-note-store.js';
const tripPhotoStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/trip-photo-store.js';
const catchStoreModule = '/src/FishingLogBook.Web/wwwroot/js/storage/catch-store.js';
const logbookModule = '/src/FishingLogBook.Web/wwwroot/js/storage/logbook-database.js';
const databaseName = 'FishingLogBook';
const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const seededCatchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const seededPhotographId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const tripPhotographId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
const firstNoteId = 'aaaaaaaa-1111-1111-1111-111111111111';
const secondNoteId = 'aaaaaaaa-2222-2222-2222-222222222222';
const startedOn = '2026-08-26T05:32:00+00:00';

test.beforeEach(async ({ page }) => {
    await page.goto('/src/FishingLogBook.Web/BrowserTests/harness/index.html');
    await page.evaluate((name) => new Promise((resolve) => {
        const request = indexedDB.deleteDatabase(name);
        request.onsuccess = () => resolve();
        request.onerror = () => resolve();
        request.onblocked = () => resolve();
    }), databaseName);
});

async function seedVersionSix(page) {
    return page.evaluate(async (input) => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(input.databaseName, 6);
            request.onupgradeneeded = () => {
                const upgrading = request.result;
                upgrading.createObjectStore('catches', { keyPath: 'id' });
                upgrading.createObjectStore('catchPhotographs', { keyPath: 'id' });
                upgrading.createObjectStore('trips', { keyPath: 'id' });
                upgrading.createObjectStore('tripPhotographs', { keyPath: 'id' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        await new Promise((resolve, reject) => {
            const stores = ['catches', 'catchPhotographs', 'trips', 'tripPhotographs'];
            const transaction = db.transaction(stores, 'readwrite');
            transaction.objectStore('catches').put({
                id: input.seededCatchId,
                userId: input.ownerUserId,
                caughtOn: '2026-06-14T09:48:00+00:00',
                notes: 'a catch note recorded before trip notes existed',
                syncStatus: 'savedLocally',
                metadataSyncStatus: 'savedLocally',
                tripId: input.tripId,
                photographs: [{
                    id: input.seededPhotographId,
                    catchId: input.seededCatchId,
                    contentType: 'image/jpeg'
                }]
            });
            transaction.objectStore('catchPhotographs').put({
                id: input.seededPhotographId,
                catchId: input.seededCatchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array(64 * 1024).fill(7).buffer
            });
            transaction.objectStore('trips').put({
                id: input.tripId,
                ownerUserId: input.ownerUserId,
                status: 'Active',
                startedOn: input.startedOn,
                endedOn: null,
                title: 'before trip notes',
                placeName: null,
                location: null,
                syncStatus: 'savedLocally',
                syncedAt: null,
                photographs: [{
                    id: input.tripPhotographId,
                    tripId: input.tripId,
                    ownerUserId: input.ownerUserId,
                    contentType: 'image/jpeg',
                    addedOn: input.startedOn,
                    capturedOn: null,
                    objectKey: null,
                    syncStatus: 'savedLocally',
                    syncedAt: null
                }]
            });
            transaction.objectStore('tripPhotographs').put({
                id: input.tripPhotographId,
                bytes: new Uint8Array(32 * 1024).fill(3)
            });
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        });

        const version = db.version;
        const stores = [...db.objectStoreNames].sort();
        db.close();
        return { version, stores };
    }, {
        databaseName,
        seededCatchId,
        seededPhotographId,
        ownerUserId,
        tripId,
        tripPhotographId,
        startedOn
    });
}

async function openThroughTheApp(page) {
    return page.evaluate(async ({ logbookModule }) => {
        const logbook = await import(logbookModule);
        const db = await logbook.openLogbookDatabase();
        const result = { version: db.version, stores: [...db.objectStoreNames].sort() };
        db.close();
        return result;
    }, { logbookModule });
}

async function addNote(page, noteId, text, recordedOn) {
    return page.evaluate(async (input) => {
        const store = await import(input.tripNoteStoreModule);
        return store.putTripNote(JSON.stringify({
            id: input.noteId,
            tripId: input.tripId,
            ownerUserId: input.ownerUserId,
            text: input.text,
            recordedOn: input.recordedOn,
            syncStatus: 'savedLocally',
            syncedAt: null
        }));
    }, { tripNoteStoreModule, noteId, tripId, ownerUserId, text, recordedOn });
}

async function readTripNotes(page, owner = ownerUserId) {
    return page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        const stored = await trips.getTrip(input.owner, input.tripId);
        return stored === null ? null : JSON.parse(stored.json).notes ?? [];
    }, { tripStoreModule, owner, tripId });
}

test('adding notes needs no database version change', async ({ page }) => {
    const seeded = await seedVersionSix(page);
    expect(seeded.version).toBe(6);

    const opened = await openThroughTheApp(page);

    expect(opened.version).toBe(6);
    expect(opened.stores).toEqual(['catchPhotographs', 'catches', 'tripPhotographs', 'trips']);
    expect(opened.stores).not.toContain('tripNotes');
});

test('notes survive a full reload alongside everything already stored', async ({ page }) => {
    await seedVersionSix(page);
    await openThroughTheApp(page);
    await addNote(page, firstNoteId, 'water dropped about a foot', '2026-08-26T06:12:00+00:00');

    await page.reload();

    const notes = await readTripNotes(page);
    expect(notes).toHaveLength(1);
    expect(notes[0].text).toBe('water dropped about a foot');
    expect(notes[0].recordedOn).toBe('2026-08-26T06:12:00+00:00');

    const survivors = await page.evaluate(async (input) => {
        const catches = await import(input.catchStoreModule);
        const photographs = await import(input.tripPhotoStoreModule);
        const stored = await catches.getAllCatchesWithPhotographs(input.ownerUserId);
        const bytes = await photographs.getTripPhotographBytes(
            input.ownerUserId,
            input.tripId,
            input.tripPhotographId);
        return {
            catchNotes: JSON.parse(stored[0].json).notes,
            catchPhotographs: stored[0].photographs.length,
            tripPhotographBytes: bytes === null ? null : bytes.length ?? bytes.byteLength
        };
    }, { catchStoreModule, tripPhotoStoreModule, ownerUserId, tripId, tripPhotographId });

    expect(survivors.catchNotes).toBe('a catch note recorded before trip notes existed');
    expect(survivors.catchPhotographs).toBe(1);
    expect(survivors.tripPhotographBytes).toBe(32 * 1024);
});

test('several notes keep the order they were written in', async ({ page }) => {
    await seedVersionSix(page);
    await openThroughTheApp(page);
    await addNote(page, secondNoteId, 'wind picked up', '2026-08-26T11:16:00+00:00');
    await addNote(page, firstNoteId, 'fish rising near the reeds', '2026-08-26T06:10:00+00:00');

    const notes = await readTripNotes(page);

    expect(notes.map(note => note.text)).toEqual([
        'fish rising near the reeds',
        'wind picked up'
    ]);
});

test('notes are scoped to the owning angler', async ({ page }) => {
    await seedVersionSix(page);
    await openThroughTheApp(page);
    await addNote(page, firstNoteId, 'water dropped about a foot', '2026-08-26T06:12:00+00:00');

    expect(await readTripNotes(page, otherUserId)).toBeNull();

    const theirPending = await page.evaluate(async (input) => {
        const store = await import(input.tripNoteStoreModule);
        return store.getPendingTripNotes(input.otherUserId);
    }, { tripNoteStoreModule, otherUserId });
    expect(theirPending).toEqual([]);
});

test('a pending note is discoverable without reading any blob store', async ({ page }) => {
    await seedVersionSix(page);
    await openThroughTheApp(page);
    await addNote(page, firstNoteId, 'changed to olive nymph', '2026-08-26T07:00:00+00:00');

    const observed = await page.evaluate(async (input) => {
        const notes = await import(input.tripNoteStoreModule);
        const opened = [];
        const originalTransaction = IDBDatabase.prototype.transaction;
        IDBDatabase.prototype.transaction = function patched(storeNames, ...rest) {
            const names = Array.isArray(storeNames) ? storeNames : [storeNames];
            opened.push(...names);
            return originalTransaction.call(this, storeNames, ...rest);
        };
        try {
            const pending = await notes.getPendingTripNotes(input.ownerUserId);
            const tripIds = await notes.getTripsWithPendingNotes(input.ownerUserId);
            return { opened, pending: pending.length, tripIds };
        } finally {
            IDBDatabase.prototype.transaction = originalTransaction;
        }
    }, { tripNoteStoreModule, ownerUserId });

    expect(observed.pending).toBe(1);
    expect(observed.tripIds).toEqual([tripId]);
    expect(observed.opened).toContain('trips');
    expect(observed.opened).not.toContain('tripPhotographs');
    expect(observed.opened).not.toContain('catchPhotographs');
});

test('a pending note holds its trip back from retention cleanup', async ({ page }) => {
    await seedVersionSix(page);
    await openThroughTheApp(page);
    await addNote(page, firstNoteId, 'stopped for lunch', '2026-08-26T12:30:00+00:00');
    await page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        await trips.putTrip(JSON.stringify({
            id: input.tripId,
            ownerUserId: input.ownerUserId,
            status: 'Completed',
            startedOn: input.startedOn,
            endedOn: input.startedOn,
            title: 'before trip notes',
            placeName: null,
            location: null,
            syncStatus: 'synchronised',
            syncedAt: '2026-08-20T05:32:00+00:00'
        }));
    }, { tripStoreModule, tripId, ownerUserId, startedOn });

    const outcome = await page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        const notes = await import(input.tripNoteStoreModule);
        const retained = await notes.getTripsWithPendingNotes(input.ownerUserId);
        const removed = await trips.cleanupSyncedTrips(
            input.ownerUserId,
            '2026-08-25T05:32:00+00:00',
            retained);
        return { retained, removed, remaining: (await trips.getTrips(input.ownerUserId)).length };
    }, { tripStoreModule, tripNoteStoreModule, ownerUserId });

    expect(outcome.retained).toEqual([tripId]);
    expect(outcome.removed).toBe(0);
    expect(outcome.remaining).toBe(1);
    expect(await readTripNotes(page)).toHaveLength(1);
});

test('finishing a trip keeps the notes already written', async ({ page }) => {
    await seedVersionSix(page);
    await openThroughTheApp(page);
    await addNote(page, firstNoteId, 'fish rising near the reeds', '2026-08-26T06:10:00+00:00');

    await page.evaluate(async (input) => {
        const trips = await import(input.tripStoreModule);
        await trips.putTrip(JSON.stringify({
            id: input.tripId,
            ownerUserId: input.ownerUserId,
            status: 'Completed',
            startedOn: input.startedOn,
            endedOn: '2026-08-26T13:00:00+00:00',
            title: 'before trip notes',
            placeName: null,
            location: null,
            syncStatus: 'savedLocally',
            syncedAt: null,
            notes: []
        }));
    }, { tripStoreModule, tripId, ownerUserId, startedOn });

    const notes = await readTripNotes(page);
    expect(notes).toHaveLength(1);
    expect(notes[0].text).toBe('fish rising near the reeds');
});
