using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingExistingTrip : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldRememberTheAssignedExistingTripAfterPersist()
    {
        // Arrange
        var existingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        OfferExistingTrip(existingId);
        await ReachTripReviewAsync();
        MockModals.ChooseExistingTripAsync(Arg.Any<IReadOnlyList<TripSummaryDto>>()).Returns(existingId);
        await Sut.DecideExistingTripAsync(Sut.Trips.Single().Proposal.Id);
        await Sut.ContinueAsync(CancellationToken.None);
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.DestinationTripId.Should().Be(existingId);
        await MockPersistence.Received(1).PersistAsync(
            Arg.Is<ImportBatchModel>(batch =>
                batch.TripProposals.Single().ExistingTripId == existingId),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>?>());
    }

    [Fact]
    public async Task ItShouldRememberANewlyCreatedTripAfterPersist()
    {
        // Arrange
        var createdId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        await ReachConfirmAsync();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(new ImportPersistenceResultModel([createdId], [Guid.NewGuid()], 1, 0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.DestinationTripId.Should().Be(createdId);
    }

    [Fact]
    public async Task ItShouldNotRememberATripWhenCatchesStayStandalone()
    {
        // Arrange
        await ReachTripReviewAsync();
        Sut.DecideKeepSeparate(Sut.Trips.Single().Proposal.Id);
        await Sut.ContinueAsync(CancellationToken.None);
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.DestinationTripId.Should().BeNull();
    }

    private void OfferExistingTrip(Guid existingId)
    {
        var summary = new TripSummaryDto(
            existingId,
            TripConstants.Active,
            new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero))
        {
            Role = TripParticipantConstants.Owner
        };
        MockExistingTrips.GetCandidatesAsync(
                Arg.Any<IReadOnlyList<ImportTripProposalModel>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var proposals = call.Arg<IReadOnlyList<ImportTripProposalModel>>();
                return proposals.ToDictionary(
                    proposal => proposal.Id,
                    _ => (IReadOnlyList<TripSummaryDto>)[summary]);
            });
    }
}
