using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Queries;
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
        config.NewConfig<GetCatchQuery, GetCatchArgs>();
        config.NewConfig<UpsertCatchCommand, UpsertCatchArgs>();
        config.NewConfig<UpdateCatchLocationVisibilityCommand, UpdateCatchLocationVisibilityArgs>();
        config.NewConfig<CreateCatchPhotographUploadCommand, CreateCatchPhotographUploadArgs>();
        config.NewConfig<RecordCatchPhotographCommand, RecordCatchPhotographArgs>()
            .Map(destination => destination.PhotographId, source => source.Photograph.PhotographId)
            .Map(destination => destination.ObjectKey, source => source.Photograph.ObjectKey)
            .Map(destination => destination.ContentType, source => source.Photograph.ContentType);
        config.NewConfig<CatchPhotograph, CatchPhotographDto>()
            .MapWith(source => new CatchPhotographDto(source.Id, source.CatchId, source.ContentType));
        config.NewConfig<CatchLocation, CatchLocationDto>()
            .MapWith(source => new CatchLocationDto(
                source.Latitude,
                source.Longitude,
                source.AccuracyMetres,
                source.CapturedOn,
                source.Source,
                source.Visibility,
                source.ConsentVersion));
        config.NewConfig<Catch, CatchDto>()
            .MapWith(source => new CatchDto(
                source.Id,
                source.CaughtOn,
                source.Photographs
                    .Select(photograph => new CatchPhotographDto(
                        photograph.Id,
                        photograph.CatchId,
                        photograph.ContentType))
                    .ToList(),
                source.Location == null
                    ? null
                    : new CatchLocationDto(
                        source.Location.Latitude,
                        source.Location.Longitude,
                        source.Location.AccuracyMetres,
                        source.Location.CapturedOn,
                        source.Location.Source,
                        source.Location.Visibility,
                        source.Location.ConsentVersion))
            {
                UserId = source.UserId
            });
    }
}
