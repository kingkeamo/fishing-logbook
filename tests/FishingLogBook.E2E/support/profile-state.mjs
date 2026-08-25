export async function withRestoredProfileState(page, journey) {
    const snapshot = await captureProfileState(page);
    try {
        await journey(snapshot);
    } finally {
        await restoreProfileState(page, snapshot);
    }
}

export async function captureProfileState(page) {
    const profileResponse = page.waitForResponse(response =>
        response.url().endsWith('/api/profiles/me')
        && response.request().method() === 'GET'
        && response.ok());
    const preferencesResponse = page.waitForResponse(response =>
        response.url().endsWith('/api/profiles/me/fishing-preferences')
        && response.request().method() === 'GET'
        && response.ok());

    await page.goto('/profile');
    const [profileResult, preferencesResult] = await Promise.all([profileResponse, preferencesResponse]);
    await page.locator('#profile-loading').waitFor({ state: 'hidden' });
    const authorization = profileResult.request().headers().authorization;
    if (!authorization) {
        throw new Error('Authenticated Profile request did not contain an authorization header.');
    }

    return {
        authorization,
        profileUrl: profileResult.url(),
        preferencesUrl: preferencesResult.url(),
        profile: await profileResult.json(),
        preferences: await preferencesResult.json()
    };
}

export async function restoreProfileState(page, snapshot) {
    const headers = { authorization: snapshot.authorization };
    const profileResponse = await page.request.put(snapshot.profileUrl, {
        headers,
        data: buildProfileUpdate(snapshot.profile)
    });
    if (!profileResponse.ok()) {
        throw new Error(`Profile cleanup failed with HTTP ${profileResponse.status()}.`);
    }

    const preferencesResponse = await page.request.put(snapshot.preferencesUrl, {
        headers,
        data: buildPreferencesUpdate(snapshot.preferences)
    });
    if (!preferencesResponse.ok()) {
        throw new Error(`Fishing-preferences cleanup failed with HTTP ${preferencesResponse.status()}.`);
    }
}

export function buildProfileUpdate(profile) {
    return {
        displayName: profile.displayName,
        homeRegion: profile.homeRegion,
        showDisplayName: profile.showDisplayName,
        showPhotograph: profile.showPhotograph,
        showHomeRegion: profile.showHomeRegion,
        showPreferredFishingMethods: profile.showPreferredFishingMethods,
        showPreferredSpecies: profile.showPreferredSpecies,
        preferredWeightUnit: profile.preferredWeightUnit,
        preferredLengthUnit: profile.preferredLengthUnit
    };
}

export function buildPreferencesUpdate(preferences) {
    return {
        methods: preferences.methods.map(method => ({
            fishingMethodId: method.fishingMethodId,
            isDefault: method.isDefault,
            species: method.species.map(species => ({
                speciesId: species.speciesId,
                isDefault: species.isDefault
            }))
        }))
    };
}
