import { appendFile } from 'node:fs/promises';
import { createPrivateKey, sign } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';

const apiBaseUrl = 'https://api.appstoreconnect.apple.com/v1';
const bundleId = 'com.catchbutdontforget.app';
const internalGroupName = 'CatchButDontForget';

export function nextBuildNumber(versions = []) {
    const values = versions.map(version => String(version ?? '').trim()).filter(Boolean);
    const nonInteger = values.filter(version => !/^\d+$/u.test(version));
    if (nonInteger.length > 0) {
        throw new Error(
            `Cannot select the next CFBundleVersion from non-integer Apple build numbers (${nonInteger.join(', ')}). Use integer build numbers.`);
    }

    const integers = values.map(version => BigInt(version));
    const latest = integers.reduce((left, right) => left > right ? left : right, 0n);
    return (latest + 1n).toString();
}

export function assertBuildStillAvailable(applicationVersion, versions = []) {
    const availableVersion = nextBuildNumber(versions);
    if (availableVersion !== applicationVersion) {
        throw new Error(
            `iOS CFBundleVersion ${applicationVersion} is no longer available; App Store Connect now requires ${availableVersion}. Run the workflow again.`);
    }
}

export function testersAvailability(processingState, assignedToInternalGroup) {
    if (processingState === 'INVALID' || processingState === 'FAILED') {
        return {
            available: false,
            message: `Apple processing failed (${processingState}). The IPA was uploaded but it is not available to internal testers.`,
        };
    }

    if (processingState !== 'VALID') {
        return {
            available: false,
            message: `The IPA was uploaded. Apple processing is ${processingState || 'not yet reported'}, so the build is not available to internal testers.`,
        };
    }

    if (!assignedToInternalGroup) {
        return {
            available: false,
            message: 'Apple processing succeeded, but the build is not in the CatchButDontForget internal group yet.',
        };
    }

    return {
        available: true,
        message: 'Apple processing succeeded and the build is assigned to the CatchButDontForget internal group.',
    };
}

function requiredEnvironment(name) {
    const value = process.env[name]?.trim();
    if (!value) {
        throw new Error(`Missing required environment value ${name}. See docs/ios-testflight-internal-testing.md.`);
    }

    return value;
}

function createToken(keyId, issuerId, privateKeyPem) {
    const header = Buffer.from(JSON.stringify({ alg: 'ES256', kid: keyId, typ: 'JWT' })).toString('base64url');
    const now = Math.floor(Date.now() / 1000);
    const payload = Buffer.from(JSON.stringify({
        iss: issuerId,
        iat: now,
        exp: now + 12 * 60,
        aud: 'appstoreconnect-v1',
    })).toString('base64url');
    const key = createPrivateKey(privateKeyPem);
    const signature = sign('SHA256', Buffer.from(`${header}.${payload}`), {
        key,
        dsaEncoding: 'ieee-p1363',
    }).toString('base64url');
    return `${header}.${payload}.${signature}`;
}

async function requestJson(url, token, options = {}) {
    const response = await fetch(url, {
        ...options,
        headers: {
            Authorization: `Bearer ${token}`,
            Accept: 'application/json',
            ...options.headers,
        },
    });
    const responseText = await response.text();
    if (!response.ok) {
        let detail = response.statusText;
        try {
            const parsed = JSON.parse(responseText);
            detail = parsed.errors?.map(error => error.detail).filter(Boolean).join('; ') || detail;
        } catch {
            // Keep the HTTP status text when Apple did not return JSON.
        }

        throw new Error(`App Store Connect API request failed (${response.status}): ${detail}`);
    }

    return responseText ? JSON.parse(responseText) : {};
}

async function collectPages(path, token) {
    const items = [];
    let url = `${apiBaseUrl}/${path}`;
    while (url) {
        const page = await requestJson(url, token);
        items.push(...(page.data ?? []));
        url = page.links?.next ?? null;
    }

    return items;
}

async function findApp(token) {
    const apps = await collectPages(`apps?filter[bundleId]=${encodeURIComponent(bundleId)}`, token);
    if (apps.length !== 1) {
        throw new Error(`Expected one App Store Connect app for ${bundleId} but found ${apps.length}.`);
    }

    return apps[0];
}

async function listBuildVersions(appId, token) {
    const builds = await collectPages(
        `builds?filter[app]=${encodeURIComponent(appId)}&fields[builds]=version,processingState,expired&limit=200`,
        token);
    return {
        builds,
        versions: builds.map(build => build.attributes?.version).filter(Boolean),
    };
}

