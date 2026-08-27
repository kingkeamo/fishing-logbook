using FishingLogBook.Application.FishingLocations.Commands;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Tests.FishingLocations.Commands.UpdateFishingLocationPreferencesCommandValidatorTests;

public class BaseUpdateFishingLocationPreferencesCommandValidatorTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected readonly UpdateFishingLocationPreferencesCommandValidator Sut = new();

    protected static UpdateFishingLocationPreferencesCommand Command(
        params UpdateFishingLocationPreferenceDto[] locations)
    {
        return new UpdateFishingLocationPreferencesCommand
        {
            UserId = OwnerUserId,
            Locations = new UpdateFishingLocationPreferencesDto(locations)
        };
    }

    protected static UpdateFishingLocationPreferenceDto Location(string name, bool isDefault = false)
    {
        return new UpdateFishingLocationPreferenceDto(Guid.Empty, name, isDefault);
    }
}
