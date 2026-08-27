import { describe, expect, it } from 'vitest';
import { cleanupSyncedTrips, getTrips, putTrip } from './trip-store.js';

describe('Trip retention with linked catches', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const startedOn = '2026-08-26T05:32:00+00:00';
    const longAgo = '2026-08-20T05:32:00+00:00';
    const cutoff = '2026-08-25T05:32:00+00:00';
    const firstTripId = 'aaaaaaaa-0000-0000-0000-000000000001';
    const secondTripId = 'aaaaaaaa-0000-0000-0000-000000000002';

    function syncedTrip(id) {
        return {
            id,
            ownerUserId,
            status: 'Completed',
            startedOn,
            endedOn: startedOn,
            title: null,
            placeName: null,
            location: null,
            syncStatus: 'synchronised',
            syncedAt: longAgo
        };
    }

    async function save(record) {
        return putTrip(JSON.stringify(record));
    }

    async function remainingIds() {
        const stored = await getTrips(ownerUserId);
        return stored.map(entry => JSON.parse(entry.json).id).sort();
    }

    it('retains a trip a pending catch still references', async () => {
        await save(syncedTrip(firstTripId));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [firstTripId])).toBe(0);
        expect(await remainingIds()).toEqual([firstTripId]);
    });

    it('still cleans up trips nothing references', async () => {
        await save(syncedTrip(firstTripId));
        await save(syncedTrip(secondTripId));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [firstTripId])).toBe(1);
        expect(await remainingIds()).toEqual([firstTripId]);
    });

    it('cleans the trip up once nothing references it', async () => {
        await save(syncedTrip(firstTripId));
        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [firstTripId])).toBe(0);

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [])).toBe(1);
        expect(await remainingIds()).toEqual([]);
    });

    it('matches retained ids whatever their casing', async () => {
        await save(syncedTrip(firstTripId));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, [firstTripId.toUpperCase()])).toBe(0);
        expect(await remainingIds()).toEqual([firstTripId]);
    });

    it('ignores blank retained ids', async () => {
        await save(syncedTrip(firstTripId));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff, ['', null, undefined])).toBe(1);
        expect(await remainingIds()).toEqual([]);
    });

    it('behaves as before when no retained ids are supplied', async () => {
        await save(syncedTrip(firstTripId));

        expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(1);
        expect(await remainingIds()).toEqual([]);
    });
});
