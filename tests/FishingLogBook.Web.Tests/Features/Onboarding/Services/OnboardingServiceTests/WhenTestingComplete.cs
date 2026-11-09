using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Services.OnboardingServiceTests;

public class WhenTestingComplete : BaseOnboardingServiceTest
{
    [Fact]
    public async Task ItShouldFailWithoutCachingWhenTheServerDoesNotRecordCompletion()
    {
        // Arrange
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>()).Returns(Profile(false));

        // Act
        var act = () => Sut.CompleteAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        Context.JSInterop.Invocations.Should().NotContain(invocation => invocation.Identifier == SetItem);
    }

    [Fact]
    public async Task ItShouldPropagateAServerFailureWithoutCaching()
    {
        // Arrange
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProfileDto>(new HttpRequestException("offline")));

        // Act
        var act = () => Sut.CompleteAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        Context.JSInterop.Invocations.Should().NotContain(invocation => invocation.Identifier == SetItem);
    }

    [Fact]
    public async Task ItShouldRecordCompletionOnTheServerAndCacheItForTheUser()
    {
        // Arrange
        CacheAcceptsWrites();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>()).Returns(Profile(true));

        // Act
        await Sut.CompleteAsync(CancellationToken.None);

        // Assert
        await MockProfileClient.Received(1).CompleteOnboardingAsync(Arg.Any<CancellationToken>());
        Writes().Should().ContainSingle().Which.Arguments.Should().Equal(CacheKey, "True");
    }
}
