import { test, expect } from '../support/fixtures.mjs';
import { createCatch, reloadServerCatches } from '../support/catch-journey.mjs';
import { jpegWithExif, jpegWithoutExif } from '../support/exif-image.mjs';

test.use({ timezoneId: 'Europe/Dublin' });

const historicWallClock = '2025:06:14 07:32:10';
const historicInstant = '2025-06-14T06:32:10+00:00';
const corribLatitude = 53.2707;
const corribLongitude = -9.0568;

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
    await expect(page.locator('#catch-photo-date-conflict')).toBeHidden();
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

test('warns about conflicting capture dates and persists the date the angler confirms', async ({ page }) => {
    await page.goto('/catches/record');
    await expect(page.locator('#record-catch-title')).toBeVisible();
    await page.locator('#catch-photo-gallery input, #catch-photo-gallery').setInputFiles([
        photograph('june.jpg', { capturedOn: historicWallClock }),
        photograph('may.jpg', { capturedOn: '2025:05:02 15:10:00' })
    ]);

    await expect(page.locator('#catch-photo-date-conflict')).toBeVisible();
    await expect(page.locator('#save-catch-button')).toBeDisabled();

    const id = await createCatch(page, true, {
        files: [
            photograph('june.jpg', { capturedOn: historicWallClock }),
            photograph('may.jpg', { capturedOn: '2025:05:02 15:10:00' })
        ],
        resolveDateConflict: true
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(persisted.filter(candidate => candidate.id === id)).toHaveLength(1);
    expect(catchRecord.photographs).toHaveLength(2);
    expect(new Date(catchRecord.caughtOn).toISOString())
        .toBe(new Date('2025-05-02T14:10:00+00:00').toISOString());
});

test('warns about conflicting photograph locations and persists no location', async ({ page }) => {
    const id = await createCatch(page, true, {
        files: [
            photograph('galway.jpg', {
                capturedOn: historicWallClock,
                latitude: corribLatitude,
                longitude: corribLongitude
            }),
            photograph('cork.jpg', {
                capturedOn: '2025:06:14 07:34:00',
                latitude: 51.8985,
                longitude: -8.4756
            })
        ]
    });

    const persisted = await reloadServerCatches(page);
    const catchRecord = persisted.find(candidate => candidate.id === id);
    expect(catchRecord.photographs).toHaveLength(2);
    expect(catchRecord.location).toBeNull();
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
            const request = indexedDB.open('FishingLogBook', 4);
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
