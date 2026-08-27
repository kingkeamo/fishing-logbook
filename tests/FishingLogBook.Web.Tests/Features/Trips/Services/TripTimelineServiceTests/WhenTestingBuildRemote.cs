using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Enums;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripTimelineServiceTests;

public class WhenTestingBuildRemote : BaseTripTimelineServiceTest
{
    [Fact]
    public void ItShouldStartAndFinishAHistoricalTripWithNothingRecorded()
    {
        // Arrange
        var detail = Detail();

        // Act
        var timeline = Sut.BuildRemote(detail);

        // Assert
        timeline.Select(item => item.Kind).Should().Equal(
            TripTimelineKindEnum.Started,
            TripTimelineKindEnum.Finished);
    }

    [Fact]
    public void ItShouldNotFinishAnActiveHistoricalTrip()
    {
        // Arrange
        var detail = new TripDetailDto(new TripViewDto(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn));

        // Act
        var timeline = Sut.BuildRemote(detail);

        // Assert
        timeline.Should().ContainSingle();
        timeline[0].Kind.Should().Be(TripTimelineKindEnum.Started);
    }

    [Fact]
    public void ItShouldCarryThePhotographDownloadUrl()
    {
        // Arrange
        var detail = Detail(photographs:
        [
            new TripPhotographViewDto(
                Guid.NewGuid(),
                PhotographContentTypeConstants.Jpeg,
                StartedOn.AddMinutes(30),
                "https://storage.test/one.jpg?signed=1")
        ]);

        // Act
        var timeline = Sut.BuildRemote(detail);

        // Assert
        var photograph = timeline.Single(item => item.Kind == TripTimelineKindEnum.Photograph);
        photograph.PhotographUrl.Should().Be("https://storage.test/one.jpg?signed=1");
        photograph.OccurredOn.Should().Be(StartedOn.AddMinutes(30));
    }

    [Fact]
    public void ItShouldCombineTheServerNotesCatchesAndPhotographsInTimeOrder()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var detail = Detail(
            notes:
            [
                new TripNoteDto(Guid.NewGuid(), TripId, "The wind dropped.", StartedOn.AddMinutes(15))
            ],
            photographs:
            [
                new TripPhotographViewDto(
                    Guid.NewGuid(),
                    PhotographContentTypeConstants.Jpeg,
                    StartedOn.AddHours(2),
                    "https://storage.test/one.jpg?signed=1",
                    StartedOn.AddMinutes(50))
            ],
            catches:
            [
                new TripCatchSummaryDto(catchId, StartedOn.AddMinutes(35)) { SpeciesName = "Pike" }
            ]);

        // Act
        var timeline = Sut.BuildRemote(detail);

        // Assert
        timeline.Select(item => item.Kind).Should().Equal(
            TripTimelineKindEnum.Started,
            TripTimelineKindEnum.Note,
            TripTimelineKindEnum.Catch,
            TripTimelineKindEnum.Photograph,
            TripTimelineKindEnum.Finished);
        timeline.Single(item => item.Kind == TripTimelineKindEnum.Catch).CatchId.Should().Be(catchId);
        timeline.Single(item => item.Kind == TripTimelineKindEnum.Note)
            .Text.Should().Be("The wind dropped.");
    }
}
