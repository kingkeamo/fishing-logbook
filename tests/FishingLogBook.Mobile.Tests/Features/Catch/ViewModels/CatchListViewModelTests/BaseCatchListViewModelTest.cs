using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public abstract class BaseCatchListViewModelTest
{
    protected static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected FixedTimeProvider Clock { get; } = new(Now);
    protected CatchListViewModel Sut { get; }

    protected BaseCatchListViewModelTest()
    {
        Sut = new CatchListViewModel(Text, new SampleCatchProvider(Text, Clock), Clock);
    }
}
