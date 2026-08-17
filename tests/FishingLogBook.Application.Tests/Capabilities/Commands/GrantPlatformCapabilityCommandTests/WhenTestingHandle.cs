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
        ((IRegister)new CapabilityMappingRegistration()).Register(TypeAdapterConfig.GlobalSettings);
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
                args.ActorUserId == command.ActorUserId
                && args.TargetUserId == command.TargetUserId
                && args.Capability == command.Capability),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSucceedWhenTheServiceGrants()
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
        await MockPlatformCapabilityService.Received(1).GrantAsync(
            Arg.Is<GrantPlatformCapabilityArgs>(args =>
                args.ActorUserId == command.ActorUserId
                && args.TargetUserId == command.TargetUserId
                && args.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
    }

    private static GrantPlatformCapabilityCommand ValidCommand()
    {
        return new GrantPlatformCapabilityCommand
        {
            ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TargetUserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Capability = PlatformCapabilityEnum.Guide
        };
    }
}
