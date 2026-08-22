import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import { latestReleaseTag, nextReleaseTag, parseReleaseTag } from './release-version.mjs';

describe('release version calculation', () => {
    it('ignores invalid tags and rejects numeric versions with leading zeroes', () => {
        assert.equal(parseReleaseTag('release-1.2.3'), null);
        assert.equal(parseReleaseTag('v01.2.3'), null);
        assert.equal(parseReleaseTag('v1.2.3')?.tag, 'v1.2.3');
    });

    it('uses semantic numeric ordering instead of lexicographic ordering', () => {
        const latest = latestReleaseTag(['v0.9.0', 'not-a-release', 'v0.10.0', 'v0.2.99']);
        assert.equal(latest.tag, 'v0.10.0');
    });

    it('calculates the next patch release', () => {
        assert.equal(nextReleaseTag(['v0.1.0'], 'patch'), 'v0.1.1');
    });

    it('calculates the next minor release and resets patch', () => {
        assert.equal(nextReleaseTag(['v1.9.8'], 'minor'), 'v1.10.0');
    });

    it('calculates the next major release and resets minor and patch', () => {
        assert.equal(nextReleaseTag(['v9.8.7'], 'major'), 'v10.0.0');
    });

    it('rejects an unsupported bump', () => {
        assert.throws(() => nextReleaseTag(['v0.1.0'], 'prerelease'), /patch, minor, or major/);
    });

    it('fails safely when no valid release tag exists', () => {
        assert.throws(() => nextReleaseTag(['latest'], 'patch'), /No existing release tag/);
    });

    it('calculates a release from newline-delimited tags on standard input', () => {
        const result = spawnSync(
            process.execPath,
            [fileURLToPath(new URL('./release-version.mjs', import.meta.url)), 'minor'],
            { input: 'v0.9.0\nv0.10.0\n', encoding: 'utf8' });

        assert.equal(result.status, 0);
        assert.equal(result.stdout, 'v0.11.0\n');
        assert.equal(result.stderr, '');
    });
});

describe('Create Release workflow safety', () => {
    const workflow = readFileSync(
        new URL('../workflows/create-release.yml', import.meta.url),
        'utf8');
    const productionWorkflow = readFileSync(
        new URL('../workflows/deploy-production.yml', import.meta.url),
        'utf8');
    const buildWorkflow = readFileSync(
        new URL('../workflows/build-test.yml', import.meta.url),
        'utf8');

    it('is manual and accepts only patch, minor, or major', () => {
        assert.match(workflow, /workflow_dispatch:/);
        assert.match(workflow, /options:\s*\n\s*- patch\s*\n\s*- minor\s*\n\s*- major/);
        assert.doesNotMatch(workflow, /\n\s*push:/);
    });

    it('serializes release mutation without cancelling an active run', () => {
        assert.match(workflow, /group: cbdf-create-release/);
        assert.match(workflow, /cancel-in-progress: false/);
    });

    it('validates the exact current main commit before release mutation', () => {
        assert.match(buildWorkflow, /workflow_call:/);
        assert.match(workflow, /ref: main/);
        assert.match(workflow, /refs\/heads\/main/);
        assert.match(workflow, /git ls-remote --exit-code origin refs\/heads\/main/);
        assert.match(workflow, /needs: \[verify-main, validate\]/);
    });

    it('rechecks main and tags and never force-pushes a tag', () => {
        assert.match(workflow, /git fetch origin main --tags/);
        assert.match(workflow, /git ls-remote --exit-code --tags origin "refs\/tags\/\$\{NEXT_TAG\}"/);
        assert.match(workflow, /git push origin "refs\/tags\/\$\{NEXT_TAG\}"/);
        assert.doesNotMatch(workflow, /git push[^\n]*--force/);
        assert.doesNotMatch(workflow, /git tag -d|git push[^\n]*--delete|v0\.1\.0/);
    });

    it('refuses to release the same main commit twice', () => {
        assert.match(workflow, /LATEST_TAG/);
        assert.match(workflow, /already released as/);
        assert.match(workflow, /git rev-list -n 1 "\$\{LATEST_TAG\}"/);
    });

    it('uses least-privilege job permissions for release and dispatch', () => {
        assert.match(workflow, /contents: read/);
        assert.match(workflow, /contents: write\s*\n\s*actions: write/);
        assert.doesNotMatch(workflow, /pull-requests: write|issues: write/);
    });

    it('creates a release only for the pushed tag then dispatches all production targets', () => {
        assert.match(workflow, /gh release create "\$\{NEXT_TAG\}"/);
        assert.match(workflow, /--verify-tag/);
        assert.match(workflow, /gh workflow run deploy-production\.yml/);
        assert.match(workflow, /--raw-field release_tag="\$\{NEXT_TAG\}"/);
        assert.match(workflow, /--raw-field target=all/);
        assert.match(productionWorkflow, /environment: prod/);
        assert.ok(workflow.indexOf('uses: ./.github/workflows/build-test.yml')
            < workflow.indexOf('git tag -a'));
    });
});
