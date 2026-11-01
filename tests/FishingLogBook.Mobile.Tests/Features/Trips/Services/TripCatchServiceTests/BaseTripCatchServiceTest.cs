using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripCatchServiceTests;

public abstract class BaseTripCatchServiceTest
{
    protected static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid FriendId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected readonly ICatchStore MockCatchStore = Substitute.For<ICatchStore>();
    protected readonly ICatchRemoteClient MockRemote = Substitute.For<ICatchRemoteClient>();
    protected readonly ICatchWorkingCopyService MockWorkingCopies = Substitute.For<ICatchWorkingCopyService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly TripCatchService Sut;

    protected BaseTripCatchServiceTest()
    {
        MockSession.Capture().Returns(new AccountSessionLeaseModel(ViewerId, 1, CancellationToken.None));
        MockConnectivity.IsOnline.Returns(true);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        MockRemote.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Succeeded([]));
        MockWorkingCopies.EnsureAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(WorkingCopyOutcomeEnum.Ready);
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 2));
        Sut = new TripCatchService(MockCatchStore, MockRemote, MockWorkingCopies, MockSession, MockConnectivity, new FixedTimeProvider(Now));
    }

    protected static LocalTripRecordModel Trip(DateTimeOffset? endedOn = null, string? title = "Corrib")
    {
        return new LocalTripRecordModel
        {
            UserId = ViewerId,
            Detail = new TripDetailDto(new TripViewDto(TripId, ViewerId, endedOn is null ? TripConstants.Active : TripConstants.Completed, Now.AddHours(-3), endedOn)
            {
                Title = title
            })
            {
                Role = TripParticipantConstants.Owner
            }
        };
    }

    protected static CatchViewDto Remote(Guid id, DateTimeOffset caughtOn, Guid? tripId = null, Guid? by = null, Guid? recordedBy = null)
    {
        return new CatchViewDto(id, by ?? ViewerId, caughtOn)
        {
            RecordedByUserId = recordedBy ?? ViewerId,
            TripId = tripId,
            SpeciesName = "Pike"
        };
    }

    protected static LocalCatchRecordModel Local(
        Guid id,
        DateTimeOffset caughtOn,
        Guid? tripId = null,
        LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved,
        Guid? by = null,
        string species = "Perch",
        Guid? recordedBy = null)
    {
        return new LocalCatchRecordModel
        {
            Id = id,
            UserId = ViewerId,
            TripId = tripId,
            CaughtByUserId = by ?? ViewerId,
            RecordedByUserId = recordedBy ?? by ?? ViewerId,
            CaughtOn = caughtOn,
            SpeciesName = species,
            Weight = 1.5,
            Length = 40,
            Method = "Spinning",
            BaitOrLure = "Spoon",
            Notes = "Edge of the weed bed",
            PlaceName = "Lough Corrib",
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = Now.AddHours(-1),
            UpdatedOn = Now.AddHours(-1),
            Photographs =
            [
                new LocalCatchPhotographModel
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    CatchId = id,
                    UserId = ViewerId,
                    SortOrder = 0,
                    LocalFilePath = "photos/a.jpg",
                    ContentType = "image/jpeg",
                    CreatedOn = Now.AddHours(-1),
                    Metadata = new PhotographMetadataModel(Now.AddHours(-1), null, null)
                }
            ]
        };
    }
}
