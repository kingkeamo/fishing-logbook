using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.UpdateOwnProfileCommandTests;

public class BaseUpdateOwnProfileCommandTest
{
    protected readonly IProfileService MockProfileService = Substitute.For<IProfileService>();
    protected readonly UpdateOwnProfileHandler Sut;

    protected BaseUpdateOwnProfileCommandTest()
    {
        ((IRegister)new ProfileMappingRegistration()).Register(TypeAdapterConfig.GlobalSettings);
        Sut = new UpdateOwnProfileHandler(MockProfileService);
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
                ["Coarse"],
                ["Pike"],
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
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false);
    }
}
