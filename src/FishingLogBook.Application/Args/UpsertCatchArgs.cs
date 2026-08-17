using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Args;

public sealed class UpsertCatchArgs
{
    public Guid UserId { get; init; }

    public CatchDto Catch { get; init; } = new(Guid.Empty, default, []);
}
