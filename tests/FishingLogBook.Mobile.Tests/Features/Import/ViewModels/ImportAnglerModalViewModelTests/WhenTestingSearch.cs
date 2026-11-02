using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using System.Globalization;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportAnglerModalViewModelTests;

public sealed class WhenTestingSearch
{
    [Fact]
    public async Task ItShouldReturnMatchingAnglersAndExcludeAlreadySelectedPeople()
    {
        // Arrange
        var excluded = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var included = new AnglerSummaryDto(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Pat Lake",
            null,
            null,
            "pat@example.test");
        var lookup = Substitute.For<IAnglerLookupClient>();
        lookup.FindAsync("pat", Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<List<AnglerSummaryDto>>.Succeeded(
            [
                new AnglerSummaryDto(excluded, "Already Added", null, null, null),
                included
            ]));
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);
        var sut = new ImportAnglerModalViewModel(
            lookup,
            connectivity,
            new UiTextService(CultureInfo.GetCultureInfo("en-GB")),
            [excluded]);
        sut.SearchText = "pat";

        // Act
        await sut.SearchAsync(CancellationToken.None);

        // Assert
        sut.Results.Should().ContainSingle().Which.Should().Be(included);
        sut.ShowEmpty.Should().BeFalse();
        await lookup.Received(1).FindAsync("pat", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSurfaceAnEmptyResult()
    {
        // Arrange
        var lookup = Substitute.For<IAnglerLookupClient>();
        lookup.FindAsync("zzz", Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<AnglerSummaryDto>>.Succeeded([]));
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);
        var sut = new ImportAnglerModalViewModel(
            lookup,
            connectivity,
            new UiTextService(CultureInfo.GetCultureInfo("en-GB")),
            []);
        sut.SearchText = "zzz";

        // Act
        await sut.SearchAsync(CancellationToken.None);

        // Assert
        sut.Results.Should().BeEmpty();
        sut.ShowEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotSearchWhenOffline()
    {
        // Arrange
        var lookup = Substitute.For<IAnglerLookupClient>();
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(false);
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var sut = new ImportAnglerModalViewModel(lookup, connectivity, text, []);
        sut.SearchText = "pat";

        // Act
        await sut.SearchAsync(CancellationToken.None);

        // Assert
        sut.MessageText.Should().Be(text["Import_OnlineRequired"]);
        await lookup.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireThreeCharactersBeforeSearching()
    {
        // Arrange
        var lookup = Substitute.For<IAnglerLookupClient>();
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);
        var sut = new ImportAnglerModalViewModel(
            lookup,
            connectivity,
            new UiTextService(CultureInfo.GetCultureInfo("en-GB")),
            []);
        sut.SearchText = "pa";

        // Act
        await sut.SearchAsync(CancellationToken.None);

        // Assert
        sut.CanSearch.Should().BeFalse();
        await lookup.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAFailureWhenLookupFails()
    {
        // Arrange
        var lookup = Substitute.For<IAnglerLookupClient>();
        lookup.FindAsync("pat", Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<AnglerSummaryDto>>.Failed(ApiOutcomeEnum.Unavailable));
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(true);
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var sut = new ImportAnglerModalViewModel(lookup, connectivity, text, []);
        sut.SearchText = "pat";

        // Act
        await sut.SearchAsync(CancellationToken.None);

        // Assert
        sut.MessageText.Should().Be(text["TripInviteSearchFailed"]);
        sut.Results.Should().BeEmpty();
    }
}
