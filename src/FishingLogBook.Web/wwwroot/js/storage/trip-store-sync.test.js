import { describe, expect, it } from 'vitest';
import {
    cleanupSyncedTrips,
    getPendingTrips,
    getTrip,
    getTrips,
    putTrip
} from './trip-store.js';
import { getAllCatchesWithPhotographs, putCatchWithPhotographs } from './catch-store.js';

describe('Trip store synchronisation support', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';
    const startedOn = '2026-08-26T05:32:00+00:00';
    const longAgo = '2026-08-20T05:32:00+00:00';
    const recently = '2026-08-26T05:00:00+00:00';
    const cutoff = '2026-08-25T05:32:00+00:00';

    function trip(overrides = {}) {
        return {
            id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
            ownerUserId,
            status: 'Active',
            startedOn,
            endedOn: null,
            title: null,
            placeName: null,
            location: null,
            syncStatus: 'savedLocally',
            syncedAt: null,
            ...overrides
        };
    }

    function syncedCompletedTrip(overrides = {}) {
        return trip({
            status: 'Completed',
            endedOn: startedOn,
            syncStatus: 'synchronised',
            syncedAt: longAgo,
            ...overrides
        });
    }

    async function save(record) {
        return putTrip(JSON.stringify(record));
    }

    describe('getPendingTrips', () => {
        it('returns nothing when the owner is unknown', async () => {
            await save(trip());

            expect(await getPendingTrips('')).toEqual([]);
            expect(await getPendingTrips(null)).toEqual([]);
            expect(await getPendingTrips('00000000-0000-0000-0000-000000000000')).toEqual([]);
        });

        it('excludes trips that have already synchronised', async () => {
            await save(syncedCompletedTrip());

            expect(await getPendingTrips(ownerUserId)).toEqual([]);
        });

        it('excludes trips synchronised under a numeric status', async () => {
            await save(syncedCompletedTrip({ syncStatus: 3 }));

            expect(await getPendingTrips(ownerUserId)).toEqual([]);
        });

        it('excludes another angler trips', async () => {
            await save(trip({ ownerUserId: otherUserId }));

            expect(await getPendingTrips(ownerUserId)).toEqual([]);
        });

        it('excludes a trip that permanently failed to synchronise', async () => {
            await save(trip({ syncStatus: 'failedToSynchronise' }));

            expect(await getPendingTrips(ownerUserId)).toEqual([]);
        });

        it('excludes a trip permanently failed under a numeric status', async () => {
            await save(trip({ syncStatus: 4 }));

            expect(await getPendingTrips(ownerUserId)).toEqual([]);
        });

        it('returns every locally saved trip for the owner', async () => {
            await save(trip());
            await save(trip({ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', status: 'Completed', endedOn: startedOn }));
            await save(syncedCompletedTrip({ id: 'cccccccc-cccc-cccc-cccc-cccccccccccc' }));
            await save(trip({ id: 'dddddddd-dddd-dddd-dddd-dddddddddddd', ownerUserId: otherUserId }));

            const pending = await getPendingTrips(ownerUserId);

            expect(pending).toHaveLength(2);
            expect(pending.map(entry => JSON.parse(entry.json).id).sort()).toEqual([
                'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
            ]);
        });
    });

    describe('cleanupSyncedTrips', () => {
        it('removes nothing when the owner is unknown', async () => {
            await save(syncedCompletedTrip());

            expect(await cleanupSyncedTrips('', cutoff)).toBe(0);
            expect(await getTrips(ownerUserId)).toHaveLength(1);
        });

        it('removes nothing when the cutoff is not a date', async () => {
            await save(syncedCompletedTrip());

            expect(await cleanupSyncedTrips(ownerUserId, 'not-a-date')).toBe(0);
            expect(await getTrips(ownerUserId)).toHaveLength(1);
        });

        it('keeps an active trip however long ago it synchronised', async () => {
            await save(trip({ syncStatus: 'synchronised', syncedAt: longAgo }));

            expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(0);
            expect(await getTrips(ownerUserId)).toHaveLength(1);
        });

        it('keeps a completed trip that has not synchronised', async () => {
            await save(trip({ status: 'Completed', endedOn: startedOn, syncedAt: longAgo }));

            expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(0);
            expect(await getTrips(ownerUserId)).toHaveLength(1);
        });

        it('keeps a trip synchronised inside the retention window', async () => {
            await save(syncedCompletedTrip({ syncedAt: recently }));

            expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(0);
            expect(await getTrips(ownerUserId)).toHaveLength(1);
        });

        it('keeps another angler eligible trip', async () => {
            await save(syncedCompletedTrip({ ownerUserId: otherUserId }));

            expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(0);
            expect(await getTrips(otherUserId)).toHaveLength(1);
        });

        it('removes only the eligible trips and reports how many', async () => {
            await save(syncedCompletedTrip());
            await save(syncedCompletedTrip({ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }));
            await save(syncedCompletedTrip({ id: 'cccccccc-cccc-cccc-cccc-cccccccccccc', syncedAt: recently }));
            await save(trip({ id: 'dddddddd-dddd-dddd-dddd-dddddddddddd' }));

            expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(2);

            const remaining = await getTrips(ownerUserId);
            expect(remaining.map(entry => JSON.parse(entry.json).id).sort()).toEqual([
                'cccccccc-cccc-cccc-cccc-cccccccccccc',
                'dddddddd-dddd-dddd-dddd-dddddddddddd'
            ]);
            expect(await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')).toBeNull();
        });

        it('does not disturb stored catches', async () => {
            await putCatchWithPhotographs(
                JSON.stringify({ id: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', userId: ownerUserId, notes: 'keep' }),
                [{
                    id: 'photo-1',
                    catchId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
                    contentType: 'image/jpeg',
                    bytes: new Uint8Array([1, 2, 3])
                }]);
            await save(syncedCompletedTrip());

            expect(await cleanupSyncedTrips(ownerUserId, cutoff)).toBe(1);

            const catches = await getAllCatchesWithPhotographs(ownerUserId);
            expect(catches).toHaveLength(1);
            expect(JSON.parse(catches[0].json).notes).toBe('keep');
            expect(catches[0].photographs).toHaveLength(1);
        });
    });
});
