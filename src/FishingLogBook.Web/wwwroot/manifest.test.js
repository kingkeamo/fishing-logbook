import { readFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('web app manifest', () => {
    it('references generated icons which exist', () => {
        const root = resolve(import.meta.dirname);
        const manifest = JSON.parse(readFileSync(resolve(root, 'manifest.webmanifest'), 'utf8'));

        expect(manifest.name).toBe('Catch, But Don’t Forget');
        expect(manifest.icons).toEqual(expect.arrayContaining([
            expect.objectContaining({ src: 'icon-192.png', sizes: '192x192' }),
            expect.objectContaining({ src: 'icon-512.png', sizes: '512x512' })
        ]));
        for (const icon of manifest.icons) {
            expect(existsSync(resolve(root, icon.src))).toBe(true);
        }
        expect(existsSync(resolve(root, 'apple-touch-icon.png'))).toBe(true);
        expect(existsSync(resolve(root, 'favicon.png'))).toBe(true);
    });
});
