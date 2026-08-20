using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;

namespace FishingLogBook.Infrastructure.Tests.Integration.Profiles.ProfileRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpdatePhotograph : BaseProfileRepositoryTest
{
    public WhenTestingUpdatePhotograph(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenNoProfileExists()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";

        // Act
        var result = await Sut.UpdatePhotographAsync(
            new RecordProfilePhotographArgs
            {
                UserId = userId,
                PhotographId = photographId,
                ObjectKey = objectKey,
                ContentType = PhotographContentTypeConstants.Png
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.HasError<ProfileNotFoundError>().Should().BeTrue();
        result.Errors[0].Message.Should().Be("Angler profile was not found.");
        var loaded = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        loaded.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistPhotographFieldsWithoutChangingOtherProfileValues()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var existing = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithHomeRegion("Westmeath")
            .ShowAll()
            .Build();
        var inserted = await Sut.UpsertAsync(existing, CancellationToken.None);
        inserted.IsSuccess.Should().BeTrue();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";

        // Act
        var result = await Sut.UpdatePhotographAsync(
            new RecordProfilePhotographArgs
            {
                UserId = userId,
                PhotographId = photographId,
                ObjectKey = objectKey,
                ContentType = PhotographContentTypeConstants.Webp
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.PhotographId.Should().Be(photographId);
        result.Value.PhotographObjectKey.Should().Be(objectKey);
        result.Value.PhotographContentType.Should().Be(PhotographContentTypeConstants.Webp);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.ShowDisplayName.Should().BeTrue();
        result.Value.ShowPhotograph.Should().BeTrue();
        var loaded = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        loaded.Value!.PhotographId.Should().Be(photographId);
        loaded.Value.DisplayName.Should().Be("Eamonn");
        loaded.Value.HomeRegion.Should().Be("Westmeath");
    }
}
