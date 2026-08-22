import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import {
    buildConfiguration,
    productionForbiddenMarkers,
    releaseVersion,
    validateProductionArtifactFiles,
} from './configure-web.mjs';

const template = { Api: { BaseUrl: '' }, Auth: {} };
const production = {
    API_BASE_URL: 'https://api.catchbutdontforget.com',
    AUTH_AUTHORITY: 'https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_prod',
    AUTH_CLIENT_ID: 'production-public-client',
    AUTH_HOSTED_UI_DOMAIN: 'https://fishing-logbook-prod.auth.eu-west-2.amazoncognito.com',
    AUTH_API_SCOPE: 'https://api.catchbutdontforget.com/access',
    AUTH_API_RESOURCE: 'https://api.catchbutdontforget.com',
    BUILD_VERSION: 'v0.1.0',
    BUILD_SHA: '0123456789abcdef0123456789abcdef01234567',
    BUILD_ENVIRONMENT: 'prod',
    BUILD_TIMESTAMP: '2026-08-22T00:00:00Z',
};

describe('production Web configuration', () => {
    it('emits complete production configuration', () => {
        const result = buildConfiguration(template, production, 'prod');
        assert.equal(result.Api.BaseUrl, 'https://api.catchbutdontforget.com');
        assert.equal(result.Auth.ClientId, 'production-public-client');
        assert.equal(result.Build.Version, '0.1.0');
    });

    it('rejects missing values', () => {
        assert.throws(
            () => buildConfiguration(template, { ...production, AUTH_CLIENT_ID: '' }, 'prod'),
            /AUTH_CLIENT_ID/);
    });

    for (const marker of productionForbiddenMarkers) {
        it(`rejects the production marker ${marker} in any field`, () => {
            assert.throws(() => buildConfiguration(template, {
                ...production,
                AUTH_CLIENT_ID: `client-${marker}`,
            }, 'prod'), /forbidden marker/);
        });
    }

    it('rejects a different production API origin', () => {
        assert.throws(() => buildConfiguration(template, {
            ...production,
            API_BASE_URL: 'https://some-other-api.example',
        }, 'prod'), /API_BASE_URL must be/);
    });

    it('rejects non-HTTPS production identity URLs', () => {
        assert.throws(() => buildConfiguration(template, {
            ...production,
            AUTH_AUTHORITY: 'http://identity.example',
        }, 'prod'), /AUTH_AUTHORITY must use HTTPS/);
    });

    it('allows environment-specific dev configuration', () => {
        const values = {
            ...production,
            API_BASE_URL: 'https://fishing-logbook-dev-api.fly.dev',
            BUILD_VERSION: '0.0.0-dev.42',
            BUILD_SHA: 'feature-sha',
            BUILD_ENVIRONMENT: 'dev',
        };
        assert.equal(buildConfiguration(template, values, 'dev').Api.BaseUrl, values.API_BASE_URL);
    });

    it('rejects missing production build metadata', () => {
        assert.throws(
            () => buildConfiguration(template, { ...production, BUILD_SHA: '' }, 'prod'),
            /BUILD_SHA/);
    });

    it('converts a valid immutable release tag to a version', () => {
        assert.equal(releaseVersion('v1.2.3'), '1.2.3');
    });

    it('rejects an invalid production release tag', () => {
        assert.throws(() => releaseVersion('main'), /vX.Y.Z/);
    });

    it('rejects dev markers outside the configuration file', () => {
        assert.throws(() => validateProductionArtifactFiles([
            ['appsettings.Production.json', JSON.stringify(buildConfiguration(template, production, 'prod'))],
            ['unexpected.json', '{"origin":"https://fishing-logbook-dev.pages.dev"}'],
        ]), /unexpected.json contains forbidden marker/);
    });

    it('accepts an artifact containing only production origins', () => {
        assert.doesNotThrow(() => validateProductionArtifactFiles([
            ['appsettings.Production.json', JSON.stringify(buildConfiguration(template, production, 'prod'))],
            ['index.html', '<a href="https://app.catchbutdontforget.com">Open</a>'],
        ]));
    });
});

describe('production release workflow', () => {
    const workflow = readFileSync(
        new URL('../../../.github/workflows/deploy-production.yml', import.meta.url),
        'utf8');

    it('requires and checks out an explicit immutable release tag', () => {
        assert.match(workflow, /release_tag:\s*[\s\S]*required: true/);
        assert.match(workflow, /ref: \$\{\{ inputs\.release_tag \}\}/);
        assert.match(workflow, /git describe --tags --exact-match HEAD/);
    });

    it('injects metadata before publish and only validates after publish', () => {
        const publishPosition = workflow.indexOf('dotnet publish');
        const configurePosition = workflow.indexOf('configure-web.mjs');
        const validateOnlyPosition = workflow.indexOf('--validate-only true');
        assert.ok(configurePosition >= 0 && configurePosition < publishPosition);
        assert.ok(validateOnlyPosition > publishPosition);
    });
});
