import { test, expect } from '../support/fixtures.mjs';
import { recordCatch, testName } from '../support/catch-journey.mjs';

test('@smoke records and edits a catch through the real application', async ({ page }) => {
    const notes = testName('online');
    const id = await recordCatch(page, notes, true);
    await expect(page.locator(`#catch-card-${id}`)).toContainText(notes);
});
