using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Features.Profile.Providers;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.AnglerPreferencesProviderTests;

public class BaseAnglerPreferencesProviderTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    protected readonly IProfileClient MockProfileClient = Substitute.For<IProfileClient>();
    protected readonly IFishingPreferenceClient MockFishingPreferenceClient =
        Substitute.For<IFishingPreferenceClient>();
    protected readonly IFishingLocationClient MockFishingLocationClient =
        Substitute.For<IFishingLocationClient>();
    protected readonly IAnglerPreferencesStore MockCache = Substitute.For<IAnglerPreferencesStore>();
    protected readonly ILocalCatchOwnerService MockLocalCatchOwner = Substitute.For<ILocalCatchOwnerService>();
    protected readonly AnglerPreferencesProvider Sut;

    protected BaseAnglerPreferencesProviderTest()
    {
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        MockFishingLocationClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new FishingLocationPreferencesDto([]));
        Sut = new AnglerPreferencesProvider(
            MockProfileClient,
            MockFishingPreferenceClient,
            MockFishingLocationClient,
            MockCache,
            MockLocalCatchOwner);
    }

    protected static FishingCatalogueDto SampleCatalogue()
    {
        return new FishingCatalogueDto(
            [new FishingMethodDto(FlyMethodId, "Fly", "Fly")],
            [new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout")]);
    }

    protected static FishingPreferencesDto SamplePreferences()
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)])
        ]);
    }

    protected static AnglerPreferencesModel CachedPreferences()
    {
        return new AnglerPreferencesModel(
            SampleCatalogue(),
            SamplePreferences(),
            WeightUnitEnum.Lb,
            LengthUnitEnum.In);
    }


    protected static AnglerPreferencesModel SavedPreferences()
    {
        return new AnglerPreferencesModel(
            SampleCatalogue(),
            new FishingPreferencesDto(
            [
                new FishingMethodPreferenceDto(SpinningMethodId, "Spinning", "Spinning", true, [])
            ]),
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm);
    }


    protected static ProfileDto OnlineProfile(
        Guid userId,
        WeightUnitEnum weightUnit,
        LengthUnitEnum lengthUnit)
    {
        return new ProfileDto(
            userId,
            null,
            null,
            null,
            null,
            null,
            true,
            false,
            false,
            false,
            false,
            weightUnit,
            lengthUnit);
    }

    protected void GivenOnlineProfile(
        WeightUnitEnum weightUnit = WeightUnitEnum.Lb,
        LengthUnitEnum lengthUnit = LengthUnitEnum.In)
    {
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(OnlineProfile(OwnerUserId, weightUnit, lengthUnit));
        MockFishingPreferenceClient.GetCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(SampleCatalogue());
        MockFishingPreferenceClient.GetPreferencesAsync(Arg.Any<CancellationToken>())
            .Returns(SamplePreferences());
    }
}
