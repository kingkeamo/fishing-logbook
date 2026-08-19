import { describe, expect, it } from 'vitest';
import {
    getAllCatchesWithPhotographs,
    getStorageEstimate,
    putCatchWithPhotographs
} from './offline-store.js';

describe('offline-store JSInterop shim', () => {
    it('re-exports Catch persistence used by Blazor', async () => {
        const ownerUserId = '11111111-1111-1111-1111-111111111111';
        await putCatchWithPhotographs(
            JSON.stringify({
                id: 'catch-1',
                userId: ownerUserId,
                notes: 'shim',
                photographs: [{ id: 'photo-1' }]
            }),
            [{ id: 'photo-1', catchId: 'catch-1', contentType: 'image/jpeg', bytes: new Uint8Array([9, 8]) }]);

        const items = await getAllCatchesWithPhotographs(ownerUserId);

        expect(items).toHaveLength(1);
        expect(JSON.parse(items[0].json)).toMatchObject({ id: 'catch-1', notes: 'shim' });
        expect(items[0].photographs[0].contentType).toBe('image/jpeg');
    });

    it('re-exports storage estimate', async () => {
        const estimate = await getStorageEstimate();

        expect(estimate).toHaveProperty('quota');
        expect(estimate).toHaveProperty('usage');
    });
});
