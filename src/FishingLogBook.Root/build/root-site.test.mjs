import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { describe, it } from 'node:test';

const html = await readFile(new URL('../index.html', import.meta.url), 'utf8');

describe('production root site', () => {
    it('presents the exact brand name', () => {
        assert.match(html, /<h1>Catch But Don't Forget<\/h1>/);
    });

    it('links to the permanent production PWA origin', () => {
        assert.match(html, /href="https:\/\/app\.catchbutdontforget\.com"[^>]*>Open the app<\/a>/);
    });
});
