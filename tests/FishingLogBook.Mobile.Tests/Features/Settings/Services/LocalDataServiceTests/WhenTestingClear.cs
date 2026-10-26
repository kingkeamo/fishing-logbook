using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.LocalDataServiceTests;

public class WhenTestingClear
{
    private readonly IInteractionPresenter _presenter = Substitute.For<IInteractionPresenter>();
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));
    private readonly LocalDataService _sut;

    public WhenTestingClear()
    {
        _sut = new LocalDataService(new InteractionService(_presenter, _text), _text);
    }

    [Fact]
    public async Task ItShouldAskForDestructiveConfirmationBeforeDoingAnything()
    {
        // Arrange
        _presenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        // Act
        await _sut.ClearAsync();
        // Assert
        await _presenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request => request.IsDestructive && request.Title == _text["ClearLocalDataTitle"] && request.Message == _text["ClearLocalDataBody"] && request.Accept == _text["Remove"] && request.Cancel == _text["Cancel"]),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingWhenTheUserCancels()
    {
        // Arrange
        _presenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        // Act
        var cleared = await _sut.ClearAsync();
        // Assert
        cleared.Should().BeFalse();
        await _presenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportTheActionWhenTheUserConfirms()
    {
        // Arrange
        _presenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        // Act
        var cleared = await _sut.ClearAsync();
        // Assert
        cleared.Should().BeTrue();
        await _presenter.Received(1).NotifyAsync(Arg.Is<NotificationRequest>(request => request.Message.Contains(_text["ActionUnavailable"])), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTheSelectedLanguageForTheConfirmation()
    {
        // Arrange
        _text.SetCulture("fr-FR");
        _presenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        // Act
        await _sut.ClearAsync();
        // Assert
        await _presenter.Received(1).ShowDialogAsync(Arg.Is<DialogRequest>(request => request.Title == _text["ClearLocalDataTitle"] && request.Cancel == "Annuler"), Arg.Any<CancellationToken>());
    }
}
