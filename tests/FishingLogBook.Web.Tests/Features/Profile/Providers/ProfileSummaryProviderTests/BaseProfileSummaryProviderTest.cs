using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Providers;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.ProfileSummaryProviderTests;

public class BaseProfileSummaryProviderTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly IProfileClient MockProfileClient = Substitute.For<IProfileClient>();
    protected readonly ILocalCatchOwnerService MockLocalCatchOwner = Substitute.For<ILocalCatchOwnerService>();
    protected readonly ProfileSummaryProvider Sut;

    protected BaseProfileSummaryProviderTest()
    {
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        Sut = new ProfileSummaryProvider(MockProfileClient, MockLocalCatchOwner);
    }

    protected static ProfileDto Profile(
        Guid userId,
        string? displayName = "Eamonn",
        string? photographUrl = "https://cdn.test/photo.jpg")
    {
        return new ProfileDto(
            userId,
            displayName,
            photographUrl is null ? null : Guid.NewGuid(),
            photographUrl,
            photographUrl is null ? null : "image/jpeg",
            null,
            [],
            [],
            true,
            true,
            false,
            false,
            false);
    }
}
