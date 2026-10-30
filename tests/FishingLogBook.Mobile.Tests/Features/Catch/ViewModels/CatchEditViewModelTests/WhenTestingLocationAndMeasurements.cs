using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public class WhenTestingLocationAndMeasurements : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldStoreDeviceLocationAsPrivateWithConsentVersionOne()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(
            new DeviceLocationCaptureModel(
                LocationCaptureOutcomeEnum.Captured,
                53.3498,
                -6.2603,
                12.5,
                Now));
        LocalCatchSaveArgs? args = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                args = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(args.Id, 1);
            });
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg"
            }
        ]);

        // Act
        await Sut.CaptureLocationAsync(CancellationToken.None);
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        args.Should().NotBeNull();
        args!.Location.Should().NotBeNull();
        args.Location!.Visibility.Should().Be(LocationDefaults.Private);
        args.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
        args.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        args.Location.Latitude.Should().Be(53.3498);
        args.Lifecycle.Should().Be(LocalCatchLifecycleEnum.Saved);
        Sut.HasLocation.Should().BeTrue();
        Sut.LocationCaption.Should().Contain("Private location");
    }

    [Fact]
    public async Task ItShouldPersistCanonicalWeightAndLengthFromTheMeasurementEditor()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var weightEditor = Sut.CreateWeightEditor();
        weightEditor.SetExactText("2.2");
        Sut.ApplyWeightEditor(weightEditor);
        var lengthEditor = Sut.CreateLengthEditor();
        lengthEditor.SetExactText("45");
        Sut.ApplyLengthEditor(lengthEditor);
        LocalCatchSaveArgs? args = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                args = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(args.Id, 1);
            });
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg"
            }
        ]);

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeTrue();
        args.Should().NotBeNull();
        args!.Weight.Should().BeApproximately(2.2, 0.001);
        args.Length.Should().BeApproximately(45, 0.001);
        Sut.WeightSummary.Should().Contain("2.2");
        Sut.LengthSummary.Should().Contain("45");
    }

    [Fact]
    public async Task ItShouldSurfaceLocationCaptureFailureWithoutClearingExistingFields()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Notes = "Still here";
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(
            new DeviceLocationCaptureModel(
                LocationCaptureOutcomeEnum.Denied,
                null,
                null,
                null,
                null));

        // Act
        await Sut.CaptureLocationAsync(CancellationToken.None);

        // Assert
        Sut.HasLocation.Should().BeFalse();
        Sut.Notes.Should().Be("Still here");
        Sut.HasStatus.Should().BeTrue();
        Sut.StatusText.Should().Be(Text["CatchLocationCaptureFailed"]);
    }
}
