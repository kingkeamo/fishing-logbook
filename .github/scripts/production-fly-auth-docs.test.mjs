import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { describe, it } from 'node:test';

const flyReadme = await readFile(
    new URL('../../infrastructure/fly/README.md', import.meta.url),
    'utf8');
const productionRunbook = await readFile(
    new URL('../../docs/ProductionRunbook.md', import.meta.url),
    'utf8');
const productionFlyConfig = await readFile(
    new URL('../../infrastructure/fly/fly.prod.toml', import.meta.url),
    'utf8');

describe('production Fly Cognito client instructions', () => {
    it('accepts only one PWA client or one PWA plus one native client', () => {
        assert.match(
            flyReadme,
            /\[object\[\]\]\$prodApprovedClientIds = \$prodApprovedClientIdsJson \| ConvertFrom-Json/);
        assert.match(flyReadme, /\$prodApprovedClientIds\.Count -notin 1, 2/);
        assert.match(flyReadme, /\$prodApprovedClientIds\.Count -eq 2/);
        assert.doesNotMatch(
            flyReadme,
            /^\$prodNativeClientId = \$prodApprovedClientIds\[1\]$/m);
    });

    it('rejects empty malformed and duplicate client IDs before staging them', () => {
        assert.match(flyReadme, /\[string\]::IsNullOrWhiteSpace\(\$_\)/);
        assert.match(flyReadme, /\^\[A-Za-z0-9_\+\]\{1,128\}\$/);
        assert.match(flyReadme, /Select-Object -Unique/);
        assert.match(
            flyReadme,
            /if \(\$invalidClientIds\.Count -ne 0\) \{\s+throw/);
    });

    it('stages the native client only when present and removes a stale value otherwise', () => {
        assert.match(
            flyReadme,
            /if \(\$null -ne \$prodNativeClientId\) \{\s+flyctl secrets set[^\n]+Auth__ApprovedClientIds__1=\$prodNativeClientId/);
        assert.match(
            flyReadme,
            /elseif \(\$prodExistingSecretNames -contains "Auth__ApprovedClientIds__1"\) \{\s+flyctl secrets unset --stage[^\n]+Auth__ApprovedClientIds__1/);
    });

    it('marks the native client secret as optional throughout production guidance', () => {
        assert.match(
            productionRunbook,
            /Auth__ApprovedClientIds__1=<native client ID; only when the Prod native client is enabled>/);
        assert.match(productionRunbook, /never stage an empty index `1`/);
        assert.match(
            productionFlyConfig,
            /Add Auth__ApprovedClientIds__1 only when the Prod native client exists/);
    });
});
