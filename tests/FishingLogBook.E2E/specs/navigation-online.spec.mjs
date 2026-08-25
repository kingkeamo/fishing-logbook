import { test, expect } from '../support/fixtures.mjs';

test('authenticated navigation reaches core online journeys and survives deep-link refresh', async ({ page }) => {
    await page.goto('/catches');
    await expect(page.locator('#catch-list-title')).toBeVisible();

    await page.locator('#record-catch-nav-link').click();
    await expect(page).toHaveURL(/\/catches\/record$/);
    await expect(page.locator('#record-catch-title')).toBeVisible();

    await page.locator('#profile-nav-link').click();
    await expect(page).toHaveURL(/\/profile$/);
    await expect(page.locator('#profile-loading')).toBeHidden();
    await page.reload();
    await expect(page).toHaveURL(/\/profile$/);
    await expect(page.locator('#profile-save-button')).toBeVisible();

    await page.locator('#catch-logbook-nav-link').click();
    await expect(page).toHaveURL(/\/catches$/);
    await expect(page.locator('#catch-list-title')).toBeVisible();
});

test('authenticated user can reach support surfaces through primary navigation', async ({ page }) => {
    await page.goto('/catches');
    await expect(page.locator('#catch-list-title')).toBeVisible();

    await page.locator('#system-status-nav-link').click();
    await expect(page).toHaveURL(/\/system-status$/);
    await expect(page.locator('#build-information')).toBeVisible();
    await expect(page.locator('#web-build-information')).toBeVisible();
    await expect(page.locator('#api-build-information')).toBeVisible();

    await page.locator('#diagnostics-nav-button').click();
    await expect(page).toHaveURL(/\/diagnostics$/);
    await expect(page.locator('#diagnostics-online')).toBeVisible();
    await expect(page.locator('#refresh-diagnostics-button')).toBeVisible();

    await page.locator('#install-nav-link').click();
    await expect(page).toHaveURL(/\/install$/);
    await expect(page.getByRole('heading', { name: /install catch but don.t forget/i })).toBeVisible();
});
