import { readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { inflateSync, deflateSync } from 'node:zlib';
import { pathToFileURL } from 'node:url';

const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const appStoreIconSize = 1024;
const rgbColorType = 2;
const rgbaColorType = 6;

const crcTable = new Uint32Array(256);
for (let index = 0; index < 256; index += 1) {
    let value = index;
    for (let bit = 0; bit < 8; bit += 1) {
        value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
    }

    crcTable[index] = value;
}

function crc32(bytes) {
    let crc = 0xffffffff;
    for (const byte of bytes) {
        crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
    }

    return (crc ^ 0xffffffff) >>> 0;
}

function readChunk(bytes, offset) {
    const length = bytes.readUInt32BE(offset);
    const type = bytes.toString('ascii', offset + 4, offset + 8);
    const dataStart = offset + 8;
    const dataEnd = dataStart + length;
    const data = bytes.subarray(dataStart, dataEnd);
    return { length, type, data, next: dataEnd + 4 };
}

export function inspectPng(bytes) {
    if (bytes.length < 8 || !bytes.subarray(0, 8).equals(pngSignature)) {
        throw new Error('File is not a PNG.');
    }

    let offset = 8;
    let header;
    let hasTransparencyChunk = false;
    while (offset + 12 <= bytes.length) {
        const chunk = readChunk(bytes, offset);
        if (chunk.type === 'IHDR') {
            header = {
                width: chunk.data.readUInt32BE(0),
                height: chunk.data.readUInt32BE(4),
                bitDepth: chunk.data[8],
                colorType: chunk.data[9],
                interlace: chunk.data[12],
            };
        }

        if (chunk.type === 'tRNS') {
            hasTransparencyChunk = true;
        }

        if (chunk.type === 'IEND') {
            break;
        }

        offset = chunk.next;
    }

    if (!header) {
        throw new Error('PNG is missing an IHDR chunk.');
    }

    return {
        ...header,
        hasTransparencyChunk,
        hasAlphaChannel: header.colorType === 4 || header.colorType === rgbaColorType || hasTransparencyChunk,
    };
}

function paethPredictor(left, up, upLeft) {
    const estimate = left + up - upLeft;
    const leftDistance = Math.abs(estimate - left);
    const upDistance = Math.abs(estimate - up);
    const upLeftDistance = Math.abs(estimate - upLeft);
    if (leftDistance <= upDistance && leftDistance <= upLeftDistance) {
        return left;
    }

    return upDistance <= upLeftDistance ? up : upLeft;
}

function unfilter(inflated, width, height, bytesPerPixel) {
    const stride = width * bytesPerPixel;
    const rows = [];
    let offset = 0;
    let previous = Buffer.alloc(stride);
    for (let row = 0; row < height; row += 1) {
        const filter = inflated[offset];
        offset += 1;
        const current = Buffer.from(inflated.subarray(offset, offset + stride));
        offset += stride;
        for (let column = 0; column < stride; column += 1) {
            const left = column >= bytesPerPixel ? current[column - bytesPerPixel] : 0;
            const up = previous[column];
            const upLeft = column >= bytesPerPixel ? previous[column - bytesPerPixel] : 0;
            if (filter === 1) {
                current[column] = (current[column] + left) & 0xff;
            } else if (filter === 2) {
                current[column] = (current[column] + up) & 0xff;
            } else if (filter === 3) {
                current[column] = (current[column] + Math.floor((left + up) / 2)) & 0xff;
            } else if (filter === 4) {
                current[column] = (current[column] + paethPredictor(left, up, upLeft)) & 0xff;
            } else if (filter !== 0) {
                throw new Error(`Unsupported PNG filter ${filter}.`);
            }
        }

        rows.push(current);
        previous = current;
    }

    return rows;
}

function decodePng(bytes) {
    const info = inspectPng(bytes);
    if (info.bitDepth !== 8 || info.interlace !== 0 || (info.colorType !== rgbColorType && info.colorType !== rgbaColorType)) {
        throw new Error(
            `Cannot decode PNG colorType=${info.colorType} bitDepth=${info.bitDepth} interlace=${info.interlace}.`);
    }

    const idat = [];
    let offset = 8;
    while (offset + 12 <= bytes.length) {
        const chunk = readChunk(bytes, offset);
        if (chunk.type === 'IDAT') {
            idat.push(chunk.data);
        }

        if (chunk.type === 'IEND') {
            break;
        }

        offset = chunk.next;
    }

    const bytesPerPixel = info.colorType === rgbaColorType ? 4 : 3;
    const inflated = inflateSync(Buffer.concat(idat));
    const rows = unfilter(inflated, info.width, info.height, bytesPerPixel);
    return { info, bytesPerPixel, rows };
}

function writeChunk(type, data) {
    const typeBytes = Buffer.from(type, 'ascii');
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(Buffer.concat([typeBytes, data])));
    return Buffer.concat([length, typeBytes, data, crc]);
}

export function encodeRgbPng(width, height, rows) {
    const ihdr = Buffer.alloc(13);
    ihdr.writeUInt32BE(width, 0);
    ihdr.writeUInt32BE(height, 4);
    ihdr[8] = 8;
    ihdr[9] = rgbColorType;
    const filtered = [];
    for (const row of rows) {
        filtered.push(Buffer.from([0]), row);
    }

    return Buffer.concat([
        pngSignature,
        writeChunk('IHDR', ihdr),
        writeChunk('IDAT', deflateSync(Buffer.concat(filtered))),
        writeChunk('IEND', Buffer.alloc(0)),
    ]);
}

