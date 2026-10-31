using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public class WhenTestingLocationAndMeasurements : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldShowProgressAndIgnoreRepeatedLocationTapsUntilCaptureCompletes()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var capture = new TaskCompletionSource<DeviceLocationCaptureModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(capture.Task);
        var changes = new List<string?>();
        Sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        var first = Sut.CaptureLocationAsync(CancellationToken.None);
        await Sut.CaptureLocationAsync(CancellationToken.None);
        // Assert
        Sut.IsCapturingLocation.Should().BeTrue();
        Sut.CanCaptureLocation.Should().BeFalse();
        Sut.CanSave.Should().BeFalse();
        Sut.LocationStatusText.Should().Be(Text["CatchLocationCapturing"]);
        await MockLocation.Received(1).CaptureCurrentAsync(Arg.Any<CancellationToken>());
        // Act
        capture.SetResult(DeviceLocationCaptureModel.FromCoordinates(53.3, -6.2, 10, Now));
        await first;
        // Assert
        Sut.IsCapturingLocation.Should().BeFalse();
        Sut.CanCaptureLocation.Should().BeTrue();
        Sut.HasLocation.Should().BeTrue();
        Sut.LocationStatusText.Should().Be(Text["CatchLocationCaptured"]);
        Sut.LocationActionText.Should().Be(Text["CatchUpdateLocation"]);
        changes.Should().Contain(nameof(Sut.IsCapturingLocation)).And.Contain(nameof(Sut.CanCaptureLocation)).And.Contain(nameof(Sut.LocationStatusText));
    }

    [Theory]
    [InlineData(LocationCaptureOutcomeEnum.Denied, "CatchLocationPermissionDenied")]
    [InlineData(LocationCaptureOutcomeEnum.PermanentlyDenied, "CatchLocationPermissionBlocked")]
    [InlineData(LocationCaptureOutcomeEnum.Timeout, "CatchLocationTimedOut")]
    [InlineData(LocationCaptureOutcomeEnum.Unavailable, "CatchLocationCaptureFailed")]
    public async Task ItShouldShowAnInlineFailureAndAllowRetry(LocationCaptureOutcomeEnum outcome, string resourceKey)
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(DeviceLocationCaptureModel.WithoutCoordinates(outcome));
        // Act
        await Sut.CaptureLocationAsync(CancellationToken.None);
        // Assert
        Sut.HasLocationStatus.Should().BeTrue();
        Sut.LocationStatusText.Should().Be(Text[resourceKey]);
        Sut.CanCaptureLocation.Should().BeTrue();
        Sut.IsCapturingLocation.Should().BeFalse();
        await MockLocation.Received(1).CaptureCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearAPreviousFailureAfterASuccessfulRetry()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(
            DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Timeout),
            DeviceLocationCaptureModel.FromCoordinates(53.3, -6.2, 10, Now));
        await Sut.CaptureLocationAsync(CancellationToken.None);
        // Act
        await Sut.CaptureLocationAsync(CancellationToken.None);
        // Assert
        Sut.HasStatus.Should().BeFalse();
        Sut.LocationStatusText.Should().Be(Text["CatchLocationCaptured"]);
        await MockLocation.Received(2).CaptureCurrentAsync(Arg.Any<CancellationToken>());
    }

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

    [Fact]
    public async Task ItShouldApplyPhotoLocationWhenCoordinatesAgree()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var capturedOn = DateTimeOffset.Parse("2026-06-01T10:00:00Z");

        // Act
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(capturedOn, 53.3498, -6.2603)
            }
        ]);

        // Assert
        Sut.HasLocation.Should().BeTrue();
        Sut.LocationCaption.Should().Contain("53.3498");
        Sut.CaughtOnLocal.Date.Should().Be(capturedOn.UtcDateTime.Date);
    }

    [Fact]
    public async Task ItShouldKeepDeviceLocationWhenPhotosDisagree()
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
        await Sut.CaptureLocationAsync(CancellationToken.None);
        var caption = Sut.LocationCaption;

        // Act
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 54.5, -8.1)
            },
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                PreviewPath = "b.jpg",
                RelativeFilePath = "b.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:05:00Z"), 51.9, -8.5)
            }
        ]);

        // Assert
        Sut.HasLocation.Should().BeTrue();
        Sut.LocationCaption.Should().Be(caption);
    }

    [Fact]
    public async Task ItShouldClearPhotoLocationWhenLaterPhotosDisagree()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 53.3498, -6.2603)
            }
        ]);
        Sut.HasLocation.Should().BeTrue();

        // Act
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 53.3498, -6.2603)
            },
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                PreviewPath = "b.jpg",
                RelativeFilePath = "b.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:05:00Z"), 51.9, -8.5)
            }
        ]);

        // Assert
        Sut.HasLocation.Should().BeFalse();
        Sut.LocationCaption.Should().Be(Text["CatchLocationNotRecorded"]);
        Sut.ShowPhotoConflict.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowNotRecordedWhenDisplayConversionReturnsNothing()
    {
        // Arrange
        var catchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var mockMeasurement = Substitute.For<IMeasurementService>();
        mockMeasurement.ToDisplayWeight(Arg.Any<decimal?>(), Arg.Any<WeightUnitEnum>()).Returns((decimal?)null);
        mockMeasurement.ToDisplayLength(Arg.Any<decimal?>(), Arg.Any<LengthUnitEnum>()).Returns((decimal?)null);
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            new LocalCatchRecordModel
            {
                Id = catchId,
                UserId = OwnerUserId,
                CaughtByUserId = OwnerUserId,
                RecordedByUserId = OwnerUserId,
                CaughtOn = Now,
                Weight = 2d,
                Length = 40d,
                Method = "Fly",
                Revision = 1,
                Lifecycle = LocalCatchLifecycleEnum.Saved,
                CreatedOn = Now,
                UpdatedOn = Now,
                Photographs = []
            });
        var sut = CreateSut(mockMeasurement);

        // Act
        await sut.InitializeAsync(catchId, CancellationToken.None);

        // Assert
        sut.WeightSummary.Should().Be(Text["MeasureNotRecorded"]);
        sut.LengthSummary.Should().Be(Text["MeasureNotRecorded"]);
        mockMeasurement.Received(1).ToDisplayWeight(
            Arg.Is<decimal?>(value => value == 2m),
            WeightUnitEnum.Kg);
        mockMeasurement.Received(1).ToDisplayLength(
            Arg.Is<decimal?>(value => value == 40m),
            LengthUnitEnum.Cm);
        await MockCatchStore.Received(1).GetAsync(catchId, Arg.Any<CancellationToken>());
    }
}
