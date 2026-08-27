namespace FishingLogBook.Shared.Dtos;

public sealed record RecordTripPhotographDto(
    Guid PhotographId,
    string ObjectKey,
    string ContentType,
    DateTimeOffset AddedOn,
    DateTimeOffset? CapturedOn = null);
