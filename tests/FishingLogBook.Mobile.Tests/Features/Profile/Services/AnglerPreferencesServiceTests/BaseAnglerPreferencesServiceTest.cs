using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AnglerPreferencesServiceTests;

public abstract class BaseAnglerPreferencesServiceTest : IDisposable
{
    protected static readonly Guid FirstUserId = Guid.Parse("5b1f0c6a-2d3e-4f50-8a71-9c2b3d4e5f60");
    protected static readonly Guid SecondUserId = Guid.Parse("e7d6c5b4-a392-4180-9f7e-6d5c4b3a2918");
    protected readonly IFishingPreferenceClient MockPreferenceClient = Substitute.For<IFishingPreferenceClient>();
    protected readonly IFishingLocationClient MockLocationClient = Substitute.For<IFishingLocationClient>();
    protected readonly IAnglerPreferencesStore MockStore = Substitute.For<IAnglerPreferencesStore>();
    protected readonly AccountSessionCoordinator SessionCoordinator = new();
    protected readonly CapturingLogger<AnglerPreferencesService> Logger = new();
    protected readonly FishingCatalogueDto Catalogue = TestAnglerPreferences.Catalogue();
    protected readonly FishingPreferencesDto Preferences = TestAnglerPreferences.Preferences();
    protected readonly FishingLocationPreferencesDto Locations = TestAnglerPreferences.Locations();
    protected readonly AnglerPreferencesService Sut;

    protected BaseAnglerPreferencesServiceTest()
    {
        Sut = new AnglerPreferencesService(
            MockPreferenceClient,
            MockLocationClient,
            MockStore,
            SessionCoordinator,
            Logger);
    }

    public void Dispose()
    {
        SessionCoordinator.Dispose();
        GC.SuppressFinalize(this);
    }

    protected Task SignInAsync(Guid userId)
    {
        return SessionCoordinator.TransitionAsync(userId, CancellationToken.None);
    }

    protected void ApiReturns(
        ApiOutcomeEnum catalogue = ApiOutcomeEnum.Success,
        ApiOutcomeEnum preferences = ApiOutcomeEnum.Success,
        ApiOutcomeEnum locations = ApiOutcomeEnum.Success)
    {
        MockPreferenceClient.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(Result(catalogue, Catalogue));
        MockPreferenceClient.GetPreferencesAsync(Arg.Any<CancellationToken>()).Returns(Result(preferences, Preferences));
        MockLocationClient.GetAsync(Arg.Any<CancellationToken>()).Returns(Result(locations, Locations));
    }

    private static ApiResultModel<T> Result<T>(ApiOutcomeEnum outcome, T value)
        where T : class
    {
        return outcome == ApiOutcomeEnum.Success
            ? ApiResultModel<T>.Succeeded(value)
            : ApiResultModel<T>.Failed(outcome);
    }
}
