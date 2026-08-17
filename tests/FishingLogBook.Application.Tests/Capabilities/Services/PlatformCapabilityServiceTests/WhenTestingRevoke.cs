using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Services.PlatformCapabilityServiceTests;

public class WhenTestingRevoke : BasePlatformCapabilityServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheCurrentUserIsNotResolved()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);
        MockCurrentUser.UserId.Returns(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var args = RevokeArgs();

        // Act
        var result = await Sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockUserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheCurrentUserLacksAdministrator()
    {
        // Arrange
        var args = RevokeArgs();
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));

        // Act
        var result = await Sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<MissingPlatformCapabilityError>();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup => lookup.UserId == args.TargetUserId),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAuthorizeUsingTheTargetUserId()
    {
        // Arrange
        var args = RevokeArgs();
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var lookup = call.ArgAt<FindUserPlatformCapabilityArgs>(0);
                return Result.Ok(
                    lookup.UserId == args.TargetUserId
                    && lookup.Capability == PlatformCapabilityEnum.Administrator);
            });

        // Act
        var result = await Sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<MissingPlatformCapabilityError>();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenRevokePersistenceFails()
    {
        // Arrange
        var args = RevokeArgs();
        GivenCurrentUserIsAdministrator();
        MockUserPlatformCapabilityRepository
            .RevokeAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Failed to persist platform capability."));

        // Act
        var result = await Sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).RevokeAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == args.TargetUserId
                && lookup.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRevokeOnlyTheRequestedCapability()
    {
        // Arrange
        var args = RevokeArgs();
        GivenCurrentUserIsAdministrator();
        MockUserPlatformCapabilityRepository
            .RevokeAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.Received(1).RevokeAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == TargetUserId
                && lookup.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.Capability == PlatformCapabilityEnum.CompetitionOrganiser),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    private void GivenCurrentUserIsAdministrator()
    {
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var lookup = call.ArgAt<FindUserPlatformCapabilityArgs>(0);
                return Result.Ok(
                    lookup.UserId == CurrentUserId
                    && lookup.Capability == PlatformCapabilityEnum.Administrator);
            });
    }

    private static RevokePlatformCapabilityArgs RevokeArgs()
    {
        return new RevokePlatformCapabilityArgs
        {
            TargetUserId = TargetUserId,
            Capability = PlatformCapabilityEnum.Guide
        };
    }
}
