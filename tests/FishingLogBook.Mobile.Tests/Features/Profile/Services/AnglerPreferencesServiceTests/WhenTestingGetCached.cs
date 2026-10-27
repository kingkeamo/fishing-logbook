using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AnglerPreferencesServiceTests;

public class WhenTestingGetCached : BaseAnglerPreferencesServiceTest
{
    [Fact]
    public async Task ItShouldReturnNothingWithoutReadingTheCacheWhenNobodyIsSignedIn()
    {
        // Arrange
        // Act
        var result = await Sut.GetCachedAsync(CancellationToken.None);
        // Assert
        result.Should().BeNull();
        await MockStore.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheDeviceCacheCannotBeRead()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        MockStore.GetAsync(FirstUserId, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("disk"));
        // Act
        var result = await Sut.GetCachedAsync(CancellationToken.None);
        // Assert
        result.Should().BeNull();
        await MockStore.Received(1).GetAsync(FirstUserId, Arg.Any<CancellationToken>());
        Logger.Entries.Should().ContainSingle(entry => entry.Message.Contains(typeof(IOException).FullName!));
    }

    [Fact]
    public async Task ItShouldDiscardCachedPreferencesReadAfterTheAccountChanged()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        MockStore.GetAsync(FirstUserId, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await SignInAsync(SecondUserId);
            return (AnglerPreferencesModel?)TestAnglerPreferences.Create();
        });
        // Act
        var result = await Sut.GetCachedAsync(CancellationToken.None);
        // Assert
        result.Should().BeNull();
        await MockStore.Received(1).GetAsync(FirstUserId, Arg.Any<CancellationToken>());
        await MockStore.DidNotReceive().GetAsync(SecondUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowTheFirstAccountsPreferencesToTheNextAccount()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        ApiReturns();
        await Sut.RefreshAsync(CancellationToken.None);
        await SignInAsync(SecondUserId);
        MockStore.GetAsync(SecondUserId, Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        // Act
        var result = await Sut.GetCachedAsync(CancellationToken.None);
        // Assert
        result.Should().BeNull();
        await MockStore.Received(1).GetAsync(SecondUserId, Arg.Any<CancellationToken>());
        await MockStore.DidNotReceive().GetAsync(FirstUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldServeRememberedPreferencesWithoutRereadingTheDevice()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var stored = TestAnglerPreferences.Create();
        MockStore.GetAsync(FirstUserId, Arg.Any<CancellationToken>()).Returns(stored);
        await Sut.GetCachedAsync(CancellationToken.None);
        // Act
        var result = await Sut.GetCachedAsync(CancellationToken.None);
        // Assert
        result.Should().BeSameAs(stored);
        await MockStore.Received(1).GetAsync(FirstUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSignedInAccountsDeviceCache()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        var stored = TestAnglerPreferences.Create();
        MockStore.GetAsync(FirstUserId, Arg.Any<CancellationToken>()).Returns(stored);
        // Act
        var result = await Sut.GetCachedAsync(CancellationToken.None);
        // Assert
        result.Should().BeSameAs(stored);
        await MockStore.Received(1).GetAsync(FirstUserId, Arg.Any<CancellationToken>());
        await MockPreferenceClient.DidNotReceive().GetPreferencesAsync(Arg.Any<CancellationToken>());
    }
}
