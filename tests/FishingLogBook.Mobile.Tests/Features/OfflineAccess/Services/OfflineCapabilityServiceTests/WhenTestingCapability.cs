using AwesomeAssertions;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
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
            .Returns(
                oldCapability,
                oldCapability,
                oldCapability with { IsDeviceReady = false });
        // Act
        await Sut.RevokeIfOwnedByAnotherAccountAsync(Subject, UserId, CancellationToken.None);
        // Assert
        await MockStore.Received(1).SaveAsync(
            Arg.Is<OfflineCapabilityModel>(value => !value.IsDeviceReady),
            CancellationToken.None);
        await MockStore.Received(1).RemoveAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRevokeACapabilityOwnedByTheSpecifiedAccount()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        var disabled = capability with { IsDeviceReady = false };
        MockStore.GetAsync(CancellationToken.None).Returns(capability, capability, disabled);

        // Act
        await Sut.RevokeIfOwnedByAsync(Subject, UserId, CancellationToken.None);

        // Assert
        await MockStore.Received(1).SaveAsync(disabled, CancellationToken.None);
        await MockStore.Received(1).RemoveAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRetainANewerCapabilityOwnedByAnotherAccount()
    {
        // Arrange
        var newer = new OfflineCapabilityModel("new-subject", Guid.NewGuid(), IsDeviceReady: true);
        MockStore.GetAsync(CancellationToken.None).Returns(newer);

        // Act
        await Sut.RevokeIfOwnedByAsync(Subject, UserId, CancellationToken.None);

        // Assert
        await MockStore.DidNotReceive().SaveAsync(
            Arg.Any<OfflineCapabilityModel>(), Arg.Any<CancellationToken>());
        await MockStore.DidNotReceive().RemoveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemainRevokedWhenDeletionFailsAfterDisabling()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        var disabled = capability with { IsDeviceReady = false };
        MockStore.GetAsync(CancellationToken.None).Returns(capability, disabled);
        MockStore.RemoveAsync(CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure deletion failed")));
        // Act
        await Sut.RevokeAsync(CancellationToken.None);
        var restartedService = new OfflineCapabilityService(MockStore);
        var resultAfterRestart = await restartedService.GetReadyAsync(CancellationToken.None);
        // Assert
        resultAfterRestart.Should().BeNull();
        Received.InOrder(() =>
        {
            MockStore.SaveAsync(disabled, CancellationToken.None);
            MockStore.RemoveAsync(CancellationToken.None);
        });
    }

    [Fact]
    public async Task ItShouldRemainRevokedAfterRestartWhenDisablingFailsButDeletionSucceeds()
    {
        // Arrange
        OfflineCapabilityModel? persisted = new(Subject, UserId, IsDeviceReady: true);
        MockStore.GetAsync(CancellationToken.None).Returns(_ => persisted);
        MockStore.SaveAsync(Arg.Any<OfflineCapabilityModel>(), CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure write failed")));
        MockStore.When(store => store.RemoveAsync(CancellationToken.None))
            .Do(_ => persisted = null);
        // Act
        await Sut.RevokeAsync(CancellationToken.None);
        var restartedService = new OfflineCapabilityService(MockStore);
        var resultAfterRestart = await restartedService.GetReadyAsync(CancellationToken.None);
        // Assert
        resultAfterRestart.Should().BeNull();
        await MockStore.Received(1).RemoveAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldFailWhenNeitherDisablingNorDeletionCanRevokeReadiness()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockStore.GetAsync(CancellationToken.None).Returns(capability);
        MockStore.SaveAsync(Arg.Any<OfflineCapabilityModel>(), CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure write failed")));
        MockStore.RemoveAsync(CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Secure deletion failed")));
        // Act
        var action = () => Sut.RevokeAsync(CancellationToken.None);
        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
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
