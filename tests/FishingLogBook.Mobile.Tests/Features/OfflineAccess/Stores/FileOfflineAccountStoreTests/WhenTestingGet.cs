using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Stores.FileOfflineAccountStoreTests;

public sealed class WhenTestingGet : BaseFileOfflineAccountStoreTest
{
    [Fact]
    public async Task ItShouldReturnNullWhenTheAccountHasNotBeenPrepared()
    {
        // Arrange

        // Act
        var result = await Sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        MockFileSystem.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldReadOnlyTheRequestedAccount()
    {
        // Arrange
        var expected = Account();
        GivenStored(expected);

        // Act
        var result = await Sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expected);
        MockFileSystem.Received(1).OpenRead(PathFor(UserId));
    }
}
