import { appendFile, readdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export async function resolveSignedAndroidBundle(publishDirectory) {
    const entries = await readdir(publishDirectory, { withFileTypes: true });
    const bundles = entries
        .filter(entry => entry.isFile() && entry.name.endsWith('-Signed.aab'))
        .map(entry => resolve(publishDirectory, entry.name));

    if (bundles.length !== 1) {
        const paths = bundles.length === 0 ? 'none' : bundles.join(', ');
        throw new Error(
            `Expected exactly one signed AAB in ${publishDirectory} but found ${bundles.length}: ${paths}`);
    }

    return bundles[0];
}

async function main() {
    const [, , publishDirectory, outputPath] = process.argv;
    if (!publishDirectory || !outputPath) {
        throw new Error('Publish directory and GitHub output path are required.');
    }

    const bundlePath = await resolveSignedAndroidBundle(publishDirectory);
    await appendFile(outputPath, `bundle_path=${bundlePath}\n`, 'utf8');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
