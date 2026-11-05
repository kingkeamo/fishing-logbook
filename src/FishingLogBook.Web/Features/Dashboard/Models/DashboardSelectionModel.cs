using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Dashboard.Models;

public sealed record DashboardSelectionModel(
    DashboardDatePresetEnum Preset,
    DateTime From,
    DateTime To,
    string? Species,
    string? Method,
    string? Venue)
{
    public static DashboardSelectionModel CreateDefault()
    {
        var year = DateTime.Today.Year;
        return new DashboardSelectionModel(
            DashboardDatePresetEnum.ThisYear,
            new DateTime(year, 1, 1),
            new DateTime(year, 12, 31),
            null,
            null,
            null);
    }
}
