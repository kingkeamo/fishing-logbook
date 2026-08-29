using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripPhotographServiceTests;

public class BaseTripPhotographServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset AddedOn = DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    protected static readonly string ExpectedObjectKey =
        $"trip-photographs/{TripId:D}/{PhotographId:D}";

    protected readonly ITripAccessService MockTripAccessService = Substitute.For<ITripAccessService>();
    protected readonly ITripPhotographRepository MockTripPhotographRepository =
        Substitute.For<ITripPhotographRepository>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly TripPhotographService Sut;

    protected BaseTripPhotographServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectStorage.CreateUploadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        MockTripPhotographRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(null));
        MockTripPhotographRepository.UpsertAsync(
                Arg.Any<TripPhotograph>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripPhotograph>(0)));
        Sut = new TripPhotographService(
            MockTripAccessService,
            MockTripPhotographRepository,
            MockObjectStorage,
            MockCurrentUser,
            TestMapper.Create(),
            NullLogger<TripPhotographService>.Instance);
    }

    protected void GivenTrip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        MockTripAccessService.GivenOwner(BuildTrip(ownerUserId, status), CurrentUserId);
    }

    protected void GivenSharedTrip(TripStatusEnum status = TripStatusEnum.Active)
    {
        MockTripAccessService.GivenParticipant(BuildTrip(OtherUserId, status), CurrentUserId);
    }

    protected void GivenNoTrip()
    {
        MockTripAccessService.GivenNoAccess(TripId);
    }

    protected static Trip BuildTrip(Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Active)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
    }

    protected static TripPhotograph StoredPhotograph(Guid tripId, Guid? contributedByUserId = null)
    {
        return new TripPhotograph
        {
            Id = PhotographId,
            TripId = tripId,
            ContributedByUserId = contributedByUserId ?? CurrentUserId,
            ObjectKey = $"trip-photographs/{tripId:D}/{PhotographId:D}",
            ContentType = PhotographContentTypeConstants.Jpeg,
            AddedOn = AddedOn
        };
    }
}
