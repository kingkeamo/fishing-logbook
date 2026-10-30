namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalCatchSaveResultModel
{
    public required Guid CatchId { get; init; }

    public required long Revision { get; init; }
}
