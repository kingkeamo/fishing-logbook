using AwesomeAssertions;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingText : BaseCatchListViewModelTest
{
    [Fact]
    public void ItShouldRefreshTheJournalWhenLanguageChanges()
    {
        // Arrange
        var ids = Sut.Catches.Select(item => item.Id).ToArray();
        var changes = new List<string?>();
        Sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        Text.SetCulture("fr-CA");
        // Assert
        Sut.Catches.Select(item => item.Id).Should().Equal(ids);
        Sut.Catches[0].Species.Should().Be("Truite fario");
        Sut.Catches[0].Weight.Should().Be("2,4 kg");
        Sut.Catches[0].ShortDate.Should().Be("14 juin 2026");
        Sut.Catches[1].Species.Should().Be("Saumon atlantique");
        Sut.Catches[3].Species.Should().Be("Non renseigné");
        changes.Should().Contain(nameof(Sut.Catches)).And.Contain(nameof(Sut.State));
    }
}
