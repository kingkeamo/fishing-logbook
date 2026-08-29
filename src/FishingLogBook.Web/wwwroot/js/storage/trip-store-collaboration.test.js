import { describe, expect, it } from 'vitest';
import {
    TRIP_SAVED_OUTCOME,
    getActiveTrip,
    getPendingTrips,
    getTrip,
    getTrips,
    hydrateTrip,
    putTrip
} from './trip-store.js';
import {
    deleteTripNote,
    getPendingTripNotes,
    getTripNotes,
    getTripsWithPendingNotes,
    putTripNote
} from './trip-note-store.js';
import {
    deleteTripPhotograph,
    getPendingTripPhotographs,
    getTripPhotographBytes,
    putTripPhotograph
} from './trip-photo-store.js';
import { getAllCatchesWithPhotographs, putCatchWithPhotographs, updateCatchTrip } from './catch-store.js';

describe('Shared trip collaboration store', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const participantUserId = '22222222-2222-2222-2222-222222222222';
    const strangerUserId = '33333333-3333-3333-3333-333333333333';
    const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const noteId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
    const photographId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
    const catchId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
    const startedOn = '2026-08-26T05:32:00+00:00';
    const recordedOn = '2026-08-26T06:12:00+00:00';

    function sharedTrip(overrides = {}) {
        return {
            id: tripId,
            ownerUserId,
            status: 'Active',
            startedOn,
            endedOn: null,
            title: null,
            placeName: 'Lough Corrib',
            location: null,
            syncStatus: 'synchronised',
            syncedAt: startedOn,
            photographs: [],
            notes: [],
            participantUserIds: [participantUserId],
            origin: 'server',
            ...overrides
        };
    }

    function ownedTrip(overrides = {}) {
        return {
            id: tripId,
            ownerUserId: participantUserId,
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
            participantUserIds: [],
            origin: 'local',
            ...overrides
        };
    }

    function note(overrides = {}) {
        return {
            id: noteId,
            tripId,
            createdByUserId: participantUserId,
            text: 'fish moving on the shallows',
            recordedOn,
            syncStatus: 'savedLocally',
            syncedAt: null,
            ...overrides
        };
    }

    function photograph(overrides = {}) {
        return {
            id: photographId,
            tripId,
            contributedByUserId: participantUserId,
            contentType: 'image/jpeg',
            addedOn: recordedOn,
            capturedOn: null,
            objectKey: null,
            syncStatus: 'savedLocally',
            syncedAt: null,
            ...overrides
        };
    }

    async function hydrateShared(viewerUserId = participantUserId, overrides = {}) {
        return hydrateTrip(JSON.stringify(sharedTrip(overrides)), viewerUserId);
    }

    it('rejects hydrating a trip for an angler who is not on it', async () => {
        await expect(hydrateShared(strangerUserId))
            .rejects.toThrow('Shared Trip is not writable by this angler');
        expect(await getTrip(strangerUserId, tripId)).toBeNull();
    });

    it('makes a hydrated shared trip readable by the accepted participant', async () => {
        expect(await hydrateShared()).toBe(TRIP_SAVED_OUTCOME);

        const stored = await getTrip(participantUserId, tripId);

        expect(stored).not.toBeNull();
        expect(JSON.parse(stored.json).id).toBe(tripId);
        expect(JSON.parse(stored.json).ownerUserId).toBe(ownerUserId);
        expect(JSON.parse(stored.json).origin).toBe('server');
    });

    it('keeps a hydrated shared trip unreadable by a non-participant', async () => {
        await hydrateShared();

        expect(await getTrip(strangerUserId, tripId)).toBeNull();
        expect(await getTrips(strangerUserId)).toEqual([]);
    });

    it('lists the shared trip under the same id for owner and participant', async () => {
        await hydrateShared();

        const forParticipant = await getTrips(participantUserId);
        const forOwner = await getTrips(ownerUserId);

        expect(forParticipant).toHaveLength(1);
        expect(forOwner).toHaveLength(1);
        expect(JSON.parse(forParticipant[0].json).id).toBe(JSON.parse(forOwner[0].json).id);
    });

    it('never treats a hydrated shared trip as the participants own active trip', async () => {
        await hydrateShared();

        expect(await getActiveTrip(participantUserId)).toBeNull();
    });

    it('never queues a hydrated shared trip for the trip upsert outbox', async () => {
        await hydrateShared(participantUserId, { syncStatus: 'savedLocally', syncedAt: null });

        expect(await getPendingTrips(participantUserId)).toEqual([]);
        expect(await getPendingTrips(ownerUserId)).toEqual([]);
    });

    it('refuses to overwrite a hydrated shared trip through the owned trip path', async () => {
        await hydrateShared();

        await expect(putTrip(JSON.stringify(sharedTrip({ title: 'renamed by a participant' }))))
            .rejects.toThrow('A shared Trip cannot be written as a locally owned Trip');
    });

    it('keeps a locally created trip in the outbox', async () => {
        expect(await putTrip(JSON.stringify(ownedTrip()))).toBe(TRIP_SAVED_OUTCOME);

        const pending = await getPendingTrips(participantUserId);

        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0].json).origin).toBe('local');
    });

    it('accepts a participant note on the hydrated shared trip', async () => {
        await hydrateShared();

        await expect(putTripNote(JSON.stringify(note()))).resolves.toBe(true);

        const notes = await getTripNotes(participantUserId, tripId);
        expect(notes).toHaveLength(1);
        expect(JSON.parse(notes[0].json).createdByUserId).toBe(participantUserId);
    });

    it('rejects a note from an angler who is not on the shared trip', async () => {
        await hydrateShared();

        await expect(putTripNote(JSON.stringify(note({ createdByUserId: strangerUserId }))))
            .rejects.toThrow('Trip note must belong to a writable Trip');
    });

    it('queues only the participants own pending notes for synchronisation', async () => {
        await hydrateShared(participantUserId, {
            notes: [
                {
                    id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
                    tripId,
                    createdByUserId: ownerUserId,
                    text: 'owner note already on the server',
                    recordedOn,
                    syncStatus: 'synchronised',
                    syncedAt: recordedOn
                }
            ]
        });
        await putTripNote(JSON.stringify(note()));

        const pending = await getPendingTripNotes(participantUserId);

        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0].json).id).toBe(noteId);
        expect(await getTripsWithPendingNotes(participantUserId)).toEqual([tripId]);
    });

    it('shows every contributors notes in one shared timeline', async () => {
        await hydrateShared(participantUserId, {
            notes: [
                {
                    id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
                    tripId,
                    createdByUserId: ownerUserId,
                    text: 'owner note',
                    recordedOn: startedOn,
                    syncStatus: 'synchronised',
                    syncedAt: startedOn
                }
            ]
        });
        await putTripNote(JSON.stringify(note()));

        const notes = await getTripNotes(participantUserId, tripId);

        expect(notes.map(entry => JSON.parse(entry.json).createdByUserId))
            .toEqual([ownerUserId, participantUserId]);
    });

    it('refuses to delete another contributors note', async () => {
        await hydrateShared(participantUserId, {
            notes: [
                {
                    id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
                    tripId,
                    createdByUserId: ownerUserId,
                    text: 'owner note',
                    recordedOn,
                    syncStatus: 'synchronised',
                    syncedAt: recordedOn
                }
            ]
        });

        const removed = await deleteTripNote(
            participantUserId,
            tripId,
            'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee');

        expect(removed).toBe(false);
        expect(await getTripNotes(participantUserId, tripId)).toHaveLength(1);
    });

    it('accepts a participant photograph on the hydrated shared trip', async () => {
        await hydrateShared();

        await expect(putTripPhotograph(JSON.stringify(photograph()), new Uint8Array([1, 2, 3])))
            .resolves.toBe(true);

        const bytes = await getTripPhotographBytes(participantUserId, tripId, photographId);
        expect(Array.from(bytes)).toEqual([1, 2, 3]);
    });

    it('rejects a photograph from an angler who is not on the shared trip', async () => {
        await hydrateShared();

        await expect(putTripPhotograph(
            JSON.stringify(photograph({ contributedByUserId: strangerUserId })),
            new Uint8Array([1])))
            .rejects.toThrow('Trip photograph must belong to a writable Trip');
    });

    it('queues only the participants own pending photographs for synchronisation', async () => {
        await hydrateShared();
        await putTripPhotograph(JSON.stringify(photograph()), new Uint8Array([9]));

        const pending = await getPendingTripPhotographs(participantUserId);

        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0].json).contributedByUserId).toBe(participantUserId);
        expect(await getPendingTripPhotographs(ownerUserId)).toEqual([]);
    });

    it('refuses to delete another contributors photograph', async () => {
        await hydrateShared();
        await putTripPhotograph(
            JSON.stringify(photograph({ contributedByUserId: ownerUserId })),
            new Uint8Array([7]));

        const removed = await deleteTripPhotograph(participantUserId, tripId, photographId);

        expect(removed).toBe(false);
    });

    it('attaches a participants own catch to the same shared trip id', async () => {
        await hydrateShared();
        await putCatchWithPhotographs(JSON.stringify({
            id: catchId,
            userId: participantUserId,
            anglerUserId: participantUserId,
            recordedByUserId: participantUserId,
            caughtOn: recordedOn,
            speciesName: 'Pike',
            syncStatus: 'savedLocally',
            metadataSyncStatus: 'savedLocally'
        }), [
            {
                id: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
                catchId,
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1, 2, 3])
            }
        ]);

        await updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: participantUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }));

        const catches = await getAllCatchesWithPhotographs(participantUserId);
        expect(catches).toHaveLength(1);
        expect(JSON.parse(catches[0].json).tripId).toBe(tripId);
    });

    it('keeps the participants pending contributions when the shared trip is re-hydrated', async () => {
        await hydrateShared();
        await putTripNote(JSON.stringify(note()));

        await hydrateShared(participantUserId, {
            notes: [
                {
                    id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
                    tripId,
                    createdByUserId: ownerUserId,
                    text: 'owner note arrived from the server',
                    recordedOn: startedOn,
                    syncStatus: 'synchronised',
                    syncedAt: startedOn
                }
            ]
        });

        const notes = await getTripNotes(participantUserId, tripId);
        expect(notes).toHaveLength(2);
        expect(notes.map(entry => JSON.parse(entry.json).id)).toContain(noteId);
    });

    it('never replaces a locally owned trip with a hydrated copy', async () => {
        await putTrip(JSON.stringify(ownedTrip({ title: 'my own trip' })));

        await hydrateTrip(
            JSON.stringify(sharedTrip({
                ownerUserId: participantUserId,
                participantUserIds: [],
                title: 'server copy'
            })),
            participantUserId);

        const stored = JSON.parse((await getTrip(participantUserId, tripId)).json);
        expect(stored.title).toBe('my own trip');
        expect(stored.origin).toBe('local');
    });
});
