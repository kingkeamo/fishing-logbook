namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalCatchRevisionStateModel
{
    public required long Revision { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
