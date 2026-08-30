import {
    TRIP_PHOTO_STORE_NAME,
    TRIP_STORE_NAME,
    canWriteTrip,
    contributorId,
    normalisedOwnerId,
    runLogbookMultiStoreTransaction,
    runLogbookTransaction
} from './logbook-database.js';

export { TRIP_PHOTO_STORE_NAME };

export async function putTripPhotograph(json, bytes) {
    const photograph = JSON.parse(json);
    const contributor = contributorId(photograph);
    if (!photograph?.id || !photograph?.tripId || !contributor) {
        throw new Error('Trip photograph contributor is required');
    }

    if (!(bytes instanceof Uint8Array) || bytes.length === 0) {
        throw new Error('Trip photograph bytes are required');
    }

    return runLogbookMultiStoreTransaction(
        [TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME],
        'readwrite',
        'write',
        (transaction, succeed, fail) => {
            const trips = transaction.objectStore(TRIP_STORE_NAME);
            const photographs = transaction.objectStore(TRIP_PHOTO_STORE_NAME);
            const read = trips.get(photograph.tripId);
            read.onerror = () => fail(read.error);
            read.onsuccess = () => {
                const trip = read.result;
                if (!canWriteTrip(trip, contributor)) {
                    fail(new Error('Trip photograph must belong to a writable Trip'));
                    return;
                }

                const write = photographs.put({ id: photograph.id, bytes });
                write.onerror = () => fail(write.error);
                write.onsuccess = () => {
                    trip.photographs = withPhotograph(trip.photographs, photograph);
                    const update = trips.put(trip);
                    update.onerror = () => fail(update.error);
                    update.onsuccess = () => succeed(true);
                };
            };
        });
}

export async function getTripPhotographBytes(viewerUserId, tripId, photographId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer || !tripId || !photographId) {
        return null;
    }

    return runLogbookMultiStoreTransaction(
        [TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME],
        'readonly',
        'photo-read',
        (transaction, succeed, fail) => {
            const read = transaction.objectStore(TRIP_STORE_NAME).get(tripId);
            read.onerror = () => fail(read.error);
            read.onsuccess = () => {
                const trip = read.result;
                if (!canWriteTrip(trip, viewer)
                    || !hasPhotograph(trip.photographs, photographId)) {
                    succeed(null);
                    return;
                }

                const bytes = transaction.objectStore(TRIP_PHOTO_STORE_NAME).get(photographId);
                bytes.onerror = () => fail(bytes.error);
                bytes.onsuccess = () => succeed(bytes.result?.bytes ?? null);
            };
        });
}

export async function deleteTripPhotograph(viewerUserId, tripId, photographId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer || !tripId || !photographId) {
        return false;
    }

    return runLogbookMultiStoreTransaction(
        [TRIP_STORE_NAME, TRIP_PHOTO_STORE_NAME],
        'readwrite',
        'delete',
        (transaction, succeed, fail) => {
            const trips = transaction.objectStore(TRIP_STORE_NAME);
            const read = trips.get(tripId);
            read.onerror = () => fail(read.error);
            read.onsuccess = () => {
                const trip = read.result;
                if (!canWriteTrip(trip, viewer)) {
                    succeed(false);
                    return;
                }

                const existing = trip.photographs ?? [];
                const remaining = existing.filter(entry =>
                    entry?.id !== photographId || contributorId(entry) !== viewer);
                const removed = remaining.length !== existing.length;
                if (!removed) {
                    succeed(false);
                    return;
                }

                const drop = transaction.objectStore(TRIP_PHOTO_STORE_NAME).delete(photographId);
                drop.onerror = () => fail(drop.error);
                drop.onsuccess = () => {
                    trip.photographs = remaining;
                    const update = trips.put(trip);
                    update.onerror = () => fail(update.error);
                    update.onsuccess = () => succeed(true);
                };
            };
        });
}

export async function getPendingTripPhotographs(viewerUserId) {
    return scanWritableTrips(viewerUserId, (trip, collected, viewer) => {
        for (const photograph of trip.photographs ?? []) {
            if (photograph
                && contributorId(photograph) === viewer
                && !isSynchronised(photograph.syncStatus)) {
                collected.push({ json: JSON.stringify(photograph) });
            }
        }
    });
}

export async function getTripsWithPendingPhotographs(viewerUserId) {
    return scanWritableTrips(viewerUserId, (trip, collected, viewer) => {
        const pending = (trip.photographs ?? []).some(photograph =>
            photograph
            && contributorId(photograph) === viewer
            && !isSynchronised(photograph.syncStatus)
            && !isFailedToSynchronise(photograph.syncStatus));
        if (pending) {
            collected.push(trip.id);
        }
    });
}

function scanWritableTrips(viewerUserId, collect) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer) {
        return Promise.resolve([]);
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'pending-read', (store, succeed, fail) => {
        const collected = [];
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                succeed(collected);
                return;
            }

            if (canWriteTrip(cursor.value, viewer)) {
                collect(cursor.value, collected, viewer);
            }

            cursor.continue();
        };
    });
}

function withPhotograph(existing, photograph) {
    const others = (existing ?? []).filter(entry => entry?.id !== photograph.id);
    others.push(photograph);
    return others.sort(byOrderedOn);
}

function hasPhotograph(existing, photographId) {
    return (existing ?? []).some(entry => entry?.id === photographId);
}

function byOrderedOn(first, second) {
    const firstOn = Date.parse(first?.capturedOn ?? first?.addedOn ?? '');
    const secondOn = Date.parse(second?.capturedOn ?? second?.addedOn ?? '');
    if (Number.isNaN(firstOn) || Number.isNaN(secondOn) || firstOn === secondOn) {
        return String(first?.id ?? '').localeCompare(String(second?.id ?? ''));
    }

    return firstOn - secondOn;
}

function isSynchronised(status) {
    return status === 'synchronised' || status === 3;
}

function isFailedToSynchronise(status) {
    return status === 'failedToSynchronise' || status === 4;
}
