using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class FishingPreferenceMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<FishingMethod, FishingMethodDto>();
        config.NewConfig<Species, SpeciesDto>();
    }
}
