using FishingLogBook.Application.FishingPreferences.Commands;
using FishingLogBook.Application.FishingPreferences.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Commands.UpdateFishingPreferencesCommandTests;

public class BaseUpdateFishingPreferencesCommandTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    protected readonly IFishingPreferenceService MockFishingPreferenceService =
        Substitute.For<IFishingPreferenceService>();

    protected readonly UpdateFishingPreferencesHandler Sut;

    protected BaseUpdateFishingPreferencesCommandTest()
    {
        Sut = new UpdateFishingPreferencesHandler(MockFishingPreferenceService);
    }
}
