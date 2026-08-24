import { webcrypto } from 'node:crypto';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getDeviceStatus, hasReadyEntitlement, removeDevice, setupDevice, unlockDevice } from './offline-access.js';

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

function assertion(prf, rawId = new Uint8Array([1, 2, 3, 4]).buffer) {
    const authenticatorData = new Uint8Array(33);
    authenticatorData[32] = 0x05;
    return {
        rawId,
        response: { authenticatorData: authenticatorData.buffer },
        getClientExtensionResults: () => ({ prf: { results: { first: prf } } })
    };
}

describe('production offline access entitlement', () => {
    beforeEach(async () => {
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
        vi.stubGlobal('crypto', webcrypto);
        vi.stubGlobal('PublicKeyCredential', class {});
        await deleteDatabase();
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

    it('discovers and unlocks a ready entitlement without exposing identity metadata', async () => {
        const prf = new Uint8Array(32).fill(6).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});
        await setupDevice(identity);
        navigator.credentials.get.mockClear();

        const available = await hasReadyEntitlement();
        const result = await unlockDevice();

        expect(available).toBe(true);
        expect(result).toEqual({ state: 'unlocked', userId: identity.userId, version: 1 });
        expect(navigator.credentials.get).toHaveBeenCalledOnce();
        expect(navigator.credentials.get.mock.calls[0][0].publicKey.allowCredentials).toHaveLength(1);
    });

    it('passes all ready credential ids in one request and unlocks the selected record', async () => {
        const firstPrf = new Uint8Array(32).fill(3).buffer;
        const secondPrf = new Uint8Array(32).fill(4).buffer;
        const secondCredentialId = new Uint8Array([5, 6, 7, 8]).buffer;
        const other = { ...identity, userId: '22222222-2222-2222-2222-222222222222', subject: 'other-subject' };
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn()
                .mockResolvedValueOnce(credential(firstPrf))
                .mockResolvedValueOnce({ ...credential(secondPrf), rawId: secondCredentialId }),
            get: vi.fn()
                .mockResolvedValueOnce(assertion(firstPrf))
                .mockResolvedValueOnce(assertion(secondPrf, secondCredentialId))
        }});
        await setupDevice(identity);
        await setupDevice(other);
        navigator.credentials.get.mockClear();
        navigator.credentials.get.mockResolvedValue(assertion(secondPrf, secondCredentialId));

        const result = await unlockDevice();

        expect(result.userId).toBe(other.userId);
        expect(navigator.credentials.get.mock.calls[0][0].publicKey.allowCredentials).toHaveLength(2);
    });

    it('fails closed when the selected credential does not map to a ready record', async () => {
        const prf = new Uint8Array(32).fill(8).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});
        await setupDevice(identity);
        navigator.credentials.get.mockResolvedValue(assertion(prf, new Uint8Array([9, 9, 9]).buffer));

        const result = await unlockDevice();

        expect(result.state).toBe('failed');
    });

    it('fails closed when GET does not return PRF material', async () => {
        const prf = new Uint8Array(32).fill(8).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});
        await setupDevice(identity);
        navigator.credentials.get.mockResolvedValue(assertion(null));

        const result = await unlockDevice();

        expect(result.state).toBe('failed');
    });

    it('fails closed when the encrypted entitlement is tampered with', async () => {
        const prf = new Uint8Array(32).fill(8).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});
        await setupDevice(identity);
        const [record] = await readPersistedRecords();
        record.ciphertext = `${record.ciphertext.slice(0, -1)}A`;
        await writePersistedRecord(record);

        const result = await unlockDevice();

        expect(result.state).toBe('failed');
    });

    it('does not offer or unlock an unsupported entitlement version', async () => {
        const prf = new Uint8Array(32).fill(8).buffer;
        Object.defineProperty(navigator, 'credentials', { configurable: true, value: {
            create: vi.fn(async () => credential(prf)),
            get: vi.fn(async () => assertion(prf))
        }});
        await setupDevice(identity);
        const [record] = await readPersistedRecords();
        record.version = 2;
        await writePersistedRecord(record);

        const available = await hasReadyEntitlement();
        const result = await unlockDevice();

        expect(available).toBe(false);
        expect(result.state).toBe('not-configured');
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

async function writePersistedRecord(record) {
    const database = await new Promise((resolve, reject) => {
        const request = indexedDB.open('FishingLogBookOfflineAccess', 1);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    try {
        await new Promise((resolve, reject) => {
            const request = database.transaction('deviceEntitlements', 'readwrite').objectStore('deviceEntitlements').put(record);
            request.onsuccess = () => resolve();
            request.onerror = () => reject(request.error);
        });
    } finally {
        database.close();
    }
}

async function deleteDatabase() {
    await new Promise((resolve, reject) => {
        const request = indexedDB.deleteDatabase('FishingLogBookOfflineAccess');
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error('Offline access test database deletion was blocked'));
    });
}
