using FishingLogBook.Mobile.Features.Catch.Persistence.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalCatchSaveArgs
{
    public required Guid Id { get; init; }

    public Guid? TripId { get; init; }

    public string? TripTitle { get; init; }

    public Guid? CaughtByUserId { get; init; }

    public Guid? RecordedByUserId { get; init; }

    public required DateTimeOffset CaughtOn { get; init; }

    public string? SpeciesName { get; init; }

    public double? Weight { get; init; }

    public double? Length { get; init; }

    public string? Method { get; init; }

    public string? BaitOrLure { get; init; }

    public string? Notes { get; init; }

    public string? PlaceName { get; init; }

    public LocalCatchLocationModel? Location { get; init; }

    public required LocalCatchLifecycleEnum Lifecycle { get; init; }

    public IReadOnlyList<LocalCatchPhotographSaveArgs> Photographs { get; init; } =
        Array.Empty<LocalCatchPhotographSaveArgs>();
}
