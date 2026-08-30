using FishingLogBook.Application.FishingLocations.Commands;
using FishingLogBook.Application.FishingLocations.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Commands.UpdateFishingLocationPreferencesCommandTests;

public class BaseUpdateFishingLocationPreferencesCommandTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid CorribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    protected readonly IFishingLocationPreferenceService MockFishingLocationPreferenceService =
        Substitute.For<IFishingLocationPreferenceService>();

    protected readonly UpdateFishingLocationPreferencesHandler Sut;

    protected BaseUpdateFishingLocationPreferencesCommandTest()
    {
        Sut = new UpdateFishingLocationPreferencesHandler(MockFishingLocationPreferenceService);
    }
}
