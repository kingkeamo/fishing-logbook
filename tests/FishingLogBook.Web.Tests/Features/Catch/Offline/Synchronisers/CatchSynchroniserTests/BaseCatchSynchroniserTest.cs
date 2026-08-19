using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class BaseCatchSynchroniserTest
{
    protected static readonly Guid OwnerUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid CatchId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid PhotographAId =
        Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    protected static readonly Guid PhotographBId =
        Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    protected static readonly Guid PhotographCId =
        Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    protected readonly ICatchClient MockCatchClient = Substitute.For<ICatchClient>();
    protected readonly INetworkService MockNetworkService = Substitute.For<INetworkService>();
    protected readonly ILocalCatchOwnerService MockLocalCatchOwner =
        Substitute.For<ILocalCatchOwnerService>();
    protected readonly IDiagnosticLogger MockDiagnostics = Substitute.For<IDiagnosticLogger>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();

    protected BaseCatchSynchroniserTest()
    {
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(OwnerUserId);
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>())
            .Returns(true);
        MockCatchClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var catchId = call.ArgAt<Guid>(0);
                var request = call.ArgAt<PhotographUploadRequestDto>(1);
                var objectKey =
                    $"catches/{OwnerUserId:D}/{catchId:D}/{request.PhotographId:D}";
                return new PhotographUploadDto(
                    objectKey,
                    $"https://storage.test/{request.PhotographId:D}");
            });
    }

    protected CatchSynchroniser CreateSut(MemoryCatchStore store)
    {
        return new CatchSynchroniser(
            store,
            MockCatchClient,
            MockNetworkService,
            MockLocalCatchOwner,
            MockDiagnostics,
            MockLogging);
    }

    protected static CatchModel CreateCatch(
        Guid? catchId = null,
        Guid? userId = null,
        SyncStatus catchStatus = SyncStatus.SavedLocally,
        SyncStatus metadataStatus = SyncStatus.SavedLocally,
        IReadOnlyList<CatchPhotographModel>? photographs = null)
    {
        var id = catchId ?? CatchId;
        return new CatchModel(
            id,
            DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            photographs ??
            [
                CreatePhotograph(PhotographAId, id),
                CreatePhotograph(PhotographBId, id),
                CreatePhotograph(PhotographCId, id)
            ],
            "Pike",
            new CatchLocationModel(
                53.2707,
                -9.0568,
                7,
                DateTimeOffset.Parse("2026-08-17T11:59:00Z"),
                "DeviceGps",
                "Private",
                "1"),
            userId ?? OwnerUserId,
            catchStatus,
            metadataStatus,
            AnglerUserId: userId ?? OwnerUserId,
            RecordedByUserId: userId ?? OwnerUserId);
    }

    protected static CatchPhotographModel CreatePhotograph(
        Guid photographId,
        Guid catchId,
        SyncStatus status = SyncStatus.SavedLocally)
    {
        return new CatchPhotographModel(
            photographId,
            catchId,
            "image/jpeg",
            [1, 2, 3],
            status);
    }

    protected static async Task<MemoryCatchStore> CreateStoreAsync(params CatchModel[] catches)
    {
        var store = new MemoryCatchStore();
        foreach (var catchRecord in catches)
        {
            await store.SaveAsync(catchRecord, CancellationToken.None);
        }

        return store;
    }
}
