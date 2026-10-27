using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public class WhenTestingCancelSignIn : BaseAuthenticationServiceTest
{
    [Fact]
    public async Task ItShouldClearThePendingTransactionSoItCannotBeResumed()
    {
        // Arrange
        var pending = CreatePending();
        MockOidcClient.PrepareLoginAsync(CancellationToken.None).Returns(pending);
        (await Sut.BeginSignInAsync(CancellationToken.None)).Should().Be(AuthenticationResultEnum.Pending);
        // Act
        await Sut.CancelSignInAsync();
        // Assert
        Sut.HasPendingAuthorization.Should().BeFalse();
        await MockStore.Received(2).RemovePendingAuthorizationAsync();
    }

    [Fact]
    public async Task ItShouldReportCancellationWhenAPendingSignInIsAbandoned()
    {
        // Arrange
        var results = new List<AuthenticationResultEnum>();
        Sut.SignInCompleted += (_, result) => results.Add(result);
        MockOidcClient.PrepareLoginAsync(CancellationToken.None).Returns(CreatePending());
        await Sut.BeginSignInAsync(CancellationToken.None);
        // Act
        await Sut.CancelSignInAsync();
        // Assert
        results.Should().Equal(AuthenticationResultEnum.Cancelled);
    }

    [Fact]
    public async Task ItShouldNotReportCancellationWhenNoSignInIsPending()
    {
        // Arrange
        var results = new List<AuthenticationResultEnum>();
        Sut.SignInCompleted += (_, result) => results.Add(result);
        // Act
        await Sut.CancelSignInAsync();
        // Assert
        results.Should().BeEmpty();
    }
}
