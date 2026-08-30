import { describe, expect, it } from 'vitest';
import {
    TRIP_SAVED_OUTCOME,
    getActiveTrip,
    getPendingTrips,
    getTrip,
    getTrips,
    hydrateTrip,
    putTrip,
    revokeParticipantAccess
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

    it('surfaces a hydrated shared active trip without treating it as an owned trip', async () => {
        await hydrateShared();

        const active = await getActiveTrip(participantUserId);

        expect(JSON.parse(active.json).id).toBe(tripId);
        expect(JSON.parse(active.json).ownerUserId).toBe(ownerUserId);
        expect(await getPendingTrips(participantUserId)).toEqual([]);
    });

    it('revokes a removed participant\'s cached access to a server-origin shared trip', async () => {
        await hydrateShared();
        expect(await getTrip(participantUserId, tripId)).not.toBeNull();

        const revoked = await revokeParticipantAccess(participantUserId, tripId);

        expect(revoked).toBe(true);
        expect(await getTrip(participantUserId, tripId)).toBeNull();
        expect(await getActiveTrip(participantUserId)).toBeNull();
    });

    it('never removes the trip record itself when revoking stale participant access', async () => {
        await hydrateShared();

        await revokeParticipantAccess(participantUserId, tripId);

        // The record still exists for the owner - only the removed participant's own access
        // was revoked, the Trip and its data were not deleted.
        const forOwner = await getTrip(ownerUserId, tripId);
        expect(forOwner).not.toBeNull();
        expect(JSON.parse(forOwner.json).id).toBe(tripId);
    });

    it('does nothing when revoking access to a locally-created trip', async () => {
        expect(await putTrip(JSON.stringify(ownedTrip()))).toBe(TRIP_SAVED_OUTCOME);

        const revoked = await revokeParticipantAccess(participantUserId, tripId);

        expect(revoked).toBe(false);
        expect(await getTrip(participantUserId, tripId)).not.toBeNull();
    });

    it('does nothing when revoking access for the trip owner', async () => {
        await hydrateShared();

        const revoked = await revokeParticipantAccess(ownerUserId, tripId);

        expect(revoked).toBe(false);
        expect(await getTrip(ownerUserId, tripId)).not.toBeNull();
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

    it('keeps a pending trip note when the trip is hydrated again without it', async () => {
        await hydrateShared();
        await putTripNote(JSON.stringify(note({ syncStatus: 'waitingToSynchronise' })));

        // A later authoritative refresh carries only server-confirmed notes - N1 has not
        // synced yet, so the server has never heard of it.
        await hydrateShared(participantUserId, { notes: [] });

        const notes = await getTripNotes(participantUserId, tripId);
        expect(notes).toHaveLength(1);
        const stored = JSON.parse(notes[0].json);
        expect(stored.id).toBe(noteId);
        expect(stored.syncStatus).toBe('waitingToSynchronise');
    });

    it('keeps a pending trip photograph when the trip is hydrated again without it', async () => {
        await hydrateShared();
        await putTripPhotograph(
            JSON.stringify(photograph({ syncStatus: 'waitingToSynchronise' })),
            new Uint8Array([1, 2, 3]));

        // A later authoritative refresh carries only server-confirmed photographs - the
        // pending photograph has not synced yet, so the server has never heard of it.
        await hydrateShared(participantUserId, { photographs: [] });

        const bytes = await getTripPhotographBytes(participantUserId, tripId, photographId);
        expect(bytes).not.toBeNull();

        const pending = await getPendingTripPhotographs(participantUserId);
        expect(pending).toHaveLength(1);
        const stored = JSON.parse(pending[0].json);
        expect(stored.id).toBe(photographId);
        expect(stored.syncStatus).toBe('waitingToSynchronise');
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

describe('Shared trip active banner lookup', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const participantUserId = '22222222-2222-2222-2222-222222222222';
    const strangerUserId = '33333333-3333-3333-3333-333333333333';
    const sharedTripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const ownTripId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
    const startedOn = '2026-08-26T05:32:00+00:00';

    function shared(overrides = {}) {
        return {
            id: sharedTripId,
            ownerUserId,
            status: 'Active',
            startedOn,
            endedOn: null,
            title: null,
            placeName: null,
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

    function own(overrides = {}) {
        return {
            id: ownTripId,
            ownerUserId: participantUserId,
            status: 'Active',
            startedOn: '2026-08-26T09:00:00+00:00',
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

    it('gives a participant no active trip when the shared trip is finished', async () => {
        await hydrateTrip(JSON.stringify(shared({ status: 'Completed', endedOn: startedOn })), participantUserId);

        expect(await getActiveTrip(participantUserId)).toBeNull();
    });

    it('never surfaces a shared active trip to an angler who is not on it', async () => {
        await hydrateTrip(JSON.stringify(shared()), participantUserId);

        expect(await getActiveTrip(strangerUserId)).toBeNull();
    });

    it('surfaces the active shared trip to an accepted participant', async () => {
        await hydrateTrip(JSON.stringify(shared()), participantUserId);

        const active = await getActiveTrip(participantUserId);

        expect(active).not.toBeNull();
        expect(JSON.parse(active.json).id).toBe(sharedTripId);
        expect(JSON.parse(active.json).ownerUserId).toBe(ownerUserId);
    });

    it('prefers the anglers own active trip over a shared one', async () => {
        await hydrateTrip(JSON.stringify(shared()), participantUserId);
        await putTrip(JSON.stringify(own()));

        const active = await getActiveTrip(participantUserId);

        expect(JSON.parse(active.json).id).toBe(ownTripId);
    });

    it('still lets the owner start their own trip while sharing another', async () => {
        await hydrateTrip(JSON.stringify(shared()), participantUserId);

        const outcome = await putTrip(JSON.stringify(own()));

        expect(outcome).toBe(TRIP_SAVED_OUTCOME);
    });
});
