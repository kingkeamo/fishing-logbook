import { describe, expect, it } from 'vitest';
import {
    deleteTripNote,
    getPendingTripNotes,
    getTripsWithPendingNotes,
    putTripNote
} from './trip-note-store.js';
import { cleanupSyncedTrips, getTrip, putTrip } from './trip-store.js';
import { getTripPhotographBytes, putTripPhotograph } from './trip-photo-store.js';
import { getAllCatchesWithPhotographs, putCatchWithPhotographs } from './catch-store.js';

describe('Trip note store', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';
    const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const noteId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
    const secondNoteId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
    const startedOn = '2026-08-26T05:32:00+00:00';
    const recordedOn = '2026-08-26T06:12:00+00:00';
    const longAgo = '2026-08-20T05:32:00+00:00';
    const cutoff = '2026-08-25T05:32:00+00:00';

    function trip(overrides = {}) {
        return {
            id: tripId,
            ownerUserId,
            status: 'Active',
            startedOn,
            endedOn: null,
            title: null,
            placeName: null,
            location: null,
            syncStatus: 'savedLocally',
            syncedAt: null,
            photographs: [],
            notes: [],
            ...overrides
        };
    }

    function note(overrides = {}) {
        return {
            id: noteId,
            tripId,
            ownerUserId,
            text: 'water dropped about a foot',
            recordedOn,
            syncStatus: 'savedLocally',
            syncedAt: null,
            ...overrides
        };
    }

    async function seedTrip(overrides = {}) {
        return putTrip(JSON.stringify(trip(overrides)));
    }

    async function save(record) {
        return putTripNote(JSON.stringify(record));
    }

    async function storedNotes() {
        const stored = await getTrip(ownerUserId, tripId);
        return stored === null ? null : JSON.parse(stored.json).notes ?? [];
    }

    it('rejects a note with no owner', async () => {
        await seedTrip();

        await expect(save(note({ ownerUserId: '' })))
            .rejects.toThrow('Trip note author is required');
    });

    it('rejects a note with no text', async () => {
        await seedTrip();

        await expect(save(note({ text: '   ' })))
            .rejects.toThrow('Trip note text is required');
    });

    it('rejects a note for a trip that is not stored', async () => {
        await expect(save(note()))
            .rejects.toThrow('Trip note must belong to a writable Trip');
    });

    it('rejects a note for another anglers trip', async () => {
        await seedTrip({ ownerUserId: otherUserId });

        await expect(save(note()))
            .rejects.toThrow('Trip note must belong to a writable Trip');
    });

    it('round-trips the note on the trip metadata', async () => {
        await seedTrip();

        await save(note());

        const stored = await storedNotes();
        expect(stored).toHaveLength(1);
        expect(stored[0].id).toBe(noteId);
        expect(stored[0].text).toBe('water dropped about a foot');
        expect(stored[0].recordedOn).toBe(recordedOn);
    });

    it('orders notes by the instant they were written', async () => {
        await seedTrip();
        await save(note({ id: secondNoteId, text: 'later', recordedOn: '2026-08-26T09:00:00+00:00' }));
        await save(note({ text: 'earlier', recordedOn: '2026-08-26T06:00:00+00:00' }));

        const stored = await storedNotes();
        expect(stored.map(entry => entry.text)).toEqual(['earlier', 'later']);
    });

    it('replaces a note on the same id rather than duplicating it', async () => {
        await seedTrip();
        await save(note());

        await save(note({ syncStatus: 'synchronised', syncedAt: recordedOn }));

        const stored = await storedNotes();
        expect(stored).toHaveLength(1);
        expect(stored[0].syncStatus).toBe('synchronised');
    });

    it('lists only this anglers pending notes', async () => {
        await seedTrip();
        await save(note());
        await save(note({ id: secondNoteId, syncStatus: 'synchronised' }));

        const pending = await getPendingTripNotes(ownerUserId);

        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0].json).id).toBe(noteId);
        expect(await getPendingTripNotes(otherUserId)).toEqual([]);
    });

    it('reports the trips holding pending notes', async () => {
        await seedTrip();
        await save(note());

        expect(await getTripsWithPendingNotes(ownerUserId)).toEqual([tripId]);

        await save(note({ syncStatus: 'synchronised' }));

        expect(await getTripsWithPendingNotes(ownerUserId)).toEqual([]);
    });

    it('does not hold a trip open for a note that permanently failed to synchronise', async () => {
        await seedTrip();
        await save(note({ syncStatus: 'failedToSynchronise' }));

        expect(await getTripsWithPendingNotes(ownerUserId)).toEqual([]);
    });

    it('removes only the deleted note', async () => {
        await seedTrip();
        await save(note());
        await save(note({ id: secondNoteId, text: 'kept' }));

        expect(await deleteTripNote(ownerUserId, tripId, noteId)).toBe(true);

        const stored = await storedNotes();
        expect(stored.map(entry => entry.id)).toEqual([secondNoteId]);
    });

    it('does not remove another anglers note', async () => {
        await seedTrip({ ownerUserId: otherUserId });
        await putTripNote(JSON.stringify(note({ ownerUserId: otherUserId })));

        expect(await deleteTripNote(ownerUserId, tripId, noteId)).toBe(false);
        expect(await getPendingTripNotes(otherUserId)).toHaveLength(1);
    });

    it('keeps the notes when the trip is finished from a stale snapshot', async () => {
        await seedTrip();
        await save(note());

        await putTrip(JSON.stringify(trip({
            status: 'Completed',
            endedOn: startedOn,
            notes: []
        })));

        expect(await storedNotes()).toHaveLength(1);
    });

    it('keeps the notes when a synchronisation write omits them', async () => {
        await seedTrip();
        await save(note());

        const withoutNotes = trip({ syncStatus: 'synchronised', syncedAt: startedOn });
        delete withoutNotes.notes;
        await putTrip(JSON.stringify(withoutNotes));

        expect(await storedNotes()).toHaveLength(1);
    });

    it('keeps notes and photographs independent of each other', async () => {
        await seedTrip();
        await save(note());
        await putTripPhotograph(
            JSON.stringify({
                id: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
                tripId,
                ownerUserId,
                contentType: 'image/jpeg',
                addedOn: recordedOn,
                capturedOn: null,
                objectKey: null,
                syncStatus: 'savedLocally',
                syncedAt: null
            }),
            new Uint8Array([1, 2, 3]));

        await deleteTripNote(ownerUserId, tripId, noteId);

        expect(await storedNotes()).toEqual([]);
        const stored = await getTrip(ownerUserId, tripId);
        expect(JSON.parse(stored.json).photographs).toHaveLength(1);
        expect(await getTripPhotographBytes(
            ownerUserId,
            tripId,
            'ffffffff-ffff-ffff-ffff-ffffffffffff')).not.toBeNull();
    });

    it('removes the notes with the trip when retention evicts it', async () => {
        await seedTrip();
        await save(note());
        await putTrip(JSON.stringify(trip({
            status: 'Completed',
            endedOn: startedOn,
            syncStatus: 'synchronised',
            syncedAt: longAgo
        })));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [])).toBe(1);

        expect(await getTrip(ownerUserId, tripId)).toBeNull();
        expect(await getPendingTripNotes(ownerUserId)).toEqual([]);
    });

    it('leaves stored catches untouched', async () => {
        await putCatchWithPhotographs(
            JSON.stringify({
                id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
                userId: ownerUserId,
                notes: 'a catch note that is not a trip note'
            }),
            [{
                id: 'catch-photo-1',
                catchId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
                contentType: 'image/jpeg',
                bytes: new Uint8Array([7, 7, 7])
            }]);
        await seedTrip();

        await save(note());
        await deleteTripNote(ownerUserId, tripId, noteId);

        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        expect(catches).toHaveLength(1);
        expect(JSON.parse(catches[0].json).notes).toBe('a catch note that is not a trip note');
        expect(catches[0].photographs).toHaveLength(1);
    });
});
