using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Queries.GetOwnProfileQueryTests;

public class BaseGetOwnProfileQueryTest
{
    protected readonly IProfileService MockProfileService = Substitute.For<IProfileService>();
    protected readonly GetOwnProfileHandler Sut;

    protected BaseGetOwnProfileQueryTest()
    {
        Sut = new GetOwnProfileHandler(MockProfileService);
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
