import { webcrypto } from 'node:crypto';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
    getProbeStatus,
    provisionTestCredential,
    removeProbeMetadata,
    testOfflineUnlock,
    verifyOnlineCredential
} from './webauthn-capability-probe.js';

function prfResult(value = 7) {
    return new Uint8Array(32).fill(value).buffer;
}

function authenticatorData(verified = true) {
    const value = new Uint8Array(33);
    value[32] = verified ? 0x05 : 0x01;
    return value.buffer;
}

function createdCredential({ enabled = true, result = null } = {}) {
    return {
        rawId: new Uint8Array([1, 2, 3, 4]).buffer,
        response: { getTransports: () => ['internal'] },
        getClientExtensionResults: () => ({
            prf: {
                enabled,
                ...(result === null ? {} : { results: { first: result } })
            }
        })
    };
}

function assertion({ verified = true, result = prfResult() } = {}) {
    return {
        response: { authenticatorData: authenticatorData(verified) },
        getClientExtensionResults: () => ({
            prf: result === null ? {} : { results: { first: result } }
        })
    };
}

function configureWebAuthn({ platform = true, create, get } = {}) {
    class TestPublicKeyCredential {
        static isUserVerifyingPlatformAuthenticatorAvailable = vi.fn(async () => platform);
    }

    vi.stubGlobal('PublicKeyCredential', TestPublicKeyCredential);
    Object.defineProperty(navigator, 'credentials', {
        configurable: true,
        value: {
            create: create ?? vi.fn(async () => createdCredential()),
            get: get ?? vi.fn(async () => assertion())
        }
    });
}

function setOnline(value) {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value });
}

