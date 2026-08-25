import { test, expect } from '../support/fixtures.mjs';
import { createCatch, reloadServerCatches, testName } from '../support/catch-journey.mjs';

test('records multiple photographs and retains their association after reload', async ({ page }) => {
    const id = await createCatch(page, true, { photoCount: 2 });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(catchRecord?.photographs).toHaveLength(2);
    await expect(page.locator(`#catch-card-photo-count-${id}`)).toContainText('1');
    await expect(page.locator(`#catch-card-photo-next-${id}`)).toBeVisible();
});

test('prevents removal of the only catch photograph', async ({ page }) => {
    const id = await createCatch(page, true);
    await openCatchEdit(page, id);

    await page.locator('#catch-edit-photo-remove').click();
    await expect(page.locator('#catch-edit-photo-last')).toBeVisible();
    const persisted = await reloadServerCatches(page);
    expect(persisted.find(candidate => candidate.id === id)?.photographs).toHaveLength(1);
});

test('stores granted browser location with the catch', async ({ page, context }) => {
    const latitude = 53.3498;
    const longitude = -6.2603;
    await prepareGrantedLocation(page, context, latitude, longitude);
    const id = await createCatch(page, true, { waitForLocation: true });

    const persisted = await reloadServerCatches(page);
    const location = persisted.find(candidate => candidate.id === id)?.location;
    expect(location).toEqual(expect.objectContaining({ latitude, longitude }));
});

test('keeps Record Catch usable when browser location is denied', async ({ page }) => {
    await prepareDeniedLocation(page);
    const id = await createCatch(page, true, { allowLocation: true });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(catchRecord).toBeDefined();
    expect(catchRecord.location).toBeNull();
    await expect(page.locator(`#catch-card-${id}`)).toBeVisible();
});

test('publishes only the Profile details selected by the angler', async ({ page }) => {
    const profileResponse = page.waitForResponse(response =>
        response.url().endsWith('/api/profiles/me')
        && response.request().method() === 'GET'
        && response.ok());
    await page.goto('/profile');
    const profile = await profileResponse.then(response => response.json());
    await expect(page.locator('#profile-loading')).toBeHidden();
    const displayName = testName('public-angler');
    const homeRegion = testName('public-water');
    await page.locator('#profile-display-name').fill(displayName);
    await page.locator('#profile-home-region').fill(homeRegion);
    await ensureChecked(page.locator('#profile-show-display-name'));
    await ensureChecked(page.locator('#profile-show-home-region'));
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/profiles/me/fishing-preferences')
            && response.request().method() === 'PUT'
            && response.ok()),
        page.locator('#profile-save-button').click()
    ]);

    await page.goto(`/profile/${profile.userId}`);
    await expect(page.locator('#public-profile-loading')).toBeHidden();
    await expect(page.locator('#public-profile-display-name')).toHaveText(displayName);
    await expect(page.locator('#public-profile-home-region')).toHaveText(homeRegion);
    await expect(page.locator('#public-profile-photo')).toHaveCount(0);
    await expect(page.locator('#public-profile-fishing-methods')).toHaveCount(0);
    await expect(page.locator('#public-profile-preferred-species')).toHaveCount(0);
});

async function openCatchEdit(page, id) {
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
}

async function prepareGrantedLocation(page, context, latitude, longitude) {
    await context.grantPermissions(['geolocation'], { origin: 'http://localhost:5019' });
    await context.setGeolocation({ latitude, longitude, accuracy: 8 });
    await page.addInitScript(() => {
        globalThis.e2eLocationCompleted = false;
        const original = navigator.geolocation.getCurrentPosition.bind(navigator.geolocation);
        navigator.geolocation.getCurrentPosition = (success, error, options) => original(
            position => {
                globalThis.e2eLocationCompleted = true;
                success(position);
            },
            error,
            options);
    });
    await clearLocationPromptDismissal(page);
}

async function prepareDeniedLocation(page) {
    await page.addInitScript(() => {
        globalThis.e2eLocationCompleted = false;
        navigator.geolocation.getCurrentPosition = (_success, error) => {
            globalThis.e2eLocationCompleted = true;
            error({ code: 1, message: 'Permission denied by E2E browser fixture.' });
        };
    });
    await clearLocationPromptDismissal(page);
}

async function clearLocationPromptDismissal(page) {
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.evaluate(() => localStorage.removeItem('flb-location-prompt-dismissed'));
}

async function ensureChecked(locator) {
    if (!await locator.isChecked()) {
        await locator.check();
    }
}
