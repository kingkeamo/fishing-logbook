using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class CatchMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<CatchDto, CatchDto>()
            .MapWith(source => source);
        config.NewConfig<UpsertCatchCommand, UpsertCatchArgs>();
        config.NewConfig<CatchPhotograph, CatchPhotographDto>()
            .MapWith(source => new CatchPhotographDto(source.Id, source.CatchId, source.ContentType));
        config.NewConfig<Catch, CatchDto>()
            .MapWith(source => new CatchDto(
                source.Id,
                source.CaughtOn,
                source.Photographs
                    .Select(photograph => new CatchPhotographDto(
                        photograph.Id,
                        photograph.CatchId,
                        photograph.ContentType))
                    .ToList())
            {
                UserId = source.UserId
            });
    }
}
