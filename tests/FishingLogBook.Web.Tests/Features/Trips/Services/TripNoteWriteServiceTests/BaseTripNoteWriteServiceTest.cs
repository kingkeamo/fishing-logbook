using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripNoteWriteServiceTests;

public class BaseTripNoteWriteServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid NoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-08-17T11:30:00Z");

    protected readonly ITripNoteStore MockNoteStore = Substitute.For<ITripNoteStore>();
    protected readonly ITripClient MockTripClient = Substitute.For<ITripClient>();
    protected readonly TripNoteWriteService Sut;

    protected BaseTripNoteWriteServiceTest()
    {
        MockNoteStore.SaveAsync(Arg.Any<Web.Features.Trips.Models.TripNoteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        MockNoteStore.DeleteAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new TripNoteDto(
                call.ArgAt<RecordTripNoteDto>(1).NoteId,
                call.ArgAt<Guid>(0),
                call.ArgAt<RecordTripNoteDto>(1).Text,
                call.ArgAt<RecordTripNoteDto>(1).RecordedOn)
            {
                CreatedByUserId = OwnerUserId
            });
        Sut = new TripNoteWriteService(MockNoteStore, MockTripClient);
    }
}
