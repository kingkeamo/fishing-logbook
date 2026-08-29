import {
    TRIP_LOCAL_ORIGIN,
    TRIP_PHOTO_STORE_NAME,
    TRIP_SERVER_ORIGIN,
    TRIP_STORE_NAME,
    canWriteTrip,
    isLocalOriginTrip,
    isTripOwner,
    normalisedOwnerId,
    runLogbookMultiStoreTransaction,
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
        let stored = null;
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

                if (cursor.value?.id === trip.id) {
                    stored = cursor.value;
                }

                cursor.continue();
                return;
            }

            if (stored && !isLocalOriginTrip(stored)) {
                fail(new Error('A shared Trip cannot be written as a locally owned Trip'));
                return;
            }

            trip.photographs = stored?.photographs ?? trip.photographs ?? [];
            trip.notes = stored?.notes ?? trip.notes ?? [];
            trip.participantUserIds = trip.participantUserIds ?? stored?.participantUserIds ?? [];
            trip.origin = TRIP_LOCAL_ORIGIN;
            const write = store.put(trip);
            write.onerror = () => fail(write.error);
            write.onsuccess = () => succeed(TRIP_SAVED_OUTCOME);
        };
    });
}

export async function hydrateTrip(json, viewerUserId) {
    const trip = JSON.parse(json);
    const viewer = normalisedOwnerId(viewerUserId);
    if (!trip?.id || !normalisedOwnerId(trip.ownerUserId) || !viewer) {
        throw new Error('Shared Trip id and viewer are required');
    }

    if (!canWriteTrip(trip, viewer)) {
        throw new Error('Shared Trip is not writable by this angler');
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'write', (store, succeed, fail) => {
        const read = store.get(trip.id);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const stored = read.result;
            if (stored && normalisedOwnerId(stored.ownerUserId) !== normalisedOwnerId(trip.ownerUserId)) {
                fail(new Error('Trip ownership cannot be changed'));
                return;
            }

            if (stored && isLocalOriginTrip(stored) && isTripOwner(stored, viewer)) {
                succeed(TRIP_SAVED_OUTCOME);
                return;
            }

            trip.origin = TRIP_SERVER_ORIGIN;
            trip.notes = mergePending(stored?.notes, trip.notes);
            trip.photographs = mergePending(stored?.photographs, trip.photographs);
            const write = store.put(trip);
            write.onerror = () => fail(write.error);
            write.onsuccess = () => succeed(TRIP_SAVED_OUTCOME);
        };
    });
}

export async function getTrips(viewerUserId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer) {
        return [];
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'metadata-read', (store, succeed, fail) => {
        const trips = [];
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (cursor) {
                if (canWriteTrip(cursor.value, viewer)) {
                    trips.push({ json: JSON.stringify(cursor.value) });
                }

                cursor.continue();
                return;
            }

            succeed(trips);
        };
    });
}

export async function getTrip(viewerUserId, tripId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer || typeof tripId !== 'string' || !tripId) {
        return null;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'single-read', (store, succeed, fail) => {
        const request = store.get(tripId);
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const trip = request.result;
            if (!canWriteTrip(trip, viewer)) {
                succeed(null);
                return;
            }

            succeed({ json: JSON.stringify(trip) });
        };
    });
}

export async function getActiveTrip(viewerUserId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer) {
        return null;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'active-read', (store, succeed, fail) => {
        let owned = null;
        let shared = null;
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                const active = owned ?? shared;
                succeed(active ? { json: JSON.stringify(active) } : null);
                return;
            }

            if (isActiveForOwner(cursor.value, viewer)) {
                owned = mostRecentlyStarted(owned, cursor.value);
            } else if (isActiveForContributor(cursor.value, viewer)) {
                shared = mostRecentlyStarted(shared, cursor.value);
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
                if (isTripOwner(cursor.value, owner)
                    && isLocalOriginTrip(cursor.value)
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

export async function cleanupSyncedTrips(viewerUserId, olderThanIso, retainedTripIds) {
    const viewer = normalisedOwnerId(viewerUserId);
    const cutoff = Date.parse(olderThanIso);
    if (!viewer || Number.isNaN(cutoff)) {
        return 0;
    }

    const retained = new Set((retainedTripIds ?? [])
        .map(tripId => normalisedOwnerId(tripId))
        .filter(Boolean));

    return runLogbookMultiStoreTransaction(
        [TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME],
        'readwrite',
        'cleanup',
        (transaction, succeed, fail) => {
            const photographs = transaction.objectStore(TRIP_PHOTO_STORE_NAME);
            let removed = 0;
            const request = transaction.objectStore(TRIP_STORE_NAME).openCursor();
            request.onerror = () => fail(request.error);
            request.onsuccess = () => {
                const cursor = request.result;
                if (!cursor) {
                    succeed(removed);
                    return;
                }

                if (canWriteTrip(cursor.value, viewer)
                    && !retained.has(normalisedOwnerId(cursor.value?.id))
                    && isEligibleForCleanup(cursor.value, cutoff)) {
                    for (const photograph of cursor.value.photographs ?? []) {
                        if (photograph?.id) {
                            const drop = photographs.delete(photograph.id);
                            drop.onerror = () => fail(drop.error);
                        }
                    }

                    cursor.delete();
                    removed += 1;
                }

                cursor.continue();
            };
        });
}

function mergePending(stored, incoming) {
    const pending = (stored ?? []).filter(entry => entry && !isSynchronised(entry.syncStatus));
    const pendingIds = new Set(pending.map(entry => entry.id));
    const server = (incoming ?? []).filter(entry => entry && !pendingIds.has(entry.id));
    return [...server, ...pending];
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
    return isTripOwner(trip, owner)
        && isLocalOriginTrip(trip)
        && trip?.status === TRIP_ACTIVE_STATUS;
}

function isActiveForContributor(trip, viewer) {
    return canWriteTrip(trip, viewer) && trip?.status === TRIP_ACTIVE_STATUS;
}

function mostRecentlyStarted(current, candidate) {
    if (!current) {
        return candidate;
    }

    const currentOn = Date.parse(current.startedOn ?? '');
    const candidateOn = Date.parse(candidate.startedOn ?? '');
    if (Number.isNaN(currentOn) || Number.isNaN(candidateOn) || currentOn === candidateOn) {
        return String(candidate.id ?? '') > String(current.id ?? '') ? candidate : current;
    }

    return candidateOn > currentOn ? candidate : current;
}

function conflictsWithActiveTrip(stored, incoming, owner) {
    return incoming.status === TRIP_ACTIVE_STATUS
        && stored?.id !== incoming.id
        && isActiveForOwner(stored, owner);
}

function isOwnedByAnother(stored, incoming, owner) {
    return stored?.id === incoming.id && normalisedOwnerId(stored?.ownerUserId) !== owner;
}
