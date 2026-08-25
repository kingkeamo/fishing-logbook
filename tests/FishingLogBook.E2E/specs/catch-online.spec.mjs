import { test, expect } from '../support/fixtures.mjs';
import {
    createCatch,
    editCatch,
    reloadServerCatches,
    testName
} from '../support/catch-journey.mjs';

test('@smoke records a catch and retains its catalogue values and photo after reload', async ({ page }) => {
    const id = await createCatch(page, true);

    const persisted = await reloadServerCatches(page);
    expect(persisted).toEqual(expect.arrayContaining([
        expect.objectContaining({ id, method: 'Fly', speciesName: 'Brown Trout' })
    ]));
    await expect(page.locator(`#catch-card-${id}`)).toBeVisible();
    await expect(page.locator(`#catch-card-method-${id}`)).toHaveText('Fly');
    await expect(page.locator(`#catch-card-species-${id}`)).toHaveText('Brown Trout');
    await expect(page.locator(`#catch-card-photo-${id}`)).toBeVisible();
    await expect(page.locator(`#catch-card-no-photo-${id}`)).toHaveCount(0);
});

test('@smoke edits catch details and retains them after server reload', async ({ page }) => {
    const id = await createCatch(page, true);
    const notes = testName('edited-notes');
    const bait = testName('bait');

    await editCatch(page, id, { notes, bait, weight: '5', length: '12' }, true);
    const persisted = await reloadServerCatches(page);
    expect(persisted).toEqual(expect.arrayContaining([
        expect.objectContaining({ id, notes, baitOrLure: bait })
    ]));
    const card = page.locator(`#catch-card-${id}`);
    await expect(card).toContainText(notes);
    await expect(card).toContainText(bait);
    await expect(page.locator(`#catch-card-measurements-${id}`)).toBeVisible();
});

test('search finds a known catch and clearing it restores the Catch List', async ({ page }) => {
    const id = await createCatch(page, true);
    const bait = testName('search-target');
    await editCatch(page, id, { bait }, true);
    await page.goto('/catches');
    await expect(page.locator('#catch-list-loading')).toBeHidden();

    await page.locator('#catch-search').fill(bait);
    await expect(page.locator('.catch-card')).toHaveCount(1);
    await expect(page.locator(`#catch-card-${id}`)).toBeVisible();
    await page.locator('#catch-clear-all-filters').click();
    await expect(page.locator(`#catch-card-${id}`)).toBeVisible();
    await expect(page.locator('#catch-search')).toHaveValue('');
    await expect(page.locator('#catch-active-filters')).toHaveCount(0);
});
