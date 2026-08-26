import { TimeoutError, withTimeout } from '../browser/timeout.js';

export { withTimeout };

export function elapsedSince(started) {
    return Math.round(performance.now() - started);
}

export function closeDatabase(db) {
    try {
        db.close();
    } catch {
        // Already closed.
    }
}

export function openDatabase({
    databaseName,
    version,
    timeoutMs,
    timeoutLabel = 'IndexedDB open',
    onUpgrade,
    onOpened,
    onFailed,
    onTimedOut,
    onVersionChange
}) {
    return withTimeout(new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, version);
        request.onupgradeneeded = (event) => {
            onUpgrade?.(request.result, event);
        };
        request.onsuccess = () => {
            const db = request.result;
            db.onversionchange = () => {
                if (onVersionChange) {
                    onVersionChange(db);
                    return;
                }

                closeDatabase(db);
            };
            db.onclose = () => { };
            onOpened?.(db);
            resolve(db);
        };
        request.onerror = () => {
            onFailed?.(request.error);
            reject(request.error);
        };
    }), timeoutMs, timeoutLabel).catch((error) => {
        if (error instanceof TimeoutError) {
            onTimedOut?.(error);
        }

        throw error;
    });
}

export function executeTransaction(db, {
    storeName,
    mode,
    abortMessage = 'IndexedDB transaction aborted',
    execute,
    closeWhenDone = true,
    onStarted,
    onCompleted,
    onAborted,
    onError,
    onRequestSucceeded,
    onClosed,
    onTransactionCreated
}) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, mode);
        onTransactionCreated?.(transaction);
        onStarted?.();

        let result;
        let settled = false;

        const finishClose = () => {
            if (!closeWhenDone) {
                return;
            }

            closeDatabase(db);
            onClosed?.();
        };

        const settle = (action) => {
            if (settled) {
                return;
            }

            settled = true;
            action();
        };

        transaction.oncomplete = () => {
            onCompleted?.();
            finishClose();
            settle(() => {
                resolve(result);
            });
        };
        transaction.onabort = () => {
            onAborted?.(transaction.error);
            finishClose();
            settle(() => reject(transaction.error || new Error(abortMessage)));
        };
        transaction.onerror = () => {
            onError?.(transaction.error);
            finishClose();
            settle(() => reject(transaction.error));
        };

        execute(transaction.objectStore(storeName), (value) => {
            result = value;
            onRequestSucceeded?.();
        }, (error) => {
            onError?.(error);
            try {
                transaction.abort();
            } catch {
                // Already aborted.
            }
            settle(() => reject(error || new Error('IndexedDB request failed')));
        });
    });
}

export function runTransaction(db, options) {
    let transaction;
    return withTimeout(
        executeTransaction(db, {
            ...options,
            onTransactionCreated: (created) => {
                transaction = created;
                options.onTransactionCreated?.(created);
            }
        }),
        options.timeoutMs,
        options.timeoutLabel
    ).catch((error) => {
        if (error instanceof TimeoutError) {
            options.onTimedOut?.(error);
            try {
                transaction?.abort();
            } catch {
                // The transaction already completed or aborted.
            }
        }
        if (options.closeWhenDone !== false) {
            closeDatabase(db);
        }

        throw error;
    });
}

export function executeMultiStoreTransaction(db, {
    storeNames,
    mode,
    abortMessage = 'IndexedDB transaction aborted',
    execute,
    closeWhenDone = true,
    onStarted,
    onCompleted,
    onAborted,
    onError,
    onRequestSucceeded,
    onClosed,
    onTransactionCreated
}) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeNames, mode);
        onTransactionCreated?.(transaction);
        onStarted?.();

        let result;
        let settled = false;

        const finishClose = () => {
            if (!closeWhenDone) {
                return;
            }

            closeDatabase(db);
            onClosed?.();
        };

        const settle = (action) => {
            if (settled) {
                return;
            }

            settled = true;
            action();
        };

        transaction.oncomplete = () => {
            onCompleted?.();
            finishClose();
            settle(() => {
                resolve(result);
            });
        };
        transaction.onabort = () => {
            onAborted?.(transaction.error);
            finishClose();
            settle(() => reject(transaction.error || new Error(abortMessage)));
        };
        transaction.onerror = () => {
            onError?.(transaction.error);
            finishClose();
            settle(() => reject(transaction.error));
        };

        execute(transaction, (value) => {
            result = value;
            onRequestSucceeded?.();
        }, (error) => {
            onError?.(error);
            try {
                transaction.abort();
            } catch {
                // Already aborted.
            }
            settle(() => reject(error || new Error('IndexedDB request failed')));
        });
    });
}

export function runMultiStoreTransaction(db, options) {
    let transaction;
    return withTimeout(
        executeMultiStoreTransaction(db, {
            ...options,
            onTransactionCreated: (created) => {
                transaction = created;
                options.onTransactionCreated?.(created);
            }
        }),
        options.timeoutMs,
        options.timeoutLabel
    ).catch((error) => {
        if (error instanceof TimeoutError) {
            options.onTimedOut?.(error);
            try {
                transaction?.abort();
            } catch {
                // The transaction already completed or aborted.
            }
        }
        if (options.closeWhenDone !== false) {
            closeDatabase(db);
        }

        throw error;
    });
}

export async function getStorageEstimate() {
    if (!navigator.storage || typeof navigator.storage.estimate !== 'function') {
        return { quota: null, usage: null };
    }

    try {
        const estimate = await navigator.storage.estimate();
        return {
            quota: Number.isFinite(estimate.quota) ? estimate.quota : null,
            usage: Number.isFinite(estimate.usage) ? estimate.usage : null
        };
    } catch {
        return { quota: null, usage: null };
    }
}
