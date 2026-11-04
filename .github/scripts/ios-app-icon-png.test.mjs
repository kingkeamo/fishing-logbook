import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { deflateSync } from 'node:zlib';
import { describe, it } from 'node:test';
import {
    assertOpaqueAppStoreIcon,
    ensureGeneratedIosAppStoreIcons,
    inspectPng,
    stripAlphaToOpaqueRgb,
} from './ios-app-icon-png.mjs';

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const iosIconPath = join(
    repositoryRoot,
    'src',
    'FishingLogBook.Mobile',
    'Resources',
    'AppIcon',
    'appicon_ios.png');
const webIconPath = join(repositoryRoot, 'src', 'FishingLogBook.Web', 'wwwroot', 'icon-512.png');

describe('iOS App Store icon PNG inspection', () => {
    it('rejects the shared web icon because it has an alpha channel', async () => {
        const info = inspectPng(await readFile(webIconPath));
        assert.equal(info.width, 512);
        assert.equal(info.colorType, 6);
        assert.equal(info.hasAlphaChannel, true);
    });

    it('accepts the committed iOS source icon as 1024x1024 8-bit RGB', async () => {
        const bytes = await readFile(iosIconPath);
        const info = assertOpaqueAppStoreIcon(bytes, iosIconPath);
        assert.equal(info.width, 1024);
        assert.equal(info.height, 1024);
        assert.equal(info.colorType, 2);
        assert.equal(info.hasTransparencyChunk, false);
        assert.equal(info.hasAlphaChannel, false);
    });

    it('fails assertion when a 1024 PNG still contains an alpha channel', () => {
        assert.throws(
            () => assertOpaqueAppStoreIcon(encodeRgbaPng(1024, 1024), 'fixture'),
            /alpha channel/);
    });

    it('composites transparent pixels onto white when stripping alpha', () => {
        const stripped = inspectPng(stripAlphaToOpaqueRgb(encodeRgbaPng(2, 1, [
            [0, 0, 0, 0, 255, 0, 0, 255],
        ])));
        assert.equal(stripped.colorType, 2);
        assert.equal(stripped.hasAlphaChannel, false);
        assert.equal(stripped.width, 2);
        assert.equal(stripped.height, 1);
    });

    it('rewrites a generated 1024 App Store icon that still has alpha', async () => {
        const projectDirectory = await mkdtemp(join(tmpdir(), 'ios-icon-'));
        const catalog = join(projectDirectory, 'obj', 'r', 'Assets.xcassets', 'appicon_ios.appiconset');
        try {
            await mkdir(catalog, { recursive: true });
            await writeFile(join(catalog, 'AppIcon-1024.png'), encodeRgbaPng(1024, 1024));
            await writeFile(join(catalog, 'Contents.json'), JSON.stringify({
                images: [
                    { size: '1024x1024', idiom: 'ios-marketing', filename: 'AppIcon-1024.png', scale: '1x' },
                ],
            }));
            const results = await ensureGeneratedIosAppStoreIcons(projectDirectory);
            assert.equal(results.length, 1);
            const ensured = inspectPng(await readFile(results[0].path));
            assert.equal(ensured.colorType, 2);
            assert.equal(ensured.hasAlphaChannel, false);
            assert.equal(ensured.width, 1024);
        } finally {
            await rm(projectDirectory, { recursive: true, force: true });
        }
    });
});

function encodeRgbaPng(width, height, pixelRows) {
    const rows = pixelRows ?? Array.from({ length: height }, (_, y) => {
        const row = Buffer.alloc(width * 4, 255);
        if (y === 0) {
            row[3] = 0;
        }

        return row;
    });
    const ihdr = Buffer.alloc(13);
    ihdr.writeUInt32BE(width, 0);
    ihdr.writeUInt32BE(height, 4);
    ihdr[8] = 8;
    ihdr[9] = 6;
    const filtered = [];
    for (const row of rows) {
        filtered.push(Buffer.from([0]), Buffer.from(row));
    }

    return Buffer.concat([
        Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
        pngChunk('IHDR', ihdr),
        pngChunk('IDAT', deflateSync(Buffer.concat(filtered))),
        pngChunk('IEND', Buffer.alloc(0)),
    ]);
}

function pngChunk(type, data) {
    const typeBytes = Buffer.from(type, 'ascii');
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const table = new Uint32Array(256);
    for (let index = 0; index < 256; index += 1) {
        let value = index;
        for (let bit = 0; bit < 8; bit += 1) {
            value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
        }

        table[index] = value;
    }

    let crc = 0xffffffff;
    const source = Buffer.concat([typeBytes, data]);
    for (const byte of source) {
        crc = table[(crc ^ byte) & 0xff] ^ (crc >>> 8);
    }

    const crcBytes = Buffer.alloc(4);
    crcBytes.writeUInt32BE((crc ^ 0xffffffff) >>> 0);
    return Buffer.concat([length, typeBytes, data, crcBytes]);
}
