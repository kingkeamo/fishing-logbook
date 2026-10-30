using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflinePreparationServiceTests;

public sealed class WhenTestingEnsurePrepared : BaseOfflinePreparationServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnIncompleteOnboardingProfile()
    {
        // Arrange

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(completed: false), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockPreferencesService.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAProfileOwnedByAnotherAccount()
    {
        // Arrange

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockPreferencesService.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBlockReadinessWhenEssentialDataCannotBeDownloaded()
    {
        // Arrange
        MockPreferencesService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBlockReadinessWhenTheDownloadedCatalogueIsIncomplete()
    {
        // Arrange
        var incomplete = Preferences with
        {
            Catalogue = Preferences.Catalogue with { AllSpecies = [] }
        };
        MockPreferencesService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(incomplete));

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockPreferencesStore.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(), Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>());
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBlockReadinessWhenThePersistedPreferencesCannotBeReadBack()
    {
        // Arrange
        MockPreferencesStore.GetAsync(UserId, Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        MockAccountStore.GetAsync(UserId, Arg.Any<CancellationToken>()).Returns(Account());

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockPreferencesStore.Received(1).SaveAsync(UserId, Preferences, Arg.Any<CancellationToken>());
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCommitReadinessAfterTheAccountChanges()
    {
        // Arrange
        ArrangeSuccessfulPersistence();
        MockSessionCoordinator.IsCurrent(Lease).Returns(false);

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReportReadinessWhenTheAccountChangesDuringTheFinalCommit()
    {
        // Arrange
        ArrangeSuccessfulPersistence();
        MockSessionCoordinator.IsCurrent(Lease).Returns(true, true, false);

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockCapabilityService.Received(1).MarkReadyAsync(
            Subject, UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPropagateCancellationWithoutMarkingReadiness()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var action = () => Sut.EnsurePreparedAsync(Profile(), cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemainIncompleteWhenSecureReadinessCannotBePersisted()
    {
        // Arrange
        ArrangeSuccessfulPersistence();
        MockCapabilityService.MarkReadyAsync(Subject, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Secure storage unavailable")));

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await MockAccountStore.Received(1).SaveAsync(
            Arg.Any<OfflineAccountModel>(), Arg.Any<CancellationToken>());
        await MockCapabilityService.Received(1).MarkReadyAsync(
            Subject, UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResumeSafelyAfterAnInterruptedPreparation()
    {
        // Arrange
        ArrangeSuccessfulPersistence();
        MockCapabilityService.MarkReadyAsync(Subject, UserId, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException(new InvalidOperationException("Interrupted")),
                Task.CompletedTask);
        MockCapabilityService.GetReadyAsync(Arg.Any<CancellationToken>())
            .Returns(
                (OfflineCapabilityModel?)null,
                (OfflineCapabilityModel?)null,
                Capability());

        // Act
        var first = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);
        var second = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        first.Should().BeFalse();
        second.Should().BeTrue();
        await MockCapabilityService.Received(2).MarkReadyAsync(
            Subject, UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSkipDownloadsAndWritesWhenTheAccountIsAlreadyPrepared()
    {
        // Arrange
        MockCapabilityService.GetReadyAsync(CancellationToken.None).Returns(Capability());
        MockAccountStore.GetAsync(UserId, CancellationToken.None).Returns(Account());
        MockPreferencesStore.GetAsync(UserId, CancellationToken.None).Returns(Preferences);

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockPreferencesService.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
        await MockAccountStore.DidNotReceive().SaveAsync(
            Arg.Any<OfflineAccountModel>(), Arg.Any<CancellationToken>());
        await MockCapabilityService.DidNotReceive().MarkReadyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistAndVerifyEssentialDataBeforeMarkingReadiness()
    {
        // Arrange
        ArrangeSuccessfulPersistence();

        // Act
        var result = await Sut.EnsurePreparedAsync(Profile(), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockPreferencesStore.Received(1).SaveAsync(UserId, Preferences, Arg.Any<CancellationToken>());
        await MockAccountStore.Received(1).SaveAsync(
            Arg.Is<OfflineAccountModel>(account =>
                account.Subject == Subject &&
                account.UserId == UserId &&
                account.Email == Session.Email &&
                account.Profile.OnboardingCompleted),
            Arg.Any<CancellationToken>());
        await MockCapabilityService.Received(1).MarkReadyAsync(
            Subject, UserId, Arg.Any<CancellationToken>());
    }
}
