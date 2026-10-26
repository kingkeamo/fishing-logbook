using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingSetMethod : BaseCatchListViewModelTest
{
    [Fact]
    public void ItShouldRejectUnsupportedMethodsWithoutChangingResults()
    {
        // Arrange
        var ids = Sut.Catches.Select(item => item.Id).ToArray();
        // Act
        var action = () => Sut.SetMethod((CatchMethodEnum)99);
        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
        Sut.Catches.Select(item => item.Id).Should().Equal(ids);
        Sut.AllSelected.Should().BeTrue();
    }

    [Theory]
    [InlineData(CatchMethodEnum.All, 7)]
    [InlineData(CatchMethodEnum.Spinning, 2)]
    [InlineData(CatchMethodEnum.Fly, 3)]
    [InlineData(CatchMethodEnum.Lure, 1)]
    [InlineData(CatchMethodEnum.Other, 0)]
    public void ItShouldFilterByStableMethodCode(CatchMethodEnum method, int count)
    {
        // Arrange
        Text.SetCulture("fr-FR");
        // Act
        Sut.SetMethod(method);
        // Assert
        Sut.Catches.Should().HaveCount(count);
        Sut.AllSelected.Should().Be(method == CatchMethodEnum.All);
        Sut.SpinningSelected.Should().Be(method == CatchMethodEnum.Spinning);
        Sut.FlySelected.Should().Be(method == CatchMethodEnum.Fly);
        Sut.LureSelected.Should().Be(method == CatchMethodEnum.Lure);
        Sut.Filters.Method.Should().Be(method);
        Sut.Catches.Should().OnlyContain(item => method == CatchMethodEnum.All || item.MethodIdentifier == method);
    }
}
