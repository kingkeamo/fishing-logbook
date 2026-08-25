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
    if (new URL(page.url()).pathname === '/catches') {
        await page.reload();
    } else {
        await page.goto('/catches');
    }
    return catches;
}

export async function recordCatch(page, notes, waitForServer = false) {
    const id = await createCatch(page, waitForServer);
    await editCatch(page, id, { notes }, waitForServer);
    await page.locator('a[href="/catches"]').first().click();
    await expect(page.locator('#catch-list-title')).toBeVisible();
    return id;
}

export async function createCatch(page, waitForServer = false, options = {}) {
    if (new URL(page.url()).pathname !== '/catches') {
        await page.goto('/catches');
    }
    await expect(page.locator('#catch-list-title')).toBeVisible();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    const existingIds = new Set(await page.locator('.catch-card').evaluateAll(cards =>
        cards.map(card => card.id.replace('catch-card-', ''))));
    await page.locator('#catch-record-link').click();
    await expect(page.locator('#record-catch-title')).toBeVisible();
    const methodCode = options.methodCode ?? 'Fly';
    const speciesCode = options.speciesCode ?? 'BrownTrout';
    await selectCatalogueValue(page, 'method', methodCode);
    await selectCatalogueValue(page, 'species', speciesCode);
    const photoCount = options.photoCount ?? 1;
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles(
        Array.from({ length: photoCount }, (_, index) => ({
            name: `e2e-catch-${index + 1}.png`, mimeType: 'image/png', buffer: png
        })));
    if (options.allowLocation) {
        await page.locator('#catch-location-allow').click();
    }
    if (options.allowLocation || options.waitForLocation) {
        await page.waitForFunction(() => globalThis.e2eLocationCompleted === true);
    }
    let uploadedPhotographs = 0;
    const synchronised = waitForServer
        ? Promise.all([
            page.waitForResponse(response =>
                response.url().endsWith('/api/catches')
                && response.request().method() === 'POST'
                && response.ok()),
            page.waitForResponse(response => {
                const isPhotographUpload =
                    /\/api\/catches\/[0-9a-f-]+\/photographs$/i.test(new URL(response.url()).pathname)
                    && response.request().method() === 'POST'
                    && response.ok();
                if (isPhotographUpload) uploadedPhotographs += 1;
                return isPhotographUpload && uploadedPhotographs === photoCount;
            })
        ])
        : null;
    await page.locator('#save-catch-button').click();
    await expect(page.locator('#catch-saved')).toBeVisible();
    if (synchronised) await synchronised;
    await page.locator('#catch-view-catches').click();
    await expect(page.locator('#catch-list-loading')).toBeHidden();
    await expect(page.locator('.catch-card')).toHaveCount(existingIds.size + 1);
    const currentIds = await page.locator('.catch-card').evaluateAll(cards =>
        cards.map(card => card.id.replace('catch-card-', '')));
    const id = currentIds.find(candidate => !existingIds.has(candidate));
    if (!id) throw new Error('The newly recorded Catch could not be identified.');
    const card = page.locator(`#catch-card-${id}`);
    await expect(card).toBeVisible();
    return id;
}

async function selectCatalogueValue(page, type, code) {
    const chip = page.locator(`#record-catch-${type}-${code}`);
    if (await chip.count() > 0) {
        await chip.click();
        return;
    }

    await page.locator(`#record-catch-${type}-more`).click();
    await page.locator('#catalogue-picker-modal-search').fill(code);
    await page.locator(`#catalogue-picker-modal-option-${code}`).click();
    await page.locator('#catalogue-picker-modal-save').click();
}

export async function editCatch(page, id, changes, waitForServer = false) {
    if (new URL(page.url()).pathname !== '/catches') {
        await page.goto('/catches');
        await expect(page.locator('#catch-list-loading')).toBeHidden();
    }
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
    for (const [field, value] of Object.entries(changes)) {
        await page.locator(`#catch-edit-${field}`).fill(value);
    }
    const save = page.locator('#catch-edit-save');
    if (waitForServer) {
        await Promise.all([
            page.waitForResponse(response =>
                response.url().endsWith('/api/catches')
                && response.request().method() === 'POST'
                && response.ok()),
            save.click()
        ]);
    } else {
        await save.click();
    }
    await expect(page.locator('#catch-edit-saved')).toBeVisible();
}
