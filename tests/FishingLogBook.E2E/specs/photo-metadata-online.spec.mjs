import { test, expect } from '../support/fixtures.mjs';
import { createCatch, reloadServerCatches, showPhoto } from '../support/catch-journey.mjs';
import { jpegWithExif, jpegWithoutExif } from '../support/exif-image.mjs';
import { mkdir, utimes, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

test.use({ timezoneId: 'Europe/Dublin' });

const historicWallClock = '2025:06:14 07:32:10';
const historicInstant = '2025-06-14T06:32:10+00:00';
const mayWallClock = '2025:05:02 15:10:00';
const corribLatitude = 53.2707;
const corribLongitude = -9.0568;
const leeLatitude = 51.8985;
const leeLongitude = -8.4756;

function photograph(name, options) {
    return { name, mimeType: 'image/jpeg', buffer: jpegWithExif(options) };
}

test('proposes and persists the capture date from a selected historical photograph', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [photograph('historic.jpg', { capturedOn: historicWallClock })]
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(new Date(catchRecord.caughtOn).toISOString()).toBe(new Date(historicInstant).toISOString());
    expect(catchRecord.location).toBeNull();
});

test('shows the proposed historical date in Record Catch before saving', async ({ page }) => {
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();

    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles([
        photograph('historic.jpg', { capturedOn: historicWallClock })
    ]);

    await expect(page.locator('#catch-caught-on')).toHaveValue('2025-06-14T07:32');
    await expect(page.locator('#catch-photo-metadata-conflict')).toBeHidden();
});

test('persists photograph coordinates with the photo metadata source', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [photograph('located.jpg', {
            capturedOn: historicWallClock,
            latitude: corribLatitude,
            longitude: corribLongitude
        })]
    });

    const persisted = await reloadServerCatches(page);
    const location = persisted.find(candidate => candidate.id === id)?.location;
    expect(location.source).toBe('PhotoMetadata');
    expect(location.visibility).toBe('Private');
    expect(location.latitude).toBeCloseTo(corribLatitude, 3);
    expect(location.longitude).toBeCloseTo(corribLongitude, 3);
});

test('creates no location when the selected photograph carries no coordinates', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [photograph('no-gps.jpg', { capturedOn: historicWallClock })]
    });

    const persisted = await reloadServerCatches(page);
    expect(persisted.find(candidate => candidate.id === id).location).toBeNull();
    await expect(page.locator(`#catch-card-${id}`)).toBeVisible();
});

test('records one catch from several compatible photographs of the same fish', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [
            photograph('second.jpg', {
                capturedOn: '2025:06:14 07:33:40',
                latitude: 53.2710,
                longitude: corribLongitude
            }),
            photograph('first.jpg', {
                capturedOn: historicWallClock,
                latitude: corribLatitude,
                longitude: -9.0570
            }),
            { name: 'plain.jpg', mimeType: 'image/jpeg', buffer: jpegWithoutExif() }
        ]
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(persisted.filter(candidate => candidate.id === id)).toHaveLength(1);
    expect(catchRecord.photographs).toHaveLength(3);
    expect(new Date(catchRecord.caughtOn).toISOString()).toBe(new Date(historicInstant).toISOString());
    expect(catchRecord.location.source).toBe('PhotoMetadata');
    expect(catchRecord.location.latitude).toBeCloseTo(corribLatitude, 3);
    expect(catchRecord.location.longitude).toBeCloseTo(-9.0570, 3);
});

