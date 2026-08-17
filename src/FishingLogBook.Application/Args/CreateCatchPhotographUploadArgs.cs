using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Args;

public sealed class CreateCatchPhotographUploadArgs
{
    public Guid CatchId { get; init; }

    public PhotographUploadRequestDto Request { get; init; } = new(Guid.Empty, string.Empty);
}
