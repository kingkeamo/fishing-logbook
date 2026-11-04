import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { assertGeneratedIosDisplayName } from './ios-display-name.mjs';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const runFile = promisify(execFile);

function infoPlist(displayName, bundleId) {
    return `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDisplayName</key>
	<string>${displayName}</string>
	<key>CFBundleIdentifier</key>
	<string>${bundleId}</string>
</dict>
</plist>
`;
}

describe('iOS generated display name', () => {
    for (const binary of [false, true]) {
        for (const displayName of ["Catch But Don&apos;t Forget", 'Catch']) {
            it(`validates ${binary ? 'binary' : 'XML'} plist with display name ${displayName} using macOS plutil`,
                { skip: process.platform !== 'darwin' }, async () => {
                    const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-name-'));
                    try {
                        const outputDirectory = join(projectDirectory, 'bin', 'Debug', 'net10.0-ios', 'iossimulator-arm64', 'App.app');
                        await mkdir(outputDirectory, { recursive: true });
                        const filePath = join(outputDirectory, 'Info.plist');
                        await writeFile(filePath, infoPlist(displayName, 'com.catchbutdontforget.app'));
                        if (binary) await runFile('/usr/bin/plutil', ['-convert', 'binary1', filePath]);
                        if (displayName === 'Catch') {
                            await assert.rejects(() => assertGeneratedIosDisplayName(projectDirectory), /CFBundleDisplayName is "Catch"/);
                        } else {
                            assert.deepEqual(await assertGeneratedIosDisplayName(projectDirectory), [filePath]);
                        }
                    } finally {
                        await rm(projectDirectory, { recursive: true, force: true });
                    }
                });
        }
    }

    it('ignores Android outputs, embedded frameworks and debug symbol plists', async () => {
        const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-name-'));
        try {
            const iosDirectory = join(projectDirectory, 'bin', 'Debug', 'net10.0-ios', 'App.app');
            const frameworkDirectory = join(iosDirectory, 'Frameworks', 'Library.framework');
            const symbolsDirectory = join(projectDirectory, 'bin', 'Release', 'net10.0-ios', 'App.app.dSYM', 'Contents');
            const androidDirectory = join(projectDirectory, 'obj', 'net10.0-android');
            await mkdir(frameworkDirectory, { recursive: true });
            await mkdir(symbolsDirectory, { recursive: true });
            await mkdir(androidDirectory, { recursive: true });
            await writeFile(join(iosDirectory, 'Info.plist'), infoPlist("Catch But Don&apos;t Forget", 'com.catchbutdontforget.app'));
            await writeFile(join(frameworkDirectory, 'Info.plist'), infoPlist('Library', 'com.library'));
            await writeFile(join(symbolsDirectory, 'Info.plist'), infoPlist('Symbols', 'com.apple.xcode.dsym.com.catchbutdontforget.app'));
            await writeFile(join(androidDirectory, 'Info.plist'), infoPlist('Other', 'com.android'));
            assert.deepEqual(await assertGeneratedIosDisplayName(projectDirectory), [join(iosDirectory, 'Info.plist')]);
        } finally {
            await rm(projectDirectory, { recursive: true, force: true });
        }
    });
    it('accepts the full brand name on a compiled Info.plist', async () => {
        const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-name-'));
        try {
            const outputDirectory = join(projectDirectory, 'obj', 'net10.0-ios', 'ios-arm64');
            await mkdir(outputDirectory, { recursive: true });
            await writeFile(
                join(outputDirectory, 'Info.plist'),
                infoPlist("Catch But Don't Forget", 'com.catchbutdontforget.app'));

            const files = await assertGeneratedIosDisplayName(projectDirectory);
            assert.equal(files.length, 1);
        } finally {
            await rm(projectDirectory, { recursive: true, force: true });
        }
    });

    it('rejects the truncated Catch display name', async () => {
        const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-name-'));
        try {
            const outputDirectory = join(projectDirectory, 'obj', 'net10.0-ios');
            await mkdir(outputDirectory, { recursive: true });
            await writeFile(
                join(outputDirectory, 'Info.plist'),
                infoPlist('Catch', 'com.catchbutdontforget.app'));

            await assert.rejects(
                () => assertGeneratedIosDisplayName(projectDirectory),
                /CFBundleDisplayName is "Catch"/);
        } finally {
            await rm(projectDirectory, { recursive: true, force: true });
        }
    });

    it('keeps the existing production bundle identifier', async () => {
        const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-name-'));
        try {
            const outputDirectory = join(projectDirectory, 'bin', 'Release', 'net10.0-ios');
            await mkdir(outputDirectory, { recursive: true });
            await writeFile(
                join(outputDirectory, 'Info.plist'),
                infoPlist("Catch But Don't Forget", 'com.example.wrong'));

            await assert.rejects(
                () => assertGeneratedIosDisplayName(projectDirectory),
                /CFBundleIdentifier is "com.example.wrong"/);
        } finally {
            await rm(projectDirectory, { recursive: true, force: true });
        }
    });

    it('fails when no compiled Info.plist exists', async () => {
        const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-name-'));
        try {
            await mkdir(join(projectDirectory, 'obj'), { recursive: true });
            await assert.rejects(
                () => assertGeneratedIosDisplayName(projectDirectory),
                /No generated iOS Info.plist/);
        } finally {
            await rm(projectDirectory, { recursive: true, force: true });
        }
    });
});