describe('WebAuthn capability probe', () => {
    beforeEach(() => {
        localStorage.clear();
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
        vi.stubGlobal('crypto', webcrypto);
        setOnline(true);
        configureWebAuthn();
    });

    it('reports an unsupported browser without invoking a credential ceremony', async () => {
        vi.stubGlobal('PublicKeyCredential', undefined);

        const result = await getProbeStatus();

        expect(result.webAuthnAvailable).toBe(false);
        expect(result.outcome).toBe('unsupported');
        expect(result.platformAuthenticatorAvailable).toBeNull();
    });

    it('stops before CREATE when no platform authenticator is available', async () => {
        const create = vi.fn();
        configureWebAuthn({ platform: false, create });

        const result = await provisionTestCredential();

        expect(result.outcome).toBe('platform-unavailable');
        expect(result.platformAuthenticatorAvailable).toBe(false);
        expect(create).not.toHaveBeenCalled();
    });

    it('records CREATE capability without automatically invoking GET', async () => {
        const create = vi.fn(async () => createdCredential({ enabled: true, result: null }));
        const get = vi.fn(async () => assertion());
        configureWebAuthn({ create, get });

        const result = await provisionTestCredential();

        expect(result.credentialCreated).toBe(true);
        expect(result.createPrfEnabled).toBe(true);
        expect(result.createPrfResultReturned).toBe(false);
        expect(result.getSucceeded).toBe(false);
        expect(create).toHaveBeenCalledTimes(1);
        expect(get).not.toHaveBeenCalled();
        expect(create.mock.calls[0][0].publicKey.authenticatorSelection.userVerification).toBe('required');
    });

    it('reports CREATE PRF result bytes without returning or storing them', async () => {
        const secret = prfResult(99);
        const consoleSpies = [
            vi.spyOn(console, 'log'),
            vi.spyOn(console, 'info'),
            vi.spyOn(console, 'warn'),
            vi.spyOn(console, 'error')
        ];
        configureWebAuthn({
            create: vi.fn(async () => createdCredential({ result: secret })),
            get: vi.fn(async () => assertion({ result: secret }))
        });

        const result = await provisionTestCredential();
        const verified = await verifyOnlineCredential();
        const stored = JSON.parse(localStorage.getItem(localStorage.key(0)));

        expect(result.createPrfResultReturned).toBe(true);
        expect(JSON.stringify(result)).not.toContain('99');
        expect(JSON.stringify(verified)).not.toContain('99');
        expect(Object.keys(stored).sort()).toEqual([
            'ciphertext', 'credentialId', 'iv', 'prfSalt', 'transports', 'version'
        ]);
        expect(stored).not.toHaveProperty('prfResult');
        for (const spy of consoleSpies) {
            expect(spy).not.toHaveBeenCalled();
        }
    });

    it('verifies online GET and encrypts the harmless payload after a separate explicit call', async () => {
        const get = vi.fn(async () => assertion());
        configureWebAuthn({ get });
        await provisionTestCredential();

        const result = await verifyOnlineCredential();

        expect(result.getSucceeded).toBe(true);
        expect(result.userVerified).toBe(true);
        expect(result.getPrfExtensionReported).toBe(true);
        expect(result.getPrfResultReturned).toBe(true);
        expect(result.testPayloadVerified).toBe(true);
        expect(result.outcome).toBe('verified-online');
        expect(get).toHaveBeenCalledTimes(1);
        expect(get.mock.calls[0][0].publicKey.userVerification).toBe('required');
    });

    it('preserves metadata but reports missing PRF when online GET has no result', async () => {
        configureWebAuthn({ get: vi.fn(async () => assertion({ result: null })) });
        await provisionTestCredential();

        const result = await verifyOnlineCredential();

        expect(result.outcome).toBe('verified-online');
        expect(result.hasProbeMetadata).toBe(true);
        expect(result.getPrfResultReturned).toBe(false);
        expect(result.testPayloadVerified).toBe(false);
        expect(localStorage.length).toBe(1);
    });

    it('treats user cancellation as a normal allow-listed result', async () => {
        const cancelled = new DOMException('private browser message', 'NotAllowedError');
        configureWebAuthn({ create: vi.fn(async () => { throw cancelled; }) });

        const result = await provisionTestCredential();

        expect(result.outcome).toBe('cancelled');
        expect(JSON.stringify(result)).not.toContain('private browser message');
    });

    it('retrieves and decrypts the harmless payload while offline after provisioning', async () => {
        const get = vi.fn(async () => assertion());
        configureWebAuthn({ get });
        await provisionTestCredential();
        await verifyOnlineCredential();
        setOnline(false);

        const result = await testOfflineUnlock();

        expect(result.isOnlineAtInvocation).toBe(false);
        expect(result.getSucceeded).toBe(true);
        expect(result.userVerified).toBe(true);
        expect(result.getPrfResultReturned).toBe(true);
        expect(result.testPayloadVerified).toBe(true);
        expect(result.outcome).toBe('retrieved');
        expect(get).toHaveBeenCalledTimes(2);
    });

    it('reports missing metadata without invoking GET', async () => {
        const get = vi.fn();
        configureWebAuthn({ get });

        const result = await testOfflineUnlock();

        expect(result.outcome).toBe('missing-metadata');
        expect(get).not.toHaveBeenCalled();
    });

    it('reports an offline retrieval failure without exposing the browser error', async () => {
        await provisionTestCredential();
        const get = vi.fn(async () => { throw new Error('sensitive authenticator detail'); });
        configureWebAuthn({ get });
        setOnline(false);

        const result = await testOfflineUnlock();

        expect(result.outcome).toBe('failed');
        expect(result.getSucceeded).toBe(false);
        expect(JSON.stringify(result)).not.toContain('sensitive authenticator detail');
    });

    it('reports a successful assertion without claiming user verification when UV is absent', async () => {
        await provisionTestCredential();
        configureWebAuthn({ get: vi.fn(async () => assertion({ verified: false })) });

        const result = await verifyOnlineCredential();

        expect(result.getSucceeded).toBe(true);
        expect(result.userVerified).toBe(false);
    });

    it('removes only the probe metadata owned by the module', async () => {
        localStorage.setItem('unrelated', 'keep');
        await provisionTestCredential();

        removeProbeMetadata();

        expect(localStorage.getItem('unrelated')).toBe('keep');
        expect((await getProbeStatus()).hasProbeMetadata).toBe(false);
    });
});
