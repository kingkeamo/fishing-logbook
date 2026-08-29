using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FluentResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchPhotographServiceTests;

public class WhenTestingDelete : BaseCatchPhotographServiceTest
{
    [Fact]
    public async Task ItShouldReturnNotFoundWhenThePhotographIsNotOwnedByTheCurrentUser()
    {
        // Arrange
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = CatchId,
                UserId = Guid.NewGuid(),
                AnglerUserId = Guid.NewGuid(),
                RecordedByUserId = Guid.NewGuid()
            }));
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync(
            new DeleteCatchPhotographArgs { CatchId = CatchId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeOfType<CatchPhotographNotFoundError>();
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await MockCatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
        await MockCatchRepository.DidNotReceive().DeletePhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenDatabaseDeletionFailsAfterObjectStorageDeletion()
    {
        // Arrange
        MockCatchRepository.DeletePhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Database unavailable"));
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync(
            new DeleteCatchPhotographArgs { CatchId = CatchId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockObjectStorage.Received(1).DeleteObjectAsync(
            $"catches/{UserId:D}/{CatchId:D}/{PhotographId:D}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotDeleteTheDatabaseRowWhenObjectStorageDeletionFails()
    {
        // Arrange
        MockObjectStorage.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("R2 unavailable"));
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync(
            new DeleteCatchPhotographArgs { CatchId = CatchId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeOfType<CatchPhotographStorageDeleteFailedError>();
        await MockCatchRepository.DidNotReceive().DeletePhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(1).DeleteObjectAsync(
            $"catches/{UserId:D}/{CatchId:D}/{PhotographId:D}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteTheDatabaseRowAndObjectStorageEntry()
    {
        // Arrange
        var operations = new List<string>();
        MockCatchRepository.DeletePhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                operations.Add("database");
                return Result.Ok();
            });
        MockObjectStorage.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                operations.Add("object-storage");
                return Task.CompletedTask;
            });
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync(
            new DeleteCatchPhotographArgs { CatchId = CatchId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operations.Should().Equal("object-storage", "database");
        await MockObjectStorage.Received(1).DeleteObjectAsync(
            $"catches/{UserId:D}/{CatchId:D}/{PhotographId:D}",
            Arg.Any<CancellationToken>());
        await MockCatchRepository.Received(1).DeletePhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.UserId == UserId
                && query.CatchId == CatchId
                && query.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
    }
}
