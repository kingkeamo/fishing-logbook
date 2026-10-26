using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchPresentationModelTests;

public class WhenTestingConstructor : BaseCatchPresentationModelTest
{
    [Fact]
    public void ItShouldRefreshARetainedSampleWithoutTranslatingUserContent()
    {
        // Arrange
        var text = CreateText("en-GB");
        var sampleText = new SampleCatchTextModel("SpeciesBrownTrout", "PlaceCorrib", "SampleNotes", "MethodFly", CatchMethodEnum.Fly, CatchSpeciesEnum.BrownTrout);
        var sample = new CatchPresentationModel(Catch, text, ["sample_catch.png"], sampleText);
        var recorded = new CatchPresentationModel(Catch with { SpeciesName = "Perch", Notes = "Released" }, text);
        var changes = new List<string?>();
        sample.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        text.SetCulture("fr-FR");
        sample.RefreshLocalizedText();
        // Assert
        sample.Species.Should().Be("Truite fario");
        sample.Notes.Should().Be(text["SampleNotes"]);
        sample.ShortDate.Should().Be("14 juin 2026");
        recorded.Species.Should().Be("Perch");
        recorded.Notes.Should().Be("Released");
        changes.Should().Contain(nameof(sample.Species)).And.Contain(nameof(sample.OpenDescription));
    }

    [Fact]
    public void ItShouldNotTreatARealSpeciesNameAsSampleLocalisationMetadata()
    {
        // Arrange
        var text = CreateText("fr-FR");
        var dto = Catch with { SpeciesName = "Atlantic salmon" };
        // Act
        var sut = new CatchPresentationModel(dto, text);
        // Assert
        sut.Species.Should().Be("Atlantic salmon");
    }

    [Fact]
    public void ItShouldDisplayTheSameInstantInUtcRegardlessOfRecordedOffset()
    {
        // Arrange
        var dto = Catch with { CaughtOn = Catch.CaughtOn.ToOffset(TimeSpan.FromHours(4)) };

        // Act
        var sut = new CatchPresentationModel(dto, CreateText("en-GB"));

        // Assert
        sut.CaughtOn.Should().Be("Sunday, 14 June 2026 07:30");
    }

    [Theory]
    [InlineData("en-GB", "2.4 kg", "Sunday, 14 June 2026 07:30")]
    [InlineData("fr-FR", "2,4 kg", "dimanche 14 juin 2026 07:30")]
    public void ItShouldFormatMeasurementsAndRecordedTimeInSelectedCulture(string culture, string weight, string date)
    {
        // Arrange
        var dto = Catch with { SpeciesName = "Perch", Method = "Fly", Weight = 2.4m, Length = 48m, PlaceName = "Lake", Notes = "Released" };

        // Act
        var sut = new CatchPresentationModel(dto, CreateText(culture), ["brand_mark.png", "second.png"]);

        // Assert
        sut.Id.Should().Be(dto.Id);
        sut.Species.Should().Be("Perch");
        sut.Weight.Should().Be(weight);
        sut.Length.Should().Be("48 cm");
        sut.CaughtOn.Should().Be(date);
        sut.Place.Should().Be("Lake");
        sut.Notes.Should().Be("Released");
        sut.MethodIdentifier.Should().Be(CatchMethodEnum.Fly);
        sut.HasPhoto.Should().BeTrue();
        sut.PreviewAsset.Should().Be("brand_mark.png");
        sut.PhotoCount.Should().Be(2);
        sut.HasMultiplePhotos.Should().BeTrue();
        sut.PhotoCounter.Should().Be("1/2");
        sut.HasNoPhoto.Should().BeFalse();
        sut.OpenDescription.Should().Contain("Perch").And.Contain(date);
    }

    [Theory]
    [InlineData("en-GB", "Not recorded")]
    [InlineData("fr-FR", "Non renseigné")]
    public void ItShouldShowMissingFieldsWithoutInventingMeasurementsOrPhotos(string culture, string missing)
    {
        // Arrange
        var dto = Catch with { SpeciesName = " ", Notes = "", PlaceName = null };

        // Act
        var sut = new CatchPresentationModel(dto, CreateText(culture));

        // Assert
        sut.Species.Should().Be(missing);
        sut.Weight.Should().Be(missing);
        sut.Length.Should().Be(missing);
        sut.Place.Should().Be(missing);
        sut.Notes.Should().Be(missing);
        sut.HasPhoto.Should().BeFalse();
        sut.HasNoPhoto.Should().BeTrue();
        sut.PreviewAsset.Should().BeNull();
        sut.PhotoCount.Should().Be(0);
        sut.HasMultiplePhotos.Should().BeFalse();
        sut.HasNotes.Should().BeFalse();
        sut.SpeciesIdentifier.Should().Be(CatchSpeciesEnum.Other);
    }

    [Fact]
    public void ItShouldNotShowAPhotoCounterBadgeForASinglePhoto()
    {
        // Arrange
        // Act
        var sut = new CatchPresentationModel(Catch, CreateText("en-GB"), ["one.png"]);
        // Assert
        sut.HasMultiplePhotos.Should().BeFalse();
        sut.PreviewAsset.Should().Be("one.png");
    }
}
