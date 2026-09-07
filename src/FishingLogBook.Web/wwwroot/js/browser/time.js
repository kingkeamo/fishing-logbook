const dateTimeLocalPattern = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/;

function pad(value) {
    return String(value).padStart(2, '0');
}

function localPartsFromUtc(utcMillis, offsetMinutes) {
    const local = new Date(utcMillis - (offsetMinutes * 60 * 1000));
    return {
        year: local.getUTCFullYear(),
        month: local.getUTCMonth() + 1,
        day: local.getUTCDate(),
        hour: local.getUTCHours(),
        minute: local.getUTCMinutes()
    };
}

export function toDateTimeLocalValue(utcIso, timeZoneOffsetMinutes) {
    const date = new Date(utcIso);
    if (Number.isNaN(date.getTime())) {
        return '';
    }

    const offsetMinutes = timeZoneOffsetMinutes ?? date.getTimezoneOffset();
    const local = localPartsFromUtc(date.getTime(), offsetMinutes);
    return `${local.year}-${pad(local.month)}-${pad(local.day)}T${pad(local.hour)}:${pad(local.minute)}`;
}

export function fromDateTimeLocalValue(localValue, timeZoneOffsetMinutes) {
    if (typeof localValue !== 'string') {
        return null;
    }

    const match = dateTimeLocalPattern.exec(localValue.trim());
    if (!match) {
        return null;
    }

    const year = Number(match[1]);
    const monthIndex = Number(match[2]) - 1;
    const day = Number(match[3]);
    const hour = Number(match[4]);
    const minute = Number(match[5]);
    const second = match[6] ? Number(match[6]) : 0;
    if (typeof timeZoneOffsetMinutes === 'string') {
        return fromWallClockInTimeZone(year, monthIndex, day, hour, minute, second, timeZoneOffsetMinutes);
    }

    const offsetMinutes = timeZoneOffsetMinutes
        ?? historicalOffsetMinutes(year, monthIndex, day, hour, minute, second);
    const converted = new Date(
        Date.UTC(year, monthIndex, day, hour, minute, second) + (offsetMinutes * 60 * 1000));
    if (Number.isNaN(converted.getTime())) {
        return null;
    }

    return converted.toISOString();
}

function historicalOffsetMinutes(year, monthIndex, day, hour, minute, second) {
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!timeZone) {
        return new Date(year, monthIndex, day, hour, minute, second).getTimezoneOffset();
    }

    const instant = fromWallClockInTimeZone(year, monthIndex, day, hour, minute, second, timeZone);
    return (Date.parse(instant) - Date.UTC(year, monthIndex, day, hour, minute, second)) / 60000;
}

function fromWallClockInTimeZone(year, monthIndex, day, hour, minute, second, timeZone) {
    const wallClockMillis = Date.UTC(year, monthIndex, day, hour, minute, second);
    let candidate = wallClockMillis;
    for (let attempt = 0; attempt < 2; attempt++) {
        const parts = new Intl.DateTimeFormat('en-CA', {
            timeZone,
            year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', second: '2-digit',
            hourCycle: 'h23'
        }).formatToParts(new Date(candidate));
        const values = Object.fromEntries(parts.map(part => [part.type, part.value]));
        const representedWallClock = Date.UTC(
            Number(values.year), Number(values.month) - 1, Number(values.day),
            Number(values.hour), Number(values.minute), Number(values.second));
        candidate = wallClockMillis - (representedWallClock - candidate);
    }

    return new Date(candidate).toISOString();
}
