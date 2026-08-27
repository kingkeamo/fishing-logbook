import { describe, expect, it } from 'vitest';
import {
    CATCH_DATABASE_VERSION,
    getCatchMetadata,
    getCatchMetadataById,
    putCatchWithPhotographs
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

    it('does not need a database version bump for the trip link', () => {
        expect(CATCH_DATABASE_VERSION).toBe(5);
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
});
