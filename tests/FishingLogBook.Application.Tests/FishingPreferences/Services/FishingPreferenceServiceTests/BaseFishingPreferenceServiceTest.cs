using FishingLogBook.Application.FishingPreferences.Contracts.Repositories;
using FishingLogBook.Application.FishingPreferences.Services;
using FishingLogBook.Domain.Catalogue;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Services.FishingPreferenceServiceTests;

public class BaseFishingPreferenceServiceTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly Guid UnknownId = Guid.Parse("dddddddd-0000-0000-0000-000000000009");

    protected readonly IFishingCatalogueRepository MockFishingCatalogueRepository =
        Substitute.For<IFishingCatalogueRepository>();

    protected readonly IFishingPreferenceRepository MockFishingPreferenceRepository =
        Substitute.For<IFishingPreferenceRepository>();

    protected readonly FishingPreferenceService Sut;

    protected BaseFishingPreferenceServiceTest()
    {
        Sut = new FishingPreferenceService(
            MockFishingCatalogueRepository,
            MockFishingPreferenceRepository,
            TestMapper.Create());
    }

    protected static IReadOnlyList<FishingMethod> CatalogueMethods()
    {
        return
        [
            new FishingMethod { Id = FlyMethodId, Code = "Fly", Name = "Fly" },
            new FishingMethod { Id = SpinningMethodId, Code = "Spinning", Name = "Spinning" }
        ];
    }

    protected static IReadOnlyList<Species> CatalogueSpecies()
    {
        return
        [
            new Species { Id = BrownTroutSpeciesId, Code = "BrownTrout", Name = "Brown Trout" },
            new Species { Id = PikeSpeciesId, Code = "Pike", Name = "Pike" }
        ];
    }
}
