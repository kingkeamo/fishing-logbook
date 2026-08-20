using System.Globalization;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class CatchDateGroupingService : ICatchDateGroupingService
{
    private readonly IStringLocalizer<UiStrings> _loc;

    public CatchDateGroupingService(IStringLocalizer<UiStrings> loc)
    {
        _loc = loc;
    }

    public string RelativeDayLabel(DateTime localDate, DateTime localToday)
    {
        var date = localDate.Date;
        var today = localToday.Date;
        if (date == today)
        {
            return _loc["Catch_DateGroupToday"];
        }

        if (date == today.AddDays(-1))
        {
            return _loc["Catch_DateGroupYesterday"];
        }

        return date.Year == today.Year
            ? date.ToString("d MMMM", CultureInfo.CurrentCulture)
            : date.ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
    }
}
