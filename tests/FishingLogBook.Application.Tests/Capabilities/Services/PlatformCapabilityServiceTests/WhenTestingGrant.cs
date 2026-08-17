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
    public async Task ItShouldFailWhenTheCurrentUserIsNotResolved()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);
        MockCurrentUser.UserId.Returns(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var args = GrantArgs();

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockUserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

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
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheCurrentUserLacksAdministrator()
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
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup => lookup.UserId == args.TargetUserId),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().RevokeAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAuthorizeUsingTheTargetUserId()
    {
        // Arrange
        var args = GrantArgs();
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
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<MissingPlatformCapabilityError>();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenGrantPersistenceFails()
    {
        // Arrange
        var args = GrantArgs();
        GivenCurrentUserIsAdministrator();
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
        GivenCurrentUserIsAdministrator();
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
    public async Task ItShouldGrantWhenTheCurrentUserHasAdministrator()
    {
        // Arrange
        var args = GrantArgs();
        GivenCurrentUserIsAdministrator();
        MockUserPlatformCapabilityRepository
            .GrantAsync(Arg.Any<UserPlatformCapability>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.GrantAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup =>
                lookup.UserId == CurrentUserId
                && lookup.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(lookup => lookup.UserId == args.TargetUserId),
            Arg.Any<CancellationToken>());
        await MockUserPlatformCapabilityRepository.Received(1).GrantAsync(
            Arg.Is<UserPlatformCapability>(association =>
                association.UserId == TargetUserId
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

    private static GrantPlatformCapabilityArgs GrantArgs(
        PlatformCapabilityEnum capability = PlatformCapabilityEnum.Guide)
    {
        return new GrantPlatformCapabilityArgs
        {
            TargetUserId = TargetUserId,
            Capability = capability
        };
    }
}
