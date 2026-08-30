namespace FishingLogBook.Application.Trips.Contracts.Builders;

public interface ITripPhotographObjectKeyBuilder
{
    string Build(Guid tripId, Guid photographId);
}
