const entries = new Map();
const maximumThumbnailDimension = 512;

export async function register(bytes, contentType) {
    const token = crypto.randomUUID();
    const blob = new Blob([bytes], { type: contentType });
    const thumbnail = await createThumbnail(blob);
    const thumbnailUrl = URL.createObjectURL(thumbnail);
    entries.set(token, { blob, thumbnailUrl });
    return { token, thumbnailUrl };
}

export async function getBytes(token) {
    const entry = requireEntry(token);
    return new Uint8Array(await entry.blob.arrayBuffer());
}

export function remove(token) {
    const entry = entries.get(token);
    if (!entry) {
        return false;
    }

    URL.revokeObjectURL(entry.thumbnailUrl);
    entries.delete(token);
    return true;
}

export function clear() {
    for (const entry of entries.values()) {
        URL.revokeObjectURL(entry.thumbnailUrl);
    }

    entries.clear();
}

async function createThumbnail(blob) {
    const bitmap = await createImageBitmap(blob, { imageOrientation: 'from-image' });
    try {
        const scale = Math.min(1, maximumThumbnailDimension / Math.max(bitmap.width, bitmap.height));
        const width = Math.max(1, Math.round(bitmap.width * scale));
        const height = Math.max(1, Math.round(bitmap.height * scale));
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        canvas.getContext('2d').drawImage(bitmap, 0, 0, width, height);
        return await canvasToBlob(canvas);
    } finally {
        bitmap.close();
    }
}

function canvasToBlob(canvas) {
    return new Promise((resolve, reject) => {
        canvas.toBlob(blob => {
            if (blob) {
                resolve(blob);
                return;
            }

            reject(new Error('thumbnail-creation-failed'));
        }, 'image/jpeg', 0.82);
    });
}

function requireEntry(token) {
    const entry = entries.get(token);
    if (!entry) {
        throw new Error('import-photo-not-found');
    }

    return entry;
}
