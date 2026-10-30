using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflinePreparationServiceTests;

public sealed class WhenTestingIsPrepared : BaseOfflinePreparationServiceTest
{
    [Fact]
    public async Task ItShouldRejectCachedDataOwnedByAnotherSubject()
    {
        // Arrange
        var capability = Capability();
        MockCapabilityService.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockAccountStore.GetAsync(UserId, CancellationToken.None)
            .Returns(Account() with { Subject = "another-subject" });
        MockPreferencesStore.GetAsync(UserId, CancellationToken.None).Returns(Preferences);

        // Act
        var result = await Sut.IsPreparedAsync(capability, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectAReadyCapabilityWithoutEssentialCachedData()
    {
        // Arrange
        var capability = Capability();
        MockCapabilityService.GetReadyAsync(CancellationToken.None).Returns(capability);

        // Act
        var result = await Sut.IsPreparedAsync(capability, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectCorruptAccountScopedData()
    {
        // Arrange
        var capability = Capability();
        MockCapabilityService.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockAccountStore.GetAsync(UserId, CancellationToken.None)
            .Returns(Task.FromException<OfflineAccountModel?>(new JsonException("Corrupt cache")));

        // Act
        var result = await Sut.IsPreparedAsync(capability, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldVerifyAccountScopedReadiness()
    {
        // Arrange
        var capability = Capability();
        MockCapabilityService.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockAccountStore.GetAsync(UserId, CancellationToken.None).Returns(Account());
        MockPreferencesStore.GetAsync(UserId, CancellationToken.None).Returns(Preferences);

        // Act
        var result = await Sut.IsPreparedAsync(capability, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }
}
