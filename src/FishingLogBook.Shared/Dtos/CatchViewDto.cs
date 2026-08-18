namespace FishingLogBook.Shared.Dtos;

public sealed record CatchViewDto(
    Guid Id,
    Guid UserId,
    DateTimeOffset CaughtOn,
    CatchLocationExposureDto? Location = null)
{
    public Guid AnglerUserId { get; init; }

    public Guid RecordedByUserId { get; init; }
}
