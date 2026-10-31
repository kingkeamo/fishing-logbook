using FishingLogBook.Mobile.Navigation;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Navigation.NavigationServiceTests;

public class WhenTestingGoBack : BaseNavigationServiceTest
{
    [Fact]
    public async Task ItShouldLeaveTheTabRootInPlace()
    {
        // Arrange
        MockNavigator.CanGoBack.Returns(false);
        // Act
        await Sut.GoBackAsync();
        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPopExactlyOneDestination()
    {
        // Arrange
        MockNavigator.CanGoBack.Returns(true);
        // Act
        await Sut.GoBackAsync();
        // Assert
        await MockNavigator.Received(1).GoToAsync("..", null, null, CancellationToken.None);
    }
}
