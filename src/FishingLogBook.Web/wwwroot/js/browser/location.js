const dismissedKey = 'flb-location-prompt-dismissed';

export async function queryPermission() {
    if (!navigator.geolocation) {
        return 'unavailable';
    }

    try {
        const status = await navigator.permissions.query({ name: 'geolocation' });
        return status.state;
    } catch {
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

export function getCurrent(timeoutMs) {
    return new Promise((resolve) => {
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
    });
}
