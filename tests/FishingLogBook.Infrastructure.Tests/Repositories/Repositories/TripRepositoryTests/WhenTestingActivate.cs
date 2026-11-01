using AwesomeAssertions;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingActivate : BaseTripRepositoryTest
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.Parse("2026-09-12T06:00:00Z");

    public WhenTestingActivate(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReportAMissingTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var activated = NewTrip(ownerUserId, status: TripStatusEnum.Active, startedOn: PlannedStartOn, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.ActivateAsync(activated, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
    }

    [Fact]
    public async Task ItShouldActivateAPlannedTripAtTheGivenStartAndKeepThePlan()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn, title: "Corrib", placeName: "Inver Bay");
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var start = PlannedStartOn.AddMinutes(40);

        // Act
        var result = await Sut.ActivateAsync(planned.ActivatedAt(start), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Active);
        result.Value.StartedOn.Should().Be(start);
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn);
        result.Value.Title.Should().Be("Corrib");
        result.Value.PlaceName.Should().Be("Inver Bay");
        result.Value.OwnerUserId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task ItShouldKeepTheFirstActualStartWhenActivatedAgain()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        await Sut.ActivateAsync(planned.ActivatedAt(PlannedStartOn.AddMinutes(10)), CancellationToken.None);

        // Act
        var again = await Sut.ActivateAsync(planned.ActivatedAt(PlannedStartOn.AddMinutes(50)), CancellationToken.None);

        // Assert
        again.IsSuccess.Should().BeTrue();
        again.Value.StartedOn.Should().Be(PlannedStartOn.AddMinutes(10));
    }

    [Theory]
    [InlineData(TripStatusEnum.Active)]
    [InlineData(TripStatusEnum.Completed)]
    public async Task ItShouldRefuseATripThatWasNeverPlanned(TripStatusEnum status)
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var started = NewTrip(
            ownerUserId,
            status: status,
            endedOn: status == TripStatusEnum.Completed ? StartedOn.AddHours(2) : null);
        await Sut.UpsertAsync(started, CancellationToken.None);

        // Act
        var result = await Sut.ActivateAsync(started.ActivatedAt(StartedOn.AddMinutes(1)), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        (await Sut.GetByIdAsync(started.Id, CancellationToken.None)).Value!.Status.Should().Be(status);
    }

    [Fact]
    public async Task ItShouldRefuseACancelledTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        await Sut.UpsertAsync(
            NewTrip(ownerUserId, planned.Id, TripStatusEnum.Cancelled, plannedStartOn: PlannedStartOn),
            CancellationToken.None);

        // Act
        var result = await Sut.ActivateAsync(planned.ActivatedAt(PlannedStartOn), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Cancelled);
    }

    [Fact]
    public async Task ItShouldRefuseWhenTheOrganiserAlreadyHasAnotherActiveTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var active = NewTrip(ownerUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(active, CancellationToken.None);
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: StartedOn.AddDays(1));
        await Sut.UpsertAsync(planned, CancellationToken.None);

        // Act
        var result = await Sut.ActivateAsync(planned.ActivatedAt(StartedOn.AddDays(1)), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripAlreadyActiveError>();
        (await Sut.GetByIdAsync(active.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Active);
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Planned);
    }

    [Fact]
    public async Task ItShouldActivateOnceWhenTwoRequestsArriveTogether()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);

        // Act
        var results = await Task.WhenAll(
            Sut.ActivateAsync(planned.ActivatedAt(PlannedStartOn.AddMinutes(5)), CancellationToken.None),
            Sut.ActivateAsync(planned.ActivatedAt(PlannedStartOn.AddMinutes(9)), CancellationToken.None));

        // Assert
        results.Should().OnlyContain(result => result.IsSuccess);
        results[0].Value.StartedOn.Should().Be(results[1].Value.StartedOn);
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.StartedOn.Should().Be(results[0].Value.StartedOn);
    }
}
