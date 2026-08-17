using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.RecordCatchPhotographCommandTests;

public class WhenTestingHandle : BaseRecordCatchPhotographCommandTest
{
    [Fact]
    public async Task ItShouldReturnTheServiceFailure()
    {
        // Arrange
        var command = Command();
        MockCatchPhotographService.RecordAsync(
                Arg.Any<RecordCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail("failed"));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("failed");
        await MockCatchPhotographService.Received(1).RecordAsync(
            Arg.Is<RecordCatchPhotographArgs>(args =>
                args.CatchId == command.CatchId
                && args.PhotographId == command.Photograph.PhotographId
                && args.ObjectKey == command.Photograph.ObjectKey
                && args.ContentType == command.Photograph.ContentType),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordThePhotograph()
    {
        // Arrange
        var command = Command();
        MockCatchPhotographService.RecordAsync(
                Arg.Any<RecordCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        await MockCatchPhotographService.Received(1).RecordAsync(
            Arg.Is<RecordCatchPhotographArgs>(args =>
                args.CatchId == command.CatchId
                && args.PhotographId == command.Photograph.PhotographId
                && args.ObjectKey == command.Photograph.ObjectKey
                && args.ContentType == command.Photograph.ContentType),
            Arg.Any<CancellationToken>());
    }

    private static RecordCatchPhotographCommand Command()
    {
        return new RecordCatchPhotographCommand
        {
            CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Photograph = new RecordPhotographDto(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "object-key",
                "image/jpeg")
        };
    }
}
