import { test, expect } from '../support/fixtures.mjs';
import { recordCatch, testName } from '../support/catch-journey.mjs';

test('records and edits locally offline, then synchronises once after reconnect', async ({ page, context }) => {
    await page.goto('/catches');
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await page.evaluate(() => navigator.serviceWorker?.ready);
    await context.setOffline(true);
    const notes = testName('offline-edited');
    const id = await recordCatch(page, notes);
    await page.goto('/catches');
    await expect(page.locator(`#catch-card-${id}`)).toContainText(notes);
    await expect(page.locator(`#catch-card-${id}`)).toContainText(/saved|device|sync/i);

    await context.setOffline(false);
    const responsePromise = page.waitForResponse(response =>
        response.url().endsWith('/api/catches')
        && response.request().method() === 'GET'
        && response.ok());
    await page.reload();
    const serverCatches = await (await responsePromise).json();
    expect(serverCatches.filter(catchRecord => catchRecord.id === id)).toHaveLength(1);
    await expect(page.locator(`#catch-card-${id}`)).toContainText(notes);
    await expect(page.locator(`#catch-card-${id} [id^="catch-card-synchronising-"]`)).toHaveCount(0, { timeout: 30_000 });
    await expect(page.locator(`#catch-card-${id}`)).toHaveCount(1);
});
