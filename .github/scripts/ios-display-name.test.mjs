import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { assertGeneratedIosDisplayName } from './ios-display-name.mjs';

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
