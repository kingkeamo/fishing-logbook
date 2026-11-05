namespace FishingLogBook.Mobile.Features.Dashboard.Models;

public sealed record DashboardFilterOptionsModel(
    IReadOnlyList<string> Species,
    IReadOnlyList<string> Methods,
    IReadOnlyList<string> Venues);
