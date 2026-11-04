using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public class WhenTestingSelectPreset : BaseDashboardViewModelTest
{
    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void ItShouldIgnoreInvalidPickerSelections(int index)
    {
        // Arrange
        var expected = Sut.PresetIndex;

        // Act
        Sut.SelectPreset(index);

        // Assert
        Sut.PresetIndex.Should().Be(expected);
        Sut.IsCustom.Should().BeFalse();
    }

    [Fact]
    public void ItShouldStopReentrantNotificationsWhenThePickerReportsItsCurrentSelection()
    {
        // Arrange
        var changes = 0;
        Sut.PropertyChanged += (_, _) =>
        {
            changes++;
            if (changes > 2)
            {
                throw new InvalidOperationException("The picker selection did not settle.");
            }

            Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        };

        // Act
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);

        // Assert
        changes.Should().Be(1);
        Sut.IsCustom.Should().BeTrue();
    }
}
