import { expect } from '@playwright/test';

const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');

export function testName(label) {
    return `E2E-${process.env.GITHUB_RUN_ID ?? Date.now()}-${label}`;
}

export async function reloadServerCatches(page) {
    const catches = page.waitForResponse(response =>
        response.url().endsWith('/api/catches')
        && response.request().method() === 'GET'
        && response.ok())
        .then(response => response.json());
    await page.reload();
    return catches;
}

export async function recordCatch(page, notes, waitForServer = false) {
    if (new URL(page.url()).pathname !== '/catches') {
        await page.goto('/catches');
    }
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    const existingIds = new Set(await page.locator('.catch-card').evaluateAll(cards =>
        cards.map(card => card.id.replace('catch-card-', ''))));
    await page.locator('#catch-record-link').click();
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator('#record-catch-method-Fly').click();
    await page.locator('#record-catch-species-BrownTrout').click();
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles({
        name: 'e2e-catch.png', mimeType: 'image/png', buffer: png
    });
    await page.locator('#save-catch-button').click();
    await expect(page.locator('#catch-saved')).toBeVisible();
    await page.locator('#catch-view-catches').click();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    await expect(page.locator('.catch-card')).toHaveCount(existingIds.size + 1);
    const currentIds = await page.locator('.catch-card').evaluateAll(cards =>
        cards.map(card => card.id.replace('catch-card-', '')));
    const id = currentIds.find(candidate => !existingIds.has(candidate));
    if (!id) throw new Error('The newly recorded Catch could not be identified.');
    const card = page.locator(`#catch-card-${id}`);
    await expect(card).toBeVisible();
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await page.locator('#catch-edit-notes').fill(notes);
    const save = page.locator('#catch-edit-save');
    if (waitForServer) {
        await Promise.all([
            page.waitForResponse(response =>
                response.url().includes('/api/catches')
                && ['POST', 'PUT'].includes(response.request().method())
                && response.ok()),
            save.click()
        ]);
    } else {
        await save.click();
    }
    await expect(page.locator('#catch-edit-saved')).toBeVisible();
    await page.locator('a[href="/catches"]').first().click();
    await expect(page.locator('#catch-list-title')).toBeVisible();
    return id;
}
