import { expect, test } from '@playwright/test';

const ownerUserId = '11111111-1111-1111-1111-111111111111';
const otherUserId = '22222222-2222-2222-2222-222222222222';
const otherCatchId = '33333333-3333-3333-3333-333333333333';
const configuredApiOrigin = 'https://localhost:7256';

test.describe('Published offline application shell', () => {
    test.beforeEach(async ({ browserName, context }) => {
        test.skip(browserName !== 'chromium', 'Playwright WebKit cannot navigate while its context is offline.');
        await context.addInitScript(() => {
            window.__credentialGetCalls = 0;
            const credentials = navigator.credentials;
            if (!credentials || typeof credentials.get !== 'function') return;
            const prototype = Object.getPrototypeOf(credentials);
            const original = prototype.get;
            prototype.get = function (...args) {
                window.__credentialGetCalls += 1;
                return original.apply(this, args);
            };
        });
    });

    test('renders Landing promptly after a cold offline reload without auth, API, or automatic WebAuthn', async ({ context, page }) => {
        const apiGuard = guardOfflineApiRequests(context);
        await context.addInitScript(() => {
            Object.defineProperty(Navigator.prototype, 'onLine', {
                configurable: true,
                get: () => false
            });
        });

        await cachePublishedShell(page);
        apiGuard.enable();
        await context.setOffline(true);
        const started = Date.now();
        await page.reload({ waitUntil: 'domcontentloaded' });
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 15000 });
        await expect(page.locator('#landing-offline-not-configured')).toBeVisible();
        await expect(page.locator('#landing-open-offline')).toHaveCount(0);
        await expect(page.locator('#landing-offline-availability-failed')).toHaveCount(0);
        const elapsed = Date.now() - started;

        expect(elapsed).toBeLessThan(15000);
        expect(page.getByText('Authorizing...')).toHaveCount(0);
        expect(await credentialGetCalls(page)).toBe(0);
        expect(apiGuard.requests).toEqual([]);
    });

    test('does not offer Open Offline on Landing while genuinely online, even with offline access configured', async ({ context, page }) => {
        await stubApiHealth(context, { reachable: true });
        await addPrfAuthenticator(page);
        await cachePublishedShell(page);
        await provisionOfflineOwner(page);

        await page.reload({ waitUntil: 'domcontentloaded' });
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 15000 });

        await expect(page.locator('#landing-open-offline')).toHaveCount(0);
        await expect(page.locator('#landing-create-account')).toBeVisible();
        await expect(page.locator('#landing-sign-in')).toBeVisible();
    });

    test('offers Open Offline on Landing when the API is unreachable, even with the browser online', async ({ context, page }) => {
        await stubApiHealth(context, { reachable: false });
        await addPrfAuthenticator(page);
        await cachePublishedShell(page);
        await provisionOfflineOwner(page);

        await page.reload({ waitUntil: 'domcontentloaded' });
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 15000 });

        await expect(page.locator('#landing-open-offline')).toBeVisible({ timeout: 15000 });
        await expect(page.locator('#landing-create-account')).toHaveCount(0);
        await expect(page.locator('#landing-sign-in')).toHaveCount(0);
    });

    test('opens read-only offline diagnostics from the shared header during cold offline startup', async ({ context, page }) => {
        const apiGuard = guardOfflineApiRequests(context);
        await context.addInitScript(() => {
            Object.defineProperty(Navigator.prototype, 'onLine', {
                configurable: true,
                get: () => false
            });
        });

        await cachePublishedShell(page);
        apiGuard.enable();
        await context.setOffline(true);
        await page.reload({ waitUntil: 'domcontentloaded' });
        await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 15000 });

        await page.locator('#offline-diagnostics-button').click();

        await expect(page).toHaveURL(/\/offline-diagnostics$/);
        await expect(page.locator('#offline-diagnostics-results')).toBeVisible();
        await expect(page.locator('#offline-diagnostics-results')).toContainText('offline-access.js');
        expect(await credentialGetCalls(page)).toBe(0);
        expect(apiGuard.requests).toEqual([]);
    });

    test('shows and retries an offline entitlement discovery failure without unlocking or using the API', async ({ context, page }) => {
        const apiGuard = guardOfflineApiRequests(context);
        await addPrfAuthenticator(page);
        await cachePublishedShell(page);
        await provisionOfflineOwner(page);
        await context.addInitScript(() => {
            window.__failOfflineEntitlementDiscovery = true;
            const originalOpen = indexedDB.open.bind(indexedDB);
            Object.defineProperty(indexedDB, 'open', {
                configurable: true,
                value: (...args) => {
                    if (window.__failOfflineEntitlementDiscovery
                        && args[0] === 'FishingLogBookOfflineAccess') {
                        throw new DOMException('Simulated discovery failure', 'UnknownError');
                    }
                    return originalOpen(...args);
                }
            });
        });

        apiGuard.enable();
        await reopenColdOffline(context, page);
        await expect(page.locator('#landing-offline-availability-failed')).toBeVisible({ timeout: 15000 });
        await expect(page.locator('#landing-open-offline')).toHaveCount(0);
        expect(await credentialGetCalls(page)).toBe(0);
        expect(apiGuard.requests).toEqual([]);

        await page.evaluate(() => { window.__failOfflineEntitlementDiscovery = false; });
        await page.locator('#landing-offline-availability-retry').click();

        await expect(page.locator('#landing-open-offline')).toBeVisible();
        await expect(page.locator('#landing-offline-availability-failed')).toHaveCount(0);
        expect(await credentialGetCalls(page)).toBe(0);
        expect(apiGuard.requests).toEqual([]);
    });

    test('unlocks explicitly, keeps owner data isolated, and records and edits across cold offline reloads', async ({ context, page }) => {
        test.setTimeout(120000);
        const apiGuard = guardOfflineApiRequests(context);
        await addPrfAuthenticator(page);
        await cachePublishedShell(page);
        await provisionOfflineOwner(page);
        await seedCachedPreferences(page);
        await seedOtherOwnersCatch(page);

        apiGuard.enable();
        await reopenColdOffline(context, page);
        await expect(page.locator('#landing-open-offline')).toBeVisible({ timeout: 15000 });
        expect(await credentialGetCalls(page)).toBe(0);

        await page.locator('#landing-open-offline').click();
        await expect(page).toHaveURL(/\/offline\/catches$/);
        await expect(page.locator('#offline-catch-list-empty')).toBeVisible();
        expect(await credentialGetCalls(page)).toBe(1);
        await expect(page.locator(`#catch-card-${otherCatchId}`)).toHaveCount(0);

        await page.locator('#offline-catch-record-link').click();
        await expect(page).toHaveURL(/\/offline\/record$/);
        await expect(page.locator('#record-catch-species-BrownTrout')).toBeVisible();
        await page.locator('#catch-photo-gallery').setInputFiles({
            name: 'offline-catch.jpg',
            mimeType: 'image/jpeg',
            buffer: Buffer.from([0xff, 0xd8, 0xff, 0xd9])
        });
        await expect(page.locator('#save-catch-button')).toBeEnabled();
        await page.locator('#save-catch-button').click();
        await expect(page.locator('#catch-saved')).toBeVisible();
        await page.locator('#catch-view-catches').click();

        const ownerCards = page.locator('.catch-card');
        await expect(ownerCards).toHaveCount(1);
        await expect(ownerCards.first()).toContainText('Brown Trout');
        const cardId = await ownerCards.first().getAttribute('id');
        expect(cardId).not.toBeNull();
        const catchId = cardId.replace('catch-card-', '');
        await page.locator(`#catch-card-link-${catchId}`).click();
        await expect(page).toHaveURL(new RegExp(`/offline/catches/${catchId}/edit$`));
        await page.locator('#offline-catch-edit-species-Pike').click();
        await page.locator('#offline-catch-edit-save').click();
        await expect(page).toHaveURL(/\/offline\/catches$/);
        await expect(page.locator(`#catch-card-${catchId}`)).toContainText('Pike');

        expect(apiGuard.requests).toEqual([]);

        await reopenColdOffline(context, page);
        await expect(page.locator('#landing-open-offline')).toBeVisible({ timeout: 15000 });
        expect(await credentialGetCalls(page)).toBe(0);
        await page.locator('#landing-open-offline').click();
        await expect(page).toHaveURL(/\/offline\/catches$/);
        await expect(page.locator(`#catch-card-${catchId}`)).toContainText('Pike');
        await expect(page.locator('.catch-card')).toHaveCount(1);
        expect(await credentialGetCalls(page)).toBe(1);
        expect(apiGuard.requests).toEqual([]);
    });
});

