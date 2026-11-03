import { appendFile, readdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export async function resolveSignedIosIpa(publishDirectory) {
    const entries = await readdir(publishDirectory, { withFileTypes: true, recursive: true });
    const ipas = entries
        .filter(entry => entry.isFile() && entry.name.endsWith('.ipa'))
        .map(entry => resolve(entry.parentPath ?? publishDirectory, entry.name));

    if (ipas.length !== 1) {
        const paths = ipas.length === 0 ? 'none' : ipas.join(', ');
        throw new Error(
            `Expected exactly one signed IPA in ${publishDirectory} but found ${ipas.length}: ${paths}`);
    }

    return ipas[0];
}

async function main() {
    const [, , publishDirectory, outputPath] = process.argv;
    if (!publishDirectory || !outputPath) {
        throw new Error('Publish directory and GitHub output path are required.');
    }

    const ipaPath = await resolveSignedIosIpa(publishDirectory);
    await appendFile(outputPath, `ipa_path=${ipaPath}\n`, 'utf8');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
