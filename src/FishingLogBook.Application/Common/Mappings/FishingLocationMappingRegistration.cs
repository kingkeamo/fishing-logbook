using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class FishingLocationMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<UserFishingLocationPreference, FishingLocationPreferenceDto>()
            .MapWith(source => new FishingLocationPreferenceDto(
                source.Id,
                source.Name,
                source.IsDefault));
    }
}
