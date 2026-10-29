import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { createInternalTrack, nextVersionCode } from './google-play-release.mjs';

describe('Google Play Android version selection', () => {
    it('starts at one when Play has no uploaded artifacts', () => {
        assert.equal(nextVersionCode(), '1');
    });

    it('increments the highest bundle including inactive or superseded bundles', () => {
        const bundles = [{ versionCode: '2' }, { versionCode: '41' }, { versionCode: '7' }];
        assert.equal(nextVersionCode(bundles), '42');
    });

    it('also accounts for legacy APK version codes', () => {
        const bundles = [{ versionCode: '9' }];
        const apks = [{ versionCode: '12' }];
        assert.equal(nextVersionCode(bundles, apks), '13');
    });

    it('rejects a version code beyond the Android limit', () => {
        assert.throws(
            () => nextVersionCode([{ versionCode: '2100000000' }]),
            /exceeds 2100000000/);
    });
});

describe('Google Play internal track release', () => {
    it('creates only a completed internal release', () => {
        assert.deepEqual(createInternalTrack('3', '0.1.2', 'Internal fixes.'), {
            track: 'internal',
            releases: [{
                status: 'completed',
                versionCodes: ['3'],
                name: '0.1.2',
                releaseNotes: [{ language: 'en-GB', text: 'Internal fixes.' }],
            }],
        });
    });

    it('omits optional release metadata when it is not supplied', () => {
        assert.deepEqual(createInternalTrack('3', '', ''), {
            track: 'internal',
            releases: [{ status: 'completed', versionCodes: ['3'] }],
        });
    });
});

describe('mobile Play workflow security', () => {
    const workflow = readFileSync(new URL('../workflows/mobile-play-bundle.yml', import.meta.url), 'utf8');

    it('is manual, serialized, and restricted to main', () => {
        assert.match(workflow, /workflow_dispatch:/);
        assert.doesNotMatch(workflow, /\n\s*push:|\n\s*pull_request:/);
        assert.match(workflow, /group: mobile-play-bundle/);
        assert.match(workflow, /cancel-in-progress: false/);
        assert.match(workflow, /refs\/heads\/main/);
        assert.match(workflow, /ref: main/);
    });

    it('uses short-lived OIDC credentials and least-privilege GitHub permissions', () => {
        assert.match(workflow, /google-github-actions\/auth@[a-f0-9]{40} # v3/);
        assert.match(workflow, /id-token: write/);
        assert.match(workflow, /contents: read/);
        assert.match(workflow, /create_credentials_file: false/);
        assert.doesNotMatch(workflow, /credentials_json|SERVICE_ACCOUNT_JSON|contents: write/);
    });

    it('publishes only to the internal track and retains an artifact-only fallback', () => {
        const publisher = readFileSync(new URL('./google-play-release.mjs', import.meta.url), 'utf8');
        assert.match(workflow, /publish_to_play:/);
        assert.match(publisher, /track: 'internal'/);
        assert.match(publisher, /tracks\/internal/);
        assert.match(workflow, /actions\/upload-artifact@v4/);
        assert.doesNotMatch(workflow, /production|closed|open testing/);
    });

    it('queries Play version history and validates before committing the edit', () => {
        const publisher = readFileSync(new URL('./google-play-release.mjs', import.meta.url), 'utf8');
        assert.match(publisher, /\/bundles/);
        assert.match(publisher, /\/apks/);
        assert.match(publisher, /:validate/);
        assert.match(publisher, /changesInReviewBehavior=ERROR_IF_IN_REVIEW/);
        assert.match(publisher, /status: 'completed'/);
    });
});
