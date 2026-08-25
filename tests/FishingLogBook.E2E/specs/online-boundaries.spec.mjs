import { test, expect } from '../support/fixtures.mjs';
import {
    createCatch,
    editCatch,
    reloadServerCatches,
    testName
} from '../support/catch-journey.mjs';

test('prevents an incomplete catch from being saved', async ({ page }) => {
    await page.goto('/catches');
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    await expect(page.locator('#catch-list, #catch-list-empty')).toBeVisible();
    const existingCount = await page.locator('.catch-card').count();

    await page.locator('#catch-record-link').click();
    await page.locator('#record-catch-method-Fly').click();
    await page.locator('#record-catch-species-BrownTrout').click();
    await expect(page.locator('#save-catch-button')).toBeDisabled();

    await page.locator('#catch-logbook-nav-link').click();
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    await expect(page.locator('#catch-list, #catch-list-empty')).toBeVisible();
    await expect(page.locator('.catch-card')).toHaveCount(existingCount);
});

test('records a canonical species selected through More search', async ({ page }) => {
    const id = await createCatch(page, true, { speciesCode: 'Tench' });

    const persisted = await reloadServerCatches(page);
    expect(persisted).toEqual(expect.arrayContaining([
        expect.objectContaining({ id, method: 'Fly', speciesName: 'Tench' })
    ]));
    await expect(page.locator(`#catch-card-species-${id}`)).toHaveText('Tench');
});

test('changes a catch to canonical method and species values through More search', async ({ page }) => {
    const id = await createCatch(page, true);
    await openCatchEdit(page, id);
    await chooseEditCatalogueValue(page, 'method', 'Bait');
    await chooseEditCatalogueValue(page, 'species', 'Tench');
    await Promise.all([
        waitForCatchUpdate(page),
        page.locator('#catch-edit-save').click()
    ]);
    await expect(page.locator('#catch-edit-saved')).toBeVisible();

    const persisted = await reloadServerCatches(page);
    expect(persisted).toEqual(expect.arrayContaining([
        expect.objectContaining({ id, method: 'Bait', speciesName: 'Tench' })
    ]));
    await expect(page.locator(`#catch-card-method-${id}`)).toHaveText('Bait');
    await expect(page.locator(`#catch-card-species-${id}`)).toHaveText('Tench');
});

test('editing one catch does not change another catch', async ({ page }) => {
    const firstId = await createCatch(page, true);
    const secondId = await createCatch(page, true, { speciesCode: 'Tench' });
    const firstBait = testName('first-only');

    await editCatch(page, firstId, { bait: firstBait }, true);
    const persisted = await reloadServerCatches(page);
    expect(persisted).toEqual(expect.arrayContaining([
        expect.objectContaining({ id: firstId, baitOrLure: firstBait }),
        expect.objectContaining({ id: secondId, baitOrLure: null })
    ]));
    await expect(page.locator(`#catch-card-${firstId}`)).toContainText(firstBait);
    await expect(page.locator(`#catch-card-${secondId}`)).not.toContainText(firstBait);
});

test('saved Profile method and species preferences appear in Record Catch', async ({ page }) => {
    await page.goto('/profile');
    await expect(page.locator('#profile-loading')).toBeHidden();
    await page.locator('#profile-fishing-details-section').click();
    if (await page.locator('#profile-species-section-Bait').count() === 0) {
        await page.locator('#profile-method-Bait').click();
    }
    if (await page.locator('#profile-species-pill-Bait-Tench').count() === 0) {
        await page.locator('#profile-species-more-Bait').click();
        await page.locator('#catalogue-picker-modal-search').fill('Tench');
        await page.locator('#catalogue-picker-modal-option-Tench').click();
        await page.locator('#catalogue-picker-modal-save').click();
    }
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/profiles/me/fishing-preferences')
            && response.request().method() === 'PUT'
            && response.ok()),
        page.locator('#profile-save-button').click()
    ]);

    await page.reload();
    await expect(page.locator('#profile-loading')).toBeHidden();
    await page.locator('#profile-fishing-details-section').click();
    await expect(page.locator('#profile-species-pill-Bait-Tench')).toBeVisible();
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator('#record-catch-method-Bait').click();
    await expect(page.locator('#record-catch-species-Tench')).toBeVisible();
});

async function openCatchEdit(page, id) {
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
}

async function chooseEditCatalogueValue(page, type, code) {
    await page.locator(`#catch-edit-${type}-more`).click();
    await page.locator('#catalogue-picker-modal-search').fill(code);
    await page.locator(`#catalogue-picker-modal-option-${code}`).click();
    await page.locator('#catalogue-picker-modal-save').click();
}

function waitForCatchUpdate(page) {
    return page.waitForResponse(response =>
        response.url().endsWith('/api/catches')
        && response.request().method() === 'POST'
        && response.ok());
}
