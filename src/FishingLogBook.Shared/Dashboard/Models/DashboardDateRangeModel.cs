using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Dashboard.Models;

public sealed record DashboardDateRangeModel(DateOnly? From, DateOnly? To)
{
    public static DashboardDateRangeModel Resolve(
        DashboardDatePresetEnum preset, DateOnly today, DateOnly customFrom, DateOnly customTo)
    {
        return preset switch
        {
            DashboardDatePresetEnum.ThisMonth => new(
                new DateOnly(today.Year, today.Month, 1),
                new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
            DashboardDatePresetEnum.ThisYear => new(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            DashboardDatePresetEnum.LastYear => new(new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)),
            DashboardDatePresetEnum.AllTime => new(null, null),
            DashboardDatePresetEnum.Custom => new(customFrom, customTo),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
    }
}
