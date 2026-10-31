using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Photographs.Enums;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public sealed class WhenTestingLabelsOptionsAndFailures : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldExposeRecordCatchLabelsOptionsAndMoreChip()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());

        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Assert
        Sut.Text.Should().BeSameAs(Text);
        Sut.PageTitle.Should().Be(Text["RecordCatch"]);
        Sut.SaveButtonText.Should().Be(Text["Save"]);
        Sut.IsCatalogueUnavailable.Should().BeFalse();
        Sut.CatalogueMethodNames.Should().Contain(["Fly", "Spinning"]);
        Sut.CatalogueSpeciesNames.Should().Contain(["Salmon", "Brown Trout"]);
        Sut.MethodChipRow.Should().Contain(option => option.Name == "Fly" && option.IsSelected);
        Sut.MethodChipRow.Last().Code.Should().Be(CatchEditViewModel.MoreOptionCode);
        Sut.MethodChipRow.Last().Name.Should().Be(Text["More"]);
        Sut.SpeciesChipRow.Should().Contain(option => option.Name == "Salmon" && option.IsSelected);
        Sut.SpeciesChipRow.Last().Code.Should().Be(CatchEditViewModel.MoreOptionCode);
        Sut.AnglerOptions.Should().ContainSingle(option => option.Code == "me" && option.Name == Text["CatchAnglerMe"] && option.IsSelected);
        Sut.WeightSummary.Should().Be(Text["MeasureNotRecorded"]);
        Sut.LengthSummary.Should().Be(Text["MeasureNotRecorded"]);
        Sut.IsMethodSelected("Fly").Should().BeTrue();
        Sut.IsSpeciesSelected("Salmon").Should().BeTrue();
        Sut.ShowAnglerSection.Should().BeTrue();
        Sut.ShowTripDeferredNote.Should().BeTrue();
        Sut.ShowLocationAction.Should().BeTrue();
        Sut.LocationActionText.Should().Be(Text["CatchCaptureLocation"]);
    }

    [Fact]
    public async Task ItShouldSwitchVisibleLabelsToFrench()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        Sut.PageTitle.Should().Be(Text["RecordCatch"]);
        Sut.MethodChipRow.Last().Name.Should().Be(Text["More"]);
        Sut.AnglerOptions.Single().Name.Should().Be(Text["CatchAnglerMe"]);
        Sut.WeightSummary.Should().Be(Text["MeasureNotRecorded"]);
    }

    [Fact]
    public async Task ItShouldShowEditCatchTitleWhenOpeningASavedCatch()
    {
        // Arrange
        var catchId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            new LocalCatchRecordModel
            {
                Id = catchId,
                UserId = OwnerUserId,
                CaughtByUserId = OwnerUserId,
                RecordedByUserId = OwnerUserId,
                CaughtOn = Now,
                Method = "Fly",
                SpeciesName = "Salmon",
                Revision = 1,
                Lifecycle = LocalCatchLifecycleEnum.Saved,
                CreatedOn = Now,
                UpdatedOn = Now,
                Photographs =
                [
                    new LocalCatchPhotographModel
                    {
                        Id = Guid.NewGuid(),
                        CatchId = catchId,
                        UserId = OwnerUserId,
                        SortOrder = 0,
                        LocalFilePath = "a.jpg",
                        ContentType = "image/jpeg",
                        CreatedOn = Now
                    }
                ]
            });

        // Act
        await Sut.InitializeAsync(catchId, CancellationToken.None);

        // Assert
        Sut.PageTitle.Should().Be(Text["EditCatch"]);
        Sut.ShowDraftRestored.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldMarkTheCatalogueUnavailableWhenItHasNoMethods()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(
            new AnglerPreferencesModel(
                new FishingCatalogueDto([], []),
                new FishingPreferencesDto([]),
                new FishingLocationPreferencesDto([])));

        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Assert
        Sut.IsCatalogueUnavailable.Should().BeTrue();
        Sut.MethodOptions.Should().BeEmpty();
        Sut.SpeciesOptions.Should().BeEmpty();
        Sut.MethodChipRow.Should().ContainSingle(option => option.Code == CatchEditViewModel.MoreOptionCode);
    }

    [Fact]
    public async Task ItShouldChooseASpeciesChipAndRequestThePickerForMore()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        var selected = Sut.ChooseSpecies(CatchChipOptionModel.FromValue("Brown Trout"));
        var more = Sut.ChooseSpecies(new CatchChipOptionModel(CatchEditViewModel.MoreOptionCode, Text["More"]));

        // Assert
        selected.Should().BeFalse();
        more.Should().BeTrue();
        Sut.SpeciesOptions.Should().Contain(option => option.Name == "Brown Trout" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldIgnoreAnUnknownPickerSelection()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var method = Sut.MethodOptions.Single(option => option.IsSelected).Name;

        // Act
        Sut.CompleteMethodPicker(new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));
        Sut.CompleteSpeciesPicker(new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));

        // Assert
        Sut.MethodOptions.Should().Contain(option => option.Name == method && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldFormatImperialWeightAndLengthSummaries()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        await Sut.InitializeAsync(null, CancellationToken.None);
        var weight = Sut.CreateWeightEditor();
        weight.SetPounds(2);
        weight.SetOunces(8);
        Sut.CompleteWeightEdit(true, weight);
        var length = Sut.CreateLengthEditor();
        length.SetExactText("12");
        Sut.CompleteLengthEdit(true, length);

        // Act
        var weightSummary = Sut.WeightSummary;
        var lengthSummary = Sut.LengthSummary;

        // Assert
        weightSummary.Should().Contain(Text["MeasureUnitLb"]);
        lengthSummary.Should().Contain(Text["MeasureUnitIn"]);
        lengthSummary.Should().Contain("12");
    }

    [Fact]
    public async Task ItShouldShowSavingLabelWhilePersistIsInFlight()
    {
        // Arrange
        string? saving = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saving = Sut.SaveButtonText;
                return LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1);
            });
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        saving.Should().Be(Text["CatchSaving"]);
        Sut.StatusText.Should().Be(Text["CatchSavedLocally"]);
    }

    [Fact]
    public async Task ItShouldSurfaceEditNotFoundAndStoreFailures()
    {
        // Arrange
        var missingId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        MockCatchStore.GetAsync(missingId, Arg.Any<CancellationToken>()).Returns((LocalCatchRecordModel?)null);
        await Sut.InitializeAsync(missingId, CancellationToken.None);
        Sut.ValidationText.Should().Be(Text["CatchEditNotFound"]);

        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.StorageFailed));

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.StatusText.Should().Be(Text["CatchSaveFailed"]);
        Sut.HasStatus.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRejectAnInvalidWeight()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        var weight = Sut.CreateWeightEditor();
        weight.SetExactText("1001");
        Sut.CompleteWeightEdit(true, weight);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.WeightSummary.Should().Contain("1001");
        Sut.ValidationKey.Should().Be("CatchWeightInvalid");
        Sut.ValidationText.Should().Be(Text["CatchWeightInvalid"]);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnInvalidLength()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        var length = Sut.CreateLengthEditor();
        length.SetExactText("1001");
        Sut.CompleteLengthEdit(true, length);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchLengthInvalid");
        Sut.ValidationText.Should().Be(Text["CatchLengthInvalid"]);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectNotesThatAreTooLong()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        Sut.Notes = new string('n', CatchDetailConstants.MaxNotesLength + 1);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchTextTooLong");
        Sut.ValidationText.Should().Be(Text["CatchTextTooLong"]);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectBaitThatIsTooLong()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        Sut.BaitOrLure = new string('b', CatchDetailConstants.MaxBaitOrLureLength + 1);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchTextTooLong");
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMethodNameThatIsTooLong()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        Sut.SelectMethod(new string('m', CatchDetailConstants.MaxMethodLength + 1));

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchTextTooLong");
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectASpeciesNameThatIsTooLong()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        Sut.SelectSpecies(new string('s', CatchDetailConstants.MaxSpeciesNameLength + 1));

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchTextTooLong");
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAFutureCaughtOn()
    {
        // Arrange
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1));
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        Sut.CaughtOnLocal = Now.UtcDateTime.Date.AddDays(2);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchCaughtOnInvalid");
        Sut.ValidationText.Should().Be(Text["CatchCaughtOnInvalid"]);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDescribePhotoDateAndLocationConflicts()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Sut.Photographs.SetItems(
        [
            Photo(
                DateTimeOffset.Parse("2026-06-01T10:00:00Z"),
                53.3,
                -6.2),
            Photo(
                DateTimeOffset.Parse("2026-06-01T12:00:00Z"),
                54.5,
                -8.1)
        ]);

        // Assert
        Sut.ShowPhotoConflict.Should().BeTrue();
        Sut.ShowDateConflict.Should().BeTrue();
        Sut.PhotoConflictText.Should().Be(Text["CatchPhotoDateAndLocationConflict"]);
        Sut.CanSave.Should().BeFalse();
        Sut.ShowCaughtOn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldDescribeAPhotoDateConflict()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Sut.Photographs.SetItems(
        [
            Photo(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), null, null),
            Photo(DateTimeOffset.Parse("2026-06-01T12:00:00Z"), null, null)
        ]);

        // Assert
        Sut.ShowDateConflict.Should().BeTrue();
        Sut.PhotoConflictText.Should().Be(Text["CatchPhotoDateConflict"]);
        Sut.CanSave.Should().BeFalse();
        (await Sut.SaveAsync(CancellationToken.None)).Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchPhotoDateConflict");
        Sut.ValidationText.Should().Be(Text["CatchPhotoDateConflict"]);
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDescribeAPhotoLocationConflict()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var capturedOn = DateTimeOffset.Parse("2026-06-01T10:00:00Z");

        // Act
        Sut.Photographs.SetItems(
        [
            Photo(capturedOn, 53.3, -6.2),
            Photo(capturedOn, 54.5, -8.1)
        ]);

        // Assert
        Sut.ShowDateConflict.Should().BeFalse();
        Sut.ShowPhotoConflict.Should().BeTrue();
        Sut.PhotoConflictText.Should().Be(Text["CatchPhotoLocationConflict"]);
        Sut.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldClearADateConflictWhenTheAnglerChoosesCaughtOn()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            Photo(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), null, null),
            Photo(DateTimeOffset.Parse("2026-06-01T12:00:00Z"), null, null)
        ]);
        Sut.CanSave.Should().BeFalse();

        // Act
        Sut.CaughtOnLocal = Now.UtcDateTime.Date;

        // Assert
        Sut.ShowDateConflict.Should().BeFalse();
        Sut.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowLocationNotRecordedAndTryAgainLabels()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.LocationCaption.Should().Be(Text["CatchLocationNotRecorded"]);
        Sut.LocationActionText.Should().Be(Text["CatchCaptureLocation"]);
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new FishingLogBook.Mobile.Features.Onboarding.Models.DeviceLocationCaptureModel(
                FishingLogBook.Mobile.Features.Onboarding.Enums.LocationCaptureOutcomeEnum.Denied,
                null,
                null,
                null,
                null));

        // Act
        await Sut.CaptureLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationActionText.Should().Be(Text["CatchLocationTryAgain"]);
        Sut.HasLocation.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldUseCatalogueMethodsWhenTheAnglerHasNoPreferredMethods()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Empty());

        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Assert
        Sut.IsCatalogueUnavailable.Should().BeFalse();
        Sut.MethodOptions.Should().Contain(option => option.Name == "Fly" && option.IsSelected);
        Sut.MethodChipRow.Last().Name.Should().Be(Text["More"]);
        Sut.SpeciesOptions.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldIgnoreANullSpeciesChip()
    {
        // Act
        var needsPicker = Sut.ChooseSpecies(null);

        // Assert
        needsPicker.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSurfaceNotSignedInWhenTheStoreRejectsTheSave()
    {
        // Arrange
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.NotSignedIn));
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.StatusText.Should().Be(Text["CatchSaveNotSignedIn"]);
        Sut.HasStatus.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepBaitNotesAndCaughtOnWhenTheValueDoesNotChange()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.BaitOrLure = "nymph";
        Sut.Notes = "left bank";
        var date = Sut.CaughtOnLocal;
        var time = Sut.CaughtOnTime;

        // Act
        Sut.BaitOrLure = "nymph";
        Sut.Notes = "left bank";
        Sut.CaughtOnLocal = date;
        Sut.CaughtOnTime = time;
        Sut.SelectMethod("Fly");

        // Assert
        Sut.BaitOrLure.Should().Be("nymph");
        Sut.Notes.Should().Be("left bank");
        Sut.CaughtOnLocal.Should().Be(date);
        Sut.CaughtOnTime.Should().Be(time);
    }

    [Fact]
    public async Task ItShouldUpdateCaughtOnTimeAndAcceptNullTextFields()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Sut.CaughtOnTime = TimeSpan.FromHours(8);
        Sut.Notes = null!;
        Sut.BaitOrLure = null!;

        // Assert
        Sut.CaughtOnTime.Should().Be(TimeSpan.FromHours(8));
        Sut.Notes.Should().BeEmpty();
        Sut.BaitOrLure.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldTreatMissingPreferencesAsAnUnavailableCatalogue()
    {
        // Act
        var unavailable = Sut.IsCatalogueUnavailable;
        var methods = Sut.CatalogueMethodNames;
        var species = Sut.CatalogueSpeciesNames;
        var methodOptions = Sut.MethodOptions;
        var speciesOptions = Sut.SpeciesOptions;

        // Assert
        unavailable.Should().BeTrue();
        methods.Should().BeEmpty();
        species.Should().BeEmpty();
        methodOptions.Should().BeEmpty();
        speciesOptions.Should().BeEmpty();
        Sut.StatusText.Should().BeEmpty();
        Sut.ValidationText.Should().BeEmpty();
        Sut.CreateMethodPicker().Options.Should().BeEmpty();
        Sut.CreateSpeciesPicker().Options.Should().BeEmpty();
        Sut.ApplyMethodPicker(new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));
        Sut.ApplySpeciesPicker(new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));
        Sut.MethodOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRefreshLabelsWhenCultureChanges()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var raised = 0;
        Sut.PropertyChanged += (_, _) => raised++;

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        raised.Should().BeGreaterThan(0);
        Sut.PageTitle.Should().Be(Text["RecordCatch"]);
        Sut.MethodChipRow.Last().Name.Should().Be(Text["More"]);
        Sut.LocationCaption.Should().Be(Text["CatchLocationNotRecorded"]);
    }

    [Fact]
    public async Task ItShouldPersistBlankNotesAsClearedAndHidePhotoConflictAfterSave()
    {
        // Arrange
        LocalCatchSaveArgs? args = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                args = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(args.Id, 1);
            });
        await Sut.InitializeAsync(null, CancellationToken.None);
        var capturedOn = DateTimeOffset.Parse("2026-06-01T10:00:00Z");
        Sut.Photographs.SetItems(
        [
            Photo(capturedOn, 53.3, -6.2),
            Photo(capturedOn, 54.5, -8.1)
        ]);
        Sut.Notes = "   ";
        Sut.ShowPhotoConflict.Should().BeTrue();

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        args.Should().NotBeNull();
        args!.Notes.Should().BeNull();
        Sut.ShowPhotoConflict.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotCaptureLocationAfterSave()
    {
        // Arrange
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1));
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        (await Sut.SaveAsync(CancellationToken.None)).Should().BeTrue();

        // Act
        await Sut.CaptureLocationAsync(CancellationToken.None);

        // Assert
        Sut.HasLocation.Should().BeFalse();
        await MockLocation.DidNotReceive().CaptureCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatALostSessionDuringSaveAsNotSignedIn()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        MockSessionCoordinator.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.NotSignedIn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldDisposeTwiceWithoutThrowing()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        var act = () =>
        {
            Sut.Dispose();
            Sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ItShouldDisposeAfterADraftWasScheduled()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Notes = "draft me";

        // Act
        var act = () => Sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    private void AddPhoto()
    {
        Sut.Photographs.SetItems([Photo(null, null, null)]);
    }

    private static PhotographEditorItemModel Photo(DateTimeOffset? capturedOn, double? latitude, double? longitude)
    {
        return new PhotographEditorItemModel
        {
            Id = Guid.NewGuid(),
            SortOrder = 0,
            PreviewPath = "a.jpg",
            RelativeFilePath = "a.jpg",
            ContentType = "image/jpeg",
            Metadata = capturedOn is null
                ? PhotographMetadataModel.Empty
                : new PhotographMetadataModel(
                    capturedOn,
                    latitude,
                    longitude,
                    PhotographCapturedOnSourceEnum.ExifOriginal)
        };
    }
}
