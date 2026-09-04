using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals.AnglerPicker;
using FishingLogBook.Web.Features.Profile.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.AnglerPickerModalTests;

public class WhenTestingSelect : BaseAnglerPickerModalTest
{
    [Fact]
    public async Task ItShouldReturnTheSelectedAnglerWithoutPersistingAnything()
    {
        // Arrange
        var angler = new AnglerSummaryDto(Guid.NewGuid(), "Patrick", null, "Galway", null);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.FindAnglersAsync("Patrick", Arg.Any<CancellationToken>()).Returns([angler]);
        await using var context = CreateContext(profileClient);
        var (cut, dialog) = await ShowAsync(context);
        await cut.Find("#angler-picker-search").InputAsync(new() { Value = "Patrick" });
        cut.WaitForElement($"#angler-picker-select-{angler.UserId:D}");

        // Act
        await cut.Find($"#angler-picker-select-{angler.UserId:D}").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result!.Canceled.Should().BeFalse();
        result.Data.Should().Be(new AnglerPickerModalResult(angler));
        await profileClient.Received(1).FindAnglersAsync("Patrick", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExcludeAlreadySelectedAnglers()
    {
        // Arrange
        var angler = new AnglerSummaryDto(Guid.NewGuid(), "Patrick", null, null, null);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.FindAnglersAsync("Patrick", Arg.Any<CancellationToken>()).Returns([angler]);
        await using var context = CreateContext(profileClient);
        var (cut, _) = await ShowAsync(context, new AnglerPickerModalModel([angler.UserId]));

        // Act
        await cut.Find("#angler-picker-search").InputAsync(new() { Value = "Patrick" });

        // Assert
        cut.WaitForAssertion(() => cut.Find("#angler-picker-empty").Should().NotBeNull());
        cut.FindAll($"#angler-picker-select-{angler.UserId:D}").Should().BeEmpty();
        await profileClient.Received(1).FindAnglersAsync("Patrick", Arg.Any<CancellationToken>());
    }
}
