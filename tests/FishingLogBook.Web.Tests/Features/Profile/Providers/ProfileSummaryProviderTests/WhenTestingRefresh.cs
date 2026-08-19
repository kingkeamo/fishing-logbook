using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.ProfileSummaryProviderTests;

public class WhenTestingRefresh : BaseProfileSummaryProviderTest
{
    [Fact]
    public async Task ItShouldStillAnnounceTheChangeWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        var announcements = 0;
        Sut.Changed += () => announcements++;
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        announcements.Should().Be(1);
        (await Sut.GetAsync(CancellationToken.None)).HasPhotograph.Should().BeFalse();
        await MockProfileClient.Received(2).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldServeTheUpdatedProfileToTheNextSummaryRead()
    {
        // Arrange
        var announcements = 0;
        Sut.Changed += () => announcements++;
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(OwnerUserId));
        await Sut.GetAsync(CancellationToken.None);
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(Profile(OwnerUserId, "Renamed Angler", "https://cdn.test/updated.jpg"));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        announcements.Should().Be(1);
        var summary = await Sut.GetAsync(CancellationToken.None);
        summary.DisplayName.Should().Be("Renamed Angler");
        summary.PhotographUrl.Should().Be("https://cdn.test/updated.jpg");
        await MockProfileClient.Received(2).GetOwnAsync(Arg.Any<CancellationToken>());
    }
}
