import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('application startup', () => {
    it('installs browser globals before starting Blazor', () => {
        const indexHtml = readFileSync(resolve(import.meta.dirname, '../../index.html'), 'utf8');
        const registrationPosition = indexHtml.indexOf('await registerServiceWorker();');
        const bootstrapPosition = indexHtml.indexOf("await import('./js/bootstrap/app.js');");
        const blazorStartPosition = indexHtml.indexOf('await Blazor.start();');

        expect(indexHtml).toContain('autostart="false"');
        expect(indexHtml).not.toContain('type="module" src="js/bootstrap/app.js"');
        expect(registrationPosition).toBeGreaterThan(-1);
        expect(bootstrapPosition).toBeGreaterThan(registrationPosition);
        expect(bootstrapPosition).toBeGreaterThan(-1);
        expect(blazorStartPosition).toBeGreaterThan(bootstrapPosition);
    });
});
