const minimalJpeg = Buffer.from(
    '/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a'
    + 'HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAHwAAAQUBAQEB'
    + 'AQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1Fh'
    + 'ByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZ'
    + 'WmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXG'
    + 'x8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APn+iiiv/9k=',
    'base64');

const dateTimeOriginalTag = 0x9003;
const offsetTimeOriginalTag = 0x9011;
const exifIfdPointerTag = 0x8769;
const gpsIfdPointerTag = 0x8825;

export function jpegWithExif({ capturedOn, offset, latitude, longitude } = {}) {
    const tiff = buildTiff({ capturedOn, offset, latitude, longitude });
    const payload = Buffer.concat([Buffer.from('Exif\0\0', 'latin1'), tiff]);
    const header = Buffer.alloc(4);
    header.writeUInt8(0xFF, 0);
    header.writeUInt8(0xE1, 1);
    header.writeUInt16BE(payload.length + 2, 2);
    return Buffer.concat([
        minimalJpeg.subarray(0, 2),
        header,
        payload,
        minimalJpeg.subarray(2)
    ]);
}

export function jpegWithoutExif() {
    return Buffer.from(minimalJpeg);
}

function buildTiff({ capturedOn, offset, latitude, longitude }) {
    const exifEntries = [];
    if (capturedOn) exifEntries.push(asciiEntry(dateTimeOriginalTag, capturedOn));
    if (offset) exifEntries.push(asciiEntry(offsetTimeOriginalTag, offset));
    const gpsEntries = buildGpsEntries(latitude, longitude);
    const rootCount = (exifEntries.length > 0 ? 1 : 0) + (gpsEntries.length > 0 ? 1 : 0);
    const exifOffset = 8 + ifdSize(rootCount);
    const gpsOffset = exifOffset + (exifEntries.length > 0 ? ifdSize(exifEntries.length) : 0);
    const dataOffset = gpsOffset + (gpsEntries.length > 0 ? ifdSize(gpsEntries.length) : 0);
    const rootEntries = [];
    if (exifEntries.length > 0) rootEntries.push(longEntry(exifIfdPointerTag, exifOffset));
    if (gpsEntries.length > 0) rootEntries.push(longEntry(gpsIfdPointerTag, gpsOffset));

    const header = Buffer.alloc(8);
    header.write('II', 0, 'latin1');
    header.writeUInt16LE(42, 2);
    header.writeUInt32LE(8, 4);
    const data = [];
    const sections = [writeIfd(rootEntries, dataOffset, data)];
    if (exifEntries.length > 0) sections.push(writeIfd(exifEntries, dataOffset, data));
    if (gpsEntries.length > 0) sections.push(writeIfd(gpsEntries, dataOffset, data));
    return Buffer.concat([header, ...sections, ...data]);
}

function buildGpsEntries(latitude, longitude) {
    if (latitude === undefined || longitude === undefined) return [];
    return [
        asciiEntry(0x0001, latitude >= 0 ? 'N' : 'S'),
        rationalEntry(0x0002, Math.abs(latitude)),
        asciiEntry(0x0003, longitude >= 0 ? 'E' : 'W'),
        rationalEntry(0x0004, Math.abs(longitude))
    ];
}

function ifdSize(entryCount) {
    return 2 + (entryCount * 12) + 4;
}

function writeIfd(entries, dataOffset, data) {
    const bytes = Buffer.alloc(ifdSize(entries.length));
    bytes.writeUInt16LE(entries.length, 0);
    entries.forEach((entry, index) => {
        const at = 2 + (index * 12);
        bytes.writeUInt16LE(entry.tag, at);
        bytes.writeUInt16LE(entry.type, at + 2);
        bytes.writeUInt32LE(entry.count, at + 4);
        if (entry.value.length <= 4) {
            entry.value.copy(bytes, at + 8);
            return;
        }

        bytes.writeUInt32LE(dataOffset + data.reduce((total, part) => total + part.length, 0), at + 8);
        data.push(entry.value);
    });
    return bytes;
}

function asciiEntry(tag, text) {
    const value = Buffer.concat([Buffer.from(text, 'latin1'), Buffer.from([0])]);
    return { tag, type: 2, count: value.length, value };
}

function longEntry(tag, number) {
    const value = Buffer.alloc(4);
    value.writeUInt32LE(number, 0);
    return { tag, type: 4, count: 1, value };
}

function rationalEntry(tag, degrees) {
    const whole = Math.floor(degrees);
    const minutesTotal = (degrees - whole) * 60;
    const minutes = Math.floor(minutesTotal);
    const seconds = Math.round((minutesTotal - minutes) * 60 * 1000);
    const value = Buffer.alloc(24);
    value.writeUInt32LE(whole, 0);
    value.writeUInt32LE(1, 4);
    value.writeUInt32LE(minutes, 8);
    value.writeUInt32LE(1, 12);
    value.writeUInt32LE(seconds, 16);
    value.writeUInt32LE(1000, 20);
    return { tag, type: 5, count: 3, value };
}
