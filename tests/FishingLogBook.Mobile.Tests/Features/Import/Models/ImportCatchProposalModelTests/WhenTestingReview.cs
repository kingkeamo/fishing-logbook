using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Tests.Features.Import.Models.ImportCatchProposalModelTests;

public sealed class WhenTestingReview
{
    private static readonly DateTimeOffset CapturedOn = new(2025, 6, 14, 9, 30, 0, TimeSpan.FromHours(1));
    private static readonly Guid PhotoId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void ItShouldKeepReviewedStateWhenWeightLengthMethodOrSpeciesChange()
    {
        // Arrange
        var proposal = Catch();
        proposal.MarkReviewed();

        // Act
        proposal.SetWeight(1.2m);
        proposal.SetLength(30m);
        proposal.OverrideMethod(new ImportCatalogueSelectionModel(Guid.NewGuid(), "Lure", "Lure"));
        proposal.OverrideSpecies(new ImportCatalogueSelectionModel(Guid.NewGuid(), "Pike", "Pike"));

        // Assert
        proposal.Weight.Should().Be(1.2m);
        proposal.Length.Should().Be(30m);
        proposal.Method.Code.Should().Be("Lure");
        proposal.Species.Code.Should().Be("Pike");
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Reviewed);
        proposal.IsReadyForConfirmation.Should().BeTrue();
    }

    [Fact]
    public void ItShouldLeaveReviewedStateWhenTheCaughtOnValueChanges()
    {
        // Arrange
        var proposal = Catch();
        proposal.MarkReviewed();

        // Act
        proposal.SetCaughtOn(ImportTimestampModel.UserConfirmed(CapturedOn.AddHours(1)));

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        proposal.IsReadyForConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ItShouldLeaveReviewedStateWhenTheLocationChanges()
    {
        // Arrange
        var proposal = Catch();
        proposal.MarkReviewed();

        // Act
        proposal.SetLocation(new ImportLocationModel(53d, -9d, true).Accept());

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        proposal.IsReadyForConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ItShouldLeaveReviewedStateWhenAPhotoIsRemoved()
    {
        // Arrange
        var secondPhotoId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var proposal = Catch([PhotoId, secondPhotoId]);
        proposal.MarkReviewed();

        // Act
        proposal.RemovePhoto(secondPhotoId);

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        proposal.IsReadyForConfirmation.Should().BeFalse();
        proposal.PhotoIds.Should().Equal(PhotoId);
    }

    private static ImportCatchProposalModel Catch(IReadOnlyList<Guid>? photoIds = null)
    {
        return new ImportCatchProposalModel(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            photoIds ?? [PhotoId],
            ImportTimestampModel.UserConfirmed(CapturedOn),
            new ImportCatalogueSelectionModel(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Fly",
                "Fly"),
            new ImportCatalogueSelectionModel(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "BrownTrout",
                "Brown Trout"));
    }
}
