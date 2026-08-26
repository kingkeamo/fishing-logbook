import { test, expect } from '../support/fixtures.mjs';
import { createCatch, editCatch, reloadServerCatches } from '../support/catch-journey.mjs';
import { withRestoredProfileState } from '../support/profile-state.mjs';

test('records a catch with weight only and retains it after reload', async ({ page }) => {
    const id = await createCatch(page, true, { weight: '3.75' });

    const persisted = await reloadServerCatches(page);

    expect(persisted.find(candidate => candidate.id === id)?.weight).toBe(3.75);
    expect(persisted.find(candidate => candidate.id === id)?.length).toBeNull();
});

test('records a catch with length only and retains it after reload', async ({ page }) => {
    const id = await createCatch(page, true, { length: '72' });

    const persisted = await reloadServerCatches(page);

    expect(persisted.find(candidate => candidate.id === id)?.weight).toBeNull();
    expect(persisted.find(candidate => candidate.id === id)?.length).toBe(72);
});

test('records both measurements and shows them when reopened for editing', async ({ page }) => {
    const id = await createCatch(page, true, { weight: '2.5', length: '64' });

    await openCatchEdit(page, id);

    await expect(page.locator('#catch-edit-weight-value')).toContainText('2.5 kg');
    await expect(page.locator('#catch-edit-length-value')).toContainText('64 cm');
});

test('edits existing catch measurements and retains the updates', async ({ page }) => {
    const id = await createCatch(page, true, { weight: '1.25', length: '40' });

    await editCatch(page, id, { weight: '4.5', length: '88' }, true);
    const persisted = await reloadServerCatches(page);

    expect(persisted.find(candidate => candidate.id === id)?.weight).toBe(4.5);
    expect(persisted.find(candidate => candidate.id === id)?.length).toBe(88);
});

test('uses metric Profile preferences for measurement entry', async ({ page }) => {
    await withRestoredProfileState(page, async () => {
        await setProfileUnits(page, 'Kilograms (kg)', 'Centimetres (cm)');

        const id = await createCatch(page, true, { weight: '5.2', length: '91' });
        const persisted = await reloadServerCatches(page);

        expect(persisted.find(candidate => candidate.id === id)?.weight).toBe(5.2);
        expect(persisted.find(candidate => candidate.id === id)?.length).toBe(91);
    });
});

test('uses natural imperial Profile preferences while retaining canonical values', async ({ page }) => {
    await withRestoredProfileState(page, async () => {
        await setProfileUnits(page, 'Pounds (lb)', 'Inches (in)');

        const id = await createCatch(page, true, {
            weight: { pounds: 3, ounces: 12 },
            length: '24'
        });
        const persisted = await reloadServerCatches(page);
        const catchRecord = persisted.find(candidate => candidate.id === id);

        expect(catchRecord?.weight).toBeCloseTo(1.701, 3);
        expect(catchRecord?.length).toBeCloseTo(60.96, 2);
    });
});

test('records a catch without either optional measurement', async ({ page }) => {
    const id = await createCatch(page, true);

    const persisted = await reloadServerCatches(page);

    expect(persisted.find(candidate => candidate.id === id)?.weight).toBeNull();
    expect(persisted.find(candidate => candidate.id === id)?.length).toBeNull();
});

async function openCatchEdit(page, id) {
    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
}

async function setProfileUnits(page, weight, length) {
    await page.locator('#profile-fishing-details-section').click();
    await chooseSelectOption(page, '#profile-weight-unit', weight);
    await chooseSelectOption(page, '#profile-length-unit', length);
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/profiles/me/fishing-preferences')
            && response.request().method() === 'PUT'
            && response.ok()),
        page.locator('#profile-save-button').click()
    ]);
    await expect(page.locator('#profile-save-spinner')).toBeHidden();
    await expect(page.locator('#profile-save-button')).toBeEnabled();
}

async function chooseSelectOption(page, selector, optionName) {
    await page.locator(selector)
        .locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " mud-input-control ")]')
        .click();
    await page.getByRole('option', { name: optionName, exact: true }).click();
}
