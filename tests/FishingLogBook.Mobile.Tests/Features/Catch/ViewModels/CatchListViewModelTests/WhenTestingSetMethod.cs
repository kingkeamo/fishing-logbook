using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingSetMethod : BaseCatchListViewModelTest
{
    [Theory]
    [InlineData("Trolling")]
    [InlineData("spinning")]
    [InlineData(null)]
    public void ItShouldRejectUnsupportedMethodsWithoutChangingResults(string? method)
    {
        // Arrange
        var ids = Sut.Catches.Select(item => item.Id).ToArray();
        // Act
        var action = () => Sut.SetMethod(method!);
        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
        Sut.Catches.Select(item => item.Id).Should().Equal(ids);
        Sut.AllSelected.Should().BeTrue();
    }

    [Theory]
    [InlineData("", 4)]
    [InlineData("Spinning", 2)]
    [InlineData("Fly", 1)]
    public void ItShouldFilterByStableMethodCode(string method, int count)
    {
        // Arrange
        Text.SetCulture("fr-FR");
        // Act
        Sut.SetMethod(method);
        // Assert
        Sut.Catches.Should().HaveCount(count);
        Sut.AllSelected.Should().Be(method == "");
        Sut.SpinningSelected.Should().Be(method == "Spinning");
        Sut.FlySelected.Should().Be(method == "Fly");
        Sut.Catches.Should().OnlyContain(item => method == "" || item.MethodCode == method);
    }
}
