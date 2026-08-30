using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using Mapster;

namespace FishingLogBook.Infrastructure.Persistence.Mappings;

public sealed class TripParticipantMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<TripParticipantRepository.TripParticipantPersistenceRow, TripParticipant>()
            .Map(destination => destination.Status, source => ToStatus(source.Status));
    }

    private static TripParticipantStatusEnum ToStatus(string? status)
    {
        return Enum.TryParse<TripParticipantStatusEnum>(status, ignoreCase: false, out var parsed)
            ? parsed
            : TripParticipantStatusEnum.Declined;
    }
}