function guardOfflineApiRequests(context) {
    const requests = [];
    let enabled = false;
    context.on('request', request => {
        const url = new URL(request.url());
        if (!enabled || (url.origin !== configuredApiOrigin && !url.pathname.startsWith('/api/'))) return;
        requests.push(`${url.origin}${url.pathname}`);
        throw new Error(`Offline route attempted an API request: ${url.origin}${url.pathname}`);
    });
    return { requests, enable: () => { enabled = true; } };
}

function stubApiHealth(context, { reachable }) {
    return context.route(url => url.pathname.endsWith('/health'), async route => {
        if (!reachable) {
            await route.abort('connectionrefused');
            return;
        }

        if (route.request().method() === 'OPTIONS') {
            await route.fulfill({
                status: 204,
                headers: {
                    'Access-Control-Allow-Origin': '*',
                    'Access-Control-Allow-Headers': '*',
                    'Access-Control-Allow-Methods': 'GET,OPTIONS'
                }
            });
            return;
        }

        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            headers: {
                'Access-Control-Allow-Origin': '*'
            },
            body: '{"status":"Healthy"}'
        });
    });
}

async function cachePublishedShell(page) {
    await page.goto('/');
    await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 30000 });
    await page.evaluate(async () => navigator.serviceWorker?.ready);
    await page.reload();
    await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 30000 });
    await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker?.controller))).toBe(true);
}

