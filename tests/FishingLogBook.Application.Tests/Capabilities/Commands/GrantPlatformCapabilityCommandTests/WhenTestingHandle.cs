using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Commands;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Domain.Enums;
using FluentResults;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Commands.GrantPlatformCapabilityCommandTests;

public class WhenTestingHandle : BaseGrantPlatformCapabilityCommandTest
{
    public WhenTestingHandle()
    {
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceDeniesTheActor()
    {
        // Arrange
        var command = ValidCommand();
        MockPlatformCapabilityService
            .GrantAsync(Arg.Any<GrantPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new MissingPlatformCapabilityError()));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<MissingPlatformCapabilityError>();
        await MockPlatformCapabilityService.Received(1).GrantAsync(
            Arg.Is<GrantPlatformCapabilityArgs>(args =>
                args.TargetUserId == command.TargetUserId
                && args.Capability == command.Capability),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapOnlyTargetUserIdAndCapability()
    {
        // Arrange
        var command = ValidCommand();
        MockPlatformCapabilityService
            .GrantAsync(Arg.Any<GrantPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        typeof(GrantPlatformCapabilityCommand).GetProperty("ActorUserId").Should().BeNull();
        typeof(GrantPlatformCapabilityArgs).GetProperty("ActorUserId").Should().BeNull();
        await MockPlatformCapabilityService.Received(1).GrantAsync(
            Arg.Is<GrantPlatformCapabilityArgs>(args =>
                args.TargetUserId == command.TargetUserId
                && args.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
    }

    private static GrantPlatformCapabilityCommand ValidCommand()
    {
        return new GrantPlatformCapabilityCommand
        {
            TargetUserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Capability = PlatformCapabilityEnum.Guide
        };
    }
}
