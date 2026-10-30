using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class LocalCatchRecordModel
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public Guid? TripId { get; init; }

    public required Guid CaughtByUserId { get; init; }

    public required Guid RecordedByUserId { get; init; }

    public required DateTimeOffset CaughtOn { get; init; }

    public string? SpeciesName { get; init; }

    public double? Weight { get; init; }

    public double? Length { get; init; }

    public string? Method { get; init; }

    public string? BaitOrLure { get; init; }

    public string? Notes { get; init; }

    public string? PlaceName { get; init; }

    public LocalCatchLocationModel? Location { get; init; }

    public required long Revision { get; init; }

    public required LocalCatchLifecycleEnum Lifecycle { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }

    public required DateTimeOffset UpdatedOn { get; init; }

    public required IReadOnlyList<LocalCatchPhotographModel> Photographs { get; init; }
}
