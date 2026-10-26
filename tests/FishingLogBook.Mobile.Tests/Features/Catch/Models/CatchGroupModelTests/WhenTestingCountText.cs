using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchGroupModelTests;

public class WhenTestingCountText
{
    [Theory]
    [InlineData("en-GB", 1, "1 catch")]
    [InlineData("en-GB", 2, "2 catches")]
    [InlineData("fr-FR", 1, "1 prise")]
    [InlineData("fr-FR", 3, "3 prises")]
    public void ItShouldUseTheSingularOnlyForOneCatchInTheSelectedLanguage(string culture, int count, string expected)
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo(culture));
        var when = new DateTimeOffset(2026, 6, 14, 9, 30, 0, TimeSpan.Zero);
        var catches = Enumerable.Range(0, count).Select(_ => new CatchPresentationModel(new CatchDto(Guid.NewGuid(), when, []), text));
        // Act
        var sut = new CatchGroupModel(new DateOnly(2026, 6, 14), catches, text);
        // Assert
        sut.CountText.Should().Be(expected);
    }

    [Fact]
    public void ItShouldFormatTheHeaderInTheSelectedLanguage()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        // Act
        var sut = new CatchGroupModel(new DateOnly(2026, 6, 14), [], text);
        text.SetCulture("fr-FR");
        // Assert
        sut.Header.Should().Be("dimanche 14 juin 2026");
    }
}
