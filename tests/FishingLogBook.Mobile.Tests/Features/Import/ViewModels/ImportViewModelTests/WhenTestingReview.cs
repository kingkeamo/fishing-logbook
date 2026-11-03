using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingReview : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldAskForCatchConfirmationInsteadOfCallingTheCatchReady()
    {
        // Arrange
        // Act
        await ReviewPreparedPhotoAsync(ReadyPhoto());

        // Assert
        var row = Sut.Catches.Should().ContainSingle().Subject;
        row.StatusText.Should().Be(Text["Import_NeedsConfirmation"]);
        row.Photos.Single().Label.Should().Be("Photo 1");
        row.WeightText.Should().StartWith("Weight");
        row.LengthText.Should().StartWith("Length");
        row.HasLocationHelp.Should().BeTrue();
        Sut.CanContinue.Should().BeFalse();
        Sut.ContinueHint.Should().Be("Confirm catch 1 to continue.");
    }

    [Fact]
    public async Task ItShouldApplyTheShownDateWhenThePhotoHasNoTimestamp()
    {
        // Arrange
        // Act
        await ReviewPreparedPhotoAsync(ReadyPhoto(ImportTimestampModel.Missing()));

        // Assert
        var row = Sut.Catches.Single();
        row.StatusText.Should().Be(Text["Import_NeedsConfirmation"]);
        row.Proposal.CaughtOn.IsResolved.Should().BeTrue();
        Sut.ContinueHint.Should().Be("Confirm catch 1 to continue.");
        Sut.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldMarkTheCatchConfirmedAndClearTheContinueHint()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());

        // Act
        Sut.ConfirmCatch(Sut.Catches.Single().Proposal.Id);

        // Assert
        Sut.Catches.Single().StatusText.Should().Be(Text["Import_Confirmed"]);
        Sut.CanContinue.Should().BeTrue();
        Sut.HasContinueHint.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepConfirmationWhenTheShownDateHasNotChanged()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var row = Sut.Catches.Single();
        Sut.ConfirmCatch(row.Proposal.Id);

        // Act
        await Sut.ConfirmCaughtOnAsync(row.Proposal.Id, row.CaughtOnLocal, CancellationToken.None);

        // Assert
        Sut.Catches.Single().StatusText.Should().Be(Text["Import_Confirmed"]);
        Sut.CanContinue.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepConfirmationWhenWeightLengthMethodOrSpeciesChange()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;
        Sut.ConfirmCatch(catchId);
        var weight = Sut.CreateWeightEditor(catchId);
        weight.SetExactText("1.2");
        var length = Sut.CreateLengthEditor(catchId);
        length.SetExactText("30");

        // Act
        Sut.CompleteWeightEdit(catchId, true, weight);
        Sut.CompleteLengthEdit(catchId, true, length);
        Sut.SetCatchMethod(catchId, OtherMethodId);
        Sut.SetCatchSpecies(catchId, SpeciesId);

        // Assert
        var row = Sut.Catches.Single();
        row.StatusText.Should().Be(Text["Import_Confirmed"]);
        row.Proposal.Weight.Should().Be(1.2m);
        row.Proposal.Length.Should().Be(30m);
        row.Proposal.Method.Id.Should().Be(OtherMethodId);
        row.Proposal.Species.Id.Should().Be(SpeciesId);
        Sut.CanContinue.Should().BeTrue();
        Sut.HasContinueHint.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRequireConfirmationAgainWhenTheCaughtOnDateChanges()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var row = Sut.Catches.Single();
        Sut.ConfirmCatch(row.Proposal.Id);

        // Act
        await Sut.ConfirmCaughtOnAsync(row.Proposal.Id, row.CaughtOnLocal.AddDays(1), CancellationToken.None);

        // Assert
        Sut.Catches.Single().StatusText.Should().Be(Text["Import_NeedsConfirmation"]);
        Sut.CanContinue.Should().BeFalse();
        Sut.ContinueHint.Should().Be("Confirm catch 1 to continue.");
    }

    [Fact]
    public async Task ItShouldShowTheChosenTripDecision()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        Sut.ConfirmCatch(Sut.Catches.Single().Proposal.Id);
        MockTripProposals.Propose(Arg.Any<ImportBatchModel>())
            .Returns(callInfo => new ImportTripProposalService().Propose(callInfo.Arg<ImportBatchModel>()));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.DecideCreateTrip(Sut.Trips.Single().Proposal.Id);

        // Assert
        var trip = Sut.Trips.Should().ContainSingle().Subject;
        trip.IsCreateNew.Should().BeTrue();
        trip.DecisionText.Should().Be(Text["Import_TripDecisionCreate"]);
        Sut.CanContinue.Should().BeTrue();
        Sut.HasContinueHint.Should().BeFalse();
    }
}
