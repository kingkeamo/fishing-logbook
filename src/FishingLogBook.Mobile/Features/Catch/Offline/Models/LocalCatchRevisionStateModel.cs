namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class LocalCatchRevisionStateModel
{
    public required long Revision { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }
}
