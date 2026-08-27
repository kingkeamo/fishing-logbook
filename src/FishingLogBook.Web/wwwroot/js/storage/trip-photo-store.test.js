import { describe, expect, it } from 'vitest';
import {
    deleteTripPhotograph,
    getPendingTripPhotographs,
    getTripPhotographBytes,
    getTripsWithPendingPhotographs,
    putTripPhotograph
} from './trip-photo-store.js';
import { getTrip, putTrip } from './trip-store.js';
import { getAllCatchesWithPhotographs, putCatchWithPhotographs } from './catch-store.js';

describe('Trip photograph store', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';
    const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const photographId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
    const secondPhotographId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
    const startedOn = '2026-08-26T05:32:00+00:00';
    const addedOn = '2026-08-26T06:00:00+00:00';

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
            ...overrides
        };
    }

    function photograph(overrides = {}) {
        return {
            id: photographId,
            tripId,
            ownerUserId,
            contentType: 'image/jpeg',
            addedOn,
            capturedOn: null,
            objectKey: null,
            syncStatus: 'savedLocally',
            syncedAt: null,
            ...overrides
        };
    }

    async function seedTrip(overrides = {}) {
        return putTrip(JSON.stringify(trip(overrides)));
    }

    async function save(record, bytes = new Uint8Array([1, 2, 3])) {
        return putTripPhotograph(JSON.stringify(record), bytes);
    }

    async function storedTripPhotographs() {
        const stored = await getTrip(ownerUserId, tripId);
        return stored === null ? null : JSON.parse(stored.json).photographs ?? [];
    }

    it('rejects a photograph with no owner', async () => {
        await seedTrip();

        await expect(save(photograph({ ownerUserId: '' })))
            .rejects.toThrow('Owned Trip photograph id is required');
    });

    it('rejects a photograph with no bytes', async () => {
        await seedTrip();

        await expect(putTripPhotograph(JSON.stringify(photograph()), new Uint8Array()))
            .rejects.toThrow('Trip photograph bytes are required');
    });

    it('rejects a photograph for a trip that is not stored', async () => {
        await expect(save(photograph()))
            .rejects.toThrow('Trip photograph must belong to an owned Trip');
    });

    it('rejects a photograph for another anglers trip', async () => {
        await seedTrip({ ownerUserId: otherUserId });

        await expect(save(photograph()))
            .rejects.toThrow('Trip photograph must belong to an owned Trip');
    });

    it('carries the photograph metadata on the trip and the bytes in its own store', async () => {
        await seedTrip();

        await save(photograph({ capturedOn: startedOn }));

        const metadata = await storedTripPhotographs();
        expect(metadata).toHaveLength(1);
        expect(metadata[0].id).toBe(photographId);
        expect(metadata[0].capturedOn).toBe(startedOn);
        expect(metadata[0].bytes).toBeUndefined();
        const bytes = await getTripPhotographBytes(ownerUserId, tripId, photographId);
        expect(Array.from(bytes)).toEqual([1, 2, 3]);
    });

    it('does not return bytes to another angler', async () => {
        await seedTrip();
        await save(photograph());

        expect(await getTripPhotographBytes(otherUserId, tripId, photographId)).toBeNull();
    });

    it('returns nothing for a photograph the trip does not list', async () => {
        await seedTrip();

        expect(await getTripPhotographBytes(ownerUserId, tripId, photographId)).toBeNull();
    });

    it('orders photographs by capture time then added time', async () => {
        await seedTrip();
        await save(photograph({ id: secondPhotographId, capturedOn: null, addedOn }));
        await save(photograph({ capturedOn: startedOn }));

        const metadata = await storedTripPhotographs();
        expect(metadata.map(entry => entry.id)).toEqual([photographId, secondPhotographId]);
    });

    it('replaces a photograph on the same id rather than duplicating it', async () => {
        await seedTrip();
        await save(photograph());

        await save(photograph({ syncStatus: 'synchronised', objectKey: 'trips/a/b/c' }));

        const metadata = await storedTripPhotographs();
        expect(metadata).toHaveLength(1);
        expect(metadata[0].syncStatus).toBe('synchronised');
        expect(metadata[0].objectKey).toBe('trips/a/b/c');
    });

    it('lists only this anglers pending photographs', async () => {
        await seedTrip();
        await save(photograph());
        await save(photograph({ id: secondPhotographId, syncStatus: 'synchronised' }));

        const pending = await getPendingTripPhotographs(ownerUserId);

        expect(pending).toHaveLength(1);
        expect(JSON.parse(pending[0].json).id).toBe(photographId);
        expect(await getPendingTripPhotographs(otherUserId)).toEqual([]);
    });

    it('reports the trips holding pending photographs', async () => {
        await seedTrip();
        await save(photograph());

        expect(await getTripsWithPendingPhotographs(ownerUserId)).toEqual([tripId]);

        await save(photograph({ syncStatus: 'synchronised' }));

        expect(await getTripsWithPendingPhotographs(ownerUserId)).toEqual([]);
    });

    it('removes the metadata and the bytes together', async () => {
        await seedTrip();
        await save(photograph());
        await save(photograph({ id: secondPhotographId }));

        expect(await deleteTripPhotograph(ownerUserId, tripId, photographId)).toBe(true);

        const metadata = await storedTripPhotographs();
        expect(metadata.map(entry => entry.id)).toEqual([secondPhotographId]);
        expect(await getTripPhotographBytes(ownerUserId, tripId, photographId)).toBeNull();
    });

    it('does not remove another anglers photograph', async () => {
        await seedTrip({ ownerUserId: otherUserId });
        await putTripPhotograph(
            JSON.stringify(photograph({ ownerUserId: otherUserId })),
            new Uint8Array([1, 2, 3]));

        expect(await deleteTripPhotograph(ownerUserId, tripId, photographId)).toBe(false);
        expect(await getTripPhotographBytes(otherUserId, tripId, photographId)).not.toBeNull();
    });

    it('leaves stored catches and their photographs untouched', async () => {
        await putCatchWithPhotographs(
            JSON.stringify({
                id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
                userId: ownerUserId,
                notes: 'keep'
            }),
            [{
                id: 'catch-photo-1',
                catchId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
                contentType: 'image/jpeg',
                bytes: new Uint8Array([7, 7, 7])
            }]);
        await seedTrip();

        await save(photograph());
        await deleteTripPhotograph(ownerUserId, tripId, photographId);

        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        expect(catches).toHaveLength(1);
        expect(JSON.parse(catches[0].json).notes).toBe('keep');
        expect(catches[0].photographs).toHaveLength(1);
        expect(catches[0].photographs[0].id).toBe('catch-photo-1');
        expect(catches[0].photographs[0].contentType).toBe('image/jpeg');
    });
});
