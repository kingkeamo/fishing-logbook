import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('keeps authenticated CI traces and auth state out of uploaded artifacts', async () => {
    const config = await readFile(new URL('../playwright.config.mjs', import.meta.url), 'utf8');
    const workflow = await readFile(new URL('../../../.github/workflows/e2e-browser.yml', import.meta.url), 'utf8');

    assert.match(config, /trace: process\.env\.CI \? 'off'/);
    assert.doesNotMatch(workflow, /\.auth/);
    assert.doesNotMatch(workflow, /artifacts\/test-results\s*$/m);
    assert.match(workflow, /\*\*\/\*\.png/);
});

test('requires explicit enablement and dedicated Cognito secrets', async () => {
    const workflow = await readFile(new URL('../../../.github/workflows/e2e-browser.yml', import.meta.url), 'utf8');

    assert.match(workflow, /vars\.E2E_ENABLED == 'true'/);
    assert.match(workflow, /secrets\.E2E_COGNITO_USERNAME/);
    assert.match(workflow, /secrets\.E2E_COGNITO_PASSWORD/);
});
