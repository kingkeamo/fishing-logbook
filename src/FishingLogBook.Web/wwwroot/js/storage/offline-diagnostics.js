import { elapsedSince } from './indexed-db.js';

export function emit(eventName, details) {
    const safe = {
        elapsedMilliseconds: details.elapsedMilliseconds,
        operation: details.operation,
        storeName: details.storeName,
        errorType: details.errorType,
        quotaBytes: details.quotaBytes,
        usageBytes: details.usageBytes
    };
    try {
        console.debug(`[FLB] ${eventName}`, safe);
        globalThis.fishingLogBookDiagnostics?.console?.('Debug', eventName, JSON.stringify(safe));
    } catch {
        // Console must never break IndexedDB.
    }
}

export function emitTimedOut(mode, objectStoreName, started, error) {
    if (!String(error?.message || '').includes('timed out')) {
        return;
    }

    const eventName = mode === 'readwrite' ? 'OfflineDbWriteTimedOut' : 'OfflineDbReadTimedOut';
    emit(eventName, {
        storeName: objectStoreName,
        operation: mode === 'readwrite' ? 'write' : 'read',
        elapsedMilliseconds: elapsedSince(started)
    });
}

export async function emitStorageEstimate() {
    if (!navigator.storage || typeof navigator.storage.estimate !== 'function') {
        return;
    }

    try {
        const estimate = await navigator.storage.estimate();
        emit('OfflineDbOpenCompleted', {
            operation: 'open',
            elapsedMilliseconds: 0,
            quotaBytes: Number.isFinite(estimate.quota) ? String(Math.trunc(estimate.quota)) : undefined,
            usageBytes: Number.isFinite(estimate.usage) ? String(Math.trunc(estimate.usage)) : undefined
        });
    } catch {
        // Estimate is debug-only and must never fail open.
    }
}
