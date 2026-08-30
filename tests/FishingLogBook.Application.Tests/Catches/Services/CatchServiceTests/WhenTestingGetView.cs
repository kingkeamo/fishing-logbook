using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingGetView : BaseCatchServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheCurrentUserIsNotResolved()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);
        var args = new GetCatchArgs { CatchId = Guid.NewGuid() };

        // Act
        var result = await Sut.GetViewAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockCatchRepository.DidNotReceive().GetDetailForUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheCatchIsMissing()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        MockCatchRepository
            .GetDetailForUserAsync(catchId, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(null));

        // Act
        var result = await Sut.GetViewAsync(new GetCatchArgs { CatchId = catchId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchNotFoundError>();
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShapeLocationUsingTheCurrentUserIdWhenAParticipantViewsAnotherAnglersSharedTripCatch()
    {
        // Arrange
        var ownerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var tripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var catchRecord = new Catch
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = ownerUserId,
            AnglerUserId = ownerUserId,
            RecordedByUserId = ownerUserId,
            TripId = tripId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            SpeciesName = "Pike",
            Weight = 2.5m,
            Length = 64m,
            Method = "Lure",
            BaitOrLure = "Spinner",
            Notes = "Weedline"
        };
        MockCatchRepository
            .GetDetailForUserAsync(catchRecord.Id, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(new CatchDetail
            {
                Catch = catchRecord,
                AnglerName = "Owner Angler",
                RecordedByName = "Owner Angler"
            }));
        MockTripAccessService
            .ResolveForAsync(tripId, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                new Trip { Id = tripId, OwnerUserId = ownerUserId, StartedOn = catchRecord.CaughtOn },
                CurrentUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = tripId,
                    UserId = CurrentUserId,
                    Status = TripParticipantStatusEnum.Accepted,
                    InvitedByUserId = ownerUserId,
                    InvitedOn = catchRecord.CaughtOn.AddDays(-1),
                    RespondedOn = catchRecord.CaughtOn.AddHours(-1)
                })));
        MockCatchLocationPrivacyService
            .GetExposureAsync(Arg.Any<Catch>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CatchLocationExposureDto
            {
                Visibility = LocationDefaults.Private,
                Mode = LocationDefaults.ExposureNone
            });

        // Act
        var result = await Sut.GetViewAsync(
            new GetCatchArgs { CatchId = catchRecord.Id },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(ownerUserId);
        result.Value.AnglerUserId.Should().Be(ownerUserId);
        result.Value.RecordedByUserId.Should().Be(ownerUserId);
        result.Value.SpeciesName.Should().Be("Pike");
        result.Value.Weight.Should().Be(2.5m);
        result.Value.Length.Should().Be(64m);
        result.Value.Method.Should().Be("Lure");
        result.Value.BaitOrLure.Should().Be("Spinner");
        result.Value.Notes.Should().Be("Weedline");
        result.Value.Location!.Mode.Should().Be(LocationDefaults.ExposureNone);
        result.Value.AnglerName.Should().Be("Owner Angler");
        result.Value.RecordedByName.Should().Be("Owner Angler");
        await MockCatchRepository.Received(1).GetDetailForUserAsync(catchRecord.Id, CurrentUserId, Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.Received(1).GetExposureAsync(
            Arg.Is<Catch>(item => item.Id == catchRecord.Id && item.UserId == ownerUserId),
            CurrentUserId,
            Arg.Any<CancellationToken>());
        await MockCatchLocationPrivacyService.DidNotReceive().GetExposureAsync(
            Arg.Any<Catch>(),
            ownerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBuildPhotographDownloadUrlsWhenObjectStorageIsConfigured()
    {
        // Arrange
        var photographId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var catchRecord = new Catch
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = CurrentUserId,
            AnglerUserId = CurrentUserId,
            RecordedByUserId = CurrentUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Photographs = [new CatchPhotograph { Id = photographId, CatchId = Guid.Empty, ContentType = "image/jpeg" }]
        };
        MockCatchRepository
            .GetDetailForUserAsync(catchRecord.Id, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(new CatchDetail { Catch = catchRecord }));
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectStorage
            .CreateDownloadUrlAsync(
                $"catch-photographs/{catchRecord.Id:D}/{photographId:D}",
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://r2.test/signed-download"));

        // Act
        var result = await Sut.GetViewAsync(
            new GetCatchArgs { CatchId = catchRecord.Id },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Photographs.Should().ContainSingle(photograph =>
            photograph.Id == photographId
            && photograph.ContentType == "image/jpeg"
            && photograph.Url == "https://r2.test/signed-download");
    }

    [Fact]
    public async Task ItShouldOmitPhotographUrlsWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        var photographId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var catchRecord = new Catch
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = CurrentUserId,
            AnglerUserId = CurrentUserId,
            RecordedByUserId = CurrentUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Photographs = [new CatchPhotograph { Id = photographId, CatchId = Guid.Empty, ContentType = "image/jpeg" }]
        };
        MockCatchRepository
            .GetDetailForUserAsync(catchRecord.Id, CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(new CatchDetail { Catch = catchRecord }));
        MockObjectStorage.IsConfigured.Returns(false);

        // Act
        var result = await Sut.GetViewAsync(
            new GetCatchArgs { CatchId = catchRecord.Id },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Photographs.Should().ContainSingle(photograph =>
            photograph.Id == photographId && photograph.Url == null);
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
