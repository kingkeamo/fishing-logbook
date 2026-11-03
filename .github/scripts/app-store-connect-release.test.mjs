import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { describe, it } from 'node:test';
import {
    assertBuildStillAvailable,
    nextBuildNumber,
    testersAvailability,
} from './app-store-connect-release.mjs';
import { resolveSignedIosIpa } from './resolve-signed-ios-ipa.mjs';

describe('signed iOS IPA resolution', () => {
    it('selects the IPA from a nested publish directory', async () => {
        const testDirectory = await mkdtemp(join(tmpdir(), 'ios-ipa-'));
        const publishDirectory = join(testDirectory, 'publish');
        const nested = join(publishDirectory, 'ios-arm64');
        const expectedIpa = join(nested, 'FishingLogBook.Mobile.ipa');

        try {
            await mkdir(nested, { recursive: true });
            await writeFile(expectedIpa, 'ipa');
            assert.equal(await resolveSignedIosIpa(publishDirectory), resolve(expectedIpa));
        } finally {
            await rm(testDirectory, { recursive: true, force: true });
        }
    });

    it('rejects ambiguity inside the publish directory', async () => {
        const publishDirectory = await mkdtemp(join(tmpdir(), 'ios-ipa-'));

        try {
            await writeFile(join(publishDirectory, 'first.ipa'), 'first');
            await writeFile(join(publishDirectory, 'second.ipa'), 'second');
            await assert.rejects(
                resolveSignedIosIpa(publishDirectory),
                /Expected exactly one signed IPA.*found 2/u);
        } finally {
            await rm(publishDirectory, { recursive: true, force: true });
        }
    });
});

describe('App Store Connect iOS build numbering', () => {
    it('starts at one when Apple has no uploaded builds', () => {
        assert.equal(nextBuildNumber(), '1');
    });

    it('increments the highest integer CFBundleVersion', () => {
        assert.equal(nextBuildNumber(['2', '41', '7']), '42');
    });

    it('fails explicitly on non-integer Apple build numbers', () => {
        assert.throws(
            () => nextBuildNumber(['1.0.1']),
            /non-integer Apple build numbers/);
    });

    it('accepts the selected version when App Store Connect state is unchanged', () => {
        assert.doesNotThrow(() => assertBuildStillAvailable('42', ['41']));
    });

    it('fails safely when another upload has consumed the selected version', () => {
        assert.throws(
            () => assertBuildStillAvailable('42', ['42']),
            /42 is no longer available; App Store Connect now requires 43/);
    });
});

describe('TestFlight tester availability', () => {
    it('does not report tester access while Apple is still processing', () => {
        const result = testersAvailability('PROCESSING', false);
        assert.equal(result.available, false);
        assert.match(result.message, /not available to internal testers/);
    });

    it('does not report tester access after a failed Apple processing state', () => {
        const result = testersAvailability('INVALID', false);
        assert.equal(result.available, false);
        assert.match(result.message, /processing failed/);
    });

    it('reports tester access only after a valid build is assigned to the internal group', () => {
        const result = testersAvailability('VALID', true);
        assert.equal(result.available, true);
        assert.match(result.message, /CatchButDontForget/);
    });
});

describe('mobile TestFlight workflow security', () => {
    const workflow = readFileSync(new URL('../workflows/mobile-testflight.yml', import.meta.url), 'utf8');
    const artifactJob = workflow.slice(
        workflow.indexOf('  artifact-only:'),
        workflow.indexOf('  publish-internal:'));
    const publishingJob = workflow.slice(workflow.indexOf('  publish-internal:'));

    it('is manual, serialized, and restricted to main', () => {
        assert.match(workflow, /workflow_dispatch:/);
        assert.doesNotMatch(workflow, /\n\s*push:|\n\s*pull_request:/);
        assert.match(workflow, /group: mobile-testflight/);
        assert.match(workflow, /cancel-in-progress: false/);
        assert.match(workflow, /refs\/heads\/main/);
        assert.match(workflow, /ref: main/);
    });

    it('keeps artifact-only builds outside the protected publishing environment', () => {
        assert.match(artifactJob, /if: inputs\.publish_to_testflight == false/);
        assert.match(artifactJob, /contents: read/);
        assert.doesNotMatch(artifactJob, /environment:|APP_STORE_CONNECT_PRIVATE_KEY|altool/);
        assert.match(publishingJob, /if: inputs\.publish_to_testflight == true/);
        assert.match(publishingJob, /environment: app-store-connect-internal/);
    });

    it('uses the same isolated signing helper and retains the IPA in both paths', () => {
        assert.equal(workflow.match(/build-signed-ios-ipa\.ps1/gu)?.length, 2);
        assert.equal(workflow.match(/actions\/upload-artifact@v4/gu)?.length, 2);
        assert.match(artifactJob, /APPLICATION_VERSION: \$\{\{ inputs\.application_version \}\}/);
        assert.match(publishingJob, /APPLICATION_VERSION: \$\{\{ steps\.testflight-version\.outputs\.application_version \}\}/);
    });

    it('uploads to TestFlight internal testing without App Store review', () => {
        assert.match(workflow, /publish_to_testflight:/);
        assert.match(publishingJob, /altool --upload-app/);
        assert.match(publishingJob, /wait-and-assign/);
        assert.doesNotMatch(workflow, /submit-for-review|appStoreVersionReleaseRequest|production track/);
        assert.doesNotMatch(workflow, /contents: write/);
    });
});
