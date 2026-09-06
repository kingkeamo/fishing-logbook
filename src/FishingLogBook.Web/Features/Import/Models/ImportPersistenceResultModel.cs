using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportPersistenceResultModel(
    IReadOnlyList<Guid> CreatedTripIds,
    IReadOnlyList<Guid> CatchIds,
    int PhotographCount,
    int ParticipantCount,
    ImportPersistenceFailureEnum? Failure = null,
    Exception? FailureException = null)
{
    public bool IsSuccess => Failure is null;

    public static ImportPersistenceResultModel Failed(
        ImportPersistenceFailureEnum failure,
        Exception exception)
    {
        return new ImportPersistenceResultModel([], [], 0, 0, failure, exception);
    }
}
