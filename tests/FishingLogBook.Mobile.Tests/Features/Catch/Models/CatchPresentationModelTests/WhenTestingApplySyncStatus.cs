using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingApplySyncStatus : BaseCatchPresentationModelTest
{
    [Fact]
    public void ItShouldShowNoStatusForACatchThatIsNotStoredOnTheDevice()
    {
        // Arrange
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png"]);

        // Act
        sut.ApplySyncStatus(CreateStatus(CatchSyncStateEnum.Synced));

        // Assert
        sut.HasSaveStatus.Should().BeFalse();
        sut.SaveStatus.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldShowTheSavedLocallyTextUntilAPersistedStatusIsKnown()
    {
        // Arrange
        // Act
        var sut = CreateStored("en-GB");

        // Assert
        sut.SaveStatus.Should().Be("Saved locally on this device.");
        sut.CanRetrySync.Should().BeFalse();
        sut.SyncState.Should().Be(CatchSyncStateEnum.WaitingToSync);
    }

    [Theory]
    [InlineData("en-GB", CatchSyncStateEnum.WaitingToSync, "Saved locally on this device. Waiting to sync.", false)]
    [InlineData("en-GB", CatchSyncStateEnum.Synchronising, "Syncing…", false)]
    [InlineData("en-GB", CatchSyncStateEnum.Synced, "Synced.", false)]
    [InlineData("en-GB", CatchSyncStateEnum.NeedsAttention, "Sync needs attention.", true)]
    [InlineData("en-GB", CatchSyncStateEnum.ActionRequired, "Couldn’t sync. Check the catch details, then retry.", true)]
    [InlineData("fr-FR", CatchSyncStateEnum.Synced, "Synchronisée.", false)]
    [InlineData("fr-FR", CatchSyncStateEnum.ActionRequired, "Synchronisation impossible. Vérifiez la prise, puis réessayez.", true)]
    public void ItShouldDescribeEachPersistedStateInTheSelectedCulture(
        string culture,
        CatchSyncStateEnum state,
        string expectedText,
        bool canRetry)
    {
        // Arrange
        var sut = CreateStored(culture);

        // Act
        sut.ApplySyncStatus(CreateStatus(state, metadataSynced: state == CatchSyncStateEnum.Synced, canRetry: canRetry));

        // Assert
        sut.SaveStatus.Should().Be(expectedText);
        sut.SyncState.Should().Be(state);
        sut.CanRetrySync.Should().Be(canRetry);
        sut.CardDescription.Should().EndWith(expectedText);
    }

    [Theory]
    [InlineData(CatchSyncStateEnum.WaitingToSync, "Details synced · 1 of 3 photos uploaded. Waiting to sync.")]
    [InlineData(CatchSyncStateEnum.NeedsAttention, "Details synced · 1 of 3 photos uploaded. Sync needs attention.")]
    [InlineData(CatchSyncStateEnum.ActionRequired, "Details synced · 1 of 3 photos uploaded. Couldn’t sync. Check the catch details, then retry.")]
    public void ItShouldNotReportEverythingSyncedWhenOnlyTheDetailsAndSomePhotographsAreSynced(
        CatchSyncStateEnum state,
        string expectedText)
    {
        // Arrange
        var sut = CreateStored("en-GB");

        // Act
        sut.ApplySyncStatus(CreateStatus(state, metadataSynced: true, photographCount: 3, recorded: 1, canRetry: false));

        // Assert
        sut.SaveStatus.Should().Be(expectedText);
        sut.SaveStatus.Should().NotContain("Synced.");
    }

    [Fact]
    public void ItShouldComposeTheFrenchWaitingTextFromTheSavedLocallyAndWaitingResources()
    {
        // Arrange
        var text = CreateText("fr-FR");
        var sut = new CatchPresentationModel(Catch, text, ["one.png"], savedLocally: true);

        // Act
        sut.ApplySyncStatus(CreateStatus(CatchSyncStateEnum.WaitingToSync));

        // Assert
        sut.SaveStatus.Should().Be(text["CatchSavedLocally"] + " " + text["CatchSyncWaiting"]);
        sut.SaveStatus.Should().EndWith("En attente de synchronisation.");
    }

    [Fact]
    public void ItShouldShowTheFrenchPartialPhotographProgress()
    {
        // Arrange
        var sut = CreateStored("fr-FR");

        // Act
        sut.ApplySyncStatus(CreateStatus(CatchSyncStateEnum.Synchronising, metadataSynced: true, photographCount: 3, recorded: 2));

        // Assert
        sut.SaveStatus.Should().Be("Détails synchronisés · photos envoyées : 2 sur 3. Synchronisation en cours…");
    }

    [Fact]
    public void ItShouldRaiseChangesForEveryPropertyThatDependsOnTheStatus()
    {
        // Arrange
        var sut = CreateStored("en-GB");
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        sut.ApplySyncStatus(CreateStatus(CatchSyncStateEnum.NeedsAttention, canRetry: true));

        // Assert
        changes.Should().BeEquivalentTo(
            [nameof(sut.SaveStatus), nameof(sut.CardDescription), nameof(sut.SyncState), nameof(sut.CanRetrySync)]);
    }

    [Fact]
    public void ItShouldFallBackToTheSavedLocallyTextWhenTheStatusIsCleared()
    {
        // Arrange
        var sut = CreateStored("en-GB");
        sut.ApplySyncStatus(CreateStatus(CatchSyncStateEnum.Synced, metadataSynced: true));

        // Act
        sut.ApplySyncStatus(null);

        // Assert
        sut.SaveStatus.Should().Be("Saved locally on this device.");
        sut.CanRetrySync.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRelocaliseThePersistedStatusWhenTheLanguageChanges()
    {
        // Arrange
        var text = CreateText("en-GB");
        var sut = new CatchPresentationModel(Catch, text, ["one.png"], savedLocally: true);
        sut.ApplySyncStatus(CreateStatus(CatchSyncStateEnum.Synced, metadataSynced: true));

        // Act
        text.SetCulture("fr-FR");
        sut.RefreshLocalizedText();

        // Assert
        sut.SaveStatus.Should().Be("Synchronisée.");
    }

    private static CatchPresentationModel CreateStored(string culture)
    {
        return new CatchPresentationModel(Catch, CreateText(culture), ["one.png", "two.png", "three.png"], savedLocally: true);
    }

    private static CatchSyncStatusModel CreateStatus(
        CatchSyncStateEnum state,
        bool metadataSynced = false,
        int photographCount = 3,
        int recorded = 0,
        bool canRetry = false)
    {
        return new CatchSyncStatusModel
        {
            CatchId = Catch.Id,
            State = state,
            MetadataSynced = metadataSynced,
            PhotographCount = photographCount,
            PhotographsRecorded = state == CatchSyncStateEnum.Synced ? photographCount : recorded,
            CanRetry = canRetry
        };
    }
}
