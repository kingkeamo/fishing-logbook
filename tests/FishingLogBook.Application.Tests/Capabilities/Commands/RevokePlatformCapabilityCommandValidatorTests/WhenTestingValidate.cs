using FishingLogBook.Application.Capabilities.Commands;
using FishingLogBook.Domain.Enums;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Capabilities.Commands.RevokePlatformCapabilityCommandValidatorTests;

public class WhenTestingValidate : BaseRevokePlatformCapabilityCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenTargetUserIdIsEmpty()
    {
        // Arrange
        var command = Command(targetUserId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetUserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenCapabilityIsNotDefined()
    {
        // Arrange
        var command = Command(capability: (PlatformCapabilityEnum)999);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Capability);
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static RevokePlatformCapabilityCommand Command(
        Guid? targetUserId = null,
        PlatformCapabilityEnum capability = PlatformCapabilityEnum.FishingVenueManager)
    {
        return new RevokePlatformCapabilityCommand
        {
            TargetUserId = targetUserId ?? Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Capability = capability
        };
    }
}
