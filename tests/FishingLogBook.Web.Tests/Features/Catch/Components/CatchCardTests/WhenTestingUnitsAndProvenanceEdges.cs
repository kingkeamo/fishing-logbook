using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Components.CatchCard;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchCardTests;

public class WhenTestingUnitsAndProvenanceEdges : BaseCatchCardTest
{
    [Fact]
    public async Task ItShouldShowMeasurementsInTheAnglersImperialUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(component => component.Catch, StoredCatch(catchId, weight: 2.5m, length: 50m))
            .Add(component => component.WeightUnit, WeightUnitEnum.Lb)
            .Add(component => component.LengthUnit, LengthUnitEnum.In));

        // Assert
        var label = cut.Find($"#catch-card-measurements-{catchId:D}").TextContent;
        label.Should().Contain("lb").And.Contain("in").And.NotContain("kg").And.NotContain("cm");
    }

    [Fact]
    public async Task ItShouldShowProvenanceForARecorderWhoIsNotTheCurrentUserWhenTheAnglerIsUnknown()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(component => component.Catch, StoredCatch(
                catchId,
                caughtByUserId: Guid.Empty,
                recordedByUserId: OtherUserId))
            .Add(component => component.CurrentUserId, OwnerUserId)
            .Add(component => component.RecordedByName, "Mary"));

        // Assert
        cut.Find($"#catch-card-provenance-{catchId:D}").TextContent.Should().Contain("Recorded by Mary");
    }

    [Fact]
    public async Task ItShouldNotShowProvenanceWhenNeitherTheAnglerNorTheRecorderIsKnown()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(component => component.Catch, StoredCatch(
                catchId,
                caughtByUserId: Guid.Empty,
                recordedByUserId: Guid.Empty))
            .Add(component => component.CurrentUserId, OwnerUserId));

        // Assert
        cut.FindAll($"#catch-card-provenance-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldPreferTheAnglerOverTheRecorderWhenBothAreSomeoneElse()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(component => component.Catch, StoredCatch(
                catchId,
                caughtByUserId: OtherUserId,
                recordedByUserId: Guid.NewGuid()))
            .Add(component => component.CurrentUserId, OwnerUserId)
            .Add(component => component.AnglerName, "Tom")
            .Add(component => component.RecordedByName, "Mary"));

        // Assert
        var provenance = cut.Find($"#catch-card-provenance-{catchId:D}").TextContent;
        provenance.Should().Contain("Recorded for Tom").And.NotContain("Mary");
    }
}
