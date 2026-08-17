using AwesomeAssertions;
using Dapper;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Shared.Constants;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Integration.Catches.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpsert : BaseCatchRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldInsertACatchWithMultiplePhotographs()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var firstPhoto = Guid.NewGuid();
        var secondPhoto = Guid.NewGuid();
        var catchRecord = NewCatch(
            userId,
            catchId,
            new CatchPhotograph
            {
                Id = firstPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            },
            new CatchPhotograph
            {
                Id = secondPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Png
            });

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(catchId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Photographs.Should().HaveCount(2);
        result.Value.Photographs.Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto]);
    }

    [Fact]
    public async Task ItShouldKeepTheSameCatchIdOnUpdate()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var original = NewCatch(userId);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var updated = new Catch
        {
            Id = original.Id,
            UserId = userId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(updated, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(original.Id);
        loaded.Value.Should().NotBeNull();
        loaded.Value!.Id.Should().Be(original.Id);
        loaded.Value.CaughtOn.Should().Be(updated.CaughtOn);
        loaded.Value.Photographs[0].Id.Should().Be(original.Photographs[0].Id);
    }

    [Fact]
    public async Task ItShouldRejectWhenAnotherUserOwnsTheCatchId()
    {
        // Arrange
        var ownerId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var original = NewCatch(ownerId);
        await Sut.UpsertAsync(original, CancellationToken.None);
        var hijack = new Catch
        {
            Id = original.Id,
            UserId = otherUserId,
            CaughtOn = original.CaughtOn,
            Photographs = original.Photographs
        };

        // Act
        var result = await Sut.UpsertAsync(hijack, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(original.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchOwnershipConflictError>();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.UserId.Should().Be(ownerId);
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var catchRecord = NewCatch(Guid.NewGuid());

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
        loaded.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRejectARawInsertForAnUnknownUser()
    {
        // Arrange
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn")
            VALUES (@Id, @UserId, @CaughtOn);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                CaughtOn = DateTimeOffset.UtcNow
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ItShouldNotKeepACatchWhenPhotographInsertFails()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var catchRecord = NewCatch(
            userId,
            catchId,
            new CatchPhotograph
            {
                Id = photographId,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            },
            new CatchPhotograph
            {
                Id = photographId,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Png
            });

        // Act
        var result = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        loaded.Value.Should().BeNull();
    }
}
