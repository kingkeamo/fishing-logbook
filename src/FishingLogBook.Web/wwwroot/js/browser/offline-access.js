const databaseName = 'FishingLogBookOfflineAccess';
const databaseVersion = 1;
const storeName = 'deviceEntitlements';
const envelopeVersion = 1;
const purpose = 'cbdf-offline-access';

export async function getDeviceStatus(identity) {
    if (!isSupported()) return { state: 'unsupported' };
    const ownerKey = await createOwnerKey(identity.provider, identity.subject);
    const record = await getRecord(ownerKey);
    return { state: record?.state === 'ready' ? 'ready' : record ? 'repair' : 'not-configured' };
}

export async function setupDevice(identity) {
    if (!isSupported()) return { state: 'unsupported' };
    const ownerKey = await createOwnerKey(identity.provider, identity.subject);
    let stage = 'TrustedIdentityLoaded';
    let createPrfMatchesGet = null;
    try {
        const salt = randomBytes(32);
        stage = 'CredentialCreated';
        const credential = await navigator.credentials.create({ publicKey: createOptions(salt) });
        if (!credential) return failure('failed', 'CredentialCreated', 'MissingCredential');
        stage = completed('CredentialCreated');
        const createPrf = getPrfResult(credential);
        completed('PrfCreateAvailable', { available: createPrf !== null });

        let record = createCandidate(ownerKey, credential, salt);
        stage = 'CandidateStored';
        await putRecord(record);
        stage = completed('CandidateStored');

        stage = 'CredentialRetrieved';
        const assertion = await getCredential(record);
        if (!assertion) return failure('repair', 'CredentialRetrieved', 'MissingCredential');
        if (!userVerified(assertion)) return failure('repair', 'CredentialRetrieved', 'UserVerificationMissing');
        stage = completed('CredentialRetrieved');
        stage = 'PrfGetAvailable';
        const getPrf = getPrfResult(assertion);
        if (!getPrf) return failure('repair', 'PrfGetAvailable', 'MissingPrfResult');
        stage = completed('PrfGetAvailable');
        createPrfMatchesGet = createPrf === null ? null : equalBytes(createPrf, getPrf);
        completed('PrfCreateCompared', { matches: createPrfMatchesGet });

        stage = 'EntitlementEncrypted';
        record = await encryptCandidate(identity, record, getPrf);
        await putRecord(record);
        completed('EntitlementEncrypted');
        stage = 'EntitlementDecrypted';
        const recovered = await decryptEntitlement(record, getPrf);
        completed('EntitlementDecrypted');
        stage = 'IdentityVerified';
        if (!sameIdentity(identity, recovered))
            return failure('repair', 'IdentityVerified', 'IdentityMismatch', null, createPrfMatchesGet);
        completed('IdentityVerified');

        record.state = 'ready';
        record.verifiedOn = new Date().toISOString();
        stage = 'EntitlementReady';
        await putRecord(record);
        completed('EntitlementReady');
        return { state: 'ready', stage, createPrfMatchesGet };
    } catch (error) {
        return failure(
            error?.name === 'NotAllowedError' ? 'cancelled' : 'failed',
            stage,
            error?.name,
            error?.message,
            createPrfMatchesGet);
    }
}

export async function removeDevice(identity) {
    const ownerKey = await createOwnerKey(identity.provider, identity.subject);
    await deleteRecord(ownerKey);
}

function isSupported() {
    return typeof PublicKeyCredential !== 'undefined'
        && typeof navigator.credentials?.create === 'function'
        && typeof navigator.credentials?.get === 'function'
        && typeof crypto?.subtle !== 'undefined';
}

function createOptions(salt) {
    return {
        challenge: randomBytes(32),
        rp: { name: 'Catch But Don’t Forget' },
        user: { id: randomBytes(32), name: 'offline-access', displayName: 'Offline access' },
        pubKeyCredParams: [{ type: 'public-key', alg: -7 }, { type: 'public-key', alg: -257 }],
        authenticatorSelection: {
            authenticatorAttachment: 'platform',
            residentKey: 'preferred',
            userVerification: 'required'
        },
        timeout: 60000,
        attestation: 'none',
        extensions: { prf: { eval: { first: salt } } }
    };
}

async function getCredential(record) {
    return await navigator.credentials.get({ publicKey: {
        challenge: randomBytes(32),
        allowCredentials: [{
            id: fromBase64Url(record.credentialId),
            type: 'public-key',
            transports: record.transports
        }],
        userVerification: 'required',
        timeout: 60000,
        extensions: { prf: { eval: { first: fromBase64Url(record.prfSalt) } } }
    }});
}