async function addPrfAuthenticator(page) {
    const session = await page.context().newCDPSession(page);
    await session.send('WebAuthn.enable', { enableUI: false });
    await session.send('WebAuthn.addVirtualAuthenticator', {
        options: {
            protocol: 'ctap2',
            ctap2Version: 'ctap2_1',
            transport: 'internal',
            hasResidentKey: true,
            hasUserVerification: true,
            hasHmacSecret: true,
            hasPrf: true,
            automaticPresenceSimulation: true,
            isUserVerified: true
        }
    });
}

async function provisionOfflineOwner(page) {
    const result = await page.evaluate(async identity => {
        const stages = [];
        for (const operation of ['create', 'get']) {
            const original = navigator.credentials[operation].bind(navigator.credentials);
            navigator.credentials[operation] = async options => {
                stages.push(`${operation}:started`);
                try {
                    const credential = await original(options);
                    const extensionResults = credential?.getClientExtensionResults?.();
                    stages.push(`${operation}:completed:${Boolean(extensionResults?.prf?.results?.first)}`);
                    return credential;
                } catch (error) {
                    stages.push(`${operation}:failed:${error?.name ?? 'Error'}:${error?.message ?? ''}`);
                    throw error;
                }
            };
        }
        const offlineAccess = await import('./js/browser/offline-access.js');
        return { result: await offlineAccess.setupDevice(identity), stages };
    }, {
        provider: 'https://cognito-idp.test/pool',
        subject: 'offline-e2e-owner',
        userId: ownerUserId
    });
    expect(result.result, JSON.stringify(result.stages)).toEqual({ state: 'ready' });
}

async function seedCachedPreferences(page) {
    await page.evaluate(async userId => {
        const preferences = await import('./js/storage/preference-store.js');
        await preferences.putFishingPreferences(userId, JSON.stringify({
            catalogue: {
                methods: [{ id: 'aaaaaaaa-0000-0000-0000-000000000001', code: 'Fly', name: 'Fly' }],
                allSpecies: [
                    { id: 'bbbbbbbb-0000-0000-0000-000000000001', code: 'BrownTrout', name: 'Brown Trout' },
                    { id: 'bbbbbbbb-0000-0000-0000-000000000002', code: 'Pike', name: 'Pike' }
                ]
            },
            preferences: {
                methods: [{
                    fishingMethodId: 'aaaaaaaa-0000-0000-0000-000000000001',
                    code: 'Fly',
                    name: 'Fly',
                    isDefault: true,
                    species: [
                        { speciesId: 'bbbbbbbb-0000-0000-0000-000000000001', code: 'BrownTrout', name: 'Brown Trout', isDefault: true },
                        { speciesId: 'bbbbbbbb-0000-0000-0000-000000000002', code: 'Pike', name: 'Pike', isDefault: false }
                    ]
                }]
            },
            weightUnit: 0,
            lengthUnit: 0
        }));
    }, ownerUserId);
}

async function seedOtherOwnersCatch(page) {
    await page.evaluate(async ({ catchId, userId }) => {
        const photographId = '44444444-4444-4444-4444-444444444444';
        const catches = await import('./js/storage/catch-store.js');
        await catches.putCatchWithPhotographs(JSON.stringify({
            id: catchId,
            userId,
            anglerUserId: userId,
            recordedByUserId: userId,
            caughtOn: '2026-08-23T08:00:00+00:00',
            speciesName: 'Other Owner Fish',
            photographs: [{ id: photographId, catchId, contentType: 'image/jpeg' }]
        }), [{
            id: photographId,
            catchId,
            contentType: 'image/jpeg',
            bytes: new Uint8Array([0xff, 0xd8, 0xff, 0xd9])
        }]);
    }, { catchId: otherCatchId, userId: otherUserId });
}

async function reopenColdOffline(context, page) {
    await page.goto('about:blank');
    await context.addInitScript(() => {
        Object.defineProperty(Navigator.prototype, 'onLine', {
            configurable: true,
            get: () => false
        });
    });
    await context.setOffline(true);
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('#public-landing-page')).toBeVisible({ timeout: 15000 });
}

async function credentialGetCalls(page) {
    return page.evaluate(() => window.__credentialGetCalls ?? 0);
}
