using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchPhotographServiceTests;

public class WhenTestingRecord : BaseCatchPhotographServiceTest
{
    [Fact]
    public async Task ItShouldAcceptTheDeterministicObjectKeyIdempotently()
    {
        // Arrange
        var sut = CreateSut();
        var args = new RecordCatchPhotographArgs
        {
            CatchId = CatchId,
            PhotographId = PhotographId,
            ContentType = "image/jpeg",
            ObjectKey = $"catch-photographs/{CatchId:D}/{PhotographId:D}"
        };

        // Act
        var first = await sut.RecordAsync(args, CancellationToken.None);
        var second = await sut.RecordAsync(args, CancellationToken.None);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(2).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CaughtByUserId == UserId
                && query.CatchId == CatchId
                && query.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAClientSuppliedReplacementObjectKey()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.RecordAsync(
            new RecordCatchPhotographArgs
            {
                CatchId = CatchId,
                PhotographId = PhotographId,
                ContentType = "image/jpeg",
                ObjectKey = "catch-photographs/another-catch/replacement"
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeOfType<CatchPhotographObjectKeyMismatchError>();
        await MockCatchRepository.Received(1).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CaughtByUserId == UserId
                && query.CatchId == CatchId
                && query.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
    }
}
