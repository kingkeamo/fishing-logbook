using FishingLogBook.Application.FishingLocations.Contracts.Services;
using FishingLogBook.Application.FishingLocations.Queries;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Queries.GetFishingLocationPreferencesQueryTests;

public class BaseGetFishingLocationPreferencesQueryTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid CorribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    protected readonly IFishingLocationPreferenceService MockFishingLocationPreferenceService =
        Substitute.For<IFishingLocationPreferenceService>();

    protected readonly GetFishingLocationPreferencesHandler Sut;

    protected BaseGetFishingLocationPreferencesQueryTest()
    {
        Sut = new GetFishingLocationPreferencesHandler(MockFishingLocationPreferenceService);
    }
}
