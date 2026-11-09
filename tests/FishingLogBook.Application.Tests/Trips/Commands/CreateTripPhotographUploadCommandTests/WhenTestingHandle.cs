using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.CreateTripPhotographUploadCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly ITripPhotographService _mockService = Substitute.For<ITripPhotographService>();
    private readonly CreateTripPhotographUploadHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new CreateTripPhotographUploadHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldFailWithoutCreatingAnUploadWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        _mockService.IsObjectStorageConfigured.Returns(false);

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<TripObjectStorageNotConfiguredError>();
        response.ErrorMessage.Should().Be("Photograph storage is not configured.");
        response.Upload.Should().BeNull();
        await _mockService.DidNotReceive().CreateUploadAsync(
            Arg.Any<CreateTripPhotographUploadArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.IsObjectStorageConfigured.Returns(true);
        _mockService.CreateUploadAsync(Arg.Any<CreateTripPhotographUploadArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<PhotographUploadDto>("The service failed."));

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Upload.Should().BeNull();
        await _mockService.Received(1).CreateUploadAsync(
            Arg.Is<CreateTripPhotographUploadArgs>(args => MatchesCommand(args)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapTheRequestAndReturnTheUpload()
    {
        // Arrange
        var upload = new PhotographUploadDto("trips/key.jpg", "https://storage.test/upload");
        _mockService.IsObjectStorageConfigured.Returns(true);
        _mockService.CreateUploadAsync(Arg.Any<CreateTripPhotographUploadArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(upload));

        // Act
        var response = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Upload.Should().Be(upload);
        await _mockService.Received(1).CreateUploadAsync(
            Arg.Is<CreateTripPhotographUploadArgs>(args => MatchesCommand(args)),
            Arg.Any<CancellationToken>());
    }

    private static CreateTripPhotographUploadCommand Command()
    {
        return new CreateTripPhotographUploadCommand
        {
            TripId = TripId,
            Request = new PhotographUploadRequestDto(PhotographId, "image/png")
        };
    }

    private static bool MatchesCommand(CreateTripPhotographUploadArgs args)
    {
        return args.TripId == TripId
            && args.Request.PhotographId == PhotographId
            && args.Request.ContentType == "image/png";
    }
}
