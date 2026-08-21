import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('application startup', () => {
    it('uses the proven autostart lifecycle and registers the worker afterwards', () => {
        const indexHtml = readFileSync(resolve(import.meta.dirname, '../../index.html'), 'utf8');
        const bootstrapPosition = indexHtml.indexOf('type="module" src="js/bootstrap/app.js"');
        const blazorPosition = indexHtml.indexOf('_framework/blazor.webassembly');
        const registrationPosition = indexHtml.indexOf('registerServiceWorker();');

        expect(indexHtml).not.toContain('autostart="false"');
        expect(indexHtml).not.toContain('Blazor.start()');
        expect(bootstrapPosition).toBeGreaterThan(-1);
        expect(blazorPosition).toBeGreaterThan(bootstrapPosition);
        expect(indexHtml).not.toContain('await registerServiceWorker();');
        expect(registrationPosition).toBeGreaterThan(blazorPosition);
    });
});
