import { describe, expect, it } from 'vitest';
import {
    PREFERENCE_DATABASE_NAME,
    PREFERENCE_DATABASE_VERSION,
    PREFERENCE_STORE_NAME,
    clearFishingPreferences,
    getFishingPreferences,
    putFishingPreferences
} from './preference-store.js';

const ownerId = '11111111-1111-1111-1111-111111111111';
const otherId = '22222222-2222-2222-2222-222222222222';

describe('Fishing preference cache', () => {
    it('returns nothing when nothing has been cached', async () => {
        const cached = await getFishingPreferences(ownerId);

        expect(cached).toBeNull();
    });

    it('rejects a write without an owner', async () => {
        await expect(putFishingPreferences('', '{}')).rejects.toBeTruthy();
    });

    it('returns nothing when no owner is supplied', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1 }));

        const cached = await getFishingPreferences('');

        expect(cached).toBeNull();
    });

    it('reads back the cached preferences for the owner', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1, lengthUnit: 1 }));

        const cached = await getFishingPreferences(ownerId);

        expect(JSON.parse(cached)).toMatchObject({ weightUnit: 1, lengthUnit: 1 });
    });

    it('replaces the cached preferences for the same owner', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1 }));
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 0 }));

        const cached = await getFishingPreferences(ownerId);

        expect(JSON.parse(cached)).toMatchObject({ weightUnit: 0 });
    });

    it('does not return another angler cached preferences', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1 }));

        const cached = await getFishingPreferences(otherId);

        expect(cached).toBeNull();
    });

    it('keeps each angler cache separate', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1 }));
        await putFishingPreferences(otherId, JSON.stringify({ weightUnit: 0 }));

        expect(JSON.parse(await getFishingPreferences(ownerId))).toMatchObject({ weightUnit: 1 });
        expect(JSON.parse(await getFishingPreferences(otherId))).toMatchObject({ weightUnit: 0 });
    });

    it('forgets every angler when the cache is cleared', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1 }));
        await putFishingPreferences(otherId, JSON.stringify({ weightUnit: 0 }));

        await clearFishingPreferences();

        expect(await getFishingPreferences(ownerId)).toBeNull();
        expect(await getFishingPreferences(otherId)).toBeNull();
    });

    it('creates its own database with only the preference store', async () => {
        await putFishingPreferences(ownerId, JSON.stringify({ weightUnit: 1 }));

        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open(PREFERENCE_DATABASE_NAME);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        expect(db.name).toBe('FishingLogBookPreferences');
        expect(db.version).toBe(PREFERENCE_DATABASE_VERSION);
        expect([...db.objectStoreNames]).toEqual([PREFERENCE_STORE_NAME]);
        db.close();
    });
});
