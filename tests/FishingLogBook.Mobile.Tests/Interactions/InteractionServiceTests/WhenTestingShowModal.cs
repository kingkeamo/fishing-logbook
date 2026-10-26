using AwesomeAssertions;
using FishingLogBook.Mobile.Interactions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Interactions.InteractionServiceTests;

public class WhenTestingShowModal : BaseInteractionServiceTest
{
    [Fact]
    public async Task ItShouldPropagateCancellationAndReleaseTheDialog()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var request = new DialogRequest("Title", "Body", "Done", "Cancel");
        MockPresenter.ShowDialogAsync(request, cancellation.Token).Returns(Task.FromCanceled<bool>(new CancellationToken(true)));
        var action = () => Sut.ShowModalAsync("Title", "Body", cancellation.Token);
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        var result = await Sut.ShowModalAsync("Title", "Body");
        // Assert
        result.Should().BeFalse();
        await MockPresenter.Received(1).ShowDialogAsync(request, cancellation.Token);
        await MockPresenter.Received(1).ShowDialogAsync(request, CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldUseLocalisedDismissalAndReturnCloseOrCancel(bool accepted)
    {
        // Arrange
        Text.SetCulture("fr-FR");
        var request = new DialogRequest("Titre", "Contenu", "Terminé", "Annuler");
        MockPresenter.ShowDialogAsync(request, CancellationToken.None).Returns(accepted);
        // Act
        var result = await Sut.ShowModalAsync("Titre", "Contenu");
        // Assert
        result.Should().Be(accepted);
        await MockPresenter.Received(1).ShowDialogAsync(request, CancellationToken.None);
    }
}
