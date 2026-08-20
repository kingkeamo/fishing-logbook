using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.DeleteCatchPhotographCommandTests;

public class WhenTestingHandle : BaseDeleteCatchPhotographCommandTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        var command = Command();
        MockCatchPhotographService.IsObjectStorageConfigured.Returns(false);

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        await MockCatchPhotographService.DidNotReceive().DeleteAsync(
            Arg.Any<DeleteCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceFailure()
    {
        // Arrange
        var command = Command();
        MockCatchPhotographService.DeleteAsync(
                Arg.Any<DeleteCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail("failed"));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("failed");
        await MockCatchPhotographService.Received(1).DeleteAsync(
            Arg.Is<DeleteCatchPhotographArgs>(args =>
                args.CatchId == command.CatchId
                && args.PhotographId == command.PhotographId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteThePhotograph()
    {
        // Arrange
        var command = Command();
        MockCatchPhotographService.DeleteAsync(
                Arg.Any<DeleteCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        await MockCatchPhotographService.Received(1).DeleteAsync(
            Arg.Is<DeleteCatchPhotographArgs>(args =>
                args.CatchId == command.CatchId
                && args.PhotographId == command.PhotographId),
            Arg.Any<CancellationToken>());
    }

    private static DeleteCatchPhotographCommand Command()
    {
        return new DeleteCatchPhotographCommand
        {
            CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        };
    }
}
