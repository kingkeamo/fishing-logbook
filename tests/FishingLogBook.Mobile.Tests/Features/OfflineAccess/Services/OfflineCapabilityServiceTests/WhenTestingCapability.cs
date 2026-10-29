using AwesomeAssertions;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflineCapabilityServiceTests;

public sealed class WhenTestingCapability : BaseOfflineCapabilityServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnIncompleteCapability()
    {
        // Arrange
        MockStore.GetAsync(CancellationToken.None)
            .Returns(new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: false));
        // Act
        var result = await Sut.GetReadyAsync(CancellationToken.None);
        // Assert
        result.Should().BeNull();
        await MockStore.Received(1).GetAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRevokeACapabilityOwnedByAnotherAccount()
    {
        // Arrange
        var oldCapability = new OfflineCapabilityModel(
            "old-subject",
            Guid.NewGuid(),
            IsDeviceReady: true);
        MockStore.GetAsync(CancellationToken.None)
            .Returns(oldCapability, oldCapability with { IsDeviceReady = false });
        // Act
        await Sut.RevokeIfOwnedByAnotherAccountAsync(Subject, UserId, CancellationToken.None);
        // Assert
        await MockStore.Received(1).SaveAsync(
            Arg.Is<OfflineCapabilityModel>(value => !value.IsDeviceReady),
            CancellationToken.None);
        await MockStore.Received(1).RemoveAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldDisableTheCapabilityBeforeAttemptingDeletion()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        var disabled = capability with { IsDeviceReady = false };
        MockStore.GetAsync(CancellationToken.None).Returns(capability, disabled);
        MockStore.RemoveAsync(CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure deletion failed")));
        // Act
        var action = () => Sut.RevokeAsync(CancellationToken.None);
        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        Received.InOrder(() =>
        {
            MockStore.SaveAsync(disabled, CancellationToken.None);
            MockStore.RemoveAsync(CancellationToken.None);
        });
    }

    [Fact]
    public async Task ItShouldRetainACapabilityForTheSameAccount()
    {
        // Arrange
        MockStore.GetAsync(CancellationToken.None)
            .Returns(new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true));
        // Act
        await Sut.RevokeIfOwnedByAnotherAccountAsync(Subject, UserId, CancellationToken.None);
        // Assert
        await MockStore.Received(1).GetAsync(CancellationToken.None);
        await MockStore.DidNotReceive().SaveAsync(
            Arg.Any<OfflineCapabilityModel>(),
            Arg.Any<CancellationToken>());
        await MockStore.DidNotReceive().RemoveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailReadinessWhenThePersistedCapabilityCannotBeVerified()
    {
        // Arrange
        MockStore.GetAsync(CancellationToken.None).Returns((OfflineCapabilityModel?)null);
        // Act
        var action = () => Sut.MarkReadyAsync(Subject, UserId, CancellationToken.None);
        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        await MockStore.Received(1).SaveAsync(
            Arg.Is<OfflineCapabilityModel>(value => value.Subject == Subject && value.UserId == UserId),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldPersistAReadyCapabilityForTheBoundIdentity()
    {
        // Arrange
        MockStore.GetAsync(CancellationToken.None)
            .Returns(new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true));
        // Act
        await Sut.MarkReadyAsync(Subject, UserId, CancellationToken.None);
        // Assert
        await MockStore.Received(1).SaveAsync(
            Arg.Is<OfflineCapabilityModel>(value =>
                value.Subject == Subject && value.UserId == UserId && value.IsDeviceReady),
            CancellationToken.None);
        await MockStore.Received(1).GetAsync(CancellationToken.None);
    }
}
