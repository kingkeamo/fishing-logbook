import {
    TRIP_PHOTO_STORE_NAME,
    TRIP_STORE_NAME,
    normalisedOwnerId,
    runLogbookMultiStoreTransaction,
    runLogbookTransaction
} from './logbook-database.js';

export { TRIP_PHOTO_STORE_NAME };

export async function putTripPhotograph(json, bytes) {
    const photograph = JSON.parse(json);
    const owner = normalisedOwnerId(photograph?.ownerUserId);
    if (!photograph?.id || !photograph?.tripId || !owner) {
        throw new Error('Owned Trip photograph id is required');
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
                if (!trip || normalisedOwnerId(trip.ownerUserId) !== owner) {
                    fail(new Error('Trip photograph must belong to an owned Trip'));
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

export async function getTripPhotographBytes(ownerUserId, tripId, photographId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || !tripId || !photographId) {
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
                if (!trip
                    || normalisedOwnerId(trip.ownerUserId) !== owner
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

export async function deleteTripPhotograph(ownerUserId, tripId, photographId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || !tripId || !photographId) {
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
                if (!trip || normalisedOwnerId(trip.ownerUserId) !== owner) {
                    succeed(false);
                    return;
                }

                const remaining = (trip.photographs ?? [])
                    .filter(entry => entry?.id !== photographId);
                const removed = remaining.length !== (trip.photographs ?? []).length;
                const drop = transaction.objectStore(TRIP_PHOTO_STORE_NAME).delete(photographId);
                drop.onerror = () => fail(drop.error);
                drop.onsuccess = () => {
                    trip.photographs = remaining;
                    const update = trips.put(trip);
                    update.onerror = () => fail(update.error);
                    update.onsuccess = () => succeed(removed);
                };
            };
        });
}

export async function getPendingTripPhotographs(ownerUserId) {
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
            if (!cursor) {
                succeed(pending);
                return;
            }

            const trip = cursor.value;
            if (normalisedOwnerId(trip?.ownerUserId) === owner) {
                for (const photograph of trip.photographs ?? []) {
                    if (photograph && !isSynchronised(photograph.syncStatus)) {
                        pending.push({ json: JSON.stringify(photograph) });
                    }
                }
            }

            cursor.continue();
        };
    });
}

export async function getTripsWithPendingPhotographs(ownerUserId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
        return [];
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'pending-read', (store, succeed, fail) => {
        const tripIds = [];
        const request = store.openCursor();
        request.onerror = () => fail(request.error);
        request.onsuccess = () => {
            const cursor = request.result;
            if (!cursor) {
                succeed(tripIds);
                return;
            }

            const trip = cursor.value;
            if (normalisedOwnerId(trip?.ownerUserId) === owner
                && (trip.photographs ?? []).some(
                    photograph => photograph && !isSynchronised(photograph.syncStatus))) {
                tripIds.push(trip.id);
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
