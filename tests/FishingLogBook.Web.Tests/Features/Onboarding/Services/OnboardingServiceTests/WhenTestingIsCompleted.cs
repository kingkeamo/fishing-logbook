using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Services.OnboardingServiceTests;

public class WhenTestingIsCompleted : BaseOnboardingServiceTest
{
    [Fact]
    public async Task ItShouldRethrowAnUnexpectedFailureWhenNothingIsCached()
    {
        // Arrange
        CacheContains(null);
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProfileDto>(new InvalidOperationException("boom")));

        // Act
        var act = () => Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTrapAnEstablishedUserInOnboardingWhenFirstLaunchedOffline()
    {
        // Arrange
        CacheContains(null);
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProfileDto>(new HttpRequestException("offline")));

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        Context.JSInterop.Invocations.Should().NotContain(invocation => invocation.Identifier == SetItem);
    }

    [Fact]
    public async Task ItShouldTrustTheCachedValueWhenTheServerCannotBeReached()
    {
        // Arrange
        CacheContains("False");
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProfileDto>(new HttpRequestException("offline")));

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnUnreadableCacheAsEmptyAndAskTheServer()
    {
        // Arrange
        CacheCannotBeRead();
        CacheAcceptsWrites();
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(true));

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillReturnTheServerAnswerWhenTheCacheCannotBeWritten()
    {
        // Arrange
        CacheContains(null);
        CacheRejectsWrites();
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(false));

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        Writes().Should().ContainSingle();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskTheServerAndCacheAnIncompleteAnswerOverAStaleCachedFalse()
    {
        // Arrange
        CacheContains("False");
        CacheAcceptsWrites();
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(false));

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        Writes().Should().ContainSingle().Which.Arguments.Should().Equal(CacheKey, "False");
    }

    [Fact]
    public async Task ItShouldReturnTrueFromTheCacheWithoutAskingTheServer()
    {
        // Arrange
        CacheContains("True");

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockProfileClient.DidNotReceive().GetOwnAsync(Arg.Any<CancellationToken>());
        Context.JSInterop.Invocations[GetItem].Should().ContainSingle()
            .Which.Arguments.Should().Equal(CacheKey);
    }

    [Fact]
    public async Task ItShouldAskTheServerAndCacheACompletedAnswerWhenNothingIsCached()
    {
        // Arrange
        CacheContains(null);
        CacheAcceptsWrites();
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(Profile(true));

        // Act
        var result = await Sut.IsCompletedAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        Writes().Should().ContainSingle().Which.Arguments.Should().Equal(CacheKey, "True");
    }
}
