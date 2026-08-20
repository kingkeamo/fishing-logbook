using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingSave : BaseProfileTest
{
    [Fact]
    public async Task ItShouldShowLoadFailureAndNotRenderASuccessfulForm()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns<ProfileDto>(_ => throw new HttpRequestException("Unable to load profile."));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-load-failed").TextContent.Should().Contain("Unable to load profile.");
            cut.FindAll("#profile-save-button").Should().BeEmpty();
            cut.FindAll("#profile-display-name").Should().BeEmpty();
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLoadFailureCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns<ProfileDto>(_ => throw new HttpRequestException("Unable to load profile."));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#profile-load-failed").TextContent.Should().Contain("Impossible de charger le profil."));
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheLoadedProfileWithoutChangingFields()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new ProfileDto(
                userId,
                "Eamonn",
                null,
                null,
                null,
                "Westmeath",
                true,
                true,
                true,
                true,
                false));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.ShowDisplayName
                && profile.ShowPhotograph
                && profile.ShowHomeRegion
                && profile.ShowPreferredFishingMethods
                && !profile.ShowPreferredSpecies),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowSaveFailureWhenUpdateFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns<ProfileDto>(_ => throw new HttpRequestException("Unable to save profile."));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#profile-save-failed").TextContent.Should().Contain("Unable to save profile."));
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotIssueADuplicateUpdateWhileSaveIsInProgress()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var saveStarted = new TaskCompletionSource();
        var saveContinue = new TaskCompletionSource<ProfileDto>();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                saveStarted.TrySetResult();
                return await saveContinue.Task;
            });
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        var firstClick = cut.Find("#profile-save-button").ClickAsync();
        await saveStarted.Task;
        await cut.Find("#profile-save-button").ClickAsync();
        saveContinue.SetResult(EmptyProfile(userId));
        await firstClick;

        // Assert
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendToggledVisibilityFlags()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        await using var context = CreateContext(profileClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        cut.Find("#profile-show-home-region").Change(true);
        cut.Find("#profile-show-preferred-species").Change(true);
        cut.Find("#profile-show-photograph").Change(true);
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.ShowDisplayName
                && profile.ShowHomeRegion
                && !profile.ShowPreferredFishingMethods
                && profile.ShowPreferredSpecies
                && profile.ShowPhotograph),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveDisplayNameHomeRegionPreferencesAndVisibility()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new ProfileDto(
                userId,
                null,
                null,
                null,
                null,
                null,
                true,
                false,
                false,
                false,
                false));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-display-name"));

        // Act
        cut.Find("#profile-display-name").Input("Eamonn");
        cut.Find("#profile-home-region").Input("Westmeath");
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.ShowDisplayName
                && !profile.ShowPhotograph
                && !profile.ShowHomeRegion
                && !profile.ShowPreferredFishingMethods
                && !profile.ShowPreferredSpecies),
            Arg.Any<CancellationToken>());
        await preferenceClient.Received(1).UpdatePreferencesAsync(
            Arg.Is<UpdateFishingPreferencesDto>(update =>
                update.Methods.Count == 1
                && update.Methods[0].FishingMethodId == FlyMethodId
                && update.Methods[0].IsDefault
                && update.Methods[0].Species.Count == 1
                && update.Methods[0].Species[0].SpeciesId == BrownTroutSpeciesId
                && update.Methods[0].Species[0].IsDefault),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRefreshTheUserMenuSummaryWhenSaveFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns<ProfileDto>(_ => throw new HttpRequestException("Unable to save profile."));
        var profileSummary = Substitute.For<IProfileSummaryProvider>();
        await using var context = CreateContext(profileClient, profileSummary: profileSummary);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#profile-save-failed").TextContent.Should().Contain("Unable to save profile."));
        await profileSummary.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshTheUserMenuSummaryAfterASuccessfulSave()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var profileSummary = Substitute.For<IProfileSummaryProvider>();
        await using var context = CreateContext(profileClient, profileSummary: profileSummary);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileSummary.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
    }
}
