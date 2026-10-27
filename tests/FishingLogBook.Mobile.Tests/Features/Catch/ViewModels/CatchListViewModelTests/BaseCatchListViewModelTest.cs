using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Measurements;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public abstract class BaseCatchListViewModelTest
{
    protected static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected FixedTimeProvider Clock { get; } = new(Now);
    protected IAccountUnitsService MockUnits { get; } = Substitute.For<IAccountUnitsService>();
    protected CatchListViewModel Sut { get; }

    protected BaseCatchListViewModelTest()
    {
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        Sut = CreateSut(Clock);
    }

    protected CatchListViewModel CreateSut(TimeProvider clock)
    {
        return new CatchListViewModel(
            Text,
            new SampleCatchProvider(Text, Clock),
            MockUnits,
            new MeasurementService(),
            clock);
    }
}
