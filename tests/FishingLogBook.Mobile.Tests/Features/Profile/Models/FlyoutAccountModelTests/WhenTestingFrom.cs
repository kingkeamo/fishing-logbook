using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Models.FlyoutAccountModelTests;

public sealed class WhenTestingFrom
{
    [Fact]
    public void ItShouldPreferADistinctDisplayNameAndShowTheEmailUnderneath()
    {
        // Arrange
        // Act
        var account = FlyoutAccountModel.From("  Eamonn  ", "  eamonn@example.com  ");

        // Assert
        account.PrimaryName.Should().Be("Eamonn");
        account.SecondaryEmail.Should().Be("eamonn@example.com");
        account.HasSecondaryEmail.Should().BeTrue();
        account.Initial.Should().Be("E");
    }

    [Fact]
    public void ItShouldNotRepeatTheEmailWhenItMatchesTheDisplayName()
    {
        // Arrange
        // Act
        var account = FlyoutAccountModel.From("Eamonn@Example.com", "eamonn@example.com");

        // Assert
        account.PrimaryName.Should().Be("Eamonn@Example.com");
        account.HasSecondaryEmail.Should().BeFalse();
        account.Initial.Should().Be("E");
    }

    [Fact]
    public void ItShouldUseTheEmailWhenTheDisplayNameIsBlank()
    {
        // Arrange
        // Act
        var account = FlyoutAccountModel.From("   ", "angler@example.com");

        // Assert
        account.PrimaryName.Should().Be("angler@example.com");
        account.HasSecondaryEmail.Should().BeFalse();
        account.Initial.Should().Be("A");
    }

    [Fact]
    public void ItShouldShowAPlaceholderWhenThereIsNoNameOrEmail()
    {
        // Arrange
        // Act
        var account = FlyoutAccountModel.From(null, null);

        // Assert
        account.PrimaryName.Should().BeEmpty();
        account.HasSecondaryEmail.Should().BeFalse();
        account.Initial.Should().Be("?");
    }
}
