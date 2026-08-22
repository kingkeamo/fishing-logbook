#!/usr/bin/env node
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export const requiredEnvironmentValues = {
    API_BASE_URL: ['Api', 'BaseUrl'],
    AUTH_AUTHORITY: ['Auth', 'Authority'],
    AUTH_CLIENT_ID: ['Auth', 'ClientId'],
    AUTH_HOSTED_UI_DOMAIN: ['Auth', 'HostedUiDomain'],
    AUTH_API_SCOPE: ['Auth', 'ApiScope'],
    AUTH_API_RESOURCE: ['Auth', 'ApiResource'],
};

export const productionExpectedValues = {
    API_BASE_URL: 'https://api.catchbutdontforget.com',
    AUTH_API_RESOURCE: 'https://api.catchbutdontforget.com',
    AUTH_API_SCOPE: 'https://api.catchbutdontforget.com/access',
};

export const productionForbiddenMarkers = [
    'fishing-logbook-dev',
    'pages.dev',
    'localhost',
    '127.0.0.1',
];

const productionUrlValues = [
    'API_BASE_URL',
    'AUTH_AUTHORITY',
    'AUTH_HOSTED_UI_DOMAIN',
    'AUTH_API_SCOPE',
    'AUTH_API_RESOURCE',
];

export function buildConfiguration(template, values, environment) {
    const missing = Object.keys(requiredEnvironmentValues).filter(name => !values[name]?.trim());
    if (missing.length > 0) {
        throw new Error(`Missing required deployment values: ${missing.join(', ')}`);
    }

    const result = structuredClone(template);
    for (const [name, [section, key]] of Object.entries(requiredEnvironmentValues)) {
        result[section] ??= {};
        result[section][key] = values[name].trim();
    }

    if (environment === 'prod') {
        const errors = Object.entries(productionExpectedValues)
            .filter(([name, expected]) => values[name].trim() !== expected)
            .map(([name, expected]) => `${name} must be ${expected}`);
        errors.push(...productionUrlValues
            .filter(name => !values[name].trim().startsWith('https://'))
            .map(name => `${name} must use HTTPS`));
        const serialized = JSON.stringify(result).toLowerCase();
        errors.push(...productionForbiddenMarkers
            .filter(marker => serialized.includes(marker))
            .map(marker => `Production configuration contains forbidden marker: ${marker}`));
        if (errors.length > 0) {
            throw new Error(`Unsafe production configuration: ${errors.join('; ')}`);
        }
    }

    return result;
}

export function validateProductionArtifactFiles(files) {
    const errors = [];
    for (const [name, content] of files) {
        const normalized = content.toLowerCase();
        errors.push(...productionForbiddenMarkers
            .filter(marker => normalized.includes(marker))
            .map(marker => `${name} contains forbidden marker: ${marker}`));
    }
    if (errors.length > 0) {
        throw new Error(`Unsafe production artifact: ${errors.join('; ')}`);
    }
}

async function findTextFiles(directory) {
    const files = [];
    for (const entry of await readdir(directory, { withFileTypes: true })) {
        const path = resolve(directory, entry.name);
        if (entry.isDirectory()) files.push(...await findTextFiles(path));
        else if (/\.(?:css|html|js|json|map|txt|webmanifest)$/i.test(entry.name)) files.push(path);
    }
    return files;
}

async function main(args = process.argv.slice(2)) {
    const options = Object.fromEntries(args.reduce((pairs, value, index) => {
        if (value.startsWith('--')) pairs.push([value.slice(2), args[index + 1]]);
        return pairs;
    }, []));
    if (!['dev', 'prod'].includes(options.environment) || !options.template || !options.output) {
        throw new Error('Usage: configure-web.mjs --environment <dev|prod> --template <path> --output <path>');
    }

    const template = JSON.parse(await readFile(options.template, 'utf8'));
    const configured = buildConfiguration(template, process.env, options.environment);
    await writeFile(options.output, `${JSON.stringify(configured, null, 2)}\n`, 'utf8');
    if (options['artifact-root']) {
        const paths = await findTextFiles(options['artifact-root']);
        validateProductionArtifactFiles(await Promise.all(paths.map(async path => [
            path,
            await readFile(path, 'utf8'),
        ])));
    }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    main().catch(error => {
        console.error(error.message);
        process.exitCode = 1;
    });
}
