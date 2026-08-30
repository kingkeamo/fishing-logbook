using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingSynchronisePending : BaseCatchSynchroniserTest
{
    [Fact]
    public async Task ItShouldSynchroniseOnlyTheExplicitlyVerifiedOwnerPartition()
    {
        // Arrange
        var ownerCatch = CreateCatch();
        var otherCatch = CreateCatch(
            catchId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            userId: OtherUserId);
        var store = await CreateStoreAsync(ownerCatch, otherCatch);
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.DidNotReceive().GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.UserId == OwnerUserId && dto.Id == ownerCatch.Id),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.UserId == OtherUserId),
            Arg.Any<CancellationToken>());
        var untouched = await store.GetAsync(OtherUserId, otherCatch.Id, CancellationToken.None);
        untouched!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldNotCallTheServerWhenOffline()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut(store);
        var stateChanged = 0;
        sut.StateChanged += (_, _) => stateChanged++;

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.Photographs.Should().OnlyContain(
            photograph => photograph.SyncStatus == SyncStatus.WaitingToSynchronise);
        stateChanged.Should().Be(1);
        await MockCatchClient.DidNotReceive()
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive()
            .CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveAllLocalDataWhenMetadataFails()
    {
        // Arrange
        var expected = CreateCatch();
        var store = await CreateStoreAsync(expected);
        MockCatchClient.UpsertAsync(
                Arg.Any<CatchDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("unavailable"));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.Location.Should().Be(expected.Location);
        saved.Photographs.Should().HaveCount(3);
        saved.Photographs.Should().OnlyContain(
            photograph => photograph.Bytes != null && photograph.Bytes.Length == 3);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId
                && dto.UserId == OwnerUserId
                && dto.AnglerUserId == OwnerUserId
                && dto.RecordedByUserId == OwnerUserId
                && dto.Location != null
                && dto.Location.Latitude == 53.2707
                && dto.Location.Longitude == -9.0568
                && dto.Location.Visibility == "Private"),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepSuccessfulPhotographsAndFailTheOverallCatchWhenOneUploadFails()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        MockCatchClient.UploadPhotographAsync(
                $"https://storage.test/{PhotographBId:D}",
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("storage unavailable"));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs.Single(item => item.Id == PhotographAId)
            .SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs.Single(item => item.Id == PhotographBId)
            .SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.Photographs.Single(item => item.Id == PhotographCId)
            .SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Location!.Visibility.Should().Be("Private");
        saved.Location.Latitude.Should().Be(53.2707);
        saved.Photographs.Should().OnlyContain(
            photograph => photograph.Bytes != null && photograph.Bytes.Length == 3);
        await MockCatchClient.Received(3).CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == PhotographAId
                || request.PhotographId == PhotographBId
                || request.PhotographId == PhotographCId),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(2).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(request =>
                request.PhotographId == PhotographAId
                || request.PhotographId == PhotographCId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecoverStaleSynchronisingState()
    {
        // Arrange
        var stale = CreateCatch(
            catchStatus: SyncStatus.Synchronising,
            metadataStatus: SyncStatus.Synchronising,
            photographs:
            [
                CreatePhotograph(PhotographAId, CatchId, SyncStatus.Synchronising)
            ]);
        var store = await CreateStoreAsync(stale);
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.Photographs.Should().ContainSingle()
            .Which.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockCatchClient.DidNotReceive()
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSynchroniseUserACatchWhileUserBIsSignedIn()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch(userId: OwnerUserId));
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(OtherUserId);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.Received(1)
            .GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive()
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive()
            .CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveLocalDataWhenAuthenticationIsUnavailable()
    {
        // Arrange
        var expected = CreateCatch();
        var store = await CreateStoreAsync(expected);
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("not signed in"));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved.Should().BeEquivalentTo(expected);
        await MockCatchClient.DidNotReceive()
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.AuthenticationUnavailable,
            "Authentication is unavailable for catch synchronisation.",
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveLocalDataWhenTheAccessTokenHasExpired()
    {
        // Arrange
        var expected = CreateCatch();
        var store = await CreateStoreAsync(expected);
        MockCatchClient.UpsertAsync(
                Arg.Any<CatchDto>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException(
                "unauthorized",
                null,
                HttpStatusCode.Unauthorized));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        saved.Location.Should().Be(expected.Location);
        saved.Photographs.Should().HaveCount(3);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.AuthenticationUnavailable,
            "Authentication is unavailable for catch synchronisation.",
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.CatchId] == CatchId.ToString("D")),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldContinueWithOtherCatchesWhenOneFails()
    {
        // Arrange
        var catchBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var catchCId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var catchA = CreateCatch(
            photographs: [CreatePhotograph(PhotographAId, CatchId)]);
        var catchB = CreateCatch(
            catchBId,
            photographs: [CreatePhotograph(PhotographBId, catchBId)]);
        var catchC = CreateCatch(
            catchCId,
            photographs: [CreatePhotograph(PhotographCId, catchCId)]);
        var store = await CreateStoreAsync(catchA, catchB, catchC);
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.Arg<CatchDto>().Id == catchBId)
                {
                    throw new HttpRequestException("metadata failed");
                }

                return Task.CompletedTask;
            });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Single(item => item.Id == CatchId).SyncStatus
            .Should().Be(SyncStatus.Synchronised);
        saved.Single(item => item.Id == catchBId).SyncStatus
            .Should().Be(SyncStatus.WaitingToSynchronise);
        saved.Single(item => item.Id == catchCId).SyncStatus
            .Should().Be(SyncStatus.Synchronised);
        await MockCatchClient.Received(3).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId || dto.Id == catchBId || dto.Id == catchCId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreventConcurrentAttemptsForTheSameCatch()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateCatch(photographs: [CreatePhotograph(PhotographAId, CatchId)]));
        var metadataStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMetadata = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                metadataStarted.SetResult();
                await releaseMetadata.Task;
            });
        var sut = CreateSut(store);

        // Act
        var first = sut.SynchronisePendingAsync(CancellationToken.None);
        await metadataStarted.Task;
        var second = sut.SynchronisePendingAsync(CancellationToken.None);
        releaseMetadata.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == PhotographAId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSynchroniseANewerEditRequestedWhileTheCatchIsInFlight()
    {
        // Arrange
        var original = CreateCatch(
            photographs: [CreatePhotograph(PhotographAId, CatchId)]);
        var updatedCaughtOn = original.CaughtOn.AddDays(-1);
        var store = await CreateStoreAsync(original);
        var metadataStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMetadata = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upsertCount = 0;
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                upsertCount += 1;
                if (upsertCount == 1)
                {
                    metadataStarted.SetResult();
                    await releaseMetadata.Task;
                }
            });
        var sut = CreateSut(store);

        // Act
        var first = sut.SynchronisePendingAsync(CancellationToken.None);
        await metadataStarted.Task;
        var current = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        await store.SaveAsync(
            current! with
            {
                CaughtOn = updatedCaughtOn,
                SyncStatus = SyncStatus.WaitingToSynchronise,
                MetadataSyncStatus = SyncStatus.WaitingToSynchronise
            },
            CancellationToken.None);
        var second = sut.SynchronisePendingAsync(CancellationToken.None);
        releaseMetadata.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockCatchClient.Received(2).UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.CaughtOn == updatedCaughtOn),
            Arg.Any<CancellationToken>());
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        saved!.CaughtOn.Should().Be(updatedCaughtOn);
        saved.SyncStatus.Should().Be(SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldNotOverwriteANewerPrivacyChoiceMadeDuringMetadataSync()
    {
        // Arrange
        var original = CreateCatch() with
        {
            Location = CreateCatch().Location! with { Visibility = "Public" }
        };
        var store = await CreateStoreAsync(original);
        MockCatchClient.UpsertAsync(
                Arg.Any<CatchDto>(),
                Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var current = await store.GetAsync(
                    OwnerUserId,
                    CatchId,
                    CancellationToken.None);
                await store.SaveAsync(
                    current! with
                    {
                        Location = current.Location! with { Visibility = "Private" },
                        SyncStatus = SyncStatus.WaitingToSynchronise,
                        MetadataSyncStatus = SyncStatus.WaitingToSynchronise
                    },
                    CancellationToken.None);
            });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.Location!.Visibility.Should().Be("Private");
        saved.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId
                && dto.Location != null
                && dto.Location.Visibility == "Public"),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSynchroniseMetadataAndEveryPhotograph()
    {
        // Arrange
        var expected = CreateCatch();
        var store = await CreateStoreAsync(expected);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs.Should().OnlyContain(
            photograph => photograph.SyncStatus == SyncStatus.Synchronised);
        saved.Photographs.Should().OnlyContain(
            photograph => photograph.Bytes != null && photograph.Bytes.Length == 3);
        saved.Location.Should().Be(expected.Location);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId
                && dto.UserId == OwnerUserId
                && dto.AnglerUserId == OwnerUserId
                && dto.RecordedByUserId == OwnerUserId
                && dto.Photographs.Count == 3
                && dto.Location != null
                && dto.Location.Latitude == expected.Location!.Latitude
                && dto.Location.Longitude == expected.Location.Longitude
                && dto.Location.Visibility == expected.Location.Visibility),
            Arg.Any<CancellationToken>());
        await MockCatchClient.Received(3).UploadPhotographAsync(
            Arg.Is<string>(url => url.StartsWith("https://storage.test/")),
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 1, 2, 3 })),
            "image/jpeg",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotMarkMetadataSynchronisedWhenDetailsChangeDuringUpsert()
    {
        // Arrange
        var original = CreateCatch();
        var store = await CreateStoreAsync(original);
        MockCatchClient.UpsertAsync(
                Arg.Any<CatchDto>(),
                Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var current = await store.GetAsync(
                    OwnerUserId,
                    CatchId,
                    CancellationToken.None);
                await store.SaveAsync(
                    current! with
                    {
                        SpeciesName = "Perch",
                        Weight = 0.8m,
                        SyncStatus = SyncStatus.WaitingToSynchronise,
                        MetadataSyncStatus = SyncStatus.WaitingToSynchronise
                    },
                    CancellationToken.None);
            });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SpeciesName.Should().Be("Perch");
        saved.Weight.Should().Be(0.8m);
        saved.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId
                && dto.SpeciesName == "Pike"
                && dto.CaughtOn == original.CaughtOn),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReuploadSynchronisedPhotographsWhenOnlyDetailsArePending()
    {
        // Arrange
        var photographs = new[]
        {
            CreatePhotograph(PhotographAId, CatchId, SyncStatus.Synchronised) with
            {
                ObjectKey = "catches/a"
            }
        };
        var catchRecord = CreateCatch(
            metadataStatus: SyncStatus.WaitingToSynchronise,
            photographs: photographs) with
        {
            SyncStatus = SyncStatus.WaitingToSynchronise,
            SpeciesName = "Pike",
            Weight = 2.5m,
            Length = 64m,
            Method = "Lure",
            BaitOrLure = "Spinner",
            Notes = "Weedline"
        };
        var store = await CreateStoreAsync(catchRecord);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs.Should().ContainSingle();
        saved.Photographs[0].SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs[0].ObjectKey.Should().Be("catches/a");
        saved.SpeciesName.Should().Be("Pike");
        saved.Weight.Should().Be(2.5m);
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == CatchId
                && dto.SpeciesName == "Pike"
                && dto.Weight == 2.5m
                && dto.Length == 64m
                && dto.Method == "Lure"
                && dto.BaitOrLure == "Spinner"
                && dto.Notes == "Weedline"
                && dto.CaughtOn == catchRecord.CaughtOn),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UploadPhotographAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteAPendingDeletionPhotographFromTheServerAndRemoveItLocally()
    {
        // Arrange
        var photographs = new[]
        {
            CreatePhotograph(PhotographAId, CatchId, SyncStatus.Synchronised) with { ObjectKey = "catches/a" },
            CreatePhotograph(PhotographBId, CatchId, SyncStatus.PendingDeletion) with { ObjectKey = "catches/b" }
        };
        var catchRecord = CreateCatch(
            metadataStatus: SyncStatus.Synchronised,
            photographs: photographs) with
        {
            SyncStatus = SyncStatus.WaitingToSynchronise
        };
        var store = await CreateStoreAsync(catchRecord);
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs.Should().ContainSingle()
            .Which.Id.Should().Be(PhotographAId);
        await MockCatchClient.Received(1).DeletePhotographAsync(
            CatchId,
            PhotographBId,
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAPendingDeletionPhotographRetryableWhenTheServerDeleteFails()
    {
        // Arrange
        var photographs = new[]
        {
            CreatePhotograph(PhotographAId, CatchId, SyncStatus.PendingDeletion) with { ObjectKey = "catches/a" }
        };
        var catchRecord = CreateCatch(
            metadataStatus: SyncStatus.Synchronised,
            photographs: photographs) with
        {
            SyncStatus = SyncStatus.WaitingToSynchronise
        };
        var store = await CreateStoreAsync(catchRecord);
        MockCatchClient.DeletePhotographAsync(CatchId, PhotographAId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("unavailable"));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.Photographs.Should().ContainSingle()
            .Which.SyncStatus.Should().Be(SyncStatus.PendingDeletion);
        saved.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);

        // Act - retry
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert - still routed through delete, never through upload
        await MockCatchClient.Received(2).DeletePhotographAsync(
            CatchId,
            PhotographAId,
            Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAutomaticallyRetryATransientlyFailedCatchAndThenSucceed()
    {
        // Arrange
        var attempts = 0;
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException(
                        "service unavailable",
                        null,
                        HttpStatusCode.ServiceUnavailable);
                }

                return Task.CompletedTask;
            });
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var afterFirstAttempt = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        afterFirstAttempt!.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(2).UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldNotAutomaticallyRetryAPermanentlyFailedCatch()
    {
        // Arrange
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException(
                "validation failed",
                null,
                HttpStatusCode.BadRequest));
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.MetadataSyncStatus.Should().Be(SyncStatus.FailedToSynchronise);

        // Act - a second sync pass must not retry a permanently rejected catch
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockCatchClient.Received(1).UpsertAsync(
            Arg.Any<CatchDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotMarkTheCatchFailedWhenSynchronisationIsCancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        MockCatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        try
        {
            await sut.SynchronisePendingAsync(OwnerUserId, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }

        // Assert
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        saved!.MetadataSyncStatus.Should().NotBe(SyncStatus.FailedToSynchronise);
    }
}
