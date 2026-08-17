using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Integration.Profiles.ProfileRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUserExists : BaseProfileRepositoryTest
{
    public WhenTestingUserExists(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenTheUserIsUnknown()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await Sut.UserExistsAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReturnTrueWhenTheUserExists()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.UserExistsAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