export function selectInternalGroup(groups = [], name = internalGroupName) {
    const matches = groups.filter(group => group.attributes?.name === name);
    if (matches.length !== 1) {
        throw new Error(
            `Expected one internal TestFlight group named ${name} but found ${matches.length}.`);
    }

    return matches[0];
}

export function assertInternalGroupForApp(group, appId) {
    if (group?.attributes?.name !== internalGroupName) {
        throw new Error(`Expected internal TestFlight group ${internalGroupName}.`);
    }

    if (group.attributes?.isInternalGroup === false) {
        throw new Error(`${internalGroupName} is not marked as an internal TestFlight group.`);
    }

    const relatedAppId = group.relationships?.app?.data?.id;
    if (relatedAppId && relatedAppId !== appId) {
        throw new Error(`${internalGroupName} belongs to app ${relatedAppId}, not ${appId}.`);
    }

    return group;
}

export function assertBuildEligibleForInternalTesting(build, appId, applicationVersion) {
    if (String(build?.attributes?.version) !== String(applicationVersion)) {
        throw new Error(
            `Expected CFBundleVersion ${applicationVersion} but found ${build?.attributes?.version ?? 'none'}.`);
    }

    if (build.attributes?.processingState !== 'VALID') {
        throw new Error(testersAvailability(build.attributes?.processingState, false).message);
    }

    if (build.attributes?.expired === true) {
        throw new Error(
            `iOS CFBundleVersion ${applicationVersion} is expired and is not eligible for internal testing.`);
    }

    const relatedAppId = build.relationships?.app?.data?.id;
    if (relatedAppId && relatedAppId !== appId) {
        throw new Error(`Build ${applicationVersion} belongs to app ${relatedAppId}, not ${appId}.`);
    }

    return build;
}

export function internalGroupAssignmentRequest(buildId, groupId) {
    return {
        method: 'POST',
        url: `${apiBaseUrl}/builds/${encodeURIComponent(buildId)}/relationships/betaGroups`,
        body: {
            data: [{ type: 'betaGroups', id: groupId }],
        },
    };
}

export function listBuildsForBetaGroupPath(groupId) {
    return `betaGroups/${encodeURIComponent(groupId)}/builds?limit=200`;
}

export function groupContainsBuild(builds, buildId) {
    return Boolean(buildId && (builds ?? []).some(item => item.id === buildId));
}

async function findInternalGroup(appId, token) {
    // GET /v1/apps/{id}/betaGroups does not accept filter[name] (HTTP 400).
    // List groups for the app and select CatchButDontForget locally.
    const groups = await collectPages(
        `betaGroups?filter[app]=${encodeURIComponent(appId)}&fields[betaGroups]=name,isInternalGroup,hasAccessToAllBuilds&limit=200`,
        token);
    return assertInternalGroupForApp(selectInternalGroup(groups, internalGroupName), appId);
}

async function selectVersion(token, outputPath) {
    const app = await findApp(token);
    const listed = await listBuildVersions(app.id, token);
    const applicationVersion = nextBuildNumber(listed.versions);
    await appendFile(outputPath, `application_version=${applicationVersion}\napp_id=${app.id}\n`, 'utf8');
    process.stdout.write(`Selected iOS CFBundleVersion ${applicationVersion}.\n`);
}

async function assertVersion(token, applicationVersion) {
    const app = await findApp(token);
    const listed = await listBuildVersions(app.id, token);
    assertBuildStillAvailable(applicationVersion, listed.versions);
}

