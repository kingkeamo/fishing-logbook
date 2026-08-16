using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingLocationDenied : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldSaveCatchWithoutLocation_WhenPermissionIsDenied()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatchModel>();
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatchModel>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatchModel>>(saved.ToArray()));
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Roach");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            saved[0].Location.Should().BeNull();
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Roach");
            cut.Find($"#test-catch-location-missing-{saved[0].Id}").Should().NotBeNull();
            cut.Find($"#test-catch-location-missing-{saved[0].Id}").ClassList.Should().Contain(c => c.Contains("error"));
            cut.FindAll($"#test-catch-location-saved-{saved[0].Id}").Should().BeEmpty();
            cut.FindAll($"#remove-test-catch-location-{saved[0].Id}").Should().BeEmpty();
            cut.FindAll("#test-catch-location-explainer").Should().BeEmpty();
            cut.Find("#test-catch-location-enable").Should().NotBeNull();
        });
    }
}