export function stripAlphaToOpaqueRgb(bytes, background = { r: 255, g: 255, b: 255 }) {
    const decoded = decodePng(bytes);
    const rows = decoded.rows.map(row => {
        const rgb = Buffer.alloc(decoded.info.width * 3);
        for (let pixel = 0; pixel < decoded.info.width; pixel += 1) {
            const source = pixel * decoded.bytesPerPixel;
            const destination = pixel * 3;
            const alpha = decoded.bytesPerPixel === 4 ? row[source + 3] : 255;
            rgb[destination] = Math.round((row[source] * alpha + background.r * (255 - alpha)) / 255);
            rgb[destination + 1] = Math.round((row[source + 1] * alpha + background.g * (255 - alpha)) / 255);
            rgb[destination + 2] = Math.round((row[source + 2] * alpha + background.b * (255 - alpha)) / 255);
        }

        return rgb;
    });

    return encodeRgbPng(decoded.info.width, decoded.info.height, rows);
}

export function assertOpaqueAppStoreIcon(bytes, label) {
    const info = inspectPng(bytes);
    if (info.width !== appStoreIconSize || info.height !== appStoreIconSize) {
        throw new Error(`${label} must be ${appStoreIconSize}x${appStoreIconSize}, found ${info.width}x${info.height}.`);
    }

    if (info.hasAlphaChannel) {
        throw new Error(
            `${label} contains an alpha channel (colorType=${info.colorType}, tRNS=${info.hasTransparencyChunk}). Apple error 90717 rejects this.`);
    }

    if (info.colorType !== rgbColorType || info.bitDepth !== 8) {
        throw new Error(`${label} must be 8-bit RGB PNG, found colorType=${info.colorType} bitDepth=${info.bitDepth}.`);
    }

    return info;
}

async function collectFiles(directory) {
    try {
        return await readdir(directory, { withFileTypes: true, recursive: true });
    } catch (error) {
        if (error.code === 'ENOENT') {
            return [];
        }

        throw error;
    }
}

export async function findGeneratedIosAppStoreIcons(projectDirectory) {
    const searchRoots = [
        join(projectDirectory, 'obj'),
        join(projectDirectory, 'bin'),
    ];
    const icons = [];
    for (const root of searchRoots) {
        const entries = await collectFiles(root);
        for (const entry of entries) {
            if (!entry.isFile() || entry.name !== 'Contents.json') {
                continue;
            }

            const catalogDirectory = entry.parentPath ?? dirname(join(root, entry.name));
            if (!/appicon[^/]*\.appiconset$/iu.test(catalogDirectory.replaceAll('\\', '/'))) {
                continue;
            }

            const catalog = JSON.parse(await readFile(join(catalogDirectory, 'Contents.json'), 'utf8'));
            for (const image of catalog.images ?? []) {
                if (image.size !== '1024x1024' || !image.filename) {
                    continue;
                }

                icons.push(resolve(catalogDirectory, image.filename));
            }
        }
    }

    return [...new Set(icons)];
}

export async function ensureGeneratedIosAppStoreIcons(projectDirectory) {
    const icons = await findGeneratedIosAppStoreIcons(projectDirectory);
    if (icons.length === 0) {
        throw new Error(
            `No generated 1024x1024 iOS App Store icon was found under ${projectDirectory}. This check requires a net10.0-ios Resizetizer output (macOS CI).`);
    }

    const results = [];
    for (const iconPath of icons) {
        const original = await readFile(iconPath);
        const inspected = inspectPng(original);
        let bytes = original;
        if (inspected.hasAlphaChannel) {
            bytes = stripAlphaToOpaqueRgb(original);
            await writeFile(iconPath, bytes);
        }

        results.push({ path: iconPath, ...assertOpaqueAppStoreIcon(bytes, iconPath) });
    }

    return results;
}

async function main() {
    const operation = process.argv[2];
    const target = resolve(process.argv[3] ?? '.');
    if (operation === 'assert-source') {
        const info = assertOpaqueAppStoreIcon(await readFile(target), target);
        process.stdout.write(`Opaque ${info.width}x${info.height} RGB icon: ${target}\n`);
        return;
    }

    if (operation === 'assert-generated') {
        const icons = await findGeneratedIosAppStoreIcons(target);
        if (icons.length === 0) {
            throw new Error(
                `No generated 1024x1024 iOS App Store icon was found under ${target}. Run this after a net10.0-ios build on macOS.`);
        }

        for (const iconPath of icons) {
            assertOpaqueAppStoreIcon(await readFile(iconPath), iconPath);
            process.stdout.write(`Generated iOS App Store icon is opaque RGB: ${iconPath}\n`);
        }

        return;
    }

    if (operation === 'ensure-generated') {
        const results = await ensureGeneratedIosAppStoreIcons(target);
        for (const result of results) {
            process.stdout.write(`Ensured opaque 1024x1024 RGB iOS App Store icon: ${result.path}\n`);
        }

        return;
    }

    throw new Error('Operation must be assert-source, assert-generated or ensure-generated.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main().catch(error => {
        process.stderr.write(`${error.message}\n`);
        process.exitCode = 1;
    });
}
