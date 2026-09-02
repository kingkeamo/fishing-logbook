import { describe, expect, it } from 'vitest';
import {
    CATCH_DATABASE_VERSION,
    getCatchMetadata,
    getCatchMetadataById,
    getCatchWithPhotographs,
    putCatchWithPhotographs,
    updateCatchMetadata,
    updateCatchTrip
} from './catch-store.js';

describe('Catch store trip association', () => {
    const ownerUserId = '11111111-1111-1111-1111-111111111111';
    const otherUserId = '22222222-2222-2222-2222-222222222222';
    const catchId = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
    const tripId = 'dddddddd-dddd-dddd-dddd-dddddddddddd';

    function photograph(id = 'photo-1') {
        return {
            id,
            catchId,
            contentType: 'image/jpeg',
            bytes: new Uint8Array(4096).fill(7)
        };
    }

    async function save(record, photographs = [photograph()]) {
        return putCatchWithPhotographs(JSON.stringify(record), photographs);
    }

    function catchRecord(overrides = {}) {
        return {
            id: catchId,
            userId: ownerUserId,
            caughtOn: '2026-08-17T08:00:00+00:00',
            speciesName: 'Pike',
            photographs: [{ id: 'photo-1', catchId, contentType: 'image/jpeg' }],
            syncStatus: 'savedLocally',
            metadataSyncStatus: 'savedLocally',
            tripId,
            ...overrides
        };
    }

    it('keeps the catch stores on the shared logbook database version', () => {
        expect(CATCH_DATABASE_VERSION).toBe(6);
    });

    it('carries the trip through the metadata list without reading photograph bytes', async () => {
        await save(catchRecord());

        const stored = await getCatchMetadata(ownerUserId);

        expect(stored).toHaveLength(1);
        expect(JSON.parse(stored[0].json).tripId).toBe(tripId);
        expect(stored[0].photographs ?? []).toEqual([]);
    });

    it('carries the trip through a single metadata read', async () => {
        await save(catchRecord());

        const stored = await getCatchMetadataById(ownerUserId, catchId);

        expect(JSON.parse(stored.json).tripId).toBe(tripId);
    });

    it('reads a pre-T5 catch stored without any trip', async () => {
        const legacy = catchRecord();
        delete legacy.tripId;
        await save(legacy);

        const stored = await getCatchMetadataById(ownerUserId, catchId);
        const parsed = JSON.parse(stored.json);

        expect(parsed.tripId).toBeUndefined();
        expect(parsed.speciesName).toBe('Pike');
        expect(parsed.photographs).toHaveLength(1);
    });

    it('keeps a standalone catch standalone', async () => {
        await save(catchRecord({ tripId: null }));

        const stored = await getCatchMetadataById(ownerUserId, catchId);

        expect(JSON.parse(stored.json).tripId).toBeNull();
    });

    it('does not expose another anglers trip-linked catch', async () => {
        await save(catchRecord({ userId: otherUserId }));

        expect(await getCatchMetadata(ownerUserId)).toEqual([]);
        expect(await getCatchMetadataById(ownerUserId, catchId)).toBeNull();
    });
    it('refuses to attach a catch to a trip without an owner', async () => {
        await save(catchRecord({ tripId: null }));

        await expect(updateCatchTrip(JSON.stringify({ id: catchId, tripId })))
            .rejects.toBeTruthy();
        const stored = await getCatchMetadataById(ownerUserId, catchId);
        expect(JSON.parse(stored.json).tripId).toBeNull();
    });

    it('refuses to attach another anglers catch to a trip', async () => {
        await save(catchRecord({ userId: otherUserId, tripId: null }));

        await expect(updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }))).rejects.toBeTruthy();
        const stored = await getCatchMetadataById(otherUserId, catchId);
        expect(JSON.parse(stored.json).tripId).toBeNull();
    });

    it('refuses to attach a catch that is not stored', async () => {
        await expect(updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }))).rejects.toBeTruthy();
    });

    it('attaches a stored catch to a trip and marks its metadata for synchronisation', async () => {
        await save(catchRecord({ tripId: null, metadataSyncStatus: 'synchronised' }));

        await updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }));

        const stored = JSON.parse((await getCatchMetadataById(ownerUserId, catchId)).json);
        expect(stored.tripId).toBe(tripId);
        expect(stored.metadataSyncStatus).toBe('savedLocally');
        expect(stored.speciesName).toBe('Pike');
        expect(stored.caughtOn).toBe('2026-08-17T08:00:00+00:00');
    });

    it('lets the recorder attach a trip to a catch logged for another angler', async () => {
        const recorderUserId = '33333333-3333-3333-3333-333333333333';
        await save(catchRecord({
            userId: otherUserId,
            anglerUserId: otherUserId,
            recordedByUserId: recorderUserId,
            tripId: null,
            metadataSyncStatus: 'synchronised'
        }));

        await updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: recorderUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }));

        const stored = JSON.parse((await getCatchMetadataById(recorderUserId, catchId)).json);
        expect(stored.tripId).toBe(tripId);
        expect(stored.metadataSyncStatus).toBe('savedLocally');
    });

    it('keeps the photograph bytes when a catch is attached to a trip', async () => {
        await save(catchRecord({ tripId: null }));

        await updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }));

        const stored = await getCatchWithPhotographs(ownerUserId, catchId);
        expect(stored.photographs).toHaveLength(1);
        expect(stored.photographs[0].bytesBase64.length).toBeGreaterThan(0);
    });

    it('takes a catch off a trip again', async () => {
        await save(catchRecord());

        await updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            tripId: null,
            metadataSyncStatus: 'savedLocally'
        }));

        const stored = JSON.parse((await getCatchMetadataById(ownerUserId, catchId)).json);
        expect(stored.tripId).toBeNull();
    });
    it('keeps a trip attached while the catch metadata was synchronising', async () => {
        await save(catchRecord({ tripId: null, metadataSyncStatus: 'synchronising' }));
        await updateCatchTrip(JSON.stringify({
            id: catchId,
            userId: ownerUserId,
            tripId,
            metadataSyncStatus: 'savedLocally'
        }));

        await updateCatchMetadata(JSON.stringify(catchRecord({
            tripId: null,
            metadataSyncStatus: 'synchronised',
            syncStatus: 'synchronised'
        })));

        const stored = JSON.parse((await getCatchMetadataById(ownerUserId, catchId)).json);
        expect(stored.tripId).toBe(tripId);
        expect(stored.metadataSyncStatus).toBe('savedLocally');
    });
});
