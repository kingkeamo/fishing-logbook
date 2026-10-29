import { appendFile, readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

const publisherBaseUrl = 'https://androidpublisher.googleapis.com/androidpublisher/v3';
const publisherUploadBaseUrl = 'https://androidpublisher.googleapis.com/upload/androidpublisher/v3';
const maximumVersionCode = 2_100_000_000n;

export function nextVersionCode(bundles = [], apks = []) {
    const versionCodes = [...bundles, ...apks]
        .map(artifact => artifact?.versionCode)
        .filter(versionCode => versionCode !== undefined && versionCode !== null)
        .map(versionCode => BigInt(versionCode));
    const latest = versionCodes.length === 0
        ? 0n
        : versionCodes.reduce((left, right) => left > right ? left : right);
    const next = latest + 1n;

    if (next > maximumVersionCode) {
        throw new Error(`The next Android version code exceeds ${maximumVersionCode}.`);
    }

    return next.toString();
}

export function createInternalTrack(versionCode, releaseName, releaseNotes) {
    const release = {
        status: 'completed',
        versionCodes: [versionCode],
    };

    if (releaseName) {
        release.name = releaseName;
    }

    if (releaseNotes) {
        release.releaseNotes = [{ language: 'en-GB', text: releaseNotes }];
    }

    return { track: 'internal', releases: [release] };
}

export function assertVersionStillAvailable(applicationVersion, bundles = [], apks = []) {
    const availableVersion = nextVersionCode(bundles, apks);
    if (availableVersion !== applicationVersion) {
        throw new Error(
            `Android version code ${applicationVersion} is no longer available; Play now requires ${availableVersion}. Run the workflow again.`);
    }
}

async function requestJson(url, accessToken, options = {}) {
    const response = await fetch(url, {
        ...options,
        headers: {
            Authorization: `Bearer ${accessToken}`,
            ...options.headers,
        },
    });

    if (!response.ok) {
        const responseText = await response.text();
        let detail = response.statusText;
        try {
            const parsed = JSON.parse(responseText);
            detail = parsed.error?.message ?? detail;
        } catch {
            // Keep the HTTP status text when Play did not return JSON.
        }

        throw new Error(`Google Play API request failed (${response.status}): ${detail}`);
    }

    const responseText = await response.text();
    return responseText ? JSON.parse(responseText) : {};
}

async function insertEdit(packagePath, accessToken) {
    const edit = await requestJson(
        `${publisherBaseUrl}/applications/${packagePath}/edits`,
        accessToken,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });

    if (!/^\d+$/u.test(String(edit.id ?? ''))) {
        throw new Error('Google Play created an edit without returning a valid edit ID.');
    }

    return edit;
}

async function listArtifacts(packagePath, editId, accessToken) {
    const editPath = `${publisherBaseUrl}/applications/${packagePath}/edits/${encodeURIComponent(editId)}`;
    const [bundleResponse, apkResponse] = await Promise.all([
        requestJson(`${editPath}/bundles`, accessToken),
        requestJson(`${editPath}/apks`, accessToken),
    ]);

    return {
        bundles: bundleResponse.bundles ?? [],
        apks: apkResponse.apks ?? [],
    };
}

async function selectVersion(packageName, accessToken, outputPath) {
    const packagePath = encodeURIComponent(packageName);
    const edit = await insertEdit(packagePath, accessToken);
    const artifacts = await listArtifacts(packagePath, edit.id, accessToken);
    const applicationVersion = nextVersionCode(artifacts.bundles, artifacts.apks);

    await requestJson(
        `${publisherBaseUrl}/applications/${packagePath}/edits/${encodeURIComponent(edit.id)}`,
        accessToken,
        { method: 'DELETE' });

    await appendFile(outputPath, `application_version=${applicationVersion}\n`, 'utf8');
    process.stdout.write(`Selected Android version code ${applicationVersion}; the temporary Play edit was deleted.\n`);
}

async function publishEdit(packageName, accessToken, applicationVersion, bundlePath, releaseName, releaseNotes) {
    if (releaseNotes.length > 500) {
        throw new Error('Google Play release notes must not exceed 500 characters.');
    }

    const packagePath = encodeURIComponent(packageName);
    const edit = await insertEdit(packagePath, accessToken);
    const artifacts = await listArtifacts(packagePath, edit.id, accessToken);
    assertVersionStillAvailable(applicationVersion, artifacts.bundles, artifacts.apks);

    const editPath = `applications/${packagePath}/edits/${encodeURIComponent(edit.id)}`;
    const bundle = await readFile(bundlePath);
    const uploaded = await requestJson(
        `${publisherUploadBaseUrl}/${editPath}/bundles?uploadType=media`,
        accessToken,
        { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: bundle });

    if (String(uploaded.versionCode) !== applicationVersion) {
        throw new Error(
            `Google Play accepted version code ${uploaded.versionCode}, but ${applicationVersion} was expected.`);
    }

    const track = createInternalTrack(applicationVersion, releaseName, releaseNotes);
    await requestJson(
        `${publisherBaseUrl}/${editPath}/tracks/internal`,
        accessToken,
        { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(track) });
    await requestJson(
        `${publisherBaseUrl}/${editPath}:validate`,
        accessToken,
        { method: 'POST' });
    await requestJson(
        `${publisherBaseUrl}/${editPath}:commit?changesInReviewBehavior=ERROR_IF_IN_REVIEW`,
        accessToken,
        { method: 'POST' });

    process.stdout.write(`Published Android version code ${applicationVersion} to the internal track.\n`);
}

function requiredEnvironment(name) {
    const value = process.env[name]?.trim();
    if (!value) {
        throw new Error(`Missing required environment value ${name}.`);
    }

    return value;
}

async function main() {
    const operation = process.argv[2];
    const packageName = requiredEnvironment('GOOGLE_PLAY_PACKAGE_NAME');
    const accessToken = requiredEnvironment('GOOGLE_PLAY_ACCESS_TOKEN');

    if (operation === 'select-version') {
        await selectVersion(packageName, accessToken, requiredEnvironment('GITHUB_OUTPUT'));
        return;
    }

    if (operation === 'publish') {
        await publishEdit(
            packageName,
            accessToken,
            requiredEnvironment('APPLICATION_VERSION'),
            requiredEnvironment('ANDROID_APP_BUNDLE'),
            process.env.APPLICATION_DISPLAY_VERSION?.trim() ?? '',
            process.env.GOOGLE_PLAY_RELEASE_NOTES?.trim() ?? 'Internal testing build.');
        return;
    }

    throw new Error('Operation must be select-version or publish.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
