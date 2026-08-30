using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.UpdateOwnProfileCommandTests;

public class BaseUpdateOwnProfileCommandTest
{
    protected readonly IProfileService MockProfileService = Substitute.For<IProfileService>();
    protected readonly UpdateOwnProfileHandler Sut;

    protected BaseUpdateOwnProfileCommandTest()
    {
        Sut = new UpdateOwnProfileHandler(MockProfileService, TestMapper.Create());
    }

    protected static UpdateOwnProfileCommand Command(
        Guid userId,
        WeightUnitEnum weightUnit = WeightUnitEnum.Kg,
        LengthUnitEnum lengthUnit = LengthUnitEnum.Cm)
    {
        return new UpdateOwnProfileCommand
        {
            UserId = userId,
            Profile = new UpdateProfileDto(
                "Eamonn",
                "Westmeath",
                true,
                false,
                true,
                true,
                false,
                weightUnit,
                lengthUnit)
        };
    }

    protected static ProfileDto OwnProfile(Guid userId)
    {
        return new ProfileDto(
            userId,
            "Eamonn",
            null,
            null,
            null,
            "Westmeath",
            true,
            false,
            true,
            true,
            false);
    }
}
