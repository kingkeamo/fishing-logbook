import { pathToFileURL } from 'node:url';

const releaseTagPattern = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const allowedBumps = new Set(['patch', 'minor', 'major']);

export function parseReleaseTag(tag) {
    const match = releaseTagPattern.exec(tag);
    if (!match) {
        return null;
    }

    return {
        tag,
        major: BigInt(match[1]),
        minor: BigInt(match[2]),
        patch: BigInt(match[3]),
    };
}

export function latestReleaseTag(tags) {
    const versions = tags
        .map(parseReleaseTag)
        .filter(version => version !== null)
        .sort(compareVersions);

    if (versions.length === 0) {
        throw new Error('No existing release tag matching vX.Y.Z was found.');
    }

    return versions.at(-1);
}

export function nextReleaseTag(tags, bump) {
    if (!allowedBumps.has(bump)) {
        throw new Error('Release bump must be patch, minor, or major.');
    }

    const latest = latestReleaseTag(tags);
    if (bump === 'major') {
        return `v${latest.major + 1n}.0.0`;
    }

    if (bump === 'minor') {
        return `v${latest.major}.${latest.minor + 1n}.0`;
    }

    return `v${latest.major}.${latest.minor}.${latest.patch + 1n}`;
}

function compareVersions(left, right) {
    if (left.major !== right.major) {
        return left.major < right.major ? -1 : 1;
    }

    if (left.minor !== right.minor) {
        return left.minor < right.minor ? -1 : 1;
    }

    if (left.patch !== right.patch) {
        return left.patch < right.patch ? -1 : 1;
    }

    return 0;
}

async function main() {
    const operation = process.argv[2];
    const input = await readStandardInput();
    const tags = input.split(/\r?\n/u).map(tag => tag.trim()).filter(Boolean);
    const result = operation === 'latest'
        ? latestReleaseTag(tags).tag
        : nextReleaseTag(tags, operation);
    process.stdout.write(`${result}\n`);
}

async function readStandardInput() {
    let input = '';
    for await (const chunk of process.stdin) {
        input += chunk;
    }

    return input;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
