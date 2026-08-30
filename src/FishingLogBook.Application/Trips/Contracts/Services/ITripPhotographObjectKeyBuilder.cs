namespace FishingLogBook.Application.Trips.Contracts.Services;

public interface ITripPhotographObjectKeyBuilder
{
    string Build(Guid tripId, Guid photographId);
}
