using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.ProfileRepositoryTests;

public class WhenTestingFindAnglers : BaseProfileRepositoryTest
{
    public WhenTestingFindAnglers(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldNeverMatchTheRequestingAngler()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var displayName = UniqueName();
        await Sut.UpsertAsync(ProfileFor(requestingUserId, displayName), CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(Args(requestingUserId, displayName), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotMatchTheNameOfAnAnglerWhoHidesIt()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var hiddenUserId = await CreateUserAsync();
        var displayName = UniqueName();
        await Sut.UpsertAsync(
            ProfileFor(hiddenUserId, displayName, showDisplayName: false),
            CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(Args(requestingUserId, displayName), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldTreatWildcardCharactersAsLiteralText()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var matchedUserId = await CreateUserAsync();
        await Sut.UpsertAsync(ProfileFor(matchedUserId, UniqueName()), CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(Args(requestingUserId, "%"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNeverReturnMoreThanTheRequestedMaximum()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var shared = UniqueName();
        for (var index = 0; index < 3; index++)
        {
            var userId = await CreateUserAsync();
            await Sut.UpsertAsync(ProfileFor(userId, $"{shared} {index}"), CancellationToken.None);
        }

        // Act
        var result = await Sut.FindAnglersAsync(
            Args(requestingUserId, shared, maxResults: 2),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldMatchPartOfTheNameCaseInsensitively()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var matchedUserId = await CreateUserAsync();
        var displayName = UniqueName();
        await Sut.UpsertAsync(ProfileFor(matchedUserId, displayName), CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(
            Args(requestingUserId, displayName.ToUpperInvariant()),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(matchedUserId);
        result.Value[0].DisplayName.Should().Be(displayName);
    }

    [Fact]
    public async Task ItShouldHideTheRegionAndPhotographTheAnglerKeepsPrivate()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var matchedUserId = await CreateUserAsync();
        var displayName = UniqueName();
        await Sut.UpsertAsync(
            ProfileFor(matchedUserId, displayName, showHomeRegion: false, showPhotograph: false),
            CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(Args(requestingUserId, displayName), CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].HomeRegion.Should().BeNull();
        result.Value[0].PhotographObjectKey.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldFindAnAnglerByTheirExactEmailEvenWithoutAProfile()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var email = $"{Guid.NewGuid():N}@example.test";
        var user = new UserBuilder().WithEmail(email).Build();
        var identity = new UserIdentityBuilder().ForUser(user).Build();
        var created = await Users.CreateAsync(user, identity, CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(
            Args(requestingUserId, email.ToUpperInvariant()),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(created.Value);
        result.Value[0].DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldFindAnAnglerByPartOfTheirEmailCaseInsensitively()
    {
        // Arrange
        var requestingUserId = await CreateUserAsync();
        var emailLocalPart = Guid.NewGuid().ToString("N");
        var email = $"{emailLocalPart}@example.test";
        var user = new UserBuilder().WithEmail(email).Build();
        var identity = new UserIdentityBuilder().ForUser(user).Build();
        var created = await Users.CreateAsync(user, identity, CancellationToken.None);

        // Act
        var result = await Sut.FindAnglersAsync(
            Args(requestingUserId, emailLocalPart[..8].ToUpperInvariant()),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(created.Value);
        result.Value[0].DisplayName.Should().BeNull();
    }

    private static string UniqueName()
    {
        return $"Angler{Guid.NewGuid():N}"[..20];
    }

    private static Profile ProfileFor(
        Guid userId,
        string displayName,
        bool showDisplayName = true,
        bool showHomeRegion = true,
        bool showPhotograph = true)
    {
        return new Profile
        {
            UserId = userId,
            DisplayName = displayName,
            HomeRegion = "Galway",
            PhotographObjectKey = "profiles/photo.jpg",
            ShowDisplayName = showDisplayName,
            ShowHomeRegion = showHomeRegion,
            ShowPhotograph = showPhotograph
        };
    }

    private static FindAnglersArgs Args(Guid requestingUserId, string query, int maxResults = 0)
    {
        return new FindAnglersArgs
        {
            RequestingUserId = requestingUserId,
            Query = query,
            MaxResults = maxResults == 0 ? AnglerLookupConstants.MaxResults : maxResults
        };
    }
}