test('shows each photograph its own capture details while the angler chooses one', async ({ page }) => {
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles([
        photograph('june.jpg', { capturedOn: historicWallClock, latitude: corribLatitude, longitude: corribLongitude }),
        photograph('may.jpg', { capturedOn: mayWallClock })
    ]);

    await expect(page.locator('#catch-photo-metadata-conflict')).toBeVisible();
    await expect(page.locator('#save-catch-button')).toBeDisabled();

    await showPhoto(page, 1, 2);
    await expect(page.locator('#catch-photo-current-date')).toHaveAttribute('data-captured-on', '2025-06-14T07:32');
    await expect(page.locator('#catch-photo-current-location')).toContainText('GPS location available');

    await showPhoto(page, 2, 2);
    await expect(page.locator('#catch-photo-current-date')).toHaveAttribute('data-captured-on', '2025-05-02T15:10');
    await expect(page.locator('#catch-photo-current-location')).toContainText('No photo location available');
    await expect(page.locator('#catch-caught-on')).toHaveValue('2025-05-02T15:10');
    await expect(page.locator('#save-catch-button')).toBeDisabled();

    await page.locator('#catch-photo-use-details').click();
    await expect(page.locator('#catch-photo-metadata-conflict')).toBeVisible();
    await expect(page.locator('#save-catch-button')).toBeEnabled();
    await expect(page.locator('#catch-photo-current-metadata')).toBeVisible();

    await showPhoto(page, 1, 2);
    await page.locator('#catch-photo-use-details').click();
    await expect(page.locator('#catch-photo-metadata-conflict')).toBeVisible();
    await expect(page.locator('#catch-caught-on')).toHaveValue('2025-06-14T07:32');
    await expect(page.locator('#catch-location-from-photo')).toBeVisible();
});

test('persists the capture date of the photograph the angler chooses as representative', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [
            photograph('june.jpg', { capturedOn: historicWallClock }),
            photograph('may.jpg', { capturedOn: mayWallClock })
        ],
        representativePhoto: 1
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(persisted.filter(candidate => candidate.id === id)).toHaveLength(1);
    expect(catchRecord.photographs).toHaveLength(2);
    expect(new Date(catchRecord.caughtOn).toISOString()).toBe(new Date(historicInstant).toISOString());
});

test('warns about conflicting photograph locations and persists no location until one is chosen', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [
            photograph('galway.jpg', {
                capturedOn: historicWallClock,
                latitude: corribLatitude,
                longitude: corribLongitude
            }),
            photograph('cork.jpg', {
                capturedOn: '2025:06:14 07:34:00',
                latitude: leeLatitude,
                longitude: leeLongitude
            })
        ]
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(catchRecord.photographs).toHaveLength(2);
    expect(catchRecord.location).toBeNull();
});

test('persists only the chosen photographs coordinates when photograph locations conflict', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [
            photograph('galway.jpg', {
                capturedOn: historicWallClock,
                latitude: corribLatitude,
                longitude: corribLongitude
            }),
            photograph('cork.jpg', {
                capturedOn: '2025:06:14 07:34:00',
                latitude: leeLatitude,
                longitude: leeLongitude
            })
        ],
        representativePhoto: 2
    });

    const persisted = await reloadServerCatches(page);
    const location = persisted.find(candidate => candidate.id === id)?.location;
    expect(location.source).toBe('PhotoMetadata');
    expect(location.visibility).toBe('Private');
    expect(location.latitude).toBeCloseTo(leeLatitude, 3);
    expect(location.longitude).toBeCloseTo(leeLongitude, 3);
    expect(location.latitude).not.toBeCloseTo(corribLatitude, 3);
});

