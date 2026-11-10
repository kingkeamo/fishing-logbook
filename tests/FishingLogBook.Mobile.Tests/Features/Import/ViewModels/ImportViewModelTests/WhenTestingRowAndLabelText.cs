using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingRowAndLabelText : BaseImportCatchActionsTest
{
    private async Task PickAsync(params ImportSelectedPhotoModel[] photos)
    {
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
                [.. photos.Select((_, index) => new PhotographPickedFileModel
                {
                    FullPath = $"photo-{index}.jpg",
                    ContentType = "image/jpeg"
                })]));
        MockPreparation.PrepareSelectionAsync(Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(), Arg.Any<CancellationToken>())
            .Returns(photos);
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        await Sut.PickPhotosAsync(CancellationToken.None);
    }

    private static ImportSelectedPhotoModel NotReady(ImportPhotoPreparationStatusEnum status, Guid id, int index)
    {
        var photo = new ImportSelectedPhotoModel(id, index, "image/jpeg", 100, null, $"p{index}.jpg", null);
        photo.SetPreparation(status);
        return photo;
    }

    [Theory]
    [InlineData(ImportPhotoPreparationStatusEnum.UnsupportedType, "Import_PhotoUnsupported")]
    [InlineData(ImportPhotoPreparationStatusEnum.TooLarge, "Import_PhotoTooLarge")]
    [InlineData(ImportPhotoPreparationStatusEnum.Cancelled, "Import_PhotoCancelled")]
    [InlineData(ImportPhotoPreparationStatusEnum.PreparationFailed, "Import_PhotoFailed")]
    public async Task ItShouldExplainWhyAPhotoCouldNotBePrepared(ImportPhotoPreparationStatusEnum status, string key)
    {
        // Arrange
        await PickAsync(NotReady(status, FirstPhotoId, 0));

        // Act
        var row = Sut.Photos.Single();

        // Assert
        row.StatusText.Should().Be(Text[key]);
        Sut.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowAReadyPhotoAsReady()
    {
        // Arrange
        await PickAsync(PhotoAt(FirstPhotoId, 0, 0));

        // Act
        var row = Sut.Photos.Single();

        // Assert
        row.StatusText.Should().Be(Text["Import_PhotoReady"]);
        row.DuplicateText.Should().BeEmpty();
        row.HasWarning.Should().BeFalse();
        row.RequiresDuplicateDecision.Should().BeFalse();
    }

    [Theory]
    [InlineData(ImportMetadataStatusEnum.Unavailable)]
    [InlineData(ImportMetadataStatusEnum.Failed)]
    public async Task ItShouldWarnWhenAReadyPhotoHasNoUsableMetadata(ImportMetadataStatusEnum status)
    {
        // Arrange
        var photo = PhotoAt(FirstPhotoId, 0, 0);
        photo.SetMetadata(status, ImportTimestampModel.Missing(), new ImportLocationModel(null, null, false), "unreadable");
        await PickAsync(photo);

        // Act
        var row = Sut.Photos.Single();

        // Assert
        row.StatusText.Should().Be(Text["Import_MetadataUnavailable"]);
    }

    [Fact]
    public async Task ItShouldLabelAnExactDuplicateAndRequireADecision()
    {
        // Arrange
        var first = PhotoAt(FirstPhotoId, 0, 0);
        var second = PhotoAt(SecondPhotoId, 1, 1);
        first.SetDuplicateState(ImportDuplicateStatusEnum.Duplicate, ImportDuplicateReasonEnum.IdenticalPreparedBytes, [SecondPhotoId]);
        second.SetDuplicateState(ImportDuplicateStatusEnum.Duplicate, ImportDuplicateReasonEnum.IdenticalPreparedBytes, [FirstPhotoId]);
        await PickAsync(first, second);

        // Act
        var row = Sut.Photos.First();

        // Assert
        row.DuplicateText.Should().Be(Text["Import_DuplicateExact"]);
        row.RequiresDuplicateDecision.Should().BeTrue();
        Sut.CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotRequireADecisionWhenTheExactDuplicateCounterpartWasRemoved()
    {
        // Arrange
        var first = PhotoAt(FirstPhotoId, 0, 0);
        var second = PhotoAt(SecondPhotoId, 1, 1);
        first.SetDuplicateState(ImportDuplicateStatusEnum.Duplicate, ImportDuplicateReasonEnum.IdenticalPreparedBytes, [SecondPhotoId]);
        second.SetDuplicateState(ImportDuplicateStatusEnum.Duplicate, ImportDuplicateReasonEnum.IdenticalPreparedBytes, [FirstPhotoId]);
        await PickAsync(first, second);

        // Act
        await Sut.RemovePhotoAsync(SecondPhotoId, CancellationToken.None);

        // Assert
        Sut.Photos.Single().RequiresDuplicateDecision.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldLabelAPossibleDuplicateAsAWarning()
    {
        // Arrange
        var first = PhotoAt(FirstPhotoId, 0, 0);
        first.SetDuplicateState(ImportDuplicateStatusEnum.Warning, ImportDuplicateReasonEnum.SameCaptureTimeAndSize, [SecondPhotoId]);
        await PickAsync(first);

        // Act
        var row = Sut.Photos.Single();

        // Assert
        row.DuplicateText.Should().Be(Text["Import_DuplicatePossible"]);
        row.HasWarning.Should().BeTrue();
        row.RequiresDuplicateDecision.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepAnExactDuplicateWhenTheAnglerChoosesTo()
    {
        // Arrange
        var first = PhotoAt(FirstPhotoId, 0, 0);
        var second = PhotoAt(SecondPhotoId, 1, 1);
        first.SetDuplicateState(ImportDuplicateStatusEnum.Duplicate, ImportDuplicateReasonEnum.IdenticalPreparedBytes, [SecondPhotoId]);
        second.SetDuplicateState(ImportDuplicateStatusEnum.Duplicate, ImportDuplicateReasonEnum.IdenticalPreparedBytes, [FirstPhotoId]);
        await PickAsync(first, second);

        // Act
        Sut.KeepDuplicate(FirstPhotoId);
        Sut.KeepDuplicate(SecondPhotoId);

        // Assert
        Sut.Photos.Should().OnlyContain(row => !row.RequiresDuplicateDecision);
        Sut.CanContinue.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowALeftBlankTimestampAsMissingAndAskForTheDate()
    {
        // Arrange
        MockLocalTime.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        var photo = PhotoAt(FirstPhotoId, 0, 0);
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            ImportTimestampModel.Missing(),
            new ImportLocationModel(null, null, false));
        await ReviewPreparedPhotosAsync(photo);

        // Act
        var row = Sut.Catches.Single();

        // Assert
        row.TimestampText.Should().Be(Text["Import_TimestampMissing"]);
        Sut.Message.Should().Be(Text["Import_CaughtOnRequired"]);
        Sut.ContinueHint.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(ImportLocationDecisionEnum.Undecided, "Import_LocationAvailable")]
    [InlineData(ImportLocationDecisionEnum.Accepted, "Import_LocationAccepted")]
    [InlineData(ImportLocationDecisionEnum.Removed, "Import_LocationRemoved")]
    public async Task ItShouldDescribeTheLocationDecision(ImportLocationDecisionEnum decision, string key)
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, new ImportLocationModel(53.35, -6.26, true)));
        var catchId = Sut.Catches.Single().Proposal.Id;
        var location = Sut.Catches.Single().Proposal.Location!;
        if (decision == ImportLocationDecisionEnum.Accepted)
        {
            Sut.AcceptLocation(catchId, location);
        }
        else if (decision == ImportLocationDecisionEnum.Removed)
        {
            Sut.RemoveLocation(catchId);
        }

        // Act
        var text = Sut.Catches.Single().LocationText;

        // Assert
        text.Should().Be(Text[key]);
    }

    [Fact]
    public async Task ItShouldShowTheReviewedPlaceNameAsTheLocation()
    {
        // Arrange
        var location = new ImportLocationModel(53.35, -6.26, true)
            .WithLookup(ImportLocationLookupStatusEnum.Resolved, new ImportLocationLookupResultModel(["Dublin", "Ireland"]));
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, location));

        // Act
        var text = Sut.Catches.Single().LocationText;

        // Assert
        text.Should().Be("Dublin, Ireland");
    }

    [Fact]
    public async Task ItShouldSayThereIsNoLocationWhenThePhotoHasNone()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0));

        // Act
        var row = Sut.Catches.Single();

        // Assert
        row.LocationText.Should().Be(Text["Import_LocationUnavailable"]);
        row.LocationHelp.Should().Be(Text["Import_LocationUnavailableHelp"]);
        row.HasLocationHelp.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldAskForAReviewWhenPhotosInOneCatchDisagreeAboutTheLocation()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(
            PhotoAt(FirstPhotoId, 0, 0, new ImportLocationModel(53.35, -6.26, true)),
            PhotoAt(SecondPhotoId, 1, 1, new ImportLocationModel(51.89, -8.47, true)));

        // Act
        var row = Sut.Catches.Single();

        // Assert
        row.LocationText.Should().Be(Text["Import_LocationNeedsReview"]);
        row.Locations.Should().HaveCount(2);
        row.StatusText.Should().NotBe(Text["Import_Confirmed"]);
    }

    [Fact]
    public async Task ItShouldFormatImperialUnitsForAnEnteredWeightAndLength()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0));
        var catchId = Sut.Catches.Single().Proposal.Id;
        var weight = Sut.CreateWeightEditor(catchId);
        weight.SetPounds(3);
        weight.SetOunces(4);
        var length = Sut.CreateLengthEditor(catchId);
        length.SetExactText("20");
        Sut.CompleteWeightEdit(catchId, true, weight);
        Sut.CompleteLengthEdit(catchId, true, length);

        // Act
        var row = Sut.Catches.Single();

        // Assert
        row.WeightText.Should().Contain(Text["MeasureUnitLb"]);
        row.LengthText.Should().Contain(Text["MeasureUnitIn"]);
    }

    [Fact]
    public async Task ItShouldFormatMetricUnitsForAnEnteredWeightAndLength()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0));
        var catchId = Sut.Catches.Single().Proposal.Id;
        var weight = Sut.CreateWeightEditor(catchId);
        weight.SetExactText("3");
        var length = Sut.CreateLengthEditor(catchId);
        length.SetExactText("40");
        Sut.CompleteWeightEdit(catchId, true, weight);
        Sut.CompleteLengthEdit(catchId, true, length);

        // Act
        var row = Sut.Catches.Single();

        // Assert
        row.WeightText.Should().Contain(Text["MeasureUnitKg"]);
        row.LengthText.Should().Contain(Text["MeasureUnitCm"]);
    }

    [Fact]
    public async Task ItShouldTitleASingleCatchTripAndAMultiCatchTripDifferently()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0));
        Sut.ConfirmCatch(Sut.Catches.Single().Proposal.Id);
        MockTripProposals.Propose(Arg.Any<ImportBatchModel>())
            .Returns(call => new ImportTripProposalService().Propose(call.Arg<ImportBatchModel>()));
        await Sut.ContinueAsync(CancellationToken.None);

        // Act
        var single = Sut.Trips.Single();

        // Assert
        single.Title.Should().Be(Text["Import_TripSingleCatch"]);
        single.HasMissingGps.Should().BeTrue();
        single.CreateText.Should().Be(Text["Import_CreateTrip"]);
    }

    [Fact]
    public async Task ItShouldTitleATripWithSeveralCatchesByTheirCount()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 20));
        foreach (var row in Sut.Catches.ToArray())
        {
            Sut.ConfirmCatch(row.Proposal.Id);
        }

        MockTripProposals.Propose(Arg.Any<ImportBatchModel>())
            .Returns(call => new ImportTripProposalService().Propose(call.Arg<ImportBatchModel>()));
        await Sut.ContinueAsync(CancellationToken.None);

        // Act
        var trip = Sut.Trips.Single();

        // Assert
        trip.Catches.Should().HaveCount(2);
        trip.Title.Should().Be(string.Format(Text.Culture, Text["Import_TripCatchCount"], 2));
    }

    [Theory]
    [InlineData("Sam", "sam@example.com", "Sam")]
    [InlineData(null, "sam@example.com", "sam@example.com")]
    [InlineData(null, null, "TripContributorUnknown")]
    public async Task ItShouldNameAnInvitedParticipantByDisplayNameThenEmailThenUnknown(
        string? displayName,
        string? email,
        string expected)
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        MockModals.ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>())
            .Returns(new AnglerSummaryDto(Guid.NewGuid(), displayName, null, null, email));

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        var participant = Sut.Trips.Single().Participants.Should().ContainSingle().Subject;
        participant.Name.Should().Be(expected == "TripContributorUnknown" ? Text[expected] : expected);
    }

    [Fact]
    public async Task ItShouldNotInviteAParticipantUnlessTheTripIsBeingCreated()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        await MockModals.DidNotReceive().ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>());
    }

    [Fact]
    public async Task ItShouldRequireAConnectionToInviteAParticipant()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        Sut.Message.Should().Be(Text["Import_OnlineRequired"]);
        await MockModals.DidNotReceive().ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>());
    }

    [Fact]
    public async Task ItShouldCountCatchesPhotosAndTripsInTheSavedSummary()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 20));
        foreach (var row in Sut.Catches.ToArray())
        {
            Sut.ConfirmCatch(row.Proposal.Id);
        }

        MockTripProposals.Propose(Arg.Any<ImportBatchModel>())
            .Returns(call => new ImportTripProposalService().Propose(call.Arg<ImportBatchModel>()));
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.DecideCreateTrip(Sut.Trips.Single().Proposal.Id);
        await Sut.ContinueAsync(CancellationToken.None);
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(new ImportPersistenceResultModel([Guid.NewGuid()], [Guid.NewGuid(), Guid.NewGuid()], 2, 0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.SummaryCatches.Should().Contain("2");
        Sut.SummaryPhotos.Should().Contain("2");
        Sut.SummaryNewTrips.Should().Contain("1");
        Sut.StepDescription.Should().Contain("2");
    }

    [Theory]
    [InlineData(ImportPersistenceStageEnum.SavingTrip, "Import_ProgressSavingTrip")]
    [InlineData(ImportPersistenceStageEnum.SavingCatch, "Import_ProgressSavingCatch")]
    [InlineData(ImportPersistenceStageEnum.UploadingPhotograph, "Import_ProgressUploadingPhotograph")]
    [InlineData(ImportPersistenceStageEnum.Verifying, "Import_ProgressVerifying")]
    public async Task ItShouldDescribeEachPersistenceProgressStage(ImportPersistenceStageEnum stage, string key)
    {
        // Arrange
        await ReachConfirmAsync();
        string? text = null;
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(call =>
            {
                call.Arg<IProgress<ImportPersistenceProgressModel>>().Report(new ImportPersistenceProgressModel(stage, 1, 3));
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (Sut.ProgressText == Text["Import_Importing"] && DateTime.UtcNow < deadline)
                {
                    Thread.Yield();
                }

                text = Sut.ProgressText;
                return new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0);
            });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        text.Should().Contain(Text[key].Split('{')[0]);
    }

    [Fact]
    public async Task ItShouldShowAGenericImportingLabelBeforeAnyProgressIsReported()
    {
        // Arrange
        await ReachConfirmAsync();

        // Act
        var text = Sut.ProgressText;

        // Assert
        text.Should().Be(Text["Import_Importing"]);
    }
}
