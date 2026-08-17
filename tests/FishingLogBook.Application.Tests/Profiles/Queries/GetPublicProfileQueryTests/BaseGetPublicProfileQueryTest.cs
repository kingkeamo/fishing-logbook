using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Queries.GetPublicProfileQueryTests;

public class BaseGetPublicProfileQueryTest
{
    protected readonly IProfileService MockProfileService = Substitute.For<IProfileService>();
    protected readonly GetPublicProfileHandler Sut;

    protected BaseGetPublicProfileQueryTest()
    {
        Sut = new GetPublicProfileHandler(MockProfileService);
    }

    protected static PublicProfileDto PublicProfile(Guid userId)
    {
        return new PublicProfileDto(
            userId,
            "Eamonn",
            null,
            "Westmeath",
            ["Fly"],
            ["Pike"]);
    }
}
