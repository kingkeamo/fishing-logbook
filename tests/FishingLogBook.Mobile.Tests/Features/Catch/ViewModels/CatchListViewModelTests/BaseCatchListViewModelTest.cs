using System.Globalization;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public abstract class BaseCatchListViewModelTest
{
    protected static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected FixedTimeProvider Clock { get; } = new(Now);
    protected IAccountUnitsService MockUnits { get; } = Substitute.For<IAccountUnitsService>();
    protected ICatchStore MockCatchStore { get; } = Substitute.For<ICatchStore>();
    protected IAccountSessionCoordinator MockSessionCoordinator { get; } = Substitute.For<IAccountSessionCoordinator>();
    protected ICatchSyncStatusStore MockSyncStatusStore { get; } = Substitute.For<ICatchSyncStatusStore>();
    protected IOutboxChangeNotifier MockNotifier { get; } = Substitute.For<IOutboxChangeNotifier>();
    protected CatchListViewModel Sut { get; }

    protected BaseCatchListViewModelTest()
    {
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        MockSessionCoordinator.Capture().Returns(new AccountSessionLeaseModel(OwnerUserId, 1, CancellationToken.None));
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var photographFileStore = Substitute.For<IPhotographFileStore>();
        Sut = CreateSut(Clock);
        Sut.LoadJournalEntries(new SampleCatchProvider(Text, Clock).Catches);
    }

    protected CatchListViewModel CreateSut(TimeProvider clock)
    {
        var mapper = new CatchPresentationMapper(Substitute.For<IPhotographFileStore>());
        var sut = new CatchListViewModel(
            Text,
            MockCatchStore,
            MockSyncStatusStore,
            MockNotifier,
            NullLogger<CatchListViewModel>.Instance,
            MockSessionCoordinator,
            mapper,
            MockUnits,
            new MeasurementService(),
            clock);
        sut.LoadJournalEntries(new SampleCatchProvider(Text, Clock).Catches);
        return sut;
    }
}
