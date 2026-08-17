namespace FishingLogBook.Shared.Dtos;

public sealed record CatchViewDto(
    Guid Id,
    Guid UserId,
    DateTimeOffset CaughtOn,
    CatchLocationExposureDto? Location = null);
