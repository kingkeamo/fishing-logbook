using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Tests.Serialization;

public class WhenTestingFishingPreferenceSerialization : BaseSerializationTest
{
    private static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public void ItShouldRoundTripFishingCatalogueDtoUsingWebDefaults()
    {
        // Arrange
        var original = new FishingCatalogueDto(
            [new FishingMethodDto(FlyMethodId, "Fly", "Fly")],
            [new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout")]);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<FishingCatalogueDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"methods\":");
        json.Should().Contain("\"allSpecies\":");
        json.Should().Contain("\"code\":\"BrownTrout\"");
        deserialized!.Methods.Should().ContainSingle(method => method.Id == FlyMethodId);
        deserialized.AllSpecies.Should().ContainSingle(species => species.Name == "Brown Trout");
    }

    [Fact]
    public void ItShouldRoundTripFishingPreferencesDtoUsingWebDefaults()
    {
        // Arrange
        var original = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)])
        ]);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<FishingPreferencesDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"fishingMethodId\":");
        json.Should().Contain("\"isDefault\":true");
        deserialized!.Methods.Should().ContainSingle();
        deserialized.Methods[0].IsDefault.Should().BeTrue();
        deserialized.Methods[0].Species[0].SpeciesId.Should().Be(BrownTroutSpeciesId);
        deserialized.Methods[0].Species[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRoundTripUpdateFishingPreferencesDtoUsingWebDefaults()
    {
        // Arrange
        var original = new UpdateFishingPreferencesDto(
        [
            new UpdateFishingMethodPreferenceDto(
                FlyMethodId,
                true,
                [new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, false)])
        ]);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<UpdateFishingPreferencesDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"speciesId\":");
        deserialized!.Methods.Should().ContainSingle();
        deserialized.Methods[0].FishingMethodId.Should().Be(FlyMethodId);
        deserialized.Methods[0].Species[0].IsDefault.Should().BeFalse();
    }

    [Fact]
    public void ItShouldSerialiseMeasurementUnitsAsTheirNumericValue()
    {
        // Arrange
        var original = new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            true,
            false,
            false,
            false,
            false,
            WeightUnitEnum.Lb,
            LengthUnitEnum.In);

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var deserialized = JsonSerializer.Deserialize<UpdateProfileDto>(json, WebOptions);

        // Assert
        json.Should().Contain("\"preferredWeightUnit\":1");
        json.Should().Contain("\"preferredLengthUnit\":1");
        deserialized!.PreferredWeightUnit.Should().Be(WeightUnitEnum.Lb);
        deserialized.PreferredLengthUnit.Should().Be(LengthUnitEnum.In);
    }

    [Fact]
    public void ItShouldDefaultMeasurementUnitsToMetricWhenAbsentFromTheJson()
    {
        // Arrange
        const string json = """
            {
              "displayName": "Eamonn",
              "homeRegion": null,
              "showDisplayName": true,
              "showPhotograph": false,
              "showHomeRegion": false,
              "showPreferredFishingMethods": false,
              "showPreferredSpecies": false
            }
            """;

        // Act
        var deserialized = JsonSerializer.Deserialize<UpdateProfileDto>(json, WebOptions);

        // Assert
        deserialized!.PreferredWeightUnit.Should().Be(WeightUnitEnum.Kg);
        deserialized.PreferredLengthUnit.Should().Be(LengthUnitEnum.Cm);
    }
}
