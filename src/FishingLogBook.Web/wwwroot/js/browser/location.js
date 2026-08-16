import { withTimeout } from './timeout.js';

const dismissedKey = 'flb-location-prompt-dismissed';
const permissionTimeoutMs = 2000;

export async function queryPermission() {
    if (!navigator.geolocation) {
        return 'unavailable';
    }

    if (!navigator.permissions || typeof navigator.permissions.query !== 'function') {
        return 'unavailable';
    }

    try {
        const status = await withTimeout(
            navigator.permissions.query({ name: 'geolocation' }),
            permissionTimeoutMs,
            'location permission');
        return status.state;
    } catch (error) {
        if (error && String(error.message || '').includes('timed out')) {
            return 'unavailable';
        }

        return 'prompt';
    }
}

export function isPromptDismissed() {
    try {
        return localStorage.getItem(dismissedKey) === '1';
    } catch {
        return false;
    }
}

export function setPromptDismissed() {
    try {
        localStorage.setItem(dismissedKey, '1');
    } catch {
        /* ignore */
    }
}

export async function getCurrent(timeoutMs) {
    try {
        return await withTimeout(new Promise((resolve) => {
            if (!navigator.geolocation) {
                resolve({ error: 'unavailable' });
                return;
            }

            navigator.geolocation.getCurrentPosition(
                (position) => {
                    resolve({
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy,
                        timestamp: new Date(position.timestamp).toISOString()
                    });
                },
                (error) => {
                    if (error.code === 1) {
                        resolve({ error: 'denied' });
                        return;
                    }

                    if (error.code === 3) {
                        resolve({ error: 'timeout' });
                        return;
                    }

                    resolve({ error: 'unavailable' });
                },
                {
                    enableHighAccuracy: true,
                    timeout: timeoutMs,
                    maximumAge: 0
                }
            );
        }), timeoutMs, 'location');
    } catch {
        return { error: 'timeout' };
    }
}
