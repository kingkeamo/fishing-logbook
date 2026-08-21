import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
    buildConfiguration,
    productionForbiddenMarkers,
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
};

describe('production Web configuration', () => {
    it('emits complete production configuration', () => {
        const result = buildConfiguration(template, production, 'prod');
        assert.equal(result.Api.BaseUrl, 'https://api.catchbutdontforget.com');
        assert.equal(result.Auth.ClientId, 'production-public-client');
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
        const values = { ...production, API_BASE_URL: 'https://fishing-logbook-dev-api.fly.dev' };
        assert.equal(buildConfiguration(template, values, 'dev').Api.BaseUrl, values.API_BASE_URL);
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
