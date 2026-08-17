using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Services.PlatformCapabilityServiceTests;

public class WhenTestingGrant : BasePlatformCapabilityServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheActorLookupFails()
    {
        // Arrange
        var args = GrantArgs();
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("Failed to persist platform capability."));

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to persist platform capability.");
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == args.ActorUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheActorLacksAdministrator()
    {
        // Arrange
        var args = GrantArgs();
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<MissingPlatformCapabilityError>();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == args.ActorUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenGrantPersistenceFails()
    {
        // Arrange
        var args = GrantArgs();
        GivenActorIsAdministrator(args.ActorUserId);
        MockUserPlatformCapabilityRepository
            .GrantAsync(Arg.Any<UserPlatformCapability>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Failed to persist platform capability."));

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).GrantAsync(
            Arg.Is<UserPlatformCapability>(association =>
                association.UserId == args.TargetUserId
                && association.Capability == args.Capability),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveOtherCapabilitiesWhenGranting()
    {
        // Arrange
        var args = GrantArgs(capability: PlatformCapabilityEnum.Administrator);
        GivenActorIsAdministrator(args.ActorUserId);
        MockUserPlatformCapabilityRepository
            .GrantAsync(Arg.Any<UserPlatformCapability>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).GrantAsync(
            Arg.Is<UserPlatformCapability>(association =>
                association.UserId == args.TargetUserId
                && association.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldGrantWhenTheActorHasAdministrator()
    {
        // Arrange
        var args = GrantArgs();
        GivenActorIsAdministrator(args.ActorUserId);
        MockUserPlatformCapabilityRepository
            .GrantAsync(Arg.Any<UserPlatformCapability>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == args.ActorUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.Received(1).GrantAsync(
            Arg.Is<UserPlatformCapability>(association =>
                association.UserId == args.TargetUserId
                && association.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
        Enum.GetNames<PlatformCapabilityEnum>().Should().NotContain("Angler");
        Enum.GetNames<PlatformCapabilityEnum>().Should().NotContain("ClubAdmin");
        Enum.GetNames<PlatformCapabilityEnum>().Should().Equal(
            nameof(PlatformCapabilityEnum.Guide),
            nameof(PlatformCapabilityEnum.FishingVenueManager),
            nameof(PlatformCapabilityEnum.CompetitionOrganiser),
            nameof(PlatformCapabilityEnum.Administrator));
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

    private static GrantPlatformCapabilityArgs GrantArgs(
        PlatformCapabilityEnum capability = PlatformCapabilityEnum.Guide)
    {
        return new GrantPlatformCapabilityArgs
        {
            ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TargetUserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Capability = capability
        };
    }
}
