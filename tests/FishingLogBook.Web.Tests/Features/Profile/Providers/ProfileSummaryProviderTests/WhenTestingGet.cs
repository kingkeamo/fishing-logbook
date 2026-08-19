using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.ProfileSummaryProviderTests;

public class WhenTestingGet : BaseProfileSummaryProviderTest
{
    [Fact]
    public async Task ItShouldReturnNothingWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.Empty);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.Should().BeSameAs(ProfileSummaryModel.Empty);
        await MockProfileClient.DidNotReceive().GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.HasPhotograph.Should().BeFalse();
        result.DisplayName.Should().BeNull();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFetchTheProfileAgainForASecondRead()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(OwnerUserId));
        await Sut.GetAsync(CancellationToken.None);

        // Act
        var second = await Sut.GetAsync(CancellationToken.None);

        // Assert
        second.PhotographUrl.Should().Be("https://cdn.test/photo.jpg");
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotServeAnotherAnglerRememberedSummary()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(OwnerUserId));
        var first = await Sut.GetAsync(CancellationToken.None);
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(Profile(OtherUserId, "Other Angler", "https://cdn.test/other.jpg"));

        // Act
        var second = await Sut.GetAsync(CancellationToken.None);

        // Assert
        first.PhotographUrl.Should().Be("https://cdn.test/photo.jpg");
        second.PhotographUrl.Should().Be("https://cdn.test/other.jpg");
        second.UserId.Should().Be(OtherUserId);
        await MockProfileClient.Received(2).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDiscardAProfileLoadedForAnAccountThatSignedOutMidFlight()
    {
        // Arrange
        var loading = new TaskCompletionSource<ProfileDto>();
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(loading.Task);
        var pending = Sut.GetAsync(CancellationToken.None);
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);

        // Act
        loading.SetResult(Profile(OwnerUserId));
        var result = await pending;

        // Assert
        result.Should().BeSameAs(ProfileSummaryModel.Empty);
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        (await Sut.GetAsync(CancellationToken.None)).PhotographUrl.Should().Be("https://cdn.test/photo.jpg");
    }

    [Fact]
    public async Task ItShouldForgetTheSummaryWhenInvalidated()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(OwnerUserId));
        await Sut.GetAsync(CancellationToken.None);

        // Act
        Sut.Invalidate();
        await Sut.GetAsync(CancellationToken.None);

        // Assert
        await MockProfileClient.Received(2).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheDisplayNameAndPhotographForTheSignedInAngler()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(OwnerUserId));

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.UserId.Should().Be(OwnerUserId);
        result.DisplayName.Should().Be("Eamonn");
        result.HasPhotograph.Should().BeTrue();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }
}
