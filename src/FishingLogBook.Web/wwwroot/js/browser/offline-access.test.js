import { webcrypto } from 'node:crypto';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getDeviceStatus, removeDevice, setupDevice } from './offline-access.js';

const identity = {
    userId: '11111111-1111-1111-1111-111111111111',
    provider: 'Cognito',
    subject: 'stable-cognito-subject'
};

function credential(prf) {
    return {
        rawId: new Uint8Array([1, 2, 3, 4]).buffer,
        response: { getTransports: () => ['internal'] },
        getClientExtensionResults: () => ({ prf: { results: { first: prf } } })
    };
}

function assertion(prf) {
    const authenticatorData = new Uint8Array(33);
    authenticatorData[32] = 0x05;
    return {
        response: { authenticatorData: authenticatorData.buffer },
        getClientExtensionResults: () => ({ prf: { results: { first: prf } } })
    };
}

describe('production offline access entitlement', () => {
    beforeEach(() => {
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
        vi.stubGlobal('crypto', webcrypto);
        vi.stubGlobal('PublicKeyCredential', class {});
    });

    it('becomes ready only after a separate GET recovers and verifies the encrypted identity', async () => {
        const prf = new Uint8Array(32).fill(9).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});

        const result = await setupDevice(identity);

        expect(result.state).toBe('ready');
        expect(navigator.credentials.create).toHaveBeenCalledOnce();
        expect(navigator.credentials.get).toHaveBeenCalledOnce();
        expect((await getDeviceStatus(identity)).state).toBe('ready');
        const persisted = await readPersistedRecords();
        expect(JSON.stringify(persisted)).not.toContain(identity.subject);
        expect(JSON.stringify(persisted)).not.toContain(identity.userId);
    });

    it('fails closed when GET returns different PRF material', async () => {
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(new Uint8Array(32).fill(1).buffer)),
            get: vi.fn(async () => assertion(new Uint8Array(32).fill(2).buffer))
        }});

        const result = await setupDevice(identity);

        expect(result.state).toBe('failed');
        expect((await getDeviceStatus(identity)).state).toBe('repair');
    });

    it('removes only the matching owner record', async () => {
        const prf = new Uint8Array(32).fill(7).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});
        const other = { ...identity, userId: '22222222-2222-2222-2222-222222222222', subject: 'other-subject' };
        await setupDevice(identity);
        await setupDevice(other);

        await removeDevice(identity);

        expect((await getDeviceStatus(identity)).state).toBe('not-configured');
        expect((await getDeviceStatus(other)).state).toBe('ready');
    });
});

async function readPersistedRecords() {
    const database = await new Promise((resolve, reject) => {
        const request = indexedDB.open('FishingLogBookOfflineAccess', 1);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    try {
        return await new Promise((resolve, reject) => {
            const request = database.transaction('deviceEntitlements').objectStore('deviceEntitlements').getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } finally {
        database.close();
    }
}
