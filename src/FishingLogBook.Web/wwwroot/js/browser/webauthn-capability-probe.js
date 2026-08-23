const storageKey = 'fishingLogBook.webAuthnCapabilityProbe.v1';
const payload = new TextEncoder().encode('cbdf-webauthn-capability-probe');

const outcomes = Object.freeze({
    unknown: 'unknown',
    ready: 'ready',
    unsupported: 'unsupported',
    platformUnavailable: 'platform-unavailable',
    provisioned: 'provisioned',
    verifiedOnline: 'verified-online',
    retrieved: 'retrieved',
    missingMetadata: 'missing-metadata',
    cancelled: 'cancelled',
    failed: 'failed'
});

function emptyResult(outcome = outcomes.unknown) {
    return {
        webAuthnAvailable: isWebAuthnAvailable(),
        platformAuthenticatorAvailable: null,
        isOnlineAtInvocation: navigator.onLine,
        hasProbeMetadata: hasProbeMetadata(),
        credentialCreated: false,
        createPrfEnabled: null,
        createPrfResultReturned: false,
        getSucceeded: false,
        userVerified: false,
        getPrfExtensionReported: false,
        getPrfResultReturned: false,
        testPayloadVerified: false,
        outcome
    };
}

function isWebAuthnAvailable() {
    return typeof PublicKeyCredential !== 'undefined'
        && typeof navigator.credentials?.create === 'function'
        && typeof navigator.credentials?.get === 'function';
}

export function hasProbeMetadata() {
    return localStorage.getItem(storageKey) !== null;
}

async function platformAuthenticatorAvailable() {
    if (!isWebAuthnAvailable()
        || typeof PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable !== 'function') {
        return null;
    }

    return await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
}

function randomBytes(length) {
    return crypto.getRandomValues(new Uint8Array(length));
}

function toBase64Url(value) {
    let binary = '';
    for (const byte of new Uint8Array(value)) {
        binary += String.fromCharCode(byte);
    }

    return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}

function fromBase64Url(value) {
    const padded = value.replaceAll('-', '+').replaceAll('_', '/')
        .padEnd(Math.ceil(value.length / 4) * 4, '=');
    const binary = atob(padded);
    return Uint8Array.from(binary, character => character.charCodeAt(0));
}

function getPrfDetails(credential) {
    const prf = credential.getClientExtensionResults?.().prf;
    const first = prf?.results?.first;
    return {
        reported: prf !== undefined,
        enabled: typeof prf?.enabled === 'boolean' ? prf.enabled : null,
        result: first instanceof ArrayBuffer
            ? new Uint8Array(first)
            : ArrayBuffer.isView(first)
                ? new Uint8Array(first.buffer, first.byteOffset, first.byteLength)
                : null
    };
}

function userVerified(assertion) {
    const authenticatorData = new Uint8Array(assertion.response.authenticatorData);
    return authenticatorData.length > 32 && (authenticatorData[32] & 0x04) !== 0;
}

function readMetadata() {
    const stored = localStorage.getItem(storageKey);
    if (stored === null) {
        return null;
    }

    try {
        const metadata = JSON.parse(stored);
        if (metadata.version !== 1
            || typeof metadata.credentialId !== 'string'
            || typeof metadata.prfSalt !== 'string') {
            return null;
        }

        return metadata;
    } catch {
        return null;
    }
}

function writeMetadata(metadata) {
    localStorage.setItem(storageKey, JSON.stringify(metadata));
}

async function createPayloadEnvelope(prfResult) {
    const key = await crypto.subtle.importKey('raw', prfResult, 'AES-GCM', false, ['encrypt', 'decrypt']);
    const iv = randomBytes(12);
    const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, payload);
    const plaintext = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, ciphertext);
    const verified = new TextDecoder().decode(plaintext) === new TextDecoder().decode(payload);

    return {
        iv: toBase64Url(iv),
        ciphertext: toBase64Url(ciphertext),
        verified
    };
}

async function verifyPayloadEnvelope(metadata, prfResult) {
    if (typeof metadata.iv !== 'string' || typeof metadata.ciphertext !== 'string') {
        return false;
    }

    try {
        const key = await crypto.subtle.importKey('raw', prfResult, 'AES-GCM', false, ['decrypt']);
        const plaintext = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: fromBase64Url(metadata.iv) },
            key,
            fromBase64Url(metadata.ciphertext));
        return new TextDecoder().decode(plaintext) === new TextDecoder().decode(payload);
    } catch {
        return false;
    }
}

async function getCredential(metadata) {
    return await navigator.credentials.get({
        publicKey: {
            challenge: randomBytes(32),
            allowCredentials: [{
                id: fromBase64Url(metadata.credentialId),
                type: 'public-key',
                transports: Array.isArray(metadata.transports) ? metadata.transports : undefined
            }],
            userVerification: 'required',
            timeout: 60000,
            extensions: {
                prf: {
                    eval: { first: fromBase64Url(metadata.prfSalt) }
                }
            }
        }
    });
}

