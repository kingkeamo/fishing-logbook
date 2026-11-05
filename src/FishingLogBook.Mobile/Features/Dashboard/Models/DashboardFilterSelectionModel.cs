using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Dashboard.Models;

public sealed record DashboardFilterSelectionModel
{
    public DashboardDatePresetEnum Preset { get; init; } = DashboardDatePresetEnum.ThisYear;
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string? Species { get; init; }
    public string? Method { get; init; }
    public string? Venue { get; init; }
}
