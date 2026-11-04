using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripPhotographServiceTests;

public sealed class WhenTestingAdd : BaseTripPhotographServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenNobodyIsSignedIn()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        // Act
        var failure = await Sut.AddAsync(Trip(), useCamera: true, CancellationToken.None);
        // Assert
        failure.Should().Be("TripPhotographAddFailed");
        await MockPhotographs.DidNotReceive().TakePhotoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingAndStaySilentWhenThePickerIsCancelled()
    {
        // Arrange
        MockPhotographs.ChooseFromGalleryAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged([], PhotographPreparationOutcomeEnum.Cancelled));
        // Act
        var failure = await Sut.AddAsync(Trip(), useCamera: false, CancellationToken.None);
        // Assert
        failure.Should().BeNull();
        await MockTrips.DidNotReceive().SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PhotographPreparationOutcomeEnum.PermissionDenied, "PhotoPermissionDenied")]
    [InlineData(PhotographPreparationOutcomeEnum.CameraPermissionDenied, "PhotoCameraAccessDenied")]
    [InlineData(PhotographPreparationOutcomeEnum.PhotoLibraryPermissionDenied, "PhotoLibraryAccessDenied")]
    [InlineData(PhotographPreparationOutcomeEnum.UnsupportedContentType, "PhotoUnsupportedFormat")]
    [InlineData(PhotographPreparationOutcomeEnum.TooLarge, "PhotoTooLarge")]
    [InlineData(PhotographPreparationOutcomeEnum.CouldNotPrepare, "PhotoPrepareFailed")]
    public async Task ItShouldExplainWhyThePhotographCouldNotBePrepared(PhotographPreparationOutcomeEnum outcome, string key)
    {
        // Arrange
        MockPhotographs.TakePhotoAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged([], outcome));
        // Act
        var failure = await Sut.AddAsync(Trip(), useCamera: true, CancellationToken.None);
        // Assert
        failure.Should().Be(key);
        await MockTrips.DidNotReceive().SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureWhenThePhotographCannotBeSavedToTheTrip()
    {
        // Arrange
        MockPhotographs.ChooseFromGalleryAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Updated([Prepared(Guid.NewGuid())]));
        MockTrips.SavePhotographAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripPhotographViewDto>(), Arg.Any<string?>(), false, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ArgumentException()));
        // Act
        var failure = await Sut.AddAsync(Trip(), useCamera: false, CancellationToken.None);
        // Assert
        failure.Should().Be("TripPhotographAddFailed");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldSaveThePreparedPhotographToTheTripAsTheViewerWithItsCaptureTimeAndDurableFile(bool useCamera)
    {
        // Arrange
        var photoId = Guid.NewGuid();
        var capturedOn = Now.AddMinutes(-20);
        var result = PhotographEditorActionResultModel.Updated([Prepared(photoId, capturedOn)]);
        MockPhotographs.TakePhotoAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>()).Returns(result);
        MockPhotographs.ChooseFromGalleryAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>()).Returns(result);
        // Act
        var failure = await Sut.AddAsync(Trip(), useCamera, CancellationToken.None);
        // Assert
        failure.Should().BeNull();
        await MockTrips.Received(1).SavePhotographAsync(
            Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId),
            Arg.Is<TripPhotographViewDto>(photo => photo.Id == photoId && photo.ContentType == "image/jpeg" && photo.ContributedByUserId == ViewerId
                && photo.AddedOn == Now && photo.CapturedOn == capturedOn),
            "photos/p.jpg",
            false,
            Arg.Any<CancellationToken>());
        await MockPhotographs.Received(useCamera ? 1 : 0).TakePhotoAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>());
        await MockPhotographs.Received(useCamera ? 0 : 1).ChooseFromGalleryAsync(ViewerId, Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), 1, Arg.Any<CancellationToken>());
    }
}
