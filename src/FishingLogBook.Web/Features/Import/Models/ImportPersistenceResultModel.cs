namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportPersistenceResultModel(
    IReadOnlyList<Guid> CreatedTripIds,
    IReadOnlyList<Guid> CatchIds,
    int PhotographCount,
    int ParticipantCount);
