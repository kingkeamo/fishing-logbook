import assert from 'node:assert/strict';
import test from 'node:test';
import {
    buildPreferencesUpdate,
    buildProfileUpdate,
    restoreProfileState,
    withRestoredProfileState
} from '../support/profile-state.mjs';

const profile = {
    userId: 'ignored',
    displayName: 'Original angler',
    photographId: 'ignored',
    photographUrl: 'ignored',
    photographContentType: 'ignored',
    homeRegion: 'Original water',
    showDisplayName: true,
    showPhotograph: false,
    showHomeRegion: true,
    showPreferredFishingMethods: false,
    showPreferredSpecies: true,
    preferredWeightUnit: 1,
    preferredLengthUnit: 0,
    onboardingCompleted: true
};

const preferences = {
    methods: [{
        fishingMethodId: 'method-id',
        code: 'Fly',
        name: 'Fly',
        isDefault: true,
        species: [{
            speciesId: 'species-id',
            code: 'BrownTrout',
            name: 'Brown Trout',
            isDefault: true
        }]
    }]
};

test('builds cleanup payloads without response-only Profile and catalogue fields', () => {
    assert.deepEqual(buildProfileUpdate(profile), {
        displayName: 'Original angler',
        homeRegion: 'Original water',
        showDisplayName: true,
        showPhotograph: false,
        showHomeRegion: true,
        showPreferredFishingMethods: false,
        showPreferredSpecies: true,
        preferredWeightUnit: 1,
        preferredLengthUnit: 0
    });
    assert.deepEqual(buildPreferencesUpdate(preferences), {
        methods: [{
            fishingMethodId: 'method-id',
            isDefault: true,
            species: [{ speciesId: 'species-id', isDefault: true }]
        }]
    });
});

test('restores both persisted Profile boundaries with the captured authorization', async () => {
    const calls = [];
    const page = {
        request: {
            put: async (url, options) => {
                calls.push({ url, options });
                return { ok: () => true, status: () => 200 };
            }
        }
    };
    const snapshot = {
        authorization: 'Bearer hidden-test-token',
        profileUrl: 'https://api.test/api/profiles/me',
        preferencesUrl: 'https://api.test/api/profiles/me/fishing-preferences',
        profile,
        preferences
    };

    await restoreProfileState(page, snapshot);

    assert.equal(calls.length, 2);
    assert.equal(calls[0].url, snapshot.profileUrl);
    assert.equal(calls[1].url, snapshot.preferencesUrl);
    assert.equal(calls[0].options.headers.authorization, snapshot.authorization);
    assert.deepEqual(calls[0].options.data, buildProfileUpdate(profile));
    assert.deepEqual(calls[1].options.data, buildPreferencesUpdate(preferences));
});

test('fails cleanup when either persisted boundary rejects restoration', async () => {
    const page = {
        request: {
            put: async url => ({
                ok: () => !url.endsWith('/fishing-preferences'),
                status: () => 503
            })
        }
    };
    const snapshot = {
        authorization: 'Bearer hidden-test-token',
        profileUrl: 'https://api.test/api/profiles/me',
        preferencesUrl: 'https://api.test/api/profiles/me/fishing-preferences',
        profile,
        preferences
    };

    await assert.rejects(
        restoreProfileState(page, snapshot),
        /Fishing-preferences cleanup failed with HTTP 503/);
});

test('restores captured state when the journey fails', async () => {
    const calls = [];
    const responses = [
        response('https://api.test/api/profiles/me', profile),
        response('https://api.test/api/profiles/me/fishing-preferences', preferences)
    ];
    const page = {
        waitForResponse: predicate => Promise.resolve(responses.find(predicate)),
        goto: async () => undefined,
        locator: () => ({ waitFor: async () => undefined }),
        request: {
            put: async (url, options) => {
                calls.push({ url, options });
                return { ok: () => true, status: () => 200 };
            }
        }
    };

    await assert.rejects(
        withRestoredProfileState(page, async () => {
            throw new Error('Journey failed.');
        }),
        /Journey failed/);

    assert.equal(calls.length, 2);
    assert.equal(calls[0].url, 'https://api.test/api/profiles/me');
    assert.equal(calls[1].url, 'https://api.test/api/profiles/me/fishing-preferences');
});

function response(url, payload) {
    return {
        url: () => url,
        ok: () => true,
        json: async () => payload,
        request: () => ({
            method: () => 'GET',
            headers: () => ({ authorization: 'Bearer hidden-test-token' })
        })
    };
}
