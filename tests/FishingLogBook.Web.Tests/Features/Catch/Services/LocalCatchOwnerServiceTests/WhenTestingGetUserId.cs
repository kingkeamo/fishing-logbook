using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Users.Clients;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.LocalCatchOwnerServiceTests;

public class WhenTestingGetUserId : BaseLocalCatchOwnerServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheUserIsNotSignedIn()
    {
        // Arrange
        var currentUser = Substitute.For<ICurrentUserClient>();
        var jsRuntime = new MemoryJsRuntime();
        var sut = CreateSut(Unauthenticated(), currentUser, jsRuntime);

        // Act
        var act = () => sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await currentUser.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
        jsRuntime.GetItemCalls.Should().Be(0);
        jsRuntime.SetItemCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldFailWhenTheApiReturnsAnEmptyUserId()
    {
        // Arrange
        var currentUser = Substitute.For<ICurrentUserClient>();
        currentUser.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrentUserDto(Guid.Empty, "owner@example.test", "Cognito", OwnerSubject));
        var jsRuntime = new MemoryJsRuntime();
        var sut = CreateSut(Authenticated(OwnerSubject), currentUser, jsRuntime);

        // Act
        var act = () => sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await currentUser.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        jsRuntime.SetItemCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldFailWhenTheApiCallFailsAndNothingIsCached()
    {
        // Arrange
        var currentUser = Substitute.For<ICurrentUserClient>();
        currentUser.GetCurrentAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        var jsRuntime = new MemoryJsRuntime();
        var sut = CreateSut(Authenticated(OwnerSubject), currentUser, jsRuntime);

        // Act
        var act = () => sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        await currentUser.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        jsRuntime.SetItemCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotReuseAnotherSubjectsCachedUserId()
    {
        // Arrange
        var currentUser = CurrentUser(OtherUserId, "other@example.test");
        var jsRuntime = new MemoryJsRuntime();
        jsRuntime.Items["fishingLogBook.localUserId." + OwnerSubject] = OwnerUserId.ToString("D");
        var sut = CreateSut(Authenticated(OtherSubject, "other@example.test"), currentUser, jsRuntime);

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OtherUserId);
        userId.Should().NotBe(OwnerUserId);
        await currentUser.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        jsRuntime.Items["fishingLogBook.localUserId." + OtherSubject].Should().Be(OtherUserId.ToString("D"));
        jsRuntime.Items["fishingLogBook.localUserId." + OwnerSubject].Should().Be(OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldUseTheCachedUserIdWithoutCallingTheApi()
    {
        // Arrange
        var currentUser = Substitute.For<ICurrentUserClient>();
        var jsRuntime = new MemoryJsRuntime();
        jsRuntime.Items["fishingLogBook.localUserId." + OwnerSubject] = OwnerUserId.ToString("D");
        var sut = CreateSut(Authenticated(OwnerSubject), currentUser, jsRuntime);

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        jsRuntime.GetItemCalls.Should().Be(1);
        await currentUser.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
        jsRuntime.SetItemCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldResolveAndCacheTheInternalUserId()
    {
        // Arrange
        var currentUser = CurrentUser(OwnerUserId);
        var jsRuntime = new MemoryJsRuntime();
        var sut = CreateSut(Authenticated(OwnerSubject), currentUser, jsRuntime);

        // Act
        var userId = await sut.GetUserIdAsync(CancellationToken.None);

        // Assert
        userId.Should().Be(OwnerUserId);
        await currentUser.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        jsRuntime.SetItemCalls.Should().Be(1);
        jsRuntime.Items["fishingLogBook.localUserId." + OwnerSubject].Should().Be(OwnerUserId.ToString("D"));
    }
}
