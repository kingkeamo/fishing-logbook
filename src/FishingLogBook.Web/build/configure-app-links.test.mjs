import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { buildAppLinks, buildHeaders, resolveAndroidApps } from './configure-app-links.mjs';

const config = JSON.parse(readFileSync(new URL('./app-links.dev.json', import.meta.url), 'utf8'));
const debugPackage = 'com.fishinglogbook.mobile.demo';
const playPackage = 'com.catchbutdontforget.app';
const sampleFingerprint = '00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF';

function app(packageName) {
    return resolveAndroidApps(config.android).find(entry => entry.packageName === packageName);
}

function withPlayFingerprint(fingerprint) {
    const next = structuredClone(config);
    const play = resolveAndroidApps(next.android).find(entry => entry.packageName === playPackage);
    play.sha256CertFingerprints = [fingerprint];
    return next;
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

    it('binds the Android association to the debug package while Play fingerprints are absent', () => {
        const statements = buildAppLinks(config, 'dev', undefined).files.get('.well-known/assetlinks.json');
        assert.equal(statements.length, 1);
        assert.deepEqual(statements[0].relation, ['delegate_permission/common.handle_all_urls']);
        assert.equal(statements[0].target.package_name, debugPackage);
        assert.deepEqual(statements[0].target.sha256_cert_fingerprints, app(debugPackage).sha256CertFingerprints);
        assert.equal(app(playPackage).packageName, playPackage);
        assert.deepEqual(app(playPackage).sha256CertFingerprints, []);
        assert.equal(statements.some(statement => statement.target.package_name === playPackage), false);
    });

    it('emits a Play statement only after a real SHA-256 fingerprint is provided', () => {
        const statements = buildAppLinks(withPlayFingerprint(sampleFingerprint), 'dev', undefined)
            .files.get('.well-known/assetlinks.json');
        assert.equal(statements.length, 2);
        assert.equal(statements[0].target.package_name, debugPackage);
        assert.equal(statements[1].target.package_name, playPackage);
        assert.deepEqual(statements[1].target.sha256_cert_fingerprints, [sampleFingerprint]);
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
