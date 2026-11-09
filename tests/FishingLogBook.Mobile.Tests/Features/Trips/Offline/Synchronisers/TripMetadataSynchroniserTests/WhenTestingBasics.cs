using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Synchronisers.TripMetadataSynchroniserTests;

public sealed class WhenTestingBasics : BaseTripServiceTest
{
    private TripMetadataSynchroniser Synchroniser => new(TripStore, MockClient);

    [Fact]
    public void ItShouldHandleTripMetadataUpserts()
    {
        // Arrange
        // Act
        var type = Synchroniser.OperationType;
        // Assert
        type.Should().Be(OutboxOperationTypeEnum.UpsertTripMetadata);
    }

    [Fact]
    public async Task ItShouldCompleteWithoutARequestWhenTheTripIsNotOnTheDevice()
    {
        // Arrange
        var operation = new OutboxOperationModel
        {
            Id = Guid.NewGuid(),
            UserId = OwnerUserId,
            AggregateId = Guid.NewGuid(),
            AggregateType = OutboxAggregateTypeEnum.Trip,
            OperationType = OutboxOperationTypeEnum.UpsertTripMetadata,
            PayloadJson = "{}",
            Status = OutboxStatusEnum.Pending,
            AttemptCount = 0,
            CreatedOn = Now,
            AvailableAt = Now
        };
        // Act
        var result = await Synchroniser.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }
}
