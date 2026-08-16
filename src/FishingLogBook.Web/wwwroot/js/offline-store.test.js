import { describe, expect, it } from 'vitest';
import {
    getAllTestCatches,
    getStorageEstimate,
    getTestCatchPhotograph,
    putTestCatch,
    putTestCatchPhotograph
} from './offline-store.js';

describe('offline-store JSInterop shim', () => {
    it('re-exports Catch persistence used by Blazor', async () => {
        await putTestCatch(JSON.stringify({ id: 'catch-1', notes: 'shim' }));

        const items = await getAllTestCatches();

        expect(items).toHaveLength(1);
        expect(JSON.parse(items[0])).toMatchObject({ id: 'catch-1', notes: 'shim' });
    });

    it('re-exports photograph persistence used by Blazor', async () => {
        await putTestCatchPhotograph('photo-1', new Uint8Array([9, 8]), 'image/jpeg');

        const stored = await getTestCatchPhotograph('photo-1');

        expect(stored.contentType).toBe('image/jpeg');
        expect(stored.bytesBase64).toBe(btoa(String.fromCharCode(9, 8)));
    });

    it('re-exports storage estimate', async () => {
        const estimate = await getStorageEstimate();

        expect(estimate).toHaveProperty('quota');
        expect(estimate).toHaveProperty('usage');
    });
});
