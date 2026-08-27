import { describe, expect, it } from 'vitest';
import {
    TRIP_ACTIVE_CONFLICT_OUTCOME,
    TRIP_SAVED_OUTCOME,
    getActiveTrip,
    getTrip,
    getTrips,
    putTrip
} from './trip-store.js';
import { getAllCatchesWithPhotographs, putCatchWithPhotographs } from './catch-store.js';

describe('Trip store', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';
    const startedOn = '2026-08-26T05:32:00+00:00';

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

    function fullLocation() {
        return {
            latitude: 53.4419,
            longitude: -9.2531,
            accuracyMetres: 8,
            capturedOn: startedOn,
            source: 'DeviceGps',
            visibility: 'Private',
            consentVersion: '1'
        };
    }

    async function save(record) {
        return putTrip(JSON.stringify(record));
    }

    it('rejects a trip with no id', async () => {
        await expect(save(trip({ id: '' }))).rejects.toThrow('Owned Trip id is required');
    });

    it('rejects a trip with no owner', async () => {
        await expect(save(trip({ ownerUserId: '' }))).rejects.toThrow('Owned Trip id is required');
    });

    it('rejects a trip whose owner is the empty guid', async () => {
        await expect(save(trip({ ownerUserId: '00000000-0000-0000-0000-000000000000' })))
            .rejects.toThrow('Owned Trip id is required');
    });

    it('round-trips a blank active trip', async () => {
        await save(trip());

        const stored = await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
        const parsed = JSON.parse(stored.json);

        expect(parsed.status).toBe('Active');
        expect(parsed.startedOn).toBe(startedOn);
        expect(parsed.endedOn).toBeNull();
        expect(parsed.title).toBeNull();
        expect(parsed.placeName).toBeNull();
        expect(parsed.location).toBeNull();
    });

    it('round-trips a completed trip with a title and place', async () => {
        await save(trip({
            status: 'Completed',
            endedOn: '2026-08-26T11:15:00+00:00',
            title: 'Day with Dad',
            placeName: 'Lough Corrib'
        }));

        const parsed = JSON.parse((await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')).json);

        expect(parsed.status).toBe('Completed');
        expect(parsed.endedOn).toBe('2026-08-26T11:15:00+00:00');
        expect(parsed.title).toBe('Day with Dad');
        expect(parsed.placeName).toBe('Lough Corrib');
    });

    it('round-trips a full location without losing provenance or privacy', async () => {
        await save(trip({ location: fullLocation() }));

        const parsed = JSON.parse((await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')).json);

        expect(parsed.location).toEqual(fullLocation());
    });

    it('keeps a null location null rather than inventing a partial one', async () => {
        await save(trip({ location: null }));

        const parsed = JSON.parse((await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')).json);

        expect(parsed.location).toBeNull();
    });

    it('does not return another owner trip by id', async () => {
        await save(trip({ ownerUserId: otherUserId }));

        const stored = await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');

        expect(stored).toBeNull();
    });

    it('returns null for a trip that is not stored', async () => {
        const stored = await getTrip(ownerUserId, 'dddddddd-dddd-dddd-dddd-dddddddddddd');

        expect(stored).toBeNull();
    });

    it('lists only the requested owner trips', async () => {
        await save(trip({ placeName: 'Lough Corrib' }));
        await save(trip({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
            ownerUserId: otherUserId,
            placeName: 'Lough Mask'
        }));

        const trips = await getTrips(ownerUserId);

        expect(trips).toHaveLength(1);
        expect(JSON.parse(trips[0].json).placeName).toBe('Lough Corrib');
    });

    it('treats an empty owner as no owner for a list read', async () => {
        await save(trip());

        const trips = await getTrips('00000000-0000-0000-0000-000000000000');

        expect(trips).toEqual([]);
    });

    it('finds the active trip for an owner', async () => {
        await save(trip({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
            status: 'Completed',
            endedOn: '2026-08-25T11:00:00+00:00'
        }));
        await save(trip());

        const active = await getActiveTrip(ownerUserId);

        expect(JSON.parse(active.json).id).toBe('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
    });

    it('finds no active trip when every trip is completed', async () => {
        await save(trip({ status: 'Completed', endedOn: '2026-08-26T11:00:00+00:00' }));

        const active = await getActiveTrip(ownerUserId);

        expect(active).toBeNull();
    });

    it('does not return another owner active trip', async () => {
        await save(trip({ ownerUserId: otherUserId }));

        const active = await getActiveTrip(ownerUserId);

        expect(active).toBeNull();
    });

    it('rejects a second distinct active trip for the same owner', async () => {
        await save(trip());

        const outcome = await save(trip({ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }));

        expect(outcome).toBe(TRIP_ACTIVE_CONFLICT_OUTCOME);
        expect(await getTrips(ownerUserId)).toHaveLength(1);
    });

    it('allows updating the same active trip', async () => {
        await save(trip());

        const outcome = await save(trip({ placeName: 'Lough Corrib' }));

        expect(outcome).toBe(TRIP_SAVED_OUTCOME);
        const parsed = JSON.parse((await getActiveTrip(ownerUserId)).json);
        expect(parsed.placeName).toBe('Lough Corrib');
    });

    it('allows a new active trip once the earlier one is completed', async () => {
        await save(trip());
        await save(trip({ status: 'Completed', endedOn: '2026-08-26T11:00:00+00:00' }));

        const outcome = await save(trip({ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }));

        expect(outcome).toBe(TRIP_SAVED_OUTCOME);
        expect(JSON.parse((await getActiveTrip(ownerUserId)).json).id)
            .toBe('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
    });

    it('allows each owner their own active trip', async () => {
        await save(trip());

        const outcome = await save(trip({
            id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
            ownerUserId: otherUserId
        }));

        expect(outcome).toBe(TRIP_SAVED_OUTCOME);
        expect(JSON.parse((await getActiveTrip(ownerUserId)).json).ownerUserId).toBe(ownerUserId);
        expect(JSON.parse((await getActiveTrip(otherUserId)).json).ownerUserId).toBe(otherUserId);
    });

    it('does not let a write reassign an existing trip to another owner', async () => {
        await save(trip());

        await expect(save(trip({ ownerUserId: otherUserId, status: 'Completed', endedOn: null })))
            .rejects.toThrow('Trip ownership cannot be changed');
        expect(JSON.parse((await getTrip(ownerUserId, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')).json).ownerUserId)
            .toBe(ownerUserId);
    });

    it('does not disturb stored catches', async () => {
        await putCatchWithPhotographs(
            JSON.stringify({ id: 'cccccccc-cccc-cccc-cccc-cccccccccccc', userId: ownerUserId, notes: 'keep' }),
            [{
                id: 'photo-1',
                catchId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
                contentType: 'image/jpeg',
                bytes: new Uint8Array([1, 2, 3])
            }]);

        await save(trip());

        const catches = await getAllCatchesWithPhotographs(ownerUserId);
        expect(catches).toHaveLength(1);
        expect(JSON.parse(catches[0].json).notes).toBe('keep');
        expect(catches[0].photographs).toHaveLength(1);
    });
});
