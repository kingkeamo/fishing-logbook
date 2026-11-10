using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.AnglerPickerModalTests;

public class WhenTestingSearch : BaseAnglerPickerModalTest
{
    [Fact]
    public async Task ItShouldNotSearchForAQueryThatIsTooShort()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        await using var context = CreateContext(profileClient);
        var (cut, _) = await ShowAsync(context);

        // Act
        await cut.Find("#angler-picker-search").InputAsync(new() { Value = "ab" });

        // Assert
        cut.FindAll("#angler-picker-empty").Should().BeEmpty();
        cut.FindAll("#angler-picker-searching").Should().BeEmpty();
        await profileClient.DidNotReceive().FindAnglersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimTheQueryAndSayWhenNobodyMatches()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.FindAnglersAsync("mary", Arg.Any<CancellationToken>()).Returns([]);
        await using var context = CreateContext(profileClient);
        var (cut, _) = await ShowAsync(context);

        // Act
        await cut.Find("#angler-picker-search").InputAsync(new() { Value = "  mary  " });

        // Assert
        cut.WaitForElement("#angler-picker-empty");
        await profileClient.Received(1).FindAnglersAsync("mary", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAFailureAndLogItWhenTheSearchFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.FindAnglersAsync("mary", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<AnglerSummaryDto>>>(_ => throw new HttpRequestException("down"));
        await using var context = CreateContext(profileClient);
        var logging = context.Services.GetService(typeof(ILoggingService)) as ILoggingService;
        var (cut, _) = await ShowAsync(context);

        // Act
        await cut.Find("#angler-picker-search").InputAsync(new() { Value = "mary" });

        // Assert
        cut.WaitForElement("#angler-picker-failed");
        await logging!.Received(1).LogErrorAsync(
            "searching for anglers",
            Arg.Is<Exception>(exception => exception is HttpRequestException),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNameAnAnglerByDisplayNameThenEmailThenAsUnknown()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var named = new AnglerSummaryDto(Guid.NewGuid(), "Mary", null, null, "mary@example.test");
        var emailOnly = new AnglerSummaryDto(Guid.NewGuid(), " ", null, null, "tom@example.test");
        var nameless = new AnglerSummaryDto(Guid.NewGuid(), null, null, null, " ");
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.FindAnglersAsync("angler", Arg.Any<CancellationToken>()).Returns([named, emailOnly, nameless]);
        await using var context = CreateContext(profileClient);
        var (cut, _) = await ShowAsync(context);

        // Act
        await cut.Find("#angler-picker-search").InputAsync(new() { Value = "angler" });

        // Assert
        cut.WaitForElement($"#angler-picker-result-{named.UserId:D}");
        cut.Find($"#angler-picker-result-{named.UserId:D}").TextContent.Should().Contain("Mary");
        cut.Find($"#angler-picker-result-{emailOnly.UserId:D}").TextContent.Should().Contain("tom@example.test");
        cut.Find($"#angler-picker-result-{nameless.UserId:D}").TextContent.Should().Contain("Another angler");
    }
}
