import { test, expect } from '../support/fixtures.mjs';
import {
    createCatch,
    editCatch,
    reloadServerCatches,
    testName
} from '../support/catch-journey.mjs';

test('User adds another photograph from Edit Catch and it persists', async ({ page }) => {
    const id = await createCatch(page, true);
    const before = await reloadServerCatches(page);
    expect(before.find(candidate => candidate.id === id)?.photographs).toHaveLength(1);

    await page.goto('/catches');
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();

    const uploadUrl = page.waitForResponse(response =>
        /\/api\/catches\/[0-9a-f-]+\/photographs\/upload-url$/i.test(new URL(response.url()).pathname)
        && response.request().method() === 'POST'
        && response.ok());
    const recorded = page.waitForResponse(response =>
        /\/api\/catches\/[0-9a-f-]+\/photographs$/i.test(new URL(response.url()).pathname)
        && response.request().method() === 'POST'
        && response.ok());

    await page.locator('#catch-edit-photo-gallery input, #catch-edit-photo-gallery').setInputFiles([{
        name: 'second.png',
        mimeType: 'image/png',
        buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64')
    }]);

    await uploadUrl;
    await recorded;

    const after = await reloadServerCatches(page);
    expect(after.find(candidate => candidate.id === id)?.photographs).toHaveLength(2);
});

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
