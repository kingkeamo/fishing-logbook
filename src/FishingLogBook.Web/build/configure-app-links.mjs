#!/usr/bin/env node
import { appendFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const fingerprintPattern = /^([0-9A-F]{2}:){31}[0-9A-F]{2}$/;
const packagePattern = /^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/;
const teamIdPattern = /^[A-Z0-9]{10}$/;
const callbackPathPattern = /^\/native\/[a-z0-9-]+$/;

const fallbackPage = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="referrer" content="no-referrer">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Return to Fishing Logbook</title>
</head>
<body>
<main>
<h1>Return to the Fishing Logbook app</h1>
<p>Close this page and return to the app. If sign-in did not finish, start it again from the app.</p>
<h1 lang="fr">Retournez dans l'application Fishing Logbook</h1>
<p lang="fr">Fermez cette page et revenez dans l'application. Si la connexion n'a pas abouti, recommencez depuis l'application.</p>
</main>
</body>
</html>
`;

export function resolveAndroidApps(android) {
    if (Array.isArray(android?.apps)) {
        return android.apps;
    }
    if (android?.packageName) {
        return [{
            packageName: android.packageName,
            sha256CertFingerprints: android.sha256CertFingerprints ?? [],
        }];
    }
    return [];
}

function androidAssociationStatements(apps, errors) {
    const statements = [];
    for (const app of apps) {
        if (!packagePattern.test(app.packageName ?? '')) {
            errors.push('android.packageName is invalid');
            continue;
        }
        const fingerprints = app.sha256CertFingerprints ?? [];
        if (fingerprints.length === 0) {
            continue;
        }
        if (!fingerprints.every(value => fingerprintPattern.test(value))) {
            errors.push('android.sha256CertFingerprints must contain uppercase colon-separated SHA-256 fingerprints');
            continue;
        }
        statements.push({
            relation: ['delegate_permission/common.handle_all_urls'],
            target: {
                namespace: 'android_app',
                package_name: app.packageName,
                sha256_cert_fingerprints: fingerprints,
            },
        });
    }
    if (apps.length === 0) {
        errors.push('android.packageName is invalid');
    } else if (statements.length === 0) {
        errors.push('at least one Android app must have a SHA-256 certificate fingerprint');
    }
    return statements;
}

export function buildAppLinks(config, environment, appleTeamId) {
    if (environment !== 'dev') {
        throw new Error('App link association files are only generated for the dev environment');
    }

    const errors = [];
    if (!/^[a-z0-9-]+(\.[a-z0-9-]+)+$/.test(config.host ?? '')) errors.push('host must be a bare DNS name');
    if (!Array.isArray(config.callbackPaths) || config.callbackPaths.length === 0 ||
        !config.callbackPaths.every(path => callbackPathPattern.test(path))) {
        errors.push('callbackPaths must be /native/<name> paths');
    }
    const androidStatements = androidAssociationStatements(resolveAndroidApps(config.android), errors);
    if (!packagePattern.test(config.ios?.bundleId ?? '')) errors.push('ios.bundleId is invalid');

    const teamId = appleTeamId?.trim();
    if (teamId && !teamIdPattern.test(teamId)) {
        errors.push('APPLE_TEAM_ID must be a 10-character uppercase Apple team identifier');
    }
    if (errors.length > 0) {
        throw new Error(`Invalid app link configuration: ${errors.join('; ')}`);
    }

    const files = new Map();
    files.set('.well-known/assetlinks.json', androidStatements);

    if (teamId) {
        const appId = `${teamId}.${config.ios.bundleId}`;
        files.set('.well-known/apple-app-site-association', {
            applinks: {
                details: [{
                    appIDs: [appId],
                    components: config.callbackPaths.map(path => ({ '/': path })),
                }],
            },
            webcredentials: { apps: [appId] },
        });
    }

    return { files, fallbackPaths: config.callbackPaths, hasAppleAssociation: Boolean(teamId) };
}

export function buildHeaders(associationFiles, fallbackPaths) {
    const rules = [];
    for (const name of associationFiles) {
        rules.push(`/${name}`, '  Content-Type: application/json', '  Cache-Control: no-cache');
    }
    for (const path of fallbackPaths) {
        rules.push(path, '  Cache-Control: no-store', '  Referrer-Policy: no-referrer', '  X-Robots-Tag: noindex');
    }
    return `${rules.join('\n')}\n`;
}

async function main(args = process.argv.slice(2)) {
    const options = Object.fromEntries(args.reduce((pairs, value, index) => {
        if (value.startsWith('--')) pairs.push([value.slice(2), args[index + 1]]);
        return pairs;
    }, []));
    if (!options.environment || !options.config || !options.output) {
        throw new Error('Usage: configure-app-links.mjs --environment dev --config <path> --output <wwwroot>');
    }

    const config = JSON.parse(await readFile(options.config, 'utf8'));
    const result = buildAppLinks(config, options.environment, process.env.APPLE_TEAM_ID);
    const root = resolve(options.output);
    await mkdir(resolve(root, '.well-known'), { recursive: true });
    for (const [name, content] of result.files) {
        await writeFile(resolve(root, name), `${JSON.stringify(content, null, 2)}\n`, 'utf8');
    }
    await mkdir(resolve(root, 'native'), { recursive: true });
    for (const path of result.fallbackPaths) {
        await writeFile(resolve(root, `${path.slice(1)}.html`), fallbackPage, 'utf8');
    }
    await appendFile(resolve(root, '_headers'), buildHeaders([...result.files.keys()], result.fallbackPaths), 'utf8');
    if (!result.hasAppleAssociation) {
        console.log('APPLE_TEAM_ID is not set: apple-app-site-association was not generated; iOS Universal Links are unavailable.');
    }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    main().catch(error => {
        console.error(error.message);
        process.exitCode = 1;
    });
}
