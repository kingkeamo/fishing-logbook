import { test as base, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

export const test = base.extend({
    page: async ({ context }, use) => {
        const sessionStorage = JSON.parse(await readFile(resolve('.auth/e2e-session.json'), 'utf8'));
        const origin = new URL(process.env.E2E_BASE_URL ?? 'http://localhost:5019').origin;
        await context.addInitScript(payload => {
            if (window.location.origin === payload.origin) {
                for (const [key, value] of Object.entries(payload.values)) window.sessionStorage.setItem(key, value);
            }
        }, { origin, values: sessionStorage });
        const page = await context.newPage();
        await use(page);
        await page.close();
    }
});

export { expect };
