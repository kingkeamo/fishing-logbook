import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { describe, it } from 'node:test';
import {
    assertBuildStillAvailable,
    assertBuildEligibleForInternalTesting,
    assertInternalGroupForApp,
    confirmInternalGroupAssignment,
    groupContainsBuild,
    groupHasVerifiedAllBuildsAccess,
    internalGroupAssignmentRequest,
    isInternalGroupAssignmentRejected,
    listBuildsForBetaGroupPath,
    nextBuildNumber,
    selectInternalGroup,
    shouldSkipInternalGroupAssignment,
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

describe('TestFlight internal group lookup', () => {
    const script = readFileSync(new URL('./app-store-connect-release.mjs', import.meta.url), 'utf8');
    const groups = [
        { id: 'GROUP-OTHER', attributes: { name: 'External Testers' } },
        { id: 'GROUP-CATCH', attributes: { name: 'CatchButDontForget' } },
    ];

    it('selects the unique CatchButDontForget group from the app group list', () => {
        assert.equal(selectInternalGroup(groups).id, 'GROUP-CATCH');
    });

    it('fails when CatchButDontForget is missing', () => {
        assert.throws(
            () => selectInternalGroup([{ id: 'GROUP-OTHER', attributes: { name: 'External Testers' } }]),
            /Expected one internal TestFlight group named CatchButDontForget but found 0/);
    });

    it('fails when more than one CatchButDontForget group exists', () => {
        assert.throws(
            () => selectInternalGroup([
                { id: 'GROUP-1', attributes: { name: 'CatchButDontForget' } },
                { id: 'GROUP-2', attributes: { name: 'CatchButDontForget' } },
            ]),
            /Expected one internal TestFlight group named CatchButDontForget but found 2/);
    });

    it('does not send filter[name] on the app-scoped betaGroups relationship', () => {
        assert.match(script, /betaGroups\?filter\[app\]=/);
        assert.match(script, /fields\[betaGroups\]=name,isInternalGroup,hasAccessToAllBuilds/);
        assert.match(script, /fields\[builds\]=version,processingState,expired/);
        assert.doesNotMatch(script, /apps\/.*\/betaGroups\?filter\[name\]=/);
    });

    it('assigns CatchButDontForget from the build relationship, not the group-to-build relationship', () => {
        const assignment = internalGroupAssignmentRequest('BUILD-1', 'GROUP-CATCH');
        assert.equal(assignment.method, 'POST');
        assert.equal(
            assignment.url,
            'https://api.appstoreconnect.apple.com/v1/builds/BUILD-1/relationships/betaGroups');
        assert.deepEqual(assignment.body, { data: [{ type: 'betaGroups', id: 'GROUP-CATCH' }] });
        assert.match(script, /internalGroupAssignmentRequest\(build\.id, group\.id\)/);
        assert.doesNotMatch(script, /betaGroups\/\$\{encodeURIComponent\(group\.id\)\}\/relationships\/builds/);
        assert.doesNotMatch(script, /builds\/\$\{encodeURIComponent\(build\.id\)\}\/relationships\/betaGroups\?/);
        assert.doesNotMatch(script, /relationships\/betaGroups\?limit/);
    });

    it('verifies CatchButDontForget membership from the group builds list, not a build relationship GET', () => {
        assert.equal(listBuildsForBetaGroupPath('GROUP-CATCH'), 'betaGroups/GROUP-CATCH/builds?limit=200');
        assert.equal(groupContainsBuild([{ id: 'BUILD-3' }], 'BUILD-3'), true);
        assert.equal(groupContainsBuild([{ id: 'BUILD-2' }], 'BUILD-3'), false);
        assert.match(script, /listBuildsForBetaGroupPath\(group\.id\)/);
        assert.match(script, /groupHasVerifiedAllBuildsAccess\(group\)/);
        assert.ok(
            script.indexOf('groupHasVerifiedAllBuildsAccess(group)')
                < script.indexOf('listBuildsForBetaGroupPath(group.id)'));
    });

    it('rejects a CatchButDontForget group that belongs to another app', () => {
        assert.throws(
            () => assertInternalGroupForApp(
                {
                    id: 'GROUP-CATCH',
                    attributes: { name: 'CatchButDontForget', isInternalGroup: true },
                    relationships: { app: { data: { id: 'OTHER-APP' } } },
                },
                'APP-1'),
            /belongs to app OTHER-APP, not APP-1/);
    });

    it('rejects CatchButDontForget when it is not an internal group', () => {
        assert.throws(
            () => assertInternalGroupForApp(
                { id: 'GROUP-CATCH', attributes: { name: 'CatchButDontForget', isInternalGroup: false } },
                'APP-1'),
            /is not marked as an internal TestFlight group/);
    });

    it('rejects an expired or non-VALID build as ineligible for internal testing', () => {
        assert.throws(
            () => assertBuildEligibleForInternalTesting(
                { attributes: { version: '2', processingState: 'VALID', expired: true } },
                'APP-1',
                '2'),
            /expired and is not eligible/);
        assert.throws(
            () => assertBuildEligibleForInternalTesting(
                { attributes: { version: '2', processingState: 'PROCESSING' } },
                'APP-1',
                '2'),
            /not available to internal testers/);
        assert.throws(
            () => assertBuildEligibleForInternalTesting(
                {
                    attributes: { version: '2', processingState: 'VALID', expired: false },
                    relationships: { app: { data: { id: 'OTHER-APP' } } },
                },
                'APP-1',
                '2'),
            /belongs to app OTHER-APP, not APP-1/);
    });

    it('accepts a VALID unexpired CatchButDontForget build for the expected app', () => {
        const group = assertInternalGroupForApp(
            {
                id: 'GROUP-CATCH',
                attributes: { name: 'CatchButDontForget', isInternalGroup: true, hasAccessToAllBuilds: false },
                relationships: { app: { data: { id: 'APP-1' } } },
            },
            'APP-1');
        const build = assertBuildEligibleForInternalTesting(
            {
                attributes: { version: '2', processingState: 'VALID', expired: false },
                relationships: { app: { data: { id: 'APP-1' } } },
            },
            'APP-1',
            '2');
        assert.equal(group.id, 'GROUP-CATCH');
        assert.equal(build.attributes.version, '2');
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

describe('TestFlight internal group assignment', () => {
    const script = readFileSync(new URL('./app-store-connect-release.mjs', import.meta.url), 'utf8');
    const buildId = 'BUILD-1';
    const internalGroupId = 'GROUP-CATCH';

    it('treats a successful assignment response as tester access', () => {
        const result = confirmInternalGroupAssignment(
            { ok: true, status: 204, statusText: 'No Content' },
            buildId,
            { internalGroupId, assignedGroupIds: [] });
        assert.equal(result.available, true);
        assert.match(result.message, /assigned to the CatchButDontForget internal group/);
    });

    it('treats HTTP 409 as already complete only after CatchButDontForget membership is verified', () => {
        const result = confirmInternalGroupAssignment(
            { ok: false, status: 409, statusText: 'Conflict', detail: 'ENTITY_ERROR.RELATIONSHIP.INVALID' },
            buildId,
            { internalGroupId, assignedGroupIds: ['OTHER-GROUP', internalGroupId] });
        assert.equal(result.available, true);
        assert.match(result.message, /assigned to the CatchButDontForget internal group/);
    });

    it('rejects HTTP 409 when CatchButDontForget does not contain the build', () => {
        assert.throws(
            () => confirmInternalGroupAssignment(
                { ok: false, status: 409, statusText: 'Conflict', detail: 'ENTITY_ERROR.RELATIONSHIP.INVALID' },
                buildId,
                { internalGroupId, assignedGroupIds: ['OTHER-GROUP'] }),
            /HTTP 409.*does not contain the build.*Testers cannot access it/u);
    });

    it('rejects HTTP 409 when group membership was not independently loaded', () => {
        assert.throws(
            () => confirmInternalGroupAssignment(
                { ok: false, status: 409, statusText: 'Conflict' },
                buildId,
                { internalGroupId, assignedGroupIds: [] }),
            /HTTP 409.*Testers cannot access it/u);
    });

    it('rejects a failed assignment without reporting tester access', () => {
        assert.throws(
            () => confirmInternalGroupAssignment(
                { ok: false, status: 500, statusText: 'Internal Server Error', detail: 'upstream failed' },
                buildId,
                { internalGroupId, assignedGroupIds: [internalGroupId] }),
            error => {
                assert.match(error.message, /App Store Connect API request failed \(500\): upstream failed/);
                assert.doesNotMatch(error.message, /assigned to the CatchButDontForget/);
                return true;
            });
    });

    it('rejects HTTP 403 GET_RELATIONSHIP without reporting tester access', () => {
        assert.throws(
            () => confirmInternalGroupAssignment(
                {
                    ok: false,
                    status: 403,
                    statusText: 'Forbidden',
                    detail: "The relationship 'betaGroups' does not allow 'GET_RELATIONSHIP'. Allowed operations are: CREATE, DELETE.",
                },
                buildId,
                { internalGroupId, assignedGroupIds: [internalGroupId] }),
            error => {
                assert.match(error.message, /App Store Connect API request failed \(403\): The relationship 'betaGroups' does not allow/);
                assert.doesNotMatch(error.message, /assigned to the CatchButDontForget/);
                return true;
            });
    });

    const internalGroupRejected = {
        ok: false,
        status: 422,
        statusText: 'Unprocessable Entity',
        detail: JSON.stringify({
            errors: [{
                code: 'ENTITY_UNPROCESSABLE',
                title: 'Builds cannot be assigned to this internal group.',
                detail: 'Cannot add internal group to a build.',
            }],
        }),
    };

    it('treats HTTP 422 as already complete only after CatchButDontForget membership is verified', () => {
        assert.equal(isInternalGroupAssignmentRejected(internalGroupRejected), true);
        const result = confirmInternalGroupAssignment(internalGroupRejected, buildId, {
            internalGroupId,
            assignedGroupIds: [internalGroupId],
        });
        assert.equal(result.available, true);
        assert.match(result.message, /assigned to the CatchButDontForget internal group/);
    });

    it('treats HTTP 422 as complete when CatchButDontForget is verified to have access to all builds', () => {
        const result = confirmInternalGroupAssignment(internalGroupRejected, buildId, {
            internalGroupId,
            assignedGroupIds: [],
            isInternalGroup: true,
            hasAccessToAllBuilds: true,
        });
        assert.equal(result.available, true);
        assert.match(result.message, /assigned to the CatchButDontForget internal group/);
    });

    it('rejects HTTP 422 when CatchButDontForget does not contain the build and does not have all-builds access', () => {
        assert.throws(
            () => confirmInternalGroupAssignment(internalGroupRejected, buildId, {
                internalGroupId,
                assignedGroupIds: [],
                isInternalGroup: true,
                hasAccessToAllBuilds: false,
            }),
            error => {
                assert.match(error.message, /HTTP 422.*Testers cannot access it/u);
                assert.doesNotMatch(error.message, /assigned to the CatchButDontForget/);
                return true;
            });
    });

    it('rejects a genuine HTTP 422 that is not an internal-group assignment conflict', () => {
        assert.throws(
            () => confirmInternalGroupAssignment(
                {
                    ok: false,
                    status: 422,
                    statusText: 'Unprocessable Entity',
                    detail: 'The build is not eligible for TestFlight testing.',
                },
                buildId,
                {
                    internalGroupId,
                    assignedGroupIds: [],
                    isInternalGroup: true,
                    hasAccessToAllBuilds: true,
                }),
            error => {
                assert.match(error.message, /App Store Connect API request failed \(422\): The build is not eligible/);
                assert.doesNotMatch(error.message, /assigned to the CatchButDontForget/);
                return true;
            });
    });

    it('rejects HTTP 422 all-builds access unless CatchButDontForget is verified internal', () => {
        assert.equal(
            groupHasVerifiedAllBuildsAccess({
                attributes: { isInternalGroup: false, hasAccessToAllBuilds: true },
            }),
            false);
        assert.throws(
            () => confirmInternalGroupAssignment(internalGroupRejected, buildId, {
                internalGroupId,
                assignedGroupIds: [],
                isInternalGroup: false,
                hasAccessToAllBuilds: true,
            }),
            /HTTP 422.*Testers cannot access it/u);
    });

    it('skips assignment when the build is already in CatchButDontForget or the group has all-builds access', () => {
        const group = {
            id: internalGroupId,
            attributes: { isInternalGroup: true, hasAccessToAllBuilds: false },
        };
        assert.equal(shouldSkipInternalGroupAssignment(group, [{ id: buildId }], buildId), true);
        assert.equal(shouldSkipInternalGroupAssignment(group, [{ id: 'OTHER-BUILD' }], buildId), false);
        assert.equal(
            shouldSkipInternalGroupAssignment(
                { id: internalGroupId, attributes: { isInternalGroup: true, hasAccessToAllBuilds: true } },
                [],
                buildId),
            true);
        assert.equal(
            shouldSkipInternalGroupAssignment(
                { id: internalGroupId, attributes: { isInternalGroup: true, hasAccessToAllBuilds: false } },
                [],
                buildId),
            false);
        assert.match(script, /shouldSkipInternalGroupAssignment\(group, groupBuilds, build\.id\)/);
    });
});

describe('mobile TestFlight workflow security', () => {
    const workflow = readFileSync(new URL('../workflows/mobile-testflight.yml', import.meta.url), 'utf8');
    const artifactJob = workflow.slice(
        workflow.indexOf('  artifact-only:'),
        workflow.indexOf('  publish-internal:'));
    const publishingJob = workflow.slice(
        workflow.indexOf('  publish-internal:'),
        workflow.indexOf('  assign-existing:'));
    const assignExistingJob = workflow.slice(workflow.indexOf('  assign-existing:'));

    it('is manual, serialized, and restricted to main', () => {
        assert.match(workflow, /workflow_dispatch:/);
        assert.doesNotMatch(workflow, /\n\s*push:|\n\s*pull_request:/);
        assert.match(workflow, /group: mobile-testflight/);
        assert.match(workflow, /cancel-in-progress: false/);
        assert.match(workflow, /refs\/heads\/main/);
        assert.match(workflow, /ref: main/);
    });

    it('keeps artifact-only builds outside the protected publishing environment', () => {
        assert.match(artifactJob, /if: inputs\.publish_to_testflight == false && inputs\.assign_existing_version == ''/);
        assert.match(artifactJob, /contents: read/);
        assert.doesNotMatch(artifactJob, /environment:|APP_STORE_CONNECT_PRIVATE_KEY|altool/);
        assert.match(publishingJob, /if: inputs\.publish_to_testflight == true && inputs\.assign_existing_version == ''/);
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

    it('pins .NET 10.0.300, workload set 10.0.300.3 and Xcode 26.5 on macos-26', () => {
        assert.match(artifactJob, /runs-on: macos-26/);
        assert.match(publishingJob, /runs-on: macos-26/);
        assert.equal(workflow.match(/DOTNET_SDK_VERSION: '10\.0\.300'/gu)?.length, 2);
        assert.equal(workflow.match(/DOTNET_WORKLOAD_SET: '10\.0\.300\.3'/gu)?.length, 2);
        assert.equal(workflow.match(/XCODE_APP: \/Applications\/Xcode_26\.5\.app/gu)?.length, 2);
        assert.doesNotMatch(workflow, /macos-15|skip-manifest-update|Show Xcode version/);
    });

    it('retries assignment of an existing build without compiling or uploading an IPA', () => {
        assert.match(workflow, /assign_existing_version:/);
        assert.match(assignExistingJob, /if: inputs\.assign_existing_version != ''/);
        assert.match(assignExistingJob, /runs-on: ubuntu-latest/);
        assert.match(assignExistingJob, /environment: app-store-connect-internal/);
        assert.match(assignExistingJob, /APPLICATION_VERSION: \$\{\{ inputs\.assign_existing_version \}\}/);
        assert.match(assignExistingJob, /wait-and-assign/);
        assert.doesNotMatch(assignExistingJob, /altool|build-signed-ios-ipa|select-version|macos-26|IOS_DISTRIBUTION/);
    });
});
