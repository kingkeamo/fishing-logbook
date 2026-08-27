import { describe, expect, it } from 'vitest';
import { cleanupSyncedTrips, getTrip, putTrip } from './trip-store.js';
import { getTripPhotographBytes, putTripPhotograph } from './trip-photo-store.js';

describe('Trip photograph ownership across trip writes', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const tripId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const photographId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
    const startedOn = '2026-08-26T05:32:00+00:00';
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
            ...overrides
        };
    }

    async function addPhotograph() {
        await putTripPhotograph(
            JSON.stringify({
                id: photographId,
                tripId,
                ownerUserId,
                contentType: 'image/jpeg',
                addedOn: startedOn,
                capturedOn: null,
                objectKey: null,
                syncStatus: 'savedLocally',
                syncedAt: null
            }),
            new Uint8Array([1, 2, 3]));
    }

    async function storedPhotographs() {
        const stored = await getTrip(ownerUserId, tripId);
        return stored === null ? null : JSON.parse(stored.json).photographs ?? [];
    }

    it('keeps the photographs when the trip is finished from a stale snapshot', async () => {
        await putTrip(JSON.stringify(trip()));
        await addPhotograph();

        await putTrip(JSON.stringify(trip({
            status: 'Completed',
            endedOn: startedOn,
            photographs: []
        })));

        const metadata = await storedPhotographs();
        expect(metadata).toHaveLength(1);
        expect(metadata[0].id).toBe(photographId);
        expect(await getTripPhotographBytes(ownerUserId, tripId, photographId)).not.toBeNull();
    });

    it('keeps the photographs when a synchronisation write omits them', async () => {
        await putTrip(JSON.stringify(trip()));
        await addPhotograph();

        const withoutPhotographs = trip({ syncStatus: 'synchronised', syncedAt: startedOn });
        delete withoutPhotographs.photographs;
        await putTrip(JSON.stringify(withoutPhotographs));

        expect(await storedPhotographs()).toHaveLength(1);
    });

    it('keeps the photographs of a brand new trip empty', async () => {
        await putTrip(JSON.stringify(trip()));

        expect(await storedPhotographs()).toEqual([]);
    });

    it('removes the photograph blobs when retention evicts the trip', async () => {
        await putTrip(JSON.stringify(trip()));
        await addPhotograph();
        await putTrip(JSON.stringify(trip({
            status: 'Completed',
            endedOn: startedOn,
            syncStatus: 'synchronised',
            syncedAt: longAgo
        })));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [])).toBe(1);

        expect(await getTrip(ownerUserId, tripId)).toBeNull();
        expect(await getTripPhotographBytes(ownerUserId, tripId, photographId)).toBeNull();
    });

    it('keeps the photograph blobs while the trip is retained', async () => {
        await putTrip(JSON.stringify(trip()));
        await addPhotograph();
        await putTrip(JSON.stringify(trip({
            status: 'Completed',
            endedOn: startedOn,
            syncStatus: 'synchronised',
            syncedAt: longAgo
        })));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [tripId])).toBe(0);

        expect(await getTripPhotographBytes(ownerUserId, tripId, photographId)).not.toBeNull();
    });
});
