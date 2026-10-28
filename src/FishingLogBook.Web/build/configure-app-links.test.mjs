import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { buildAppLinks, buildHeaders, resolveAndroidApps } from './configure-app-links.mjs';

const config = JSON.parse(readFileSync(new URL('./app-links.dev.json', import.meta.url), 'utf8'));
const debugPackage = 'com.fishinglogbook.mobile.demo';
const playPackage = 'com.catchbutdontforget.app';
const playFingerprint = '7C:34:00:29:A4:87:17:8C:DA:20:51:B0:3E:CB:61:60:00:23:69:3A:C4:40:E8:D4:BB:63:55:7D:CF:08:45:28';

function app(packageName) {
    return resolveAndroidApps(config.android).find(entry => entry.packageName === packageName);
}

describe('dev app link association files', () => {
    it('refuses to generate association files for production', () => {
        assert.throws(() => buildAppLinks(config, 'prod', 'ABCDE12345'), /only generated for the dev environment/);
    });

    it('rejects an invalid Apple team identifier instead of publishing it', () => {
        for (const teamId of ['TEAMID', 'abcde12345', 'ABCDE123456', 'ABCDE-1234']) {
            assert.throws(() => buildAppLinks(config, 'dev', teamId), /APPLE_TEAM_ID/);
        }
    });

    it('rejects malformed Android signing fingerprints', () => {
        const invalid = structuredClone(config);
        resolveAndroidApps(invalid.android)[0].sha256CertFingerprints = ['5a:18:dd'];
        assert.throws(() => buildAppLinks(invalid, 'dev', undefined), /sha256CertFingerprints/);
    });

    it('rejects an invalid Play package name even before a fingerprint is supplied', () => {
        const invalid = structuredClone(config);
        resolveAndroidApps(invalid.android).find(entry => entry.packageName === playPackage).packageName = 'Catch';
        assert.throws(() => buildAppLinks(invalid, 'dev', undefined), /android.packageName is invalid/);
    });

    it('rejects callback paths outside the native namespace', () => {
        const invalid = structuredClone(config);
        invalid.callbackPaths = ['/authentication/login-callback'];
        assert.throws(() => buildAppLinks(invalid, 'dev', undefined), /callbackPaths/);
    });

    it('omits the Apple association when no team identifier is configured', () => {
        const result = buildAppLinks(config, 'dev', '  ');
        assert.equal(result.hasAppleAssociation, false);
        assert.equal(result.files.has('.well-known/apple-app-site-association'), false);
    });

    it('keeps the native HTTPS callback paths and iOS development bundle', () => {
        assert.deepEqual(config.callbackPaths, ['/native/oauth2redirect', '/native/signedout']);
        assert.equal(config.host, 'fishing-logbook-dev.pages.dev');
        assert.equal(config.ios.bundleId, debugPackage);
    });

    it('binds Android associations to the debug package and the Play signing certificate', () => {
        const statements = buildAppLinks(config, 'dev', undefined).files.get('.well-known/assetlinks.json');
        assert.equal(statements.length, 2);
        assert.deepEqual(statements[0].relation, ['delegate_permission/common.handle_all_urls']);
        assert.equal(statements[0].target.package_name, debugPackage);
        assert.deepEqual(statements[0].target.sha256_cert_fingerprints, app(debugPackage).sha256CertFingerprints);
        assert.equal(statements[1].target.package_name, playPackage);
        assert.deepEqual(statements[1].target.sha256_cert_fingerprints, [playFingerprint]);
    });

    it('omits a Play statement when the fingerprint array is empty', () => {
        const withoutPlay = structuredClone(config);
        resolveAndroidApps(withoutPlay.android)
            .find(entry => entry.packageName === playPackage).sha256CertFingerprints = [];
        const statements = buildAppLinks(withoutPlay, 'dev', undefined).files.get('.well-known/assetlinks.json');
        assert.equal(statements.length, 1);
        assert.equal(statements[0].target.package_name, debugPackage);
    });

    it('limits the Apple association to the native callback paths with web credentials', () => {
        const association = buildAppLinks(config, 'dev', 'ABCDE12345').files.get('.well-known/apple-app-site-association');
        const appId = `ABCDE12345.${debugPackage}`;
        assert.deepEqual(association.applinks.details[0].appIDs, [appId]);
        assert.deepEqual(association.applinks.details[0].components, [
            { '/': '/native/oauth2redirect' },
            { '/': '/native/signedout' },
        ]);
        assert.deepEqual(association.webcredentials.apps, [appId]);
    });

    it('serves generated association files as JSON and fallback pages without caching or referrers', () => {
        const result = buildAppLinks(config, 'dev', 'ABCDE12345');
        const headers = buildHeaders([...result.files.keys()], result.fallbackPaths);
        assert.match(headers, /\/\.well-known\/assetlinks\.json\n {2}Content-Type: application\/json/);
        assert.match(headers, /\/\.well-known\/apple-app-site-association\n {2}Content-Type: application\/json/);
        assert.match(headers, /\/native\/oauth2redirect\n {2}Cache-Control: no-store\n {2}Referrer-Policy: no-referrer/);
    });

    it('does not label the SPA fallback as JSON when the Apple association is not generated', () => {
        const result = buildAppLinks(config, 'dev', undefined);
        const headers = buildHeaders([...result.files.keys()], result.fallbackPaths);
        assert.match(headers, /\/\.well-known\/assetlinks\.json/);
        assert.doesNotMatch(headers, /apple-app-site-association/);
    });
});
