import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { buildAppLinks, buildHeaders } from './configure-app-links.mjs';

const config = JSON.parse(readFileSync(new URL('./app-links.dev.json', import.meta.url), 'utf8'));

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
        invalid.android.sha256CertFingerprints = ['5a:18:dd'];
        assert.throws(() => buildAppLinks(invalid, 'dev', undefined), /sha256CertFingerprints/);
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

    it('binds the Android association to the dev package and signing certificate only', () => {
        const [statement] = buildAppLinks(config, 'dev', undefined).files.get('.well-known/assetlinks.json');
        assert.deepEqual(statement.relation, ['delegate_permission/common.handle_all_urls']);
        assert.equal(statement.target.package_name, 'com.fishinglogbook.mobile.demo');
        assert.deepEqual(statement.target.sha256_cert_fingerprints, config.android.sha256CertFingerprints);
    });

    it('limits the Apple association to the native callback paths with web credentials', () => {
        const association = buildAppLinks(config, 'dev', 'ABCDE12345').files.get('.well-known/apple-app-site-association');
        const appId = 'ABCDE12345.com.fishinglogbook.mobile.demo';
        assert.deepEqual(association.applinks.details[0].appIDs, [appId]);
        assert.deepEqual(association.applinks.details[0].components, [
            { '/': '/native/oauth2redirect' },
            { '/': '/native/signedout' },
        ]);
        assert.deepEqual(association.webcredentials.apps, [appId]);
    });

    it('serves association files as JSON and fallback pages without caching or referrers', () => {
        const headers = buildHeaders(config.callbackPaths);
        assert.match(headers, /\/\.well-known\/apple-app-site-association\n {2}Content-Type: application\/json/);
        assert.match(headers, /\/native\/oauth2redirect\n {2}Cache-Control: no-store\n {2}Referrer-Policy: no-referrer/);
    });
});
