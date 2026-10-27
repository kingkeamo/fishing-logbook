using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingUnits : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldInitialiseBothSelectionsFromTheLoadedProfile()
    {
        // Arrange
        // Act
        await LoadAsync();

        // Assert
        Sut.SelectedWeightUnit!.Value.Should().Be(WeightUnitEnum.Lb);
        Sut.SelectedLengthUnit!.Value.Should().Be(LengthUnitEnum.In);
        Sut.WeightUnitOptions.Select(option => option.Value).Should().Equal(WeightUnitEnum.Kg, WeightUnitEnum.Lb);
        Sut.LengthUnitOptions.Select(option => option.Value).Should().Equal(LengthUnitEnum.Cm, LengthUnitEnum.In);
    }

    [Fact]
    public async Task ItShouldSaveAChangedWeightUnitWithoutChangingTheLengthUnit()
    {
        // Arrange
        await LoadAsync();
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.Saved(Data));
        Sut.SelectedWeightUnit = Sut.WeightUnitOptions.Single(option => option.Value == WeightUnitEnum.Kg);

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockService.Received(1).SaveAsync(
            Arg.Is<ProfileEditorDataModel>(value => ReferenceEquals(value, Data)),
            Arg.Is<UpdateProfileDto>(value =>
                value.PreferredWeightUnit == WeightUnitEnum.Kg &&
                value.PreferredLengthUnit == LengthUnitEnum.In),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveAChangedLengthUnitWithoutChangingTheWeightUnit()
    {
        // Arrange
        await LoadAsync();
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.Saved(Data));
        Sut.SelectedLengthUnit = Sut.LengthUnitOptions.Single(option => option.Value == LengthUnitEnum.Cm);

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockService.Received(1).SaveAsync(
            Arg.Is<ProfileEditorDataModel>(value => ReferenceEquals(value, Data)),
            Arg.Is<UpdateProfileDto>(value =>
                value.PreferredWeightUnit == WeightUnitEnum.Lb &&
                value.PreferredLengthUnit == LengthUnitEnum.Cm),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveBothUnitsAndPreserveUnrelatedProfileFields()
    {
        // Arrange
        await LoadAsync();
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.Saved(Data));
        Sut.SelectedWeightUnit = Sut.WeightUnitOptions.Single(option => option.Value == WeightUnitEnum.Kg);
        Sut.SelectedLengthUnit = Sut.LengthUnitOptions.Single(option => option.Value == LengthUnitEnum.Cm);

        // Act
        var result = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await MockService.Received(1).SaveAsync(
            Arg.Is<ProfileEditorDataModel>(value => ReferenceEquals(value, Data)),
            Arg.Is<UpdateProfileDto>(value =>
                value.DisplayName == "Eamonn" &&
                value.HomeRegion == "Connemara" &&
                value.ShowDisplayName &&
                !value.ShowPhotograph &&
                value.ShowHomeRegion &&
                !value.ShowPreferredFishingMethods &&
                !value.ShowPreferredSpecies &&
                value.PreferredWeightUnit == WeightUnitEnum.Kg &&
                value.PreferredLengthUnit == LengthUnitEnum.Cm),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearTheSelectionsWhenTheAccountChanges()
    {
        // Arrange
        await LoadAsync();

        // Act
        MockAuthentication.SessionChanged += Raise.Event();

        // Assert
        Sut.HasContent.Should().BeFalse();
        Sut.SelectedWeightUnit.Should().BeNull();
        Sut.SelectedLengthUnit.Should().BeNull();
    }
}
