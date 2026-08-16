import { defineConfig } from 'vitest/config';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(webRoot, '../..');

export default defineConfig({
    root: webRoot,
    cacheDir: resolve(repoRoot, 'artifacts/javascript-tests/vitest/cache'),
    test: {
        environment: 'jsdom',
        setupFiles: ['./vitest.setup.js'],
        include: ['./wwwroot/js/**/*.test.js'],
        fileParallelism: false,
        pool: 'forks',
        singleFork: true,
        coverage: {
            provider: 'v8',
            reportsDirectory: resolve(repoRoot, 'artifacts/javascript-tests/vitest/coverage'),
            reporter: ['text', 'html'],
            include: ['./wwwroot/js/**/*.js'],
            exclude: ['./wwwroot/js/**/*.test.js']
        }
    }
});
