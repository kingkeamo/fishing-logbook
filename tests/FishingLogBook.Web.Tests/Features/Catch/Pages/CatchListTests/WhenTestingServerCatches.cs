using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingServerCatches : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldIncludeServerOnlyCatchesInTheList()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CatchModel>());
        var serverCatchId = Guid.NewGuid();
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)
            [
                new CatchViewDto(serverCatchId, OwnerUserId, DateTimeOffset.UtcNow)
                {
                    SpeciesName = "Perch",
                    AnglerUserId = OwnerUserId,
                    RecordedByUserId = OwnerUserId
                }
            ]);
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{serverCatchId:D}").TextContent.Should().Contain("Perch"));
    }

    [Fact]
    public async Task ItShouldPreferTheLocalCatchWhenTheSameIdExistsOnTheServer()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var sharedId = Guid.NewGuid();
        var local = StoredCatch(sharedId, DateTimeOffset.UtcNow, speciesName: "Local Pike");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new[] { local });
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)
            [
                new CatchViewDto(sharedId, OwnerUserId, DateTimeOffset.UtcNow)
                {
                    SpeciesName = "Server Pike",
                    AnglerUserId = OwnerUserId,
                    RecordedByUserId = OwnerUserId
                }
            ]);
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{sharedId:D}").TextContent.Should().Contain("Local Pike"));
        cut.Markup.Should().NotContain("Server Pike");
    }

    [Fact]
    public async Task ItShouldStillShowLocalCatchesWhenTheServerFetchFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var localId = Guid.NewGuid();
        var local = StoredCatch(localId, DateTimeOffset.UtcNow, speciesName: "Trout");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new[] { local });
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, catchClient: catchClient, logging: logging);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{localId:D}").TextContent.Should().Contain("Trout"));
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
        await logging.Received(1).LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<HttpRequestException>(),
            Arg.Any<CancellationToken>());
    }
}
