namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportTripSuggestionPolicyModel(
    double NearbyDistanceKilometres,
    double DistantVetoKilometres,
    TimeSpan MaximumAdjacentGap,
    TimeSpan MaximumTripSpan)
{
    public static ImportTripSuggestionPolicyModel Default { get; } = new(
        5d,
        25d,
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(18));
}