async function waitAndAssign(token, applicationVersion) {
    const app = await findApp(token);
    const group = await findInternalGroup(app.id, token);
    const attempts = Number.parseInt(process.env.APP_STORE_CONNECT_POLL_ATTEMPTS ?? '40', 10);
    const intervalMs = Number.parseInt(process.env.APP_STORE_CONNECT_POLL_MS ?? '15000', 10);
    let build;

    for (let attempt = 1; attempt <= attempts; attempt += 1) {
        const listed = await listBuildVersions(app.id, token);
        build = listed.builds.find(item => String(item.attributes?.version) === applicationVersion);
        const state = build?.attributes?.processingState;
        process.stdout.write(
            `Build ${applicationVersion} processingState=${state ?? 'missing'} (attempt ${attempt}/${attempts}).\n`);
        if (state === 'VALID') {
            break;
        }

        if (state === 'INVALID' || state === 'FAILED') {
            throw new Error(testersAvailability(state, false).message);
        }

        await delay(intervalMs);
    }

    assertBuildEligibleForInternalTesting(build, app.id, applicationVersion);

    if (groupHasVerifiedAllBuildsAccess(group)) {
        const availability = testersAvailability('VALID', true);
        process.stdout.write(`${availability.message}\n`);
        return;
    }

    const groupBuilds = await collectPages(listBuildsForBetaGroupPath(group.id), token);
    if (shouldSkipInternalGroupAssignment(group, groupBuilds, build.id)) {
        const availability = testersAvailability('VALID', true);
        process.stdout.write(`${availability.message}\n`);
        return;
    }

    const assignment = internalGroupAssignmentRequest(build.id, group.id);
    const assignResponse = await fetch(assignment.url, {
        method: assignment.method,
        headers: {
            Authorization: `Bearer ${token}`,
            Accept: 'application/json',
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(assignment.body),
    });
    const assignResult = {
        ok: assignResponse.ok,
        status: assignResponse.status,
        statusText: assignResponse.statusText,
        detail: await assignResponse.text(),
    };
    const confirmedBuilds = assignmentRequiresMembershipCheck(assignResult)
        ? await collectPages(listBuildsForBetaGroupPath(group.id), token)
        : [];
    const availability = confirmInternalGroupAssignment(assignResult, build.id, {
        internalGroupId: group.id,
        assignedGroupIds: groupContainsBuild(confirmedBuilds, build.id) ? [group.id] : [],
        isInternalGroup: group.attributes?.isInternalGroup === true,
        hasAccessToAllBuilds: group.attributes?.hasAccessToAllBuilds === true,
    });
    process.stdout.write(`${availability.message}\n`);
}

export function groupHasVerifiedAllBuildsAccess(group) {
    return group?.attributes?.isInternalGroup === true
        && group?.attributes?.hasAccessToAllBuilds === true;
}

export function shouldSkipInternalGroupAssignment(group, groupBuilds = [], buildId) {
    return groupHasVerifiedAllBuildsAccess(group) || groupContainsBuild(groupBuilds, buildId);
}

export function isInternalGroupAssignmentRejected(assignResult) {
    if (assignResult?.status !== 422) {
        return false;
    }

    const detail = `${assignResult.detail ?? ''} ${assignResult.statusText ?? ''}`;
    return /cannot add internal group to a build/iu.test(detail)
        || /builds cannot be assigned to this internal group/iu.test(detail);
}

function assignmentRequiresMembershipCheck(assignResult) {
    return assignResult.status === 409 || isInternalGroupAssignmentRejected(assignResult);
}

function groupAlreadyContainsBuild(membership) {
    const internalGroupId = membership.internalGroupId;
    return Boolean(internalGroupId && (membership.assignedGroupIds ?? []).includes(internalGroupId));
}

export function confirmInternalGroupAssignment(assignResult, buildId, membership = {}) {
    if (assignResult?.ok) {
        return testersAvailability('VALID', true);
    }

    const status = assignResult?.status;
    if (status === 409) {
        if (groupAlreadyContainsBuild(membership)) {
            return testersAvailability('VALID', true);
        }

        throw new Error(
            `App Store Connect returned HTTP 409 when assigning build ${buildId} to CatchButDontForget, but that group does not contain the build. Testers cannot access it.`);
    }

    if (isInternalGroupAssignmentRejected(assignResult)) {
        if (groupAlreadyContainsBuild(membership)) {
            return testersAvailability('VALID', true);
        }

        if (membership.isInternalGroup === true && membership.hasAccessToAllBuilds === true) {
            return testersAvailability('VALID', true);
        }

        throw new Error(
            `App Store Connect returned HTTP 422 when assigning build ${buildId} to CatchButDontForget, but that group does not contain the build. Testers cannot access it.`);
    }

    throw new Error(
        `App Store Connect API request failed (${status}): ${assignResult?.detail || assignResult?.statusText || 'unknown error'}`);
}

async function main() {
    const operation = process.argv[2];
    const token = createToken(
        requiredEnvironment('APP_STORE_CONNECT_KEY_ID'),
        requiredEnvironment('APP_STORE_CONNECT_ISSUER_ID'),
        requiredEnvironment('APP_STORE_CONNECT_PRIVATE_KEY'));

    if (operation === 'select-version') {
        await selectVersion(token, requiredEnvironment('GITHUB_OUTPUT'));
        return;
    }

    if (operation === 'assert-version') {
        await assertVersion(token, requiredEnvironment('APPLICATION_VERSION'));
        return;
    }

    if (operation === 'wait-and-assign') {
        await waitAndAssign(token, requiredEnvironment('APPLICATION_VERSION'));
        return;
    }

    throw new Error('Operation must be select-version, assert-version or wait-and-assign.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
