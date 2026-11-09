using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchPhotographServiceTests;

public class WhenTestingDependencyFailures : BaseCatchPhotographServiceTest
{
    private static CreateCatchPhotographUploadArgs UploadArgs(string contentType = "image/jpeg")
    {
        return new CreateCatchPhotographUploadArgs
        {
            CatchId = CatchId,
            Request = new PhotographUploadRequestDto(PhotographId, contentType)
        };
    }

    private static RecordCatchPhotographArgs RecordArgs(string contentType = "image/jpeg")
    {
        return new RecordCatchPhotographArgs
        {
            CatchId = CatchId,
            PhotographId = PhotographId,
            ObjectKey = $"catch-photographs/{CatchId:D}/{PhotographId:D}",
            ContentType = contentType
        };
    }

    private void GivenCatchLookupFails()
    {
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch?>("The catch lookup failed."));
    }

    private void GivenPhotographLookupFails()
    {
        MockCatchRepository.GetPhotographAsync(Arg.Any<GetCatchPhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchPhotograph?>("The photograph lookup failed."));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ItShouldReportWhetherObjectStorageIsConfigured(bool configured)
    {
        // Arrange
        MockObjectStorage.IsConfigured.Returns(configured);
        var sut = CreateSut();

        // Act
        var result = sut.IsObjectStorageConfigured;

        // Assert
        result.Should().Be(configured);
    }

    [Fact]
    public async Task ItShouldReturnTheCatchLookupFailureWhenCreatingAnUpload()
    {
        // Arrange
        GivenCatchLookupFails();
        var sut = CreateSut();

        // Act
        var result = await sut.CreateUploadAsync(UploadArgs(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The catch lookup failed.");
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePhotographLookupFailureWhenCreatingAnUpload()
    {
        // Arrange
        GivenPhotographLookupFails();
        var sut = CreateSut();

        // Act
        var result = await sut.CreateUploadAsync(UploadArgs(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The photograph lookup failed.");
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCreateAnUploadWhenTheContentTypeDiffersFromTheRegisteredPhotograph()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.CreateUploadAsync(UploadArgs("image/png"), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchPhotographNotFoundError>();
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheCatchLookupFailureWhenRecording()
    {
        // Arrange
        GivenCatchLookupFails();
        var sut = CreateSut();

        // Act
        var result = await sut.RecordAsync(RecordArgs(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The catch lookup failed.");
        await MockCatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePhotographLookupFailureWhenRecording()
    {
        // Arrange
        GivenPhotographLookupFails();
        var sut = CreateSut();

        // Act
        var result = await sut.RecordAsync(RecordArgs(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The photograph lookup failed.");
        await MockObjectStorage.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRecordAPhotographWhoseContentTypeDiffersFromTheRegisteredOne()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.RecordAsync(RecordArgs("image/png"), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchPhotographNotFoundError>();
        await MockObjectStorage.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePhotographLookupFailureWhenDeleting()
    {
        // Arrange
        GivenPhotographLookupFails();
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync(
            new DeleteCatchPhotographArgs { CatchId = CatchId, PhotographId = PhotographId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The photograph lookup failed.");
        await MockCatchRepository.DidNotReceive().DeletePhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
