using AwesomeAssertions;
using FishingLogBook.Mobile.Interactions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Interactions.InteractionServiceTests;

public class WhenTestingNotify : BaseInteractionServiceTest
{
    [Fact]
    public async Task ItShouldRejectBlankFeedback()
    {
        // Arrange
        var action = () => Sut.NotifyAsync(NotificationKindEnum.Info, " ");
        // Act
        await action.Should().ThrowAsync<ArgumentException>();
        // Assert
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowCancelledNotifications()
    {
        // Arrange
        var action = () => Sut.NotifyAsync(NotificationKindEnum.Info, "Message", new CancellationToken(true));
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(NotificationKindEnum.Success, "Succès")]
    [InlineData(NotificationKindEnum.Info, "Information")]
    [InlineData(NotificationKindEnum.Warning, "Attention")]
    [InlineData(NotificationKindEnum.Error, "Erreur")]
    public async Task ItShouldUseLocalisedSeverityAsWellAsColour(NotificationKindEnum kind, string label)
    {
        // Arrange
        Text.SetCulture("fr-FR");
        using var cancellation = new CancellationTokenSource();
        // Act
        await Sut.NotifyAsync(kind, "Message", cancellation.Token);
        // Assert
        await MockPresenter.Received(1).NotifyAsync(new NotificationRequest(kind, label + " · Message", "Fermer"), cancellation.Token);
    }
}
