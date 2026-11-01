using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using Microsoft.Data.Sqlite;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Stores.SqliteTripStoreTests;

public abstract class BaseSqliteTripStoreTest : BaseTripServiceTest
{
    protected LocalTripRecordModel NewTrip(Guid? id = null, Guid? userId = null)
    {
        var tripId = id ?? Guid.NewGuid();
        return new LocalTripRecordModel
        {
            UserId = userId ?? OwnerUserId,
            Detail = new TripDetailDto(new TripViewDto(tripId, userId ?? OwnerUserId, TripConstants.Active, Now) { Title = "Corrib" }) { Role = TripParticipantConstants.Owner }
        };
    }

    protected LocalTripContributionModel NewNote(Guid tripId, Guid? id = null)
    {
        var noteId = id ?? Guid.NewGuid();
        return new LocalTripContributionModel
        {
            Id = noteId,
            TripId = tripId,
            UserId = OwnerUserId,
            Note = new TripNoteDto(noteId, tripId, "Water dropped", Now) { CreatedByUserId = OwnerUserId }
        };
    }

    protected async Task<Guid> SeedTripAsync()
    {
        var trip = NewTrip();
        await TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        return trip.Detail.Trip.Id;
    }

    protected void RevokeSession()
    {
        MockSessionCoordinator.TryCommitAsync(
                Arg.Any<AccountSessionLeaseModel>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
    }

    protected async Task RunAsync(Func<SqliteConnection, SqliteTransaction, Task> action)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await action(connection, transaction);
        await transaction.CommitAsync();
    }

    protected async Task<IReadOnlyList<OutboxOperationModel>> PendingAsync()
    {
        return await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None);
    }
}
