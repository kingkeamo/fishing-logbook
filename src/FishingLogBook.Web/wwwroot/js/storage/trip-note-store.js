import {
    TRIP_STORE_NAME,
    normalisedOwnerId,
    runLogbookTransaction
} from './logbook-database.js';

export async function putTripNote(json) {
    const note = JSON.parse(json);
    const owner = normalisedOwnerId(note?.ownerUserId);
    if (!note?.id || !note?.tripId || !owner) {
        throw new Error('Owned Trip note id is required');
    }

    if (typeof note.text !== 'string' || note.text.trim().length === 0) {
        throw new Error('Trip note text is required');
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'write', (store, succeed, fail) => {
        const read = store.get(note.tripId);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const trip = read.result;
            if (!trip || normalisedOwnerId(trip.ownerUserId) !== owner) {
                fail(new Error('Trip note must belong to an owned Trip'));
                return;
            }

            trip.notes = withNote(trip.notes, note);
            const write = store.put(trip);
            write.onerror = () => fail(write.error);
            write.onsuccess = () => succeed(true);
        };
    });
}

export async function deleteTripNote(ownerUserId, tripId, noteId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || !tripId || !noteId) {
        return false;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'delete', (store, succeed, fail) => {
        const read = store.get(tripId);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const trip = read.result;
            if (!trip || normalisedOwnerId(trip.ownerUserId) !== owner) {
                succeed(false);
                return;
            }

            const existing = trip.notes ?? [];
            const remaining = existing.filter(entry => entry?.id !== noteId);
            if (remaining.length === existing.length) {
                succeed(false);
                return;
            }

            trip.notes = remaining;
            const write = store.put(trip);
            write.onerror = () => fail(write.error);
            write.onsuccess = () => succeed(true);
        };
    });
}

export async function getTripNotes(ownerUserId, tripId) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner || !tripId) {
        return [];
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'metadata-read', (store, succeed, fail) => {
        const read = store.get(tripId);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const trip = read.result;
            if (!trip || normalisedOwnerId(trip.ownerUserId) !== owner) {
                succeed([]);
                return;
            }

            succeed((trip.notes ?? [])
                .filter(Boolean)
                .sort(byRecordedOn)
                .map(note => ({ json: JSON.stringify(note) })));
        };
    });
}

export async function getTripNote(ownerUserId, tripId, noteId) {
    const notes = await getTripNotes(ownerUserId, tripId);
    return notes.find(entry => JSON.parse(entry.json).id === noteId) ?? null;
}

export async function getPendingTripNotes(ownerUserId) {
    return scanOwnedTrips(ownerUserId, (trip, collected) => {
        for (const note of trip.notes ?? []) {
            if (note && !isSynchronised(note.syncStatus)) {
                collected.push({ json: JSON.stringify(note) });
            }
        }
    });
}

export async function getTripsWithPendingNotes(ownerUserId) {
    return scanOwnedTrips(ownerUserId, (trip, collected) => {
        if ((trip.notes ?? []).some(note => note && !isSynchronised(note.syncStatus))) {
            collected.push(trip.id);
        }
    });
}

function scanOwnedTrips(ownerUserId, collect) {
    const owner = normalisedOwnerId(ownerUserId);
    if (!owner) {
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

            if (normalisedOwnerId(cursor.value?.ownerUserId) === owner) {
                collect(cursor.value, collected);
            }

            cursor.continue();
        };
    });
}

function withNote(existing, note) {
    const others = (existing ?? []).filter(entry => entry?.id !== note.id);
    others.push(note);
    return others.sort(byRecordedOn);
}

function byRecordedOn(first, second) {
    const firstOn = Date.parse(first?.recordedOn ?? '');
    const secondOn = Date.parse(second?.recordedOn ?? '');
    if (Number.isNaN(firstOn) || Number.isNaN(secondOn) || firstOn === secondOn) {
        return String(first?.id ?? '').localeCompare(String(second?.id ?? ''));
    }

    return firstOn - secondOn;
}

function isSynchronised(status) {
    return status === 'synchronised' || status === 3;
}
