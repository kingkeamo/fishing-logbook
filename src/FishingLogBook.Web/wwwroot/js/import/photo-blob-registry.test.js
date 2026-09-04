import { beforeEach, describe, expect, it, vi } from 'vitest';
import { clear, getBytes, register, remove } from './photo-blob-registry.js';

const createdUrls = [];

beforeEach(() => {
    clear();
    createdUrls.length = 0;
    let nextToken = 0;
    vi.stubGlobal('crypto', { randomUUID: () => `token-${++nextToken}` });
    vi.stubGlobal('createImageBitmap', vi.fn(async () => ({
        width: 1200,
        height: 600,
        close: vi.fn()
    })));
    URL.createObjectURL = vi.fn(() => {
        const url = `blob:thumbnail-${createdUrls.length + 1}`;
        createdUrls.push(url);
        return url;
    });
    URL.revokeObjectURL = vi.fn();
    HTMLCanvasElement.prototype.getContext = vi.fn(() => ({ drawImage: vi.fn() }));
    HTMLCanvasElement.prototype.toBlob = vi.fn(callback => callback(new Blob([[9]], { type: 'image/jpeg' })));
});

describe('Import photo blob registry', () => {
    it('registers opaque tokens and creates bounded object URL thumbnails', async () => {
        const first = await register(new Uint8Array([1, 2, 3]), 'image/jpeg');
        const second = await register(new Uint8Array([4, 5]), 'image/png');

        expect(first).toEqual({ token: 'token-1', thumbnailUrl: 'blob:thumbnail-1' });
        expect(second).toEqual({ token: 'token-2', thumbnailUrl: 'blob:thumbnail-2' });
        expect(URL.createObjectURL).toHaveBeenCalledTimes(2);
        expect(createImageBitmap).toHaveBeenCalledTimes(2);
    });

    it('retrieves the sanitised bytes by opaque token', async () => {
        const registration = await register(new Uint8Array([1, 2, 3]), 'image/jpeg');

        const bytes = await getBytes(registration.token);

        expect([...bytes]).toEqual([1, 2, 3]);
    });

    it('deletes one blob and revokes only its thumbnail URL', async () => {
        const first = await register(new Uint8Array([1]), 'image/jpeg');
        const second = await register(new Uint8Array([2]), 'image/jpeg');

        expect(remove(first.token)).toBe(true);

        expect(URL.revokeObjectURL).toHaveBeenCalledWith(first.thumbnailUrl);
        expect([...await getBytes(second.token)]).toEqual([2]);
        await expect(getBytes(first.token)).rejects.toThrow('import-photo-not-found');
    });

    it('clears every blob and revokes every thumbnail URL', async () => {
        const first = await register(new Uint8Array([1]), 'image/jpeg');
        const second = await register(new Uint8Array([2]), 'image/jpeg');

        clear();

        expect(URL.revokeObjectURL).toHaveBeenCalledWith(first.thumbnailUrl);
        expect(URL.revokeObjectURL).toHaveBeenCalledWith(second.thumbnailUrl);
        await expect(getBytes(first.token)).rejects.toThrow('import-photo-not-found');
        await expect(getBytes(second.token)).rejects.toThrow('import-photo-not-found');
    });

    it('closes each decoded bitmap after creating its thumbnail', async () => {
        const close = vi.fn();
        createImageBitmap.mockResolvedValueOnce({ width: 100, height: 200, close });

        await register(new Uint8Array([1]), 'image/jpeg');

        expect(close).toHaveBeenCalledTimes(1);
    });

    it('does not retain an entry when thumbnail creation fails', async () => {
        HTMLCanvasElement.prototype.toBlob = vi.fn(callback => callback(null));

        await expect(register(new Uint8Array([1]), 'image/jpeg'))
            .rejects.toThrow('thumbnail-creation-failed');

        await expect(getBytes('token-1')).rejects.toThrow('import-photo-not-found');
        expect(URL.createObjectURL).not.toHaveBeenCalled();
    });
});
