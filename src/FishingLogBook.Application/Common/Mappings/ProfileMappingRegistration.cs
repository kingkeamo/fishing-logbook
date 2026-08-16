using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class ProfileMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<UpdateOwnProfileCommand, UpdateProfileArgs>()
            .Map(dest => dest.UserId, src => src.UserId)
            .Map(dest => dest.DisplayName, src => src.Profile.DisplayName)
            .Map(dest => dest.HomeRegion, src => src.Profile.HomeRegion)
            .Map(dest => dest.PreferredFishingTypes, src => src.Profile.PreferredFishingTypes)
            .Map(dest => dest.PreferredSpecies, src => src.Profile.PreferredSpecies)
            .Map(dest => dest.ShowDisplayName, src => src.Profile.ShowDisplayName)
            .Map(dest => dest.ShowPhotograph, src => src.Profile.ShowPhotograph)
            .Map(dest => dest.ShowHomeRegion, src => src.Profile.ShowHomeRegion)
            .Map(dest => dest.ShowPreferredFishingTypes, src => src.Profile.ShowPreferredFishingTypes)
            .Map(dest => dest.ShowPreferredSpecies, src => src.Profile.ShowPreferredSpecies)
            .Map(dest => dest.Location, src => src.Profile.Location);

        config.NewConfig<RecordProfilePhotographCommand, RecordProfilePhotographArgs>()
            .Map(dest => dest.UserId, src => src.UserId)
            .Map(dest => dest.PhotographId, src => src.Photograph.PhotographId)
            .Map(dest => dest.ObjectKey, src => src.Photograph.ObjectKey)
            .Map(dest => dest.ContentType, src => src.Photograph.ContentType);
    }
}
