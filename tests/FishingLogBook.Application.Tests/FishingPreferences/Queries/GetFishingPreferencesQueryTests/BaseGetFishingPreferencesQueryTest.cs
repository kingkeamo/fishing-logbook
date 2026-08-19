using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.FishingPreferences.Queries;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Queries.GetFishingPreferencesQueryTests;

public class BaseGetFishingPreferencesQueryTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    protected readonly IFishingPreferenceService MockFishingPreferenceService =
        Substitute.For<IFishingPreferenceService>();

    protected readonly GetFishingPreferencesHandler Sut;

    protected BaseGetFishingPreferencesQueryTest()
    {
        Sut = new GetFishingPreferencesHandler(MockFishingPreferenceService);
    }
}