function failureOutcome(error) {
    return error?.name === 'NotAllowedError' ? outcomes.cancelled : outcomes.failed;
}

export async function getProbeStatus() {
    const result = emptyResult(isWebAuthnAvailable() ? outcomes.ready : outcomes.unsupported);
    try {
        result.platformAuthenticatorAvailable = await platformAuthenticatorAvailable();
    } catch {
        result.platformAuthenticatorAvailable = null;
    }

    return result;
}

export async function provisionTestCredential() {
    const result = emptyResult();
    if (!result.webAuthnAvailable) {
        result.outcome = outcomes.unsupported;
        return result;
    }

    try {
        result.platformAuthenticatorAvailable = await platformAuthenticatorAvailable();
        if (result.platformAuthenticatorAvailable === false) {
            result.outcome = outcomes.platformUnavailable;
            return result;
        }

        const prfSalt = randomBytes(32);
        const credential = await navigator.credentials.create({
            publicKey: {
                challenge: randomBytes(32),
                rp: { name: 'Catch But Don’t Forget' },
                user: {
                    id: randomBytes(32),
                    name: 'webauthn-capability-probe',
                    displayName: 'WebAuthn capability probe'
                },
                pubKeyCredParams: [
                    { type: 'public-key', alg: -7 },
                    { type: 'public-key', alg: -257 }
                ],
                authenticatorSelection: {
                    authenticatorAttachment: 'platform',
                    residentKey: 'preferred',
                    userVerification: 'required'
                },
                timeout: 60000,
                attestation: 'none',
                extensions: {
                    prf: { eval: { first: prfSalt } }
                }
            }
        });

        if (credential === null) {
            result.outcome = outcomes.failed;
            return result;
        }

        result.credentialCreated = true;
        const createPrf = getPrfDetails(credential);
        result.createPrfEnabled = createPrf.enabled;
        result.createPrfResultReturned = createPrf.result !== null;

        const metadata = {
            version: 1,
            credentialId: toBase64Url(credential.rawId),
            prfSalt: toBase64Url(prfSalt),
            transports: credential.response.getTransports?.() ?? []
        };
        writeMetadata(metadata);
        result.hasProbeMetadata = true;

        result.outcome = outcomes.provisioned;
        return result;
    } catch (error) {
        result.hasProbeMetadata = hasProbeMetadata();
        result.outcome = failureOutcome(error);
        return result;
    }
}

export async function verifyOnlineCredential() {
    const result = emptyResult();
    if (!result.webAuthnAvailable) {
        result.outcome = outcomes.unsupported;
        return result;
    }

    const metadata = readMetadata();
    if (metadata === null) {
        result.outcome = outcomes.missingMetadata;
        return result;
    }

    try {
        result.platformAuthenticatorAvailable = await platformAuthenticatorAvailable();
        const assertion = await getCredential(metadata);
        if (assertion === null) {
            result.outcome = outcomes.failed;
            return result;
        }

        result.getSucceeded = true;
        result.userVerified = userVerified(assertion);
        const getPrf = getPrfDetails(assertion);
        result.getPrfExtensionReported = getPrf.reported;
        result.getPrfResultReturned = getPrf.result !== null;

        if (getPrf.result !== null) {
            const envelope = await createPayloadEnvelope(getPrf.result);
            metadata.iv = envelope.iv;
            metadata.ciphertext = envelope.ciphertext;
            writeMetadata(metadata);
            result.testPayloadVerified = envelope.verified;
        }

        result.outcome = outcomes.verifiedOnline;
        return result;
    } catch (error) {
        result.outcome = failureOutcome(error);
        return result;
    }
}

export async function testOfflineUnlock() {
    const result = emptyResult();
    if (!result.webAuthnAvailable) {
        result.outcome = outcomes.unsupported;
        return result;
    }

    const metadata = readMetadata();
    if (metadata === null) {
        result.outcome = outcomes.missingMetadata;
        return result;
    }

    try {
        result.platformAuthenticatorAvailable = await platformAuthenticatorAvailable();
        const assertion = await getCredential(metadata);
        if (assertion === null) {
            result.outcome = outcomes.failed;
            return result;
        }

        result.getSucceeded = true;
        result.userVerified = userVerified(assertion);
        const getPrf = getPrfDetails(assertion);
        result.getPrfExtensionReported = getPrf.reported;
        result.getPrfResultReturned = getPrf.result !== null;
        result.testPayloadVerified = getPrf.result !== null
            && await verifyPayloadEnvelope(metadata, getPrf.result);
        result.outcome = outcomes.retrieved;
        return result;
    } catch (error) {
        result.outcome = failureOutcome(error);
        return result;
    }
}

export function removeProbeMetadata() {
    localStorage.removeItem(storageKey);
}
