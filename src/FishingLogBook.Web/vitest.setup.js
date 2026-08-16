import 'fake-indexeddb/auto';
import { afterEach } from 'vitest';

afterEach(async () => {
    const databases = await indexedDB.databases();
    await Promise.all(
        (databases ?? [])
            .map((database) => database.name)
            .filter(Boolean)
            .map((name) => new Promise((resolve, reject) => {
                const request = indexedDB.deleteDatabase(name);
                request.onsuccess = () => resolve();
                request.onerror = () => reject(request.error);
                request.onblocked = () => resolve();
            })));
});
