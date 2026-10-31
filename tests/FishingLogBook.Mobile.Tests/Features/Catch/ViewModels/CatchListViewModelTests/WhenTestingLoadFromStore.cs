using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingLoadFromStore : BaseCatchListViewModelTest
{
    [Fact]
    public async Task ItShouldShowEmptyJournalWhenNoSavedCatchesExist()
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.IsEmptyJournal.Should().BeTrue();
        Sut.Catches.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowLocalDraftsWithADraftStatus()
    {
        // Arrange
        var draft = CreateRecord(LocalCatchLifecycleEnum.Draft);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([draft]);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.IsEmptyJournal.Should().BeFalse();
        var item = Sut.Catches.Should().ContainSingle().Subject;
        item.IsDraft.Should().BeTrue();
        item.SaveStatus.Should().Be(Text["CatchDraftStatus"]);
    }

    [Fact]
    public async Task ItShouldLoadSavedCatchesForTheSignedInAccount()
    {
        // Arrange
        var saved = CreateRecord(LocalCatchLifecycleEnum.Saved);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([saved]);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.HasContent.Should().BeTrue();
        Sut.Catches.Should().ContainSingle(item => item.Id == saved.Id);
    }

    private static LocalCatchRecordModel CreateRecord(LocalCatchLifecycleEnum lifecycle)
    {
        return new LocalCatchRecordModel
        {
            Id = Guid.NewGuid(),
            UserId = BaseCatchListViewModelTest.OwnerUserId,
            CaughtByUserId = BaseCatchListViewModelTest.OwnerUserId,
            RecordedByUserId = BaseCatchListViewModelTest.OwnerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
            SpeciesName = "Brown trout",
            Method = "Fly",
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = DateTimeOffset.UtcNow,
            UpdatedOn = DateTimeOffset.UtcNow,
            Photographs = []
        };
    }
}