function createCandidate(ownerKey, credential, salt) {
    return {
        ownerKey,
        version: envelopeVersion,
        state: 'candidate',
        credentialId: toBase64Url(credential.rawId),
        prfSalt: toBase64Url(salt),
        transports: credential.response.getTransports?.() ?? []
    };
}

async function encryptCandidate(identity, record, prf) {
    const iv = randomBytes(12);
    const aad = new TextEncoder().encode(`${purpose}:${record.version}:${record.ownerKey}:${record.credentialId}`);
    const key = await crypto.subtle.importKey('raw', prf, 'AES-GCM', false, ['encrypt']);
    const plaintext = new TextEncoder().encode(JSON.stringify({
        version: envelopeVersion,
        purpose,
        provider: identity.provider,
        subject: identity.subject,
        userId: identity.userId,
        configuredOn: new Date().toISOString()
    }));
    const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: aad }, key, plaintext);
    return {
        ...record,
        iv: toBase64Url(iv),
        ciphertext: toBase64Url(ciphertext)
    };
}

async function decryptEntitlement(record, prf) {
    const aad = new TextEncoder().encode(`${purpose}:${record.version}:${record.ownerKey}:${record.credentialId}`);
    const key = await crypto.subtle.importKey('raw', prf, 'AES-GCM', false, ['decrypt']);
    const plaintext = await crypto.subtle.decrypt({
        name: 'AES-GCM', iv: fromBase64Url(record.iv), additionalData: aad
    }, key, fromBase64Url(record.ciphertext));
    return JSON.parse(new TextDecoder().decode(plaintext));
}

function sameIdentity(expected, actual) {
    return actual?.version === envelopeVersion && actual?.purpose === purpose
        && actual.provider === expected.provider && actual.subject === expected.subject
        && actual.userId === expected.userId;
}

function getPrfResult(credential) {
    const first = credential.getClientExtensionResults?.().prf?.results?.first;
    if (first instanceof ArrayBuffer) return new Uint8Array(first);
    return ArrayBuffer.isView(first)
        ? new Uint8Array(first.buffer, first.byteOffset, first.byteLength)
        : null;
}

function userVerified(assertion) {
    const data = new Uint8Array(assertion.response.authenticatorData);
    return data.length > 32 && (data[32] & 0x04) !== 0;
}

function equalBytes(first, second) {
    if (first.byteLength !== second.byteLength) return false;
    return first.every((value, index) => value === second[index]);
}

function completed(stage, details) {
    console.info('[FLB] OfflineAccessSetup', { stage, ...details });
    return stage;
}

function failure(state, stage, errorName, errorMessage, createPrfMatchesGet = null) {
    const safeMessage = typeof errorMessage === 'string' ? errorMessage.slice(0, 160) : null;
    console.warn('[FLB] OfflineAccessSetup', {
        state,
        failedStage: stage,
        errorName: errorName ?? null,
        errorMessage: safeMessage,
        createPrfMatchesGet
    });
    return { state, stage, errorName: errorName ?? null, errorMessage: safeMessage, createPrfMatchesGet };
}

async function createOwnerKey(provider, subject) {
    const value = new TextEncoder().encode(`${provider}\n${subject}`);
    return toBase64Url(await crypto.subtle.digest('SHA-256', value));
}

function randomBytes(length) { return crypto.getRandomValues(new Uint8Array(length)); }
function toBase64Url(value) {
    let binary = '';
    for (const byte of new Uint8Array(value)) binary += String.fromCharCode(byte);
    return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}
function fromBase64Url(value) {
    const padded = value.replaceAll('-', '+').replaceAll('_', '/').padEnd(Math.ceil(value.length / 4) * 4, '=');
    return Uint8Array.from(atob(padded), character => character.charCodeAt(0));
}

async function openDatabase() {
    return await new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, databaseVersion);
        request.onupgradeneeded = () => {
            if (!request.result.objectStoreNames.contains(storeName))
                request.result.createObjectStore(storeName, { keyPath: 'ownerKey' });
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function transact(mode, action) {
    const database = await openDatabase();
    try {
        return await new Promise((resolve, reject) => {
            const transaction = database.transaction(storeName, mode);
            const request = action(transaction.objectStore(storeName));
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } finally { database.close(); }
}

function getRecord(ownerKey) { return transact('readonly', store => store.get(ownerKey)); }
function putRecord(record) { return transact('readwrite', store => store.put(record)); }
function deleteRecord(ownerKey) { return transact('readwrite', store => store.delete(ownerKey)); }
