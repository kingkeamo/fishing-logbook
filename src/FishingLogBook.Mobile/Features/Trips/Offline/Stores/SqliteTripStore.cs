using System.Text.Json;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Stores;

public sealed class SqliteTripStore(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabase database,
    IAccountSessionCoordinator session,
    IOutboxStore outbox,
    IOutboxChangeNotifier notifier,
    TimeProvider time) : ITripStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<LocalTripRecordModel>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        await database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payloadjson, revision, syncedrevision FROM localtripdocuments WHERE userid = $user;";
        command.Parameters.AddWithValue("$user", userId.ToString("D"));
        var result = new List<LocalTripRecordModel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var trip = JsonSerializer.Deserialize<LocalTripRecordModel>(reader.GetString(0), JsonOptions)!;
            result.Add(trip with { Revision = reader.GetInt64(1), SyncedRevision = reader.GetInt64(2) });
        }
        return result;
    }

    public async Task<LocalTripRecordModel?> GetAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        return (await ListAsync(userId, cancellationToken)).SingleOrDefault(trip => trip.Detail.Trip.Id == tripId);
    }

    public async Task SaveAsync(AccountSessionLeaseModel lease, LocalTripRecordModel trip, long expectedRevision, CancellationToken cancellationToken)
    {
        RequireAccount(lease, trip.UserId);
        var saved = trip with { Revision = expectedRevision + 1 };
        await CommitAsync(lease, async (connection, transaction, token) =>
        {
            await WriteTripAsync(connection, transaction, saved, expectedRevision, token);
            await EnqueueAsync(connection, transaction, lease.UserId, saved.Detail.Trip.Id,
                OutboxAggregateTypeEnum.Trip, OutboxOperationTypeEnum.UpsertTripMetadata, saved.Detail.Trip.Id, token);
        }, cancellationToken);
        notifier.NotifyWorkEnqueued();
    }

    public async Task HydrateAsync(AccountSessionLeaseModel lease, LocalTripRecordModel trip, CancellationToken cancellationToken)
    {
        RequireAccount(lease, trip.UserId);
        await CommitAsync(lease, async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO localtripdocuments (tripid, userid, payloadjson, revision, syncedrevision)
                VALUES ($id, $user, $payload, 0, 0)
                ON CONFLICT(tripid, userid) DO UPDATE SET payloadjson = excluded.payloadjson
                WHERE localtripdocuments.revision = localtripdocuments.syncedrevision;
                """;
            command.Parameters.AddWithValue("$id", trip.Detail.Trip.Id.ToString("D"));
            command.Parameters.AddWithValue("$user", lease.UserId.ToString("D"));
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(trip, JsonOptions));
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    public async Task RevokeAsync(AccountSessionLeaseModel lease, Guid tripId, CancellationToken cancellationToken)
    {
        await CommitAsync(lease, async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE localtripdocuments SET payloadjson = json_set(payloadjson, '$.accessRevoked', json('true'))
                WHERE tripid = $id AND userid = $user;
                """;
            command.Parameters.AddWithValue("$id", tripId.ToString("D"));
            command.Parameters.AddWithValue("$user", lease.UserId.ToString("D"));
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<LocalTripContributionModel>> ListContributionsAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        await database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payloadjson, revision, syncedrevision FROM localtripcontributions WHERE userid = $user AND tripid = $trip;";
        command.Parameters.AddWithValue("$user", userId.ToString("D"));
        command.Parameters.AddWithValue("$trip", tripId.ToString("D"));
        var result = new List<LocalTripContributionModel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var contribution = JsonSerializer.Deserialize<LocalTripContributionModel>(reader.GetString(0), JsonOptions)!;
            result.Add(contribution with { Revision = reader.GetInt64(1), SyncedRevision = reader.GetInt64(2) });
        }
        return result;
    }

    public async Task SaveContributionAsync(AccountSessionLeaseModel lease, LocalTripContributionModel contribution, long expectedRevision, CancellationToken cancellationToken)
    {
        RequireAccount(lease, contribution.UserId);
        var saved = contribution with { Revision = expectedRevision + 1 };
        await CommitAsync(lease, async (connection, transaction, token) =>
        {
            await WriteContributionAsync(connection, transaction, saved, expectedRevision, token);
            var isNote = saved.Note is not null;
            await EnqueueAsync(connection, transaction, lease.UserId, saved.Id,
                isNote ? OutboxAggregateTypeEnum.TripNote : OutboxAggregateTypeEnum.TripPhotograph,
                isNote ? OutboxOperationTypeEnum.WriteTripNote : OutboxOperationTypeEnum.WriteTripPhotograph, saved.TripId, token);
        }, cancellationToken);
        notifier.NotifyWorkEnqueued();
    }

    public async Task ConfirmAsync(SqliteConnection connection, SqliteTransaction transaction, LocalTripRecordModel trip, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE localtripdocuments
            SET syncedrevision = MAX(syncedrevision, $revision),
                payloadjson = CASE WHEN revision = $revision THEN $payload ELSE payloadjson END
            WHERE tripid = $id AND userid = $user;
            """;
        command.Parameters.AddWithValue("$id", trip.Detail.Trip.Id.ToString("D"));
        command.Parameters.AddWithValue("$user", trip.UserId.ToString("D"));
        command.Parameters.AddWithValue("$revision", trip.Revision);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(trip, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ConfirmContributionAsync(SqliteConnection connection, SqliteTransaction transaction, LocalTripContributionModel contribution, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE localtripcontributions SET syncedrevision = MAX(syncedrevision, $revision)
            WHERE id = $id AND userid = $user;
            """;
        command.Parameters.AddWithValue("$id", contribution.Id.ToString("D"));
        command.Parameters.AddWithValue("$user", contribution.UserId.ToString("D"));
        command.Parameters.AddWithValue("$revision", contribution.Revision);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CommitAsync(AccountSessionLeaseModel lease, Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
        var committed = await session.TryCommitAsync(lease,
            token => database.ExecuteInTransactionAsync(action, token), linked.Token);
        if (!committed)
        {
            throw new OperationCanceledException(lease.CancellationToken);
        }
    }

    private static void RequireAccount(AccountSessionLeaseModel lease, Guid userId)
    {
        if (userId == Guid.Empty || lease.UserId != userId)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private static async Task WriteTripAsync(SqliteConnection connection, SqliteTransaction transaction, LocalTripRecordModel trip, long expectedRevision, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO localtripdocuments (tripid, userid, payloadjson, revision, syncedrevision)
            VALUES ($id, $user, $payload, $revision, 0)
            ON CONFLICT(tripid, userid) DO UPDATE SET payloadjson = excluded.payloadjson, revision = excluded.revision
            WHERE localtripdocuments.revision = $expected;
            """;
        command.Parameters.AddWithValue("$id", trip.Detail.Trip.Id.ToString("D"));
        command.Parameters.AddWithValue("$user", trip.UserId.ToString("D"));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(trip, JsonOptions));
        command.Parameters.AddWithValue("$revision", trip.Revision);
        command.Parameters.AddWithValue("$expected", expectedRevision);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The trip changed while it was being edited.");
        }
    }

    private static async Task WriteContributionAsync(SqliteConnection connection, SqliteTransaction transaction, LocalTripContributionModel contribution, long expectedRevision, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO localtripcontributions (id, userid, tripid, payloadjson, localfilepath, revision, syncedrevision)
            VALUES ($id, $user, $trip, $payload, $path, $revision, 0)
            ON CONFLICT(id, userid) DO UPDATE SET payloadjson = excluded.payloadjson,
                localfilepath = excluded.localfilepath, revision = excluded.revision
            WHERE localtripcontributions.revision = $expected AND localtripcontributions.tripid = excluded.tripid;
            """;
        command.Parameters.AddWithValue("$id", contribution.Id.ToString("D"));
        command.Parameters.AddWithValue("$user", contribution.UserId.ToString("D"));
        command.Parameters.AddWithValue("$trip", contribution.TripId.ToString("D"));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(contribution, JsonOptions));
        command.Parameters.AddWithValue("$path", (object?)contribution.RelativeFilePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$revision", contribution.Revision);
        command.Parameters.AddWithValue("$expected", expectedRevision);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The contribution changed while it was being edited.");
        }
    }

    private Task EnqueueAsync(SqliteConnection connection, SqliteTransaction transaction, Guid userId, Guid id,
        OutboxAggregateTypeEnum aggregate, OutboxOperationTypeEnum operation, Guid tripId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return outbox.UpsertPendingAsync(connection, transaction, new OutboxOperationModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AggregateId = id,
            AggregateType = aggregate,
            OperationType = operation,
            PayloadJson = JsonSerializer.Serialize(new TripOperationPayloadModel(tripId), JsonOptions),
            Status = OutboxStatusEnum.Pending,
            AttemptCount = 0,
            CreatedOn = now,
            AvailableAt = now
        }, cancellationToken);
    }
}