test('keeps an explicitly chosen device location while the angler moves between photographs', async ({ page, context }) => {
    const deviceLatitude = 53.3498;
    const deviceLongitude = -6.2603;
    await context.grantPermissions(['geolocation'], { origin: 'http://localhost:5019' });
    await context.setGeolocation({ latitude: deviceLatitude, longitude: deviceLongitude, accuracy: 8 });
    await page.addInitScript(() => {
        globalThis.e2eLocationCompleted = false;
        const original = navigator.geolocation.getCurrentPosition.bind(navigator.geolocation);
        navigator.geolocation.getCurrentPosition = (success, error, options) => original(
            position => {
                globalThis.e2eLocationCompleted = true;
                success(position);
            },
            error,
            options);
    });
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles([
        photograph('galway.jpg', {
            capturedOn: historicWallClock,
            latitude: corribLatitude,
            longitude: corribLongitude
        }),
        photograph('cork.jpg', {
            capturedOn: '2025:06:14 07:34:00',
            latitude: leeLatitude,
            longitude: leeLongitude
        })
    ]);
    await expect(page.locator('#catch-photo-metadata-conflict')).toBeVisible();
    await expect(page.locator('#catch-location-from-photo')).toHaveCount(0);
    await page.locator('#catch-location-use-current').click();
    await page.waitForFunction(() => globalThis.e2eLocationCompleted === true);

    await showPhoto(page, 1, 2);
    await showPhoto(page, 2, 2);
    await expect(page.locator('#catch-photo-metadata-conflict')).toBeVisible();
    await expect(page.locator('#catch-location-from-photo')).toHaveCount(0);

    const saved = page.waitForResponse(response =>
        response.url().endsWith('/api/catches')
        && response.request().method() === 'POST'
        && response.ok()).then(response => response.request().postDataJSON());
    await page.locator('#save-catch-button').click();
    await expect(page.locator('#catch-saved')).toBeVisible();
    const request = await saved;

    const persisted = await reloadServerCatches(page);
    const location = persisted.find(candidate => candidate.id === request.id)?.location;
    expect(location.source).toBe('DeviceGps');
    expect(location.latitude).toBeCloseTo(deviceLatitude, 3);
    expect(location.longitude).toBeCloseTo(deviceLongitude, 3);
});

test('keeps current-catch behaviour when a photograph is captured through the camera', async ({ page }) => {
    const before = Date.now();
    const id = await createCatch(page, true, {
        useCamera: true,
        files: [photograph('camera.jpg', {
            capturedOn: historicWallClock,
            latitude: corribLatitude,
            longitude: corribLongitude
        })]
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(new Date(catchRecord.caughtOn).getTime()).toBeGreaterThanOrEqual(before - 60_000);
    expect(catchRecord.location).toBeNull();
});

test('keeps an angler edit of the proposed capture date', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [photograph('historic.jpg', { capturedOn: historicWallClock })],
        caughtOn: '2025-06-13T19:15'
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(new Date(catchRecord.caughtOn).toISOString())
        .toBe(new Date('2025-06-13T18:15:00+00:00').toISOString());
});

test('keeps no photograph EXIF metadata in the persisted image bytes', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [photograph('located.jpg', {
            capturedOn: historicWallClock,
            latitude: corribLatitude,
            longitude: corribLongitude
        })]
    });

    const stored = await page.evaluate(async catchId => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open('FishingLogBook');
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        const records = await new Promise((resolve, reject) => {
            const request = db.transaction('catchPhotographs', 'readonly')
                .objectStore('catchPhotographs')
                .getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        db.close();
        return records
            .filter(record => record.catchId === catchId)
            .map(record => {
                const value = record.bytes ?? record.bytesBase64;
                return typeof value === 'string'
                    ? Array.from(atob(value), character => character.charCodeAt(0))
                    : Array.from(new Uint8Array(value));
            });
    }, id);

    expect(stored).toHaveLength(1);
    const persisted = Buffer.from(stored[0]);
    expect(persisted.subarray(0, 2)).toEqual(Buffer.from([0xFF, 0xD8]));
    const text = persisted.toString('latin1');
    expect(text).not.toContain('Exif');
    expect(text).not.toContain('2025:06:14');

    const catchRecord = (await reloadServerCatches(page)).find(candidate => candidate.id === id);
    expect(catchRecord.location.source).toBe('PhotoMetadata');
    expect(catchRecord.location.latitude).toBeCloseTo(corribLatitude, 3);
});

