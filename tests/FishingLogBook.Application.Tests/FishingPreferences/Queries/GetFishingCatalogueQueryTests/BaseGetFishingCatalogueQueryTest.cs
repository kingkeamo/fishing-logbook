using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.FishingPreferences.Queries;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Queries.GetFishingCatalogueQueryTests;

public class BaseGetFishingCatalogueQueryTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    protected readonly IFishingPreferenceService MockFishingPreferenceService =
        Substitute.For<IFishingPreferenceService>();

    protected readonly GetFishingCatalogueHandler Sut;

    protected BaseGetFishingCatalogueQueryTest()
    {
        Sut = new GetFishingCatalogueHandler(MockFishingPreferenceService);
    }
}
