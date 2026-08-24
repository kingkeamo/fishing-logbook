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

test('provides project-local headed, debug and single-test commands', async () => {
    const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));

    assert.equal(packageJson.scripts['test-e2e'], 'playwright test --headed --workers=1');
    assert.equal(packageJson.scripts['test-e2e-debug'], 'playwright test --debug --workers=1');
    assert.equal(packageJson.scripts['test-e2e-single'], 'playwright test --headed --workers=1 --grep');
});

test('registers independent disposable-container teardown', async () => {
    const config = await readFile(new URL('../playwright.config.mjs', import.meta.url), 'utf8');
    const teardown = await readFile(new URL('../support/teardown.mjs', import.meta.url), 'utf8');

    assert.match(config, /globalTeardown: '\.\/support\/teardown\.mjs'/);
    assert.match(teardown, /\^fishing-logbook-e2e-/);
    assert.match(teardown, /docker', \['rm', '--force'/);
});

test('uses IPv4 for deterministic local Cognito metadata retrieval', async () => {
    const stack = await readFile(new URL('../support/start-stack.mjs', import.meta.url), 'utf8');

    assert.match(stack, /DOTNET_SYSTEM_NET_DISABLEIPV6: '1'/);
    assert.match(stack, /Host=127\.0\.0\.1/);
});

test('keeps the offline journey inside the loaded app shell', async () => {
    const journey = await readFile(new URL('../support/catch-journey.mjs', import.meta.url), 'utf8');
    const offlineSpec = await readFile(new URL('../specs/catch-offline.spec.mjs', import.meta.url), 'utf8');
    const offlineSection = offlineSpec.split('await context.setOffline(true);')[1]
        .split('await context.setOffline(false);')[0];

    assert.match(journey, /#catch-record-link/);
    assert.doesNotMatch(offlineSection, /page\.goto/);
});

test('onboards the dedicated Cognito user through the real UI for each disposable database', async () => {
    const setup = await readFile(new URL('../support/auth.setup.mjs', import.meta.url), 'utf8');

    assert.match(setup, /completeOnboardingWhenRequired/);
    assert.match(setup, /const landingRouteTimeout = 90_000/);
    assert.match(setup, /\['\/catches', '\/onboarding'\]\.includes\(url\.pathname\)/);
    assert.match(setup, /#onboarding-method-Fly/);
    assert.match(setup, /#catalogue-picker-modal-option-BrownTrout/);
    assert.match(setup, /#onboarding-finish/);
    const afterLocation = setup.slice(setup.indexOf('#onboarding-skip-location'));
    assert.equal(afterLocation.match(/#onboarding-next/g)?.length, 2);
    assert.ok(afterLocation.lastIndexOf('#onboarding-next') < afterLocation.indexOf('#onboarding-finish'));
    assert.doesNotMatch(setup, /must complete onboarding before running/);
});

test('shows the authentication setup browser when Playwright debug mode is enabled', async () => {
    const setup = await readFile(new URL('../support/auth.setup.mjs', import.meta.url), 'utf8');

    assert.match(setup, /process\.env\.PWDEBUG === '1'/);
    assert.match(setup, /headless: !debugging/);
    assert.match(setup, /message\.text\(\)\.startsWith\('\[FLB\]'\)/);
    assert.match(setup, /page\.on\('pageerror'/);
    assert.doesNotMatch(setup, /E2E auth diagnostic/);
});
