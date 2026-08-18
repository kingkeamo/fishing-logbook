const dateTimeLocalPattern = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/;

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
    const offsetMinutes = timeZoneOffsetMinutes
        ?? new Date(year, monthIndex, day, hour, minute).getTimezoneOffset();
    const converted = new Date(Date.UTC(year, monthIndex, day, hour, minute) + (offsetMinutes * 60 * 1000));
    if (Number.isNaN(converted.getTime())) {
        return null;
    }

    return converted.toISOString();
}
