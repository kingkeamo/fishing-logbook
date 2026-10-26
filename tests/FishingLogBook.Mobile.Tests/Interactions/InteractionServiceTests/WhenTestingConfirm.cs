using AwesomeAssertions;
using FishingLogBook.Mobile.Interactions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Interactions.InteractionServiceTests;

public class WhenTestingConfirm : BaseInteractionServiceTest
{
    [Fact]
    public async Task ItShouldRejectMissingActionLabels()
    {
        // Arrange
        var action = () => Sut.ConfirmAsync(Request with { Cancel = "" });
        // Act
        await action.Should().ThrowAsync<ArgumentException>();
        // Assert
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSuppressDuplicateDialogsWithoutAcceptingThem()
    {
        // Arrange
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockPresenter.ShowDialogAsync(Request, CancellationToken.None).Returns(pending.Task);
        // Act
        var first = Sut.ConfirmAsync(Request);
        var duplicate = await Sut.ConfirmAsync(Request);
        pending.SetResult(true);
        var accepted = await first;
        // Assert
        duplicate.Should().BeFalse();
        accepted.Should().BeTrue();
        await MockPresenter.Received(1).ShowDialogAsync(Request, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldAllowAnotherDialogAfterPresenterFailure()
    {
        // Arrange
        MockPresenter.ShowDialogAsync(Request, CancellationToken.None).Returns(Task.FromException<bool>(new InvalidOperationException("Failed")), Task.FromResult(false));
        var action = () => Sut.ConfirmAsync(Request);
        // Act
        await action.Should().ThrowAsync<InvalidOperationException>();
        var result = await Sut.ConfirmAsync(Request);
        // Assert
        result.Should().BeFalse();
        await MockPresenter.Received(2).ShowDialogAsync(Request, CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldPreserveTheConfirmationOutcomeAndDestructiveIntent(bool accepted)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        MockPresenter.ShowDialogAsync(Request, cancellation.Token).Returns(accepted);
        // Act
        var result = await Sut.ConfirmAsync(Request, cancellation.Token);
        // Assert
        result.Should().Be(accepted);
        await MockPresenter.Received(1).ShowDialogAsync(Request, cancellation.Token);
    }
}
