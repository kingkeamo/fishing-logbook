using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
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
        CatchLocationDto? location = null)
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
                location)
        };
    }

    protected static CatchLocationDto PrivateLocation()
    {
        return new CatchLocationDto(
            53.4,
            -7.9,
            12,
            DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }
}
