using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Enums;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripTimelineServiceTests;

public class WhenTestingBuildLocal : BaseTripTimelineServiceTest
{
    [Fact]
    public void ItShouldStartAnEmptyActiveTripWithOnlyTheStartEntry()
    {
        // Arrange
        var trip = Trip();

        // Act
        var timeline = Sut.BuildLocal(trip, []);

        // Assert
        timeline.Should().ContainSingle();
        timeline[0].Kind.Should().Be(TripTimelineKindEnum.Started);
        timeline[0].OccurredOn.Should().Be(StartedOn);
    }

    [Fact]
    public void ItShouldNotAddAFinishedEntryToAnActiveTrip()
    {
        // Arrange
        var trip = Trip(notes: [Note("The wind dropped.", StartedOn.AddMinutes(20))]);

        // Act
        var timeline = Sut.BuildLocal(trip, []);

        // Assert
        timeline.Should().NotContain(item => item.Kind == TripTimelineKindEnum.Finished);
        timeline.Last().Kind.Should().Be(TripTimelineKindEnum.Note);
    }

    [Fact]
    public void ItShouldIgnoreACatchThatBelongsToAnotherTrip()
    {
        // Arrange
        var trip = Trip();
        var catches = new[]
        {
            Catch(StartedOn.AddMinutes(30), "Pike", Guid.NewGuid()),
            Catch(StartedOn.AddMinutes(40), "Brown Trout", null)
        };

        // Act
        var timeline = Sut.BuildLocal(trip, catches);

        // Assert
        timeline.Should().ContainSingle();
        timeline[0].Kind.Should().Be(TripTimelineKindEnum.Started);
    }

    [Fact]
    public void ItShouldIncludeTheCatchesOfThisTripWithTheirSpeciesAndPhotographCount()
    {
        // Arrange
        var trip = Trip();
        var catches = new[]
        {
            Catch(StartedOn.AddMinutes(45), "Pike", TripId, photographCount: 2),
            Catch(StartedOn.AddMinutes(20), null, TripId)
        };

        // Act
        var timeline = Sut.BuildLocal(trip, catches);

        // Assert
        var entries = timeline.Where(item => item.Kind == TripTimelineKindEnum.Catch).ToArray();
        entries.Should().HaveCount(2);
        entries[0].OccurredOn.Should().Be(StartedOn.AddMinutes(20));
        entries[0].SpeciesName.Should().BeNull();
        entries[1].SpeciesName.Should().Be("Pike");
        entries[1].PhotographCount.Should().Be(2);
        entries.Should().OnlyContain(item => item.CatchId != null);
    }

    [Fact]
    public void ItShouldOrderTheCapturedTimeOfAPhotographBeforeTheTimeItWasAdded()
    {
        // Arrange
        var trip = Trip(photographs:
        [
            Photograph(StartedOn.AddHours(3), StartedOn.AddMinutes(10))
        ]);

        // Act
        var timeline = Sut.BuildLocal(trip, []);

        // Assert
        timeline.Single(item => item.Kind == TripTimelineKindEnum.Photograph)
            .OccurredOn.Should().Be(StartedOn.AddMinutes(10));
    }

    [Fact]
    public void ItShouldCombineCatchesPhotographsAndNotesInTimeOrder()
    {
        // Arrange
        var trip = Trip(
            status: TripConstants.Completed,
            endedOn: StartedOn.AddHours(4),
            photographs: [Photograph(StartedOn.AddMinutes(50))],
            notes: [Note("The wind dropped.", StartedOn.AddMinutes(15))]);
        var catches = new[] { Catch(StartedOn.AddMinutes(35), "Pike", TripId) };

        // Act
        var timeline = Sut.BuildLocal(trip, catches);

        // Assert
        timeline.Select(item => item.Kind).Should().Equal(
            TripTimelineKindEnum.Started,
            TripTimelineKindEnum.Note,
            TripTimelineKindEnum.Catch,
            TripTimelineKindEnum.Photograph,
            TripTimelineKindEnum.Finished);
        timeline.Last().OccurredOn.Should().Be(StartedOn.AddHours(4));
        timeline.Should().BeInAscendingOrder(item => item.OccurredOn);
    }
}
