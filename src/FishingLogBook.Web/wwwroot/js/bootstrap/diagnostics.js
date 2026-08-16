import { getPlatform } from '../browser/platform.js';

const sessionKey = 'flb-anonymous-session-id';
const lastErrorKey = 'flb-last-error';

export function createDiagnosticsApi(targetWindow) {
    return {
        getSessionId: () => {
            try { return targetWindow.localStorage.getItem(sessionKey); }
            catch { return null; }
        },
        setSessionId: (value) => {
            try { targetWindow.localStorage.setItem(sessionKey, value); } catch { /* ignore */ }
        },
        getPlatform: () => getPlatform(targetWindow.navigator),
        console: (level, eventName, message) => {
            const line = `[FLB] ${eventName}: ${message}`;
            if (level === 'Error' || level === 'Critical') {
                targetWindow.console.error(line);
            } else if (level === 'Warning') {
                targetWindow.console.warn(line);
            } else {
                targetWindow.console.debug(line);
            }
        },
        setLastError: (json) => {
            try { targetWindow.localStorage.setItem(lastErrorKey, json); } catch { }
        },
        getLastError: () => {
            try { return targetWindow.localStorage.getItem(lastErrorKey); } catch { return null; }
        }
    };
}

export function installDiagnostics(targetWindow) {
    targetWindow.fishingLogBookDiagnostics = createDiagnosticsApi(targetWindow);
}