test('proposes the file timestamp when a gallery photograph carries no capture date', async ({ page }) => {
    const modifiedOn = new Date('2026-08-22T10:28:43.000Z');
    const fixtureDirectory = resolve('artifacts/fixtures');
    await mkdir(fixtureDirectory, { recursive: true });
    const fixture = resolve(fixtureDirectory, 'historic-without-exif.jpg');
    await writeFile(fixture, jpegWithoutExif());
    await utimes(fixture, modifiedOn, modifiedOn);

    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles([fixture]);
    await expect(page.locator('#catch-caught-on')).toHaveValue('2026-08-22T11:28');
    await expect(page.locator('#catch-photo-metadata-conflict')).toBeHidden();

    const id = await createCatch(page, true, { files: [fixture] });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(new Date(catchRecord.caughtOn).toISOString()).toBe(modifiedOn.toISOString());
    expect(catchRecord.location).toBeNull();
});

test('does not change an existing catch until the angler applies an added photograph', async ({ page }) => {
    const id = await createCatch(page, true, { caughtOn: '2026-08-20T09:15' });

    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
    await expect(page.locator('#catch-edit-caught-on')).toHaveValue('2026-08-20T09:15');

    await page.locator('#catch-edit-photo-gallery input, #catch-edit-photo-gallery').setInputFiles([
        photograph('historic.jpg', {
            capturedOn: historicWallClock,
            latitude: corribLatitude,
            longitude: corribLongitude
        })
    ]);

    await expect(page.locator('#catch-edit-photo-current-date'))
        .toHaveAttribute('data-captured-on', '2025-06-14T07:32');
    await expect(page.locator('#catch-edit-photo-current-location')).toContainText('GPS location available');
    await expect(page.locator('#catch-edit-caught-on')).toHaveValue('2026-08-20T09:15');

    await page.locator('#catch-edit-photo-use-details').click();
    await expect(page.locator('#catch-edit-caught-on')).toHaveValue('2025-06-14T07:32');
    await Promise.all([
        page.waitForResponse(response =>
            response.url().endsWith('/api/catches')
            && response.request().method() === 'POST'
            && response.ok()),
        page.locator('#catch-edit-save').click()
    ]);
    await expect(page.locator('#catch-edit-saved')).toBeVisible();

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(catchRecord.photographs).toHaveLength(2);
    expect(new Date(catchRecord.caughtOn).toISOString()).toBe(new Date(historicInstant).toISOString());
    expect(catchRecord.location.source).toBe('PhotoMetadata');
    expect(catchRecord.location.visibility).toBe('Private');
    expect(catchRecord.location.latitude).toBeCloseTo(corribLatitude, 3);
    expect(catchRecord.location.longitude).toBeCloseTo(corribLongitude, 3);
});

test('keeps no photograph EXIF metadata in bytes added through Edit Catch', async ({ page }) => {
    const id = await createCatch(page, true, {});

    await page.locator(`#catch-card-menu-${id}`).click();
    await page.locator(`#catch-card-edit-${id}`).click();
    await expect(page.locator('#catch-edit-loading')).toBeHidden();
    await page.locator('#catch-edit-photo-gallery input, #catch-edit-photo-gallery').setInputFiles([
        photograph('located.jpg', {
            capturedOn: historicWallClock,
            latitude: corribLatitude,
            longitude: corribLongitude
        })
    ]);
    await expect(page.locator('#catch-edit-photo-current-location')).toContainText('GPS location available');

    const stored = await page.evaluate(async catchId => {
        const db = await new Promise((resolve, reject) => {
            const request = indexedDB.open('FishingLogBook');
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        const records = await new Promise((resolve, reject) => {
            const request = db.transaction('catchPhotographs', 'readonly')
                .objectStore('catchPhotographs')
                .getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        db.close();
        return records
            .filter(record => record.catchId === catchId)
            .map(record => {
                const value = record.bytes ?? record.bytesBase64;
                return typeof value === 'string'
                    ? Array.from(atob(value), character => character.charCodeAt(0))
                    : Array.from(new Uint8Array(value));
            });
    }, id);

    expect(stored).toHaveLength(2);
    const text = stored.map(bytes => Buffer.from(bytes).toString('latin1')).join('');
    expect(text).not.toContain('Exif');
    expect(text).not.toContain('2025:06:14');
});
