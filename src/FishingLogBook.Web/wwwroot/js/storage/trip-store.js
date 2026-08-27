import {
    TRIP_STORE_NAME,
    normalisedOwnerId,
    runLogbookTransaction
} from './logbook-database.js';

export const TRIP_ACTIVE_STATUS = 'Active';
export const TRIP_COMPLETED_STATUS = 'Completed';
export const TRIP_SYNCHRONISED_STATUS = 'synchronised';
export const TRIP_SAVED_OUTCOME = 'saved';
export const TRIP_ACTIVE_CONFLICT_OUTCOME = 'activeConflict';

export { TRIP_STORE_NAME };

export async function putTrip(json) {
    const trip = JSON.parse(json);
    if (!trip?.id || !normalisedOwnerId(trip.ownerUserId)) {
        throw new Error('Owned Trip id is required');
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'write', (store, succeed, fail) => {
        const owner = normalisedOwnerId(trip.ownerUserId);
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (cursor) {
                if (conflictsWithActiveTrip(cursor.value, trip, owner)) {
                    succeed(TRIP_ACTIVE_CONFLICT_OUTCOME);
                    return;
                }

                if (isOwnedByAnother(cursor.value, trip, owner)) {
                    fail(new Error('Trip ownership cannot be changed'));
                    return;
                }

                cursor.continue();
                return;
            }

            const write = store.put(trip);
            write.onerror = () => fail(write.error);
            write.onsuccess = () => succeed(TRIP_SAVED_OUTCOME);
        };
    });
}

export async function getTrips(ownerUserId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
        return [];
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'metadata-read', (store, succeed, fail) => {
        const trips = [];
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (cursor) {
                if (normalisedOwnerId(cursor.value?.ownerUserId) === owner) {
                    trips.push({ json: JSON.stringify(cursor.value) });
                }

                cursor.continue();
                return;
            }

            succeed(trips);
        };
    });
}

export async function getTrip(ownerUserId, tripId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || typeof tripId !== 'string' || !tripId) {
        return null;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'single-read', (store, succeed, fail) => {
        const request = store.get(tripId);
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const trip = request.result;
            if (!trip || normalisedOwnerId(trip.ownerUserId) !== owner) {
                succeed(null);
                return;
            }

            succeed({ json: JSON.stringify(trip) });
        };
    });
}

export async function getActiveTrip(ownerUserId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
        return null;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'active-read', (store, succeed, fail) => {
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                succeed(null);
                return;
            }

            if (isActiveForOwner(cursor.value, owner)) {
                succeed({ json: JSON.stringify(cursor.value) });
                return;
            }

            cursor.continue();
        };
    });
}

export async function getPendingTrips(ownerUserId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
        return [];
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'pending-read', (store, succeed, fail) => {
        const pending = [];
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (cursor) {
                if (normalisedOwnerId(cursor.value?.ownerUserId) === owner
                    && !isSynchronised(cursor.value?.syncStatus)) {
                    pending.push({ json: JSON.stringify(cursor.value) });
                }

                cursor.continue();
                return;
            }

            succeed(pending);
        };
    });
}

export async function cleanupSyncedTrips(ownerUserId, olderThanIso) {
    const owner = normalisedOwnerId(ownerUserId);
    const cutoff = Date.parse(olderThanIso);
    if (!owner || Number.isNaN(cutoff)) {
        return 0;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'cleanup', (store, succeed, fail) => {
        let removed = 0;
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                succeed(removed);
                return;
            }

            if (normalisedOwnerId(cursor.value?.ownerUserId) === owner
                && isEligibleForCleanup(cursor.value, cutoff)) {
                cursor.delete();
                removed += 1;
            }

            cursor.continue();
        };
    });
}

function isEligibleForCleanup(trip, cutoff) {
    if (trip?.status !== TRIP_COMPLETED_STATUS || !isSynchronised(trip?.syncStatus)) {
        return false;
    }

    const syncedAt = Date.parse(trip.syncedAt);
    return !Number.isNaN(syncedAt) && syncedAt <= cutoff;
}

function isSynchronised(status) {
    return status === TRIP_SYNCHRONISED_STATUS || status === 3;
}

function isActiveForOwner(trip, owner) {
    return normalisedOwnerId(trip?.ownerUserId) === owner && trip?.status === TRIP_ACTIVE_STATUS;
}

function conflictsWithActiveTrip(stored, incoming, owner) {
    return incoming.status === TRIP_ACTIVE_STATUS
        && stored?.id !== incoming.id
        && isActiveForOwner(stored, owner);
}

function isOwnedByAnother(stored, incoming, owner) {
    return stored?.id === incoming.id && normalisedOwnerId(stored?.ownerUserId) !== owner;
}
