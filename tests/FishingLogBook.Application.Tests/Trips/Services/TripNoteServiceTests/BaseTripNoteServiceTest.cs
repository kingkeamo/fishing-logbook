using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripNoteServiceTests;

public class BaseTripNoteServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid NoteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-08-17T09:12:00Z");

    protected readonly ITripAccessService MockTripAccessService = Substitute.For<ITripAccessService>();
    protected readonly ITripNoteRepository MockTripNoteRepository =
        Substitute.For<ITripNoteRepository>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly TripNoteService Sut;

    protected BaseTripNoteServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockTripNoteRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(null));
        MockTripNoteRepository.UpsertAsync(Arg.Any<TripNote>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripNote>(0)));
        MockTripNoteRepository.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        Sut = new TripNoteService(
            MockTripAccessService,
            MockTripNoteRepository,
            MockCurrentUser,
            TestMapper.Create());
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

    protected static TripNote StoredNote(Guid tripId, Guid? createdByUserId = null)
    {
        return new TripNote
        {
            Id = NoteId,
            TripId = tripId,
            CreatedByUserId = createdByUserId ?? CurrentUserId,
            Text = "water dropped about a foot",
            RecordedOn = RecordedOn
        };
    }
}
