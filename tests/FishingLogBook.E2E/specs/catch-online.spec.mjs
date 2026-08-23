import { test, expect } from '../support/fixtures.mjs';
import { recordCatch, testName } from '../support/catch-journey.mjs';

test('@smoke records and edits a catch through the real application', async ({ page }) => {
    const notes = testName('online');
    const id = await recordCatch(page, notes, true);
    const responsePromise = page.waitForResponse(response =>
        response.url().endsWith('/api/catches')
        && response.request().method() === 'GET'
        && response.ok());
    await page.goto('/catches');
    const serverCatches = await (await responsePromise).json();
    expect(serverCatches.filter(catchRecord => catchRecord.id === id)).toHaveLength(1);
    await expect(page.locator(`#catch-card-${id}`)).toContainText(notes);
});
