import { test, expect } from '../support/fixtures.mjs';
import { testName } from '../support/catch-journey.mjs';
import { withRestoredProfileState } from '../support/profile-state.mjs';

test('updates personal profile details and retains them after reload', async ({ page }) => {
    await withRestoredProfileState(page, async () => {
        const displayName = testName('angler');
        const homeRegion = testName('water');

        await page.locator('#profile-display-name').fill(displayName);
        await page.locator('#profile-home-region').fill(homeRegion);
        await Promise.all([
            page.waitForResponse(response =>
                response.url().endsWith('/api/profiles/me/fishing-preferences')
                && response.request().method() === 'PUT'
                && response.ok()),
            page.locator('#profile-save-button').click()
        ]);

        await page.reload();
        await expect(page.locator('#profile-loading')).toBeHidden();
        await expect(page.locator('#profile-display-name')).toHaveValue(displayName);
        await expect(page.locator('#profile-home-region')).toHaveValue(homeRegion);
    });
});

test('leaving Profile does not persist unsaved personal-detail changes', async ({ page }) => {
    await page.goto('/profile');
    await expect(page.locator('#profile-loading')).toBeHidden();
    const savedDisplayName = await page.locator('#profile-display-name').inputValue();
    const unsavedDisplayName = testName('unsaved');

    await page.locator('#profile-display-name').fill(unsavedDisplayName);
    await page.goto('/catches');
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    await page.goto('/profile');
    await expect(page.locator('#profile-loading')).toBeHidden();
    await expect(page.locator('#profile-display-name')).toHaveValue(savedDisplayName);
    await expect(page.locator('#profile-display-name')).not.toHaveValue(unsavedDisplayName);
});
