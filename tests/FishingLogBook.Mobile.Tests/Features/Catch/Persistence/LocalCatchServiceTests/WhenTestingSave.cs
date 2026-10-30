using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Persistence.Enums;
using FishingLogBook.Mobile.Features.LocalPersistence.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Persistence.LocalCatchServiceTests;

public sealed class WhenTestingSave : BaseLocalCatchServiceTest
{
    [Fact]
    public async Task ItShouldPersistTheCatchAcrossANewConnection()
    {
        // Arrange
        var args = CreateSaveArgs();

        // Act
        var saved = await Sut.SaveAsync(args, CancellationToken.None);
        await using (var _ = await ConnectionFactory.OpenAsync(CancellationToken.None))
        {
        }

        var reopened = RecreateServiceAgainstSameDatabase();
        var loaded = await reopened.GetAsync(args.Id, CancellationToken.None);

        // Assert
        saved.CatchId.Should().Be(args.Id);
        saved.Revision.Should().Be(1);
        loaded.Should().NotBeNull();
        loaded!.SpeciesName.Should().Be("Brown trout");
        loaded.WeightGrams.Should().Be(1200);
        loaded.LengthMillimetres.Should().Be(420);
        loaded.Photographs.Should().HaveCount(1);
        loaded.Revision.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRollBackCatchAndOutboxWhenTheTransactionFails()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await CatchStore.UpsertCatchAsync(
                    connection,
                    transaction,
                    new FishingLogBook.Mobile.Features.Catch.Persistence.Models.LocalCatchRecordModel
                    {
                        Id = catchId,
                        UserId = OwnerUserId,
                        CaughtByUserId = OwnerUserId,
                        RecordedByUserId = OwnerUserId,
                        CaughtOn = DateTimeOffset.UtcNow,
                        Revision = 1,
                        Lifecycle = LocalCatchLifecycleEnum.Saved,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Photographs = []
                    },
                    ct);
                await OutboxStore.UpsertPendingAsync(
                    connection,
                    transaction,
                    new FishingLogBook.Mobile.Features.LocalPersistence.Models.OutboxOperationModel
                    {
                        Id = Guid.NewGuid(),
                        UserId = OwnerUserId,
                        AggregateType = OutboxAggregateTypeEnum.Catch,
                        AggregateId = catchId,
                        OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                        PayloadJson = "{}",
                        Status = OutboxStatusEnum.Pending,
                        AttemptCount = 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        AvailableAt = DateTimeOffset.UtcNow
                    },
                    ct);
                throw new InvalidOperationException("Forced mid-transaction failure.");
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await CatchStore.GetAsync(OwnerUserId, catchId, CancellationToken.None)).Should().BeNull();
        (await OutboxStore.ListPendingForAggregateAsync(
                OwnerUserId,
                OutboxAggregateTypeEnum.Catch,
                catchId,
                CancellationToken.None))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task ItShouldWriteCatchAndOutboxAtomicallyOnSuccessfulSave()
    {
        // Arrange
        var args = CreateSaveArgs();

        // Act
        await Sut.SaveAsync(args, CancellationToken.None);
        var loaded = await Sut.GetAsync(args.Id, CancellationToken.None);
        var pending = await OutboxStore.ListPendingForAggregateAsync(
            OwnerUserId,
            OutboxAggregateTypeEnum.Catch,
            args.Id,
            CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        pending.Should().HaveCount(1);
        pending[0].OperationType.Should().Be(OutboxOperationTypeEnum.UpsertCatchMetadata);
        pending[0].Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldNotAllowAnotherAccountToReadOrOverwriteACatch()
    {
        // Arrange
        var args = CreateSaveArgs();
        await Sut.SaveAsync(args, CancellationToken.None);
        var otherService = CreateServiceForUser(OtherUserId);

        // Act
        var otherRead = await otherService.GetAsync(args.Id, CancellationToken.None);
        var otherList = await otherService.ListAsync(CancellationToken.None);
        var overwrite = async () => await otherService.SaveAsync(
            CreateSaveArgs(catchId: args.Id, speciesName: "Hijacked"),
            CancellationToken.None);

        // Assert
        otherRead.Should().BeNull();
        otherList.Should().BeEmpty();
        await overwrite.Should().ThrowAsync<InvalidOperationException>();
        var ownerRead = await Sut.GetAsync(args.Id, CancellationToken.None);
        ownerRead!.SpeciesName.Should().Be("Brown trout");
    }

    [Fact]
    public async Task ItShouldCoalescePendingMetadataOutboxAndBumpRevisionOnRepeatEdits()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await Sut.SaveAsync(CreateSaveArgs(catchId: catchId, speciesName: "First"), CancellationToken.None);

        // Act
        var second = await Sut.SaveAsync(
            CreateSaveArgs(catchId: catchId, speciesName: "Second", weightGrams: 1300),
            CancellationToken.None);
        var third = await Sut.SaveAsync(
            CreateSaveArgs(catchId: catchId, speciesName: "Third", weightGrams: 1400),
            CancellationToken.None);
        var loaded = await Sut.GetAsync(catchId, CancellationToken.None);
        var pending = await OutboxStore.ListPendingForAggregateAsync(
            OwnerUserId,
            OutboxAggregateTypeEnum.Catch,
            catchId,
            CancellationToken.None);

        // Assert
        second.Revision.Should().Be(2);
        third.Revision.Should().Be(3);
        loaded!.Revision.Should().Be(3);
        loaded.SpeciesName.Should().Be("Third");
        pending.Should().HaveCount(1);
        pending[0].PayloadJson.Should().Contain("Third");
        pending[0].OperationType.Should().Be(OutboxOperationTypeEnum.UpsertCatchMetadata);
    }

    [Fact]
    public async Task ItShouldNotLeavePartialStateWhenSaveIsCancelled()
    {
        // Arrange
        var args = CreateSaveArgs();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // Act
        var act = async () => await Sut.SaveAsync(args, cancelled.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await CatchStore.GetAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().BeNull();
        (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepPendingRecordsAfterSimulatedLogout()
    {
        // Arrange
        var args = CreateSaveArgs();
        await Sut.SaveAsync(args, CancellationToken.None);

        // Act — simulate logout by clearing the session capture without deleting SQLite rows
        MockSessionCoordinator.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);
        var whileLoggedOut = await Sut.GetAsync(args.Id, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(OwnerLease);
        MockSessionCoordinator.IsCurrent(OwnerLease).Returns(true);
        var afterLogin = await Sut.GetAsync(args.Id, CancellationToken.None);
        var otherService = CreateServiceForUser(OtherUserId);
        var otherRead = await otherService.GetAsync(args.Id, CancellationToken.None);
        var pending = await OutboxStore.ListPendingForAggregateAsync(
            OwnerUserId,
            OutboxAggregateTypeEnum.Catch,
            args.Id,
            CancellationToken.None);

        // Assert
        whileLoggedOut.Should().BeNull();
        afterLogin.Should().NotBeNull();
        afterLogin!.Id.Should().Be(args.Id);
        otherRead.Should().BeNull();
        pending.Should().HaveCount(1);
    }

    [Fact]
    public async Task ItShouldUpsertATripStubWhenTripIdIsProvided()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        var args = CreateSaveArgs(tripId: tripId, tripTitle: "May evening");

        // Act
        await Sut.SaveAsync(args, CancellationToken.None);
        var loaded = await Sut.GetAsync(args.Id, CancellationToken.None);

        // Assert
        loaded!.TripId.Should().Be(tripId);
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT title FROM local_trips WHERE id = $id AND user_id = $userId;";
        command.Parameters.AddWithValue("$id", tripId.ToString("D"));
        command.Parameters.AddWithValue("$userId", OwnerUserId.ToString("D"));
        var title = (string?)await command.ExecuteScalarAsync();
        title.Should().Be("May evening");
    }

    [Fact]
    public async Task ItShouldPersistDistinctCaughtByAndRecordedByIdentities()
    {
        // Arrange
        var anglerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var args = new FishingLogBook.Mobile.Features.Catch.Persistence.Models.LocalCatchSaveArgs
        {
            Id = Guid.NewGuid(),
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
            SpeciesName = "Brown trout",
            WeightGrams = 1200,
            LengthMillimetres = 420,
            Lifecycle = LocalCatchLifecycleEnum.Saved,
            CaughtByUserId = anglerUserId,
            RecordedByUserId = OwnerUserId,
            Photographs = []
        };

        // Act
        await Sut.SaveAsync(args, CancellationToken.None);
        var loaded = await Sut.GetAsync(args.Id, CancellationToken.None);

        // Assert
        loaded!.UserId.Should().Be(OwnerUserId);
        loaded.CaughtByUserId.Should().Be(anglerUserId);
        loaded.RecordedByUserId.Should().Be(OwnerUserId);
        var pending = await OutboxStore.ListPendingForAggregateAsync(
            OwnerUserId,
            OutboxAggregateTypeEnum.Catch,
            args.Id,
            CancellationToken.None);
        pending.Should().HaveCount(1);
        pending[0].PayloadJson.Should().Contain(anglerUserId.ToString("D"));
        pending[0].PayloadJson.Should().Contain(OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldRejectASaveRecordedAsAnotherAccount()
    {
        // Arrange
        var args = new FishingLogBook.Mobile.Features.Catch.Persistence.Models.LocalCatchSaveArgs
        {
            Id = Guid.NewGuid(),
            CaughtOn = DateTimeOffset.UtcNow,
            Lifecycle = LocalCatchLifecycleEnum.Saved,
            RecordedByUserId = OtherUserId,
            Photographs = []
        };

        // Act
        var act = async () => await Sut.SaveAsync(args, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await CatchStore.GetAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistDraftLifecycleWithoutLeavingTheQueue()
    {
        // Arrange
        var args = CreateSaveArgs(lifecycle: LocalCatchLifecycleEnum.Draft, speciesName: "Draft trout");

        // Act
        await Sut.SaveAsync(args, CancellationToken.None);
        var loaded = await Sut.GetAsync(args.Id, CancellationToken.None);
        var pending = await OutboxStore.ListPendingForAggregateAsync(
            OwnerUserId,
            OutboxAggregateTypeEnum.Catch,
            args.Id,
            CancellationToken.None);

        // Assert
        loaded!.Lifecycle.Should().Be(LocalCatchLifecycleEnum.Draft);
        pending.Should().HaveCount(1);
        pending[0].PayloadJson.Should().Contain("Draft");
    }

    [Fact]
    public async Task ItShouldPreservePriorCatchAndOutboxWhenALaterWriteFails()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await Sut.SaveAsync(CreateSaveArgs(catchId: catchId, speciesName: "Kept"), CancellationToken.None);

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await CatchStore.UpsertCatchAsync(
                    connection,
                    transaction,
                    new FishingLogBook.Mobile.Features.Catch.Persistence.Models.LocalCatchRecordModel
                    {
                        Id = catchId,
                        UserId = OwnerUserId,
                        CaughtByUserId = OwnerUserId,
                        RecordedByUserId = OwnerUserId,
                        CaughtOn = DateTimeOffset.UtcNow,
                        SpeciesName = "Should roll back",
                        Revision = 2,
                        Lifecycle = LocalCatchLifecycleEnum.Saved,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Photographs = []
                    },
                    ct);
                throw new IOException("Simulated storage failure.");
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IOException>();
        var loaded = await Sut.GetAsync(catchId, CancellationToken.None);
        var pending = await OutboxStore.ListPendingForAggregateAsync(
            OwnerUserId,
            OutboxAggregateTypeEnum.Catch,
            catchId,
            CancellationToken.None);
        loaded!.SpeciesName.Should().Be("Kept");
        loaded.Revision.Should().Be(1);
        pending.Should().HaveCount(1);
        pending[0].PayloadJson.Should().Contain("Kept");
    }
}
