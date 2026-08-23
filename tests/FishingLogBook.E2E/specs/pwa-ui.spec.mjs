import { test, expect } from '../support/fixtures.mjs';

test('@smoke keeps actionable manual install guidance available', async ({ page }) => {
    await page.goto('/install');
    await expect(page.getByRole('heading', { name: /install catch but don.t forget/i })).toBeVisible();
    await expect(page.getByText('iPhone / iPad')).toBeVisible();
    await expect(page.getByText('Android', { exact: true })).toBeVisible();
    await expect(page.getByText('Computer', { exact: true })).toBeVisible();
});
