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

test('registers independent disposable-container teardown', async () => {
    const config = await readFile(new URL('../playwright.config.mjs', import.meta.url), 'utf8');
    const teardown = await readFile(new URL('../support/teardown.mjs', import.meta.url), 'utf8');

    assert.match(config, /globalTeardown: '\.\/support\/teardown\.mjs'/);
    assert.match(teardown, /\^fishing-logbook-e2e-/);
    assert.match(teardown, /docker', \['rm', '--force'/);
});

test('onboards the dedicated Cognito user through the real UI for each disposable database', async () => {
    const setup = await readFile(new URL('../support/auth.setup.mjs', import.meta.url), 'utf8');

    assert.match(setup, /completeOnboardingWhenRequired/);
    assert.match(setup, /#onboarding-method-Fly/);
    assert.match(setup, /#catalogue-picker-modal-option-BrownTrout/);
    assert.match(setup, /#onboarding-finish/);
    assert.doesNotMatch(setup, /must complete onboarding before running/);
});
