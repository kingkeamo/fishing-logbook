import { readFile, readdir } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const expectedDisplayName = "Catch But Don't Forget";
const allowedBundleIds = new Set([
    'com.fishinglogbook.mobile.demo',
    'com.catchbutdontforget.app'
]);

function readPlistString(text, key) {
    const match = text.match(new RegExp(`<key>${key}</key>\\s*<string>([^<]*)</string>`));
    return match ? match[1] : null;
}

async function walk(directory, files = []) {
    let entries;
    try {
        entries = await readdir(directory, { withFileTypes: true });
    } catch (error) {
        if (error && error.code === 'ENOENT') {
            return files;
        }

        throw error;
    }

    for (const entry of entries) {
        const fullPath = join(directory, entry.name);
        if (entry.isDirectory()) {
            await walk(fullPath, files);
            continue;
        }

        if (entry.name === 'Info.plist') {
            files.push(fullPath);
        }
    }

    return files;
}

export async function assertGeneratedIosDisplayName(target) {
    const root = resolve(target);
    const plists = [
        ...await walk(join(root, 'obj')),
        ...await walk(join(root, 'bin'))
    ];
    const compiled = [];
    for (const filePath of plists) {
        const text = await readFile(filePath, 'utf8');
        if (!text.includes('<key>CFBundleIdentifier</key>')) {
            continue;
        }

        compiled.push({ filePath, text });
    }

    if (compiled.length === 0) {
        throw new Error(
            `No generated iOS Info.plist with CFBundleIdentifier was found under ${root}. Run this after a net10.0-ios build.`);
    }

    for (const { filePath, text } of compiled) {
        const displayName = readPlistString(text, 'CFBundleDisplayName');
        const bundleId = readPlistString(text, 'CFBundleIdentifier');
        if (displayName !== expectedDisplayName) {
            throw new Error(
                `${filePath} CFBundleDisplayName is ${JSON.stringify(displayName)}, expected ${JSON.stringify(expectedDisplayName)}.`);
        }

        if (!allowedBundleIds.has(bundleId)) {
            throw new Error(
                `${filePath} CFBundleIdentifier is ${JSON.stringify(bundleId)}, expected one of ${[...allowedBundleIds].join(', ')}.`);
        }
    }

    return compiled.map(item => item.filePath);
}

async function main() {
    const target = resolve(process.argv[2] ?? '.');
    const files = await assertGeneratedIosDisplayName(target);
    for (const filePath of files) {
        process.stdout.write(`Generated iOS display name is ${expectedDisplayName}: ${filePath}\n`);
    }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
