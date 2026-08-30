import {
    TRIP_STORE_NAME,
    canWriteTrip,
    contributorId,
    normalisedOwnerId,
    runLogbookTransaction
} from './logbook-database.js';

export async function putTripNote(json) {
    const note = JSON.parse(json);
    const author = contributorId(note);
    if (!note?.id || !note?.tripId || !author) {
        throw new Error('Trip note author is required');
    }

    if (typeof note.text !== 'string' || note.text.trim().length === 0) {
        throw new Error('Trip note text is required');
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'write', (store, succeed, fail) => {
        const read = store.get(note.tripId);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const trip = read.result;
            if (!canWriteTrip(trip, author)) {
                fail(new Error('Trip note must belong to a writable Trip'));
                return;
            }

            trip.notes = withNote(trip.notes, note);
            const write = store.put(trip);
            write.onerror = () => fail(write.error);
            write.onsuccess = () => succeed(true);
        };
    });
}

export async function deleteTripNote(viewerUserId, tripId, noteId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer || !tripId || !noteId) {
        return false;
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readwrite', 'delete', (store, succeed, fail) => {
        const read = store.get(tripId);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const trip = read.result;
            if (!canWriteTrip(trip, viewer)) {
                succeed(false);
                return;
            }

            const existing = trip.notes ?? [];
            const remaining = existing.filter(entry =>
                entry?.id !== noteId || contributorId(entry) !== viewer);
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

export async function getTripNotes(viewerUserId, tripId) {
    const viewer = normalisedOwnerId(viewerUserId);
    if (!viewer || !tripId) {
        return [];
    }

    return runLogbookTransaction(TRIP_STORE_NAME, 'readonly', 'metadata-read', (store, succeed, fail) => {
        const read = store.get(tripId);
        read.onerror = () => fail(read.error);
        read.onsuccess = () => {
            const trip = read.result;
            if (!canWriteTrip(trip, viewer)) {
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

export async function getTripNote(viewerUserId, tripId, noteId) {
    const notes = await getTripNotes(viewerUserId, tripId);
    return notes.find(entry => JSON.parse(entry.json).id === noteId) ?? null;
}

export async function getPendingTripNotes(viewerUserId) {
    return scanWritableTrips(viewerUserId, (trip, collected, viewer) => {
        for (const note of trip.notes ?? []) {
            if (note && contributorId(note) === viewer && !isSynchronised(note.syncStatus)) {
                collected.push({ json: JSON.stringify(note) });
            }
        }
    });
}

export async function getTripsWithPendingNotes(viewerUserId) {
    return scanWritableTrips(viewerUserId, (trip, collected, viewer) => {
        const pending = (trip.notes ?? []).some(note =>
            note
            && contributorId(note) === viewer
            && !isSynchronised(note.syncStatus)
            && !isFailedToSynchronise(note.syncStatus));
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

function isFailedToSynchronise(status) {
    return status === 'failedToSynchronise' || status === 4;
}
