using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchPhotographServiceTests;

public class WhenTestingCreateUpload : BaseCatchPhotographServiceTest
{
    [Fact]
    public async Task ItShouldReturnNotFoundWhenThePhotographDoesNotExistYet()
    {
        // Arrange
        MockCatchRepository.GetPhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchPhotograph?>(null));
        var sut = CreateSut();

        // Act
        var result = await sut.CreateUploadAsync(
            new CreateCatchPhotographUploadArgs
            {
                CatchId = CatchId,
                Request = new PhotographUploadRequestDto(PhotographId, "image/jpeg")
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeOfType<CatchPhotographNotFoundError>();
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeriveTheSameObjectKeyForRepeatedRequests()
    {
        // Arrange
        var sut = CreateSut();
        var args = new CreateCatchPhotographUploadArgs
        {
            CatchId = CatchId,
            Request = new PhotographUploadRequestDto(PhotographId, "image/jpeg")
        };

        // Act
        var first = await sut.CreateUploadAsync(args, CancellationToken.None);
        var second = await sut.CreateUploadAsync(args, CancellationToken.None);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.ObjectKey.Should().Be(
            $"catch-photographs/{CatchId:D}/{PhotographId:D}");
        second.Value.ObjectKey.Should().Be(first.Value.ObjectKey);
        await MockCatchRepository.Received(2).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.UserId == UserId
                && query.CatchId == CatchId
                && query.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(2).CreateUploadUrlAsync(
            $"catch-photographs/{CatchId:D}/{PhotographId:D}",
            "image/jpeg",
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPhotographThatIsNotOwnedByTheCurrentUser()
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
        var result = await sut.CreateUploadAsync(
            new CreateCatchPhotographUploadArgs
            {
                CatchId = CatchId,
                Request = new PhotographUploadRequestDto(PhotographId, "image/jpeg")
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeOfType<CatchPhotographNotFoundError>();
        await MockCatchRepository.Received(1).GetByIdAsync(CatchId, Arg.Any<CancellationToken>());
        await MockCatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeriveTheObjectKeyFromTheCatchRegardlessOfWhoTheAnglerIs()
    {
        // Arrange
        var anglerUserId = Guid.NewGuid();
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = CatchId,
                UserId = anglerUserId,
                AnglerUserId = anglerUserId,
                RecordedByUserId = UserId
            }));
        var sut = CreateSut();

        // Act
        var result = await sut.CreateUploadAsync(
            new CreateCatchPhotographUploadArgs
            {
                CatchId = CatchId,
                Request = new PhotographUploadRequestDto(PhotographId, "image/jpeg")
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ObjectKey.Should().Be($"catch-photographs/{CatchId:D}/{PhotographId:D}");
        await MockCatchRepository.Received(1).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.UserId == anglerUserId
                && query.CatchId == CatchId
                && query.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
    }
}
