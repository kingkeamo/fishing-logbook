import { test, expect } from '../support/fixtures.mjs';
import { createCatch, reloadServerCatches } from '../support/catch-journey.mjs';
import { withRestoredProfileState } from '../support/profile-state.mjs';

test('filters the Catch List by fishing method and clears the filter', async ({ page }) => {
    const flyId = await createCatch(page, true);
    const baitId = await createCatch(page, true, { methodCode: 'Bait', speciesCode: 'Tench' });

    await page.locator('#catch-filter-method-Bait').click();
    await expect(page.locator(`#catch-card-${baitId}`)).toBeVisible();
    await expect(page.locator(`#catch-card-${flyId}`)).toHaveCount(0);
    await page.locator('#catch-filter-method-all').click();
    await expect(page.locator(`#catch-card-${baitId}`)).toBeVisible();
    await expect(page.locator(`#catch-card-${flyId}`)).toBeVisible();
});

test('filters the Catch List by species and clears the filter', async ({ page }) => {
    const troutId = await createCatch(page, true);
    const tenchId = await createCatch(page, true, { speciesCode: 'Tench' });

    await page.locator('#catch-filters-button').click();
    await page.locator('#catch-filter-species-Tench').click();
    await expect(page.locator(`#catch-card-${tenchId}`)).toBeVisible();
    await expect(page.locator(`#catch-card-${troutId}`)).toHaveCount(0);
    await page.locator('#catch-clear-all-filters').click();
    await expect(page.locator(`#catch-card-${tenchId}`)).toBeVisible();
    await expect(page.locator(`#catch-card-${troutId}`)).toBeVisible();
});

test('filters an older catch out of the Today view without changing its date', async ({ page }) => {
    const id = await createCatch(page, true);
    await openCatchEdit(page, id);
    await page.locator('#catch-edit-caught-on').fill('2020-01-02T12:00');
    await saveCatchEdit(page);
    const persisted = await reloadServerCatches(page);
    expect(persisted.find(candidate => candidate.id === id)?.caughtOn).toContain('2020-01-02');

    await page.locator('#catch-filters-button').click();
    await page.locator('#catch-filter-date-Today').click();
    await expect(page.locator(`#catch-card-${id}`)).toHaveCount(0);
    await page.locator('#catch-clear-all-filters').click();
    await expect(page.locator(`#catch-card-${id}`)).toBeVisible();
});

test('uses saved Profile measurement units when editing a catch', async ({ page }) => {
    await withRestoredProfileState(page, async () => {
        await page.locator('#profile-fishing-details-section').click();
        await chooseSelectOption(page, '#profile-weight-unit', 'Pounds (lb)');
        await chooseSelectOption(page, '#profile-length-unit', 'Centimetres (cm)');
        await saveProfile(page);

        const id = await createCatch(page, true);
        await openCatchEdit(page, id);
        await page.locator('#catch-edit-weight').click();
        await expect(page.locator('#measurement-exact-pounds')).toBeVisible();
        await page.locator('#measurement-cancel').click();
        await page.locator('#catch-edit-length').click();
        await expect(page.locator('#measurement-exact-value')
            .locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " mud-input-control ")]'))
            .toContainText('Exact value (cm)');
        await page.locator('#measurement-cancel').click();
    });
});

test('cancelling the Profile species picker preserves the saved selection', async ({ page }) => {
    await withRestoredProfileState(page, async () => {
        await page.locator('#profile-fishing-details-section').click();
        if (await page.locator('#profile-species-section-Bait').count() === 0) {
            await page.locator('#profile-method-Bait').click();
        }
        if (await page.locator('#profile-species-pill-Bait-Tench').count() === 0) {
            await page.locator('#profile-species-more-Bait').click();
            await page.locator('#catalogue-picker-modal-search').fill('Tench');
            await page.locator('#catalogue-picker-modal-option-Tench').click();
            await page.locator('#catalogue-picker-modal-save').click();
            await saveProfile(page);
        }

        await page.locator('#profile-species-more-Bait').click();
        await page.locator('#catalogue-picker-modal-search').fill('Tench');
        await page.locator('#catalogue-picker-modal-option-Tench').click();
        await page.locator('#catalogue-picker-modal-cancel').click();
        await expect(page.locator('#profile-species-pill-Bait-Tench')).toBeVisible();
    });
});

async function openCatchEdit(page, id) {
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
}

async function saveCatchEdit(page) {
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/catches')
            && response.request().method() === 'POST'
            && response.ok()),
        page.locator('#catch-edit-save').click()
    ]);
    await expect(page.locator('#catch-edit-saved')).toBeVisible();
}

async function chooseSelectOption(page, selector, optionName) {
    await page.locator(selector)
        .locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " mud-input-control ")]')
        .click();
    await page.getByRole('option', { name: optionName, exact: true }).click();
}

async function saveProfile(page) {
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/profiles/me/fishing-preferences')
            && response.request().method() === 'PUT'
            && response.ok()),
        page.locator('#profile-save-button').click()
    ]);
    await expect(page.locator('#profile-save-button')).toBeEnabled();
}
