using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Stores;

public interface ITripStore
{
    Task<IReadOnlyList<LocalTripRecordModel>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<LocalTripRecordModel?> GetAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task SaveAsync(AccountSessionLeaseModel lease, LocalTripRecordModel trip, long expectedRevision, CancellationToken cancellationToken);
    Task HydrateAsync(AccountSessionLeaseModel lease, LocalTripRecordModel trip, CancellationToken cancellationToken);
    Task RevokeAsync(AccountSessionLeaseModel lease, Guid tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalTripContributionModel>> ListContributionsAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
    Task SaveContributionAsync(AccountSessionLeaseModel lease, LocalTripContributionModel contribution, long expectedRevision, CancellationToken cancellationToken);
    Task ConfirmAsync(SqliteConnection connection, SqliteTransaction transaction, LocalTripRecordModel trip, CancellationToken cancellationToken);
    Task ConfirmContributionAsync(SqliteConnection connection, SqliteTransaction transaction, LocalTripContributionModel contribution, CancellationToken cancellationToken);
}
