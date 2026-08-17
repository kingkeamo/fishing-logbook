using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingUpsert : BaseCatchServiceTest
{
    public WhenTestingUpsert()
    {
        ((IRegister)new FishingLogBook.Application.Common.Mappings.CatchMappingRegistration())
            .Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task ItShouldFailWhenPhotographsAreMissing()
    {
        // Arrange
        var args = Args(photographs: []);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchHasNoPhotographsError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenAPhotographIdIsEmpty()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var args = Args(
            catchId: catchId,
            photographs: [new CatchPhotographDto(Guid.Empty, catchId, PhotographContentTypeConstants.Jpeg)]);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchPhotographIdentityError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenAPhotographCatchIdDoesNotMatch()
    {
        // Arrange
        var args = Args(
            photographs:
            [
                new CatchPhotographDto(Guid.NewGuid(), Guid.NewGuid(), PhotographContentTypeConstants.Jpeg)
            ]);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchPhotographIdentityError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryFails()
    {
        // Arrange
        var args = Args();
        MockCatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch>(new CatchOwnershipConflictError()));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchOwnershipConflictError>();
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item => item.UserId == args.UserId && item.Id == args.Catch.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistOwnershipFromTheAuthenticatedUserId()
    {
        // Arrange
        var authenticatedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var clientUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var args = new UpsertCatchArgs
        {
            UserId = authenticatedUserId,
            Catch = new CatchDto(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographDto(photographId, catchId, PhotographContentTypeConstants.Png)])
            {
                UserId = clientUserId
            }
        };
        MockCatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(authenticatedUserId);
        result.Value.UserId.Should().NotBe(clientUserId);
        result.Value.Photographs.Should().ContainSingle(photograph => photograph.Id == photographId);
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item =>
                item.UserId == authenticatedUserId
                && item.Id == catchId
                && item.Photographs.Count == 1
                && item.Photographs[0].Id == photographId
                && item.Photographs[0].CatchId == catchId
                && item.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenLocationIsInvalid()
    {
        // Arrange
        var args = Args(location: new CatchLocationDto(
            91,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchLocationInvalidError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistAValidPrivateDeviceLocation()
    {
        // Arrange
        var capturedOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");
        var location = new CatchLocationDto(
            53.2707,
            -9.0568,
            12,
            capturedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var args = Args(location: location);
        MockCatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().Be(location);
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item =>
                item.Id == args.Catch.Id
                && item.UserId == args.UserId
                && item.Location != null
                && item.Location.Latitude == 53.2707
                && item.Location.Longitude == -9.0568
                && item.Location.AccuracyMetres == 12
                && item.Location.CapturedOn == capturedOn
                && item.Location.Source == LocationDefaults.DeviceGps
                && item.Location.Visibility == LocationDefaults.Private
                && item.Location.ConsentVersion == LocationDefaults.ConsentVersion),
            Arg.Any<CancellationToken>());
    }

    private static UpsertCatchArgs Args(
        Guid? catchId = null,
        IReadOnlyList<CatchPhotographDto>? photographs = null,
        CatchLocationDto? location = null)
    {
        var resolvedCatchId = catchId ?? Guid.NewGuid();
        return new UpsertCatchArgs
        {
            UserId = Guid.NewGuid(),
            Catch = new CatchDto(
                resolvedCatchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                photographs ??
                [
                    new CatchPhotographDto(Guid.NewGuid(), resolvedCatchId, PhotographContentTypeConstants.Jpeg)
                ],
                location)
        };
    }
}
