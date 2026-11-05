using System.Globalization;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Dashboard.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardFilterViewModelTests;

public abstract class BaseDashboardFilterViewModelTest
{
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly DashboardFilterOptionsModel Options = new(["Pike", "Perch"], ["Fly", "Lure"], ["Lake"]);
    protected readonly DashboardFilterSelectionModel Applied = new()
    {
        From = new DateTime(2026, 1, 1),
        To = new DateTime(2026, 12, 31)
    };
    protected readonly DashboardFilterViewModel Sut;

    protected BaseDashboardFilterViewModelTest()
    {
        Sut = new DashboardFilterViewModel(Text, new FixedTimeProvider(DateTimeOffset.Parse("2026-11-05T08:00Z"), TimeZoneInfo.Utc));
        Sut.Initialize(Applied, Options);
    }
}
