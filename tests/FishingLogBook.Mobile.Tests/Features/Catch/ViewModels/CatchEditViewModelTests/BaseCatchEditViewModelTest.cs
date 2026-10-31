using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Photographs.Mappers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Photographs.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Catches.Services;
using FishingLogBook.Shared.Measurements;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public abstract class BaseCatchEditViewModelTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly DateTimeOffset Now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected ICatchStore MockCatchStore { get; } = Substitute.For<ICatchStore>();
    protected IAccountSessionCoordinator MockSessionCoordinator { get; } = Substitute.For<IAccountSessionCoordinator>();
    protected IAnglerPreferencesService MockPreferences { get; } = Substitute.For<IAnglerPreferencesService>();
    protected IAccountUnitsService MockUnits { get; } = Substitute.For<IAccountUnitsService>();
    protected IDeviceLocationService MockLocation { get; } = Substitute.For<IDeviceLocationService>();
    protected IPhotographCollectionEditorService MockPhotographEditor { get; } = Substitute.For<IPhotographCollectionEditorService>();
    protected CatchEditViewModel Sut { get; }

    protected BaseCatchEditViewModelTest()
    {
        MockSessionCoordinator.Capture().Returns(new AccountSessionLeaseModel(OwnerUserId, 1, CancellationToken.None));
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns((FishingLogBook.Mobile.Features.Profile.Models.AnglerPreferencesModel?)null);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        Sut = CreateSut();
    }

    protected CatchEditViewModel CreateSut(
        IMeasurementService? measurement = null,
        TimeProvider? clock = null)
    {
        return new CatchEditViewModel(
            MockCatchStore,
            MockSessionCoordinator,
            MockPreferences,
            new CatchPhotographProposalService(),
            new CatchPhotographSaveArgsMapper(),
            Substitute.For<IPhotographFileStore>(),
            MockLocation,
            measurement ?? new MeasurementService(),
            MockUnits,
            new PhotographCollectionEditorViewModel(MockPhotographEditor, Text),
            clock ?? new FixedTimeProvider(Now),
            Text);
    }
}
