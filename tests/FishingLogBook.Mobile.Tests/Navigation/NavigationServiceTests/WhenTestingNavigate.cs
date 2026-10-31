using AwesomeAssertions;
using FishingLogBook.Mobile.Navigation;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Navigation.NavigationServiceTests;

public class WhenTestingNavigate : BaseNavigationServiceTest
{
    [Fact]
    public async Task ItShouldOpenTheExistingCatchEditorWithTheSelectedCatchId()
    {
        // Arrange
        var catchId = Catch.Id;

        // Act
        await Sut.NavigateAsync(MobileRouteEnum.CatchEdit, catchEditId: catchId);

        // Assert
        await MockNavigator.Received(1).GoToAsync("catch-edit", null, catchId, CancellationToken.None);
    }

    [Theory]
    [InlineData(MobileRouteEnum.CatchDetail)]
    [InlineData(MobileRouteEnum.PhotoPreview)]
    public async Task ItShouldRejectMissingCatchBeforeChangingTheStack(MobileRouteEnum route)
    {
        // Arrange
        var action = () => Sut.NavigateAsync(route);
        // Act
        var assertion = await action.Should().ThrowAsync<ArgumentException>();
        // Assert
        assertion.Which.ParamName.Should().Be("item");
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectUnknownRoutes()
    {
        // Arrange
        var action = () => Sut.NavigateAsync((MobileRouteEnum)99);
        // Act
        var assertion = await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        // Assert
        assertion.Which.ParamName.Should().Be("route");
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSuppressRapidDuplicateNavigation()
    {
        // Arrange
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNavigator.GoToAsync("catch-detail", Catch, null, CancellationToken.None).Returns(pending.Task);
        // Act
        var first = Sut.NavigateAsync(MobileRouteEnum.CatchDetail, Catch);
        await Sut.NavigateAsync(MobileRouteEnum.CatchDetail, Catch);
        pending.SetResult();
        await first;
        // Assert
        await MockNavigator.Received(1).GoToAsync("catch-detail", Catch, null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotPushTheSameVisibleDestinationAgain()
    {
        // Arrange
        MockNavigator.CurrentPath.Returns("//main/catches/catches-root/catch-detail");
        // Act
        await Sut.NavigateAsync(MobileRouteEnum.CatchDetail, Catch);
        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReleaseTheTransitionAfterFailure()
    {
        // Arrange
        MockNavigator.GoToAsync("catch-detail", Catch, null, CancellationToken.None).Returns(Task.FromException(new InvalidOperationException("Transition failed")), Task.CompletedTask);
        var action = () => Sut.NavigateAsync(MobileRouteEnum.CatchDetail, Catch);
        // Act
        await action.Should().ThrowAsync<InvalidOperationException>();
        await Sut.NavigateAsync(MobileRouteEnum.CatchDetail, Catch);
        // Assert
        await MockNavigator.Received(2).GoToAsync("catch-detail", Catch, null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotNavigateWhenCancelled()
    {
        // Arrange
        var token = new CancellationToken(true);
        var action = () => Sut.NavigateAsync(MobileRouteEnum.CatchDetail, Catch, cancellationToken: token);
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepMissingPhotosOutOfThePreviewStack()
    {
        // Arrange
        var item = new FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel(
            new FishingLogBook.Shared.Dtos.CatchDto(Guid.NewGuid(), DateTimeOffset.UtcNow, []),
            new FishingLogBook.Mobile.Localization.UiTextService());
        // Act
        await Sut.NavigateAsync(MobileRouteEnum.PhotoPreview, item);
        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MobileRouteEnum.CatchDetail, "catch-detail")]
    [InlineData(MobileRouteEnum.PhotoPreview, "photo-preview")]
    [InlineData(MobileRouteEnum.Settings, "//main/settings")]
    [InlineData(MobileRouteEnum.Trips, "//main/trips")]
    [InlineData(MobileRouteEnum.Explore, "//main/explore")]
    public async Task ItShouldPassTheDestinationAndSameCatchToTheNativeBoundary(MobileRouteEnum route, string path)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        // Act
        await Sut.NavigateAsync(route, Catch, cancellationToken: cancellation.Token);
        // Assert
        await MockNavigator.Received(1).GoToAsync(path, Catch, null, cancellation.Token);
    }
}
