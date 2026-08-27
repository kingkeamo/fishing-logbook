using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripDetailServiceTests;

public class BaseTripDetailServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly ITripRepository MockTripRepository = Substitute.For<ITripRepository>();
    protected readonly ITripNoteRepository MockTripNoteRepository = Substitute.For<ITripNoteRepository>();
    protected readonly ITripPhotographRepository MockTripPhotographRepository =
        Substitute.For<ITripPhotographRepository>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly TripDetailService Sut;

    protected BaseTripDetailServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockTripRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(StoredTrip()));
        MockTripRepository.GetCatchSummariesByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripCatchSummary>>([]));
        MockTripNoteRepository.GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>([]));
        MockTripPhotographRepository.GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripPhotograph>>([]));
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectStorage.CreateDownloadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new Uri($"https://storage.test/{call.ArgAt<string>(0)}?signed=1"));
        Sut = new TripDetailService(
            MockTripRepository,
            MockTripNoteRepository,
            MockTripPhotographRepository,
            MockCurrentUser,
            MockObjectStorage,
            TestMapper.Create());
    }

    protected static Trip StoredTrip(
        Guid? ownerUserId = null,
        TripStatusEnum status = TripStatusEnum.Active,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = ownerUserId ?? CurrentUserId,
            Title = title,
            PlaceName = placeName,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = endedOn,
            CreatedOn = StartedOn,
            UpdatedOn = StartedOn
        };
    }

    protected static TripNote Note(string text, DateTimeOffset recordedOn)
    {
        return new TripNote
        {
            Id = Guid.NewGuid(),
            TripId = TripId,
            CreatedByUserId = CurrentUserId,
            Text = text,
            RecordedOn = recordedOn
        };
    }

    protected static TripPhotograph Photograph(string objectKey, DateTimeOffset addedOn)
    {
        return new TripPhotograph
        {
            Id = Guid.NewGuid(),
            TripId = TripId,
            ObjectKey = objectKey,
            ContentType = PhotographContentTypeConstants.Jpeg,
            AddedOn = addedOn
        };
    }

    protected static TripCatchSummary CatchSummary(string? speciesName, DateTimeOffset caughtOn)
    {
        return new TripCatchSummary
        {
            Id = Guid.NewGuid(),
            CaughtOn = caughtOn,
            SpeciesName = speciesName
        };
    }
}
