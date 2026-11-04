import { test, expect } from '../support/fixtures.mjs';
import { randomUUID } from 'node:crypto';

test('dashboard keeps lifetime records while filtering and opens the actual winning catch', async ({ page }, testInfo) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: 390, height: 844 });
    const initialResponse = page.waitForResponse(isDashboardResponse);
    await page.goto('/dashboard');
    const seedAuthorization = await initialResponse;
    const initial = await seedAuthorization.json();
    await expect(page.locator('#dashboard-loading')).toHaveCount(0);
    await expect(page.locator('#dashboard-total-catches')).toHaveText(String(initial.lifetime.catches));
    await expect(page.locator('#dashboard-all-time')).toBeVisible();
    await expect(page.locator('#dashboard-explore')).toBeVisible();
    await expect(page.locator('#dashboard-statistics')).toBeVisible();
    await expect(page.locator('#dashboard-filtered-table')).toBeVisible();
    await expect(page.locator('#dashboard-record')).toHaveAttribute('href', '/catches/record');
    if (initial.lifetime.catches === 0) {
        await expect(page.locator('#dashboard-total-species')).toHaveText('0');
        await expect(page.locator('#dashboard-all-time')).toContainText('Your first personal best awaits');
        await expect(page.locator('#dashboard-import-link')).toHaveAttribute('href', '/import');
    }
    await page.screenshot({ path: testInfo.outputPath('dashboard-initial-mobile.png'), fullPage: true });

    await seedDashboardCatch(page, seedAuthorization, 12, '2021-03-08T12:00:00Z');
    const filteredWinner = await seedDashboardCatch(page, seedAuthorization, 3, '2022-03-08T12:00:00Z');
    const populated = await loadDashboard(page);
    await expect(page.locator('#dashboard-total-catches')).toHaveText(String(initial.lifetime.catches + 2));
    await expect(page.locator('#dashboard-import')).toHaveCount(0);
    const allTimeWinner = populated.allTimePersonalBests.find(row => row.speciesName === 'Brown Trout' && row.method === 'Fly');
    expect(allTimeWinner).toBeTruthy();
    expect(allTimeWinner.catchId).not.toBe(filteredWinner);
    const lifetimeLink = page.locator(`#dashboard-all-time-table a[href="/catches/${allTimeWinner.catchId}"]`);
    await expect(lifetimeLink).toBeVisible();

    await page.locator('#dashboard-date').selectOption('Custom');
    await page.locator('#dashboard-from').fill('2022-03-08');
    await page.locator('#dashboard-to').fill('2022-03-08');
    await page.locator('#dashboard-species').selectOption('Brown Trout');
    await page.locator('#dashboard-method').selectOption('Fly');
    const filtered = await applyFilters(page);
    expect(filtered.catchStatistics).toEqual([{ speciesName: 'Brown Trout', method: 'Fly', count: 1 }]);
    expect(filtered.filteredPersonalBests).toEqual([expect.objectContaining({ catchId: filteredWinner })]);
    expect(filtered.lifetime).toEqual(populated.lifetime);
    expect(filtered.allTimePersonalBests).toEqual(populated.allTimePersonalBests);
    await expect(page.locator('#dashboard-statistics tbody tr')).toHaveCount(1);
    await expect(page.locator(`#dashboard-filtered-table a[href="/catches/${filteredWinner}"]`)).toBeVisible();
    await expect(lifetimeLink).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath('dashboard-filtered-mobile.png'), fullPage: true });
    const themeLabel = await page.locator('#theme-toggle-button').getAttribute('aria-label');
    await page.locator('#theme-toggle-button').click();
    await expect(page.locator('#theme-toggle-button')).not.toHaveAttribute('aria-label', themeLabel);
    await page.screenshot({ path: testInfo.outputPath('dashboard-dark-mobile.png'), fullPage: true });
    await page.setViewportSize({ width: 1280, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(1280);
    await page.screenshot({ path: testInfo.outputPath('dashboard-dark-desktop.png'), fullPage: true });
    await page.locator('#dashboard-filtered-table tbody tr').click();
    await expect(page).toHaveURL(new RegExp(`/catches/${filteredWinner}$`));
    await expect(page.locator('#catch-view-details')).toBeVisible();
    await expect(page.locator('#catch-view-species')).toContainText('Brown Trout');
});

test('dashboard distinguishes API errors from empty data and recovers on retry', async ({ page }) => {
    await page.route('**/api/dashboard?*', route => route.fulfill({
        status: 503, contentType: 'application/problem+json', body: '{"title":"Unavailable"}'
    }));
    await page.goto('/dashboard');
    await expect(page.locator('#dashboard-status')).toContainText('could not be loaded');
    await expect(page.locator('#dashboard-import')).toHaveCount(0);
    await expect(page.locator('#dashboard-total-catches')).toHaveText('—');
    await page.unroute('**/api/dashboard?*');
    const response = page.waitForResponse(isDashboardResponse);
    await page.locator('#dashboard-retry').click();
    const data = await (await response).json();
    await expect(page.locator('#dashboard-loading')).toHaveCount(0);
    await expect(page.locator('#dashboard-total-catches')).toHaveText(String(data.lifetime.catches));
});

function isDashboardResponse(response) {
    return new URL(response.url()).pathname === '/api/dashboard'
        && response.request().method() === 'GET' && response.ok();
}

async function loadDashboard(page) {
    const response = page.waitForResponse(isDashboardResponse);
    await page.goto('/dashboard');
    const data = await (await response).json();
    await expect(page.locator('#dashboard-loading')).toHaveCount(0);
    return data;
}

async function applyFilters(page) {
    const response = page.waitForResponse(isDashboardResponse);
    await page.locator('#dashboard-apply').click();
    const data = await (await response).json();
    await expect(page.locator('#dashboard-loading')).toHaveCount(0);
    return data;
}

async function seedDashboardCatch(page, dashboardResponse, weight, caughtOn) {
    const id = randomUUID();
    const response = await page.request.post(new URL('/api/catches', dashboardResponse.url()).href, {
        headers: { authorization: dashboardResponse.request().headers().authorization },
        data: {
            id, caughtOn, speciesName: 'Brown Trout', method: 'Fly', weight,
            photographs: [{ id: randomUUID(), catchId: id, contentType: 'image/jpeg' }]
        }
    });
    expect(response.status()).toBe(200);
    return id;
}
