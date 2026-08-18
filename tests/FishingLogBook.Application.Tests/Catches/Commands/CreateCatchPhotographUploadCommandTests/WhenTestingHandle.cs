using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.CreateCatchPhotographUploadCommandTests;

public class WhenTestingHandle : BaseCreateCatchPhotographUploadCommandTest
{
    [Fact]
    public async Task ItShouldNotCallTheServiceWhenObjectStorageIsUnavailable()
    {
        // Arrange
        MockCatchPhotographService.IsObjectStorageConfigured.Returns(false);
        var command = Command();

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        await MockCatchPhotographService.DidNotReceive().CreateUploadAsync(
            Arg.Any<CreateCatchPhotographUploadArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceFailure()
    {
        // Arrange
        var command = Command();
        MockCatchPhotographService.CreateUploadAsync(
                Arg.Any<CreateCatchPhotographUploadArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail<PhotographUploadDto>("failed"));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("failed");
        await MockCatchPhotographService.Received(1).CreateUploadAsync(
            Arg.Is<CreateCatchPhotographUploadArgs>(args =>
                args.CatchId == command.CatchId
                && args.Request == command.Request),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUpload()
    {
        // Arrange
        var command = Command();
        var expected = new PhotographUploadDto("object-key", "https://storage.test/object");
        MockCatchPhotographService.CreateUploadAsync(
                Arg.Any<CreateCatchPhotographUploadArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok(expected));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Upload.Should().Be(expected);
        await MockCatchPhotographService.Received(1).CreateUploadAsync(
            Arg.Is<CreateCatchPhotographUploadArgs>(args =>
                args.CatchId == command.CatchId
                && args.Request == command.Request),
            Arg.Any<CancellationToken>());
    }

    private static CreateCatchPhotographUploadCommand Command()
    {
        return new CreateCatchPhotographUploadCommand
        {
            CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Request = new PhotographUploadRequestDto(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "image/jpeg")
        };
    }
}
