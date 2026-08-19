using FishingLogBook.Application.FishingPreferences.Commands;

namespace FishingLogBook.Application.Tests.FishingPreferences.Commands.UpdateFishingPreferencesCommandValidatorTests;

public class BaseUpdateFishingPreferencesCommandValidatorTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    protected readonly UpdateFishingPreferencesCommandValidator Sut = new();
}
