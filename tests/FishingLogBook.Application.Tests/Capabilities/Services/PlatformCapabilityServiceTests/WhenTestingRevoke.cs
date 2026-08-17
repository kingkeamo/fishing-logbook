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
    public async Task ItShouldFailWhenTheActorLacksAdministrator()
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
                lookup.UserId == args.ActorUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenRevokePersistenceFails()
    {
        // Arrange
        var args = RevokeArgs();
        GivenActorIsAdministrator(args.ActorUserId);
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
        GivenActorIsAdministrator(args.ActorUserId);
        MockUserPlatformCapabilityRepository
            .RevokeAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).RevokeAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == args.TargetUserId
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

    private void GivenActorIsAdministrator(Guid actorUserId)
    {
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var lookup = call.ArgAt<FindUserPlatformCapabilityArgs>(0);
                return Result.Ok(
                    lookup.UserId == actorUserId
                    && lookup.Capability == PlatformCapabilityEnum.Administrator);
            });
    }

    private static RevokePlatformCapabilityArgs RevokeArgs()
    {
        return new RevokePlatformCapabilityArgs
        {
            ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TargetUserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Capability = PlatformCapabilityEnum.Guide
        };
    }
}
