using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.AnglerPreferencesViewModelTests;

public class WhenTestingDetach : BaseAnglerPreferencesViewModelTest
{
    [Fact]
    public async Task ItShouldStopReactingToAccountChanges()
    {
        // Arrange
        RefreshReturns(TestAnglerPreferences.Create());
        await Sut.LoadAsync(CancellationToken.None);
        // Act
        Sut.Detach();
        ChangeAccount();
        // Assert
        Sut.HasPreferences.Should().BeTrue();
    }
}
