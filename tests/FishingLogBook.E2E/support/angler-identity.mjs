export async function ensureDisplayName(page, displayName) {
    const profileResponse = page.waitForResponse(response =>
        response.url().endsWith('/api/profiles/me')
        && response.request().method() === 'GET'
        && response.ok());
    await page.goto('/profile');
    const response = await profileResponse;
    await page.locator('#profile-loading').waitFor({ state: 'hidden' });
    const profile = await response.json();
    if (profile.displayName === displayName) return;

    const authorization = response.request().headers().authorization;
    const update = await page.request.put(response.url(), {
        headers: { authorization },
        data: {
            displayName,
            homeRegion: profile.homeRegion,
            showDisplayName: true,
            showPhotograph: profile.showPhotograph,
            showHomeRegion: profile.showHomeRegion,
            showPreferredFishingMethods: profile.showPreferredFishingMethods,
            showPreferredSpecies: profile.showPreferredSpecies,
            preferredWeightUnit: profile.preferredWeightUnit,
            preferredLengthUnit: profile.preferredLengthUnit
        }
    });
    if (!update.ok()) {
        const body = await update.text();
        throw new Error(`Failed to set display name "${displayName}": HTTP ${update.status()} ${body}`);
    }
}
