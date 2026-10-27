using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingStart : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldPopulateTheDisplayNameAndUnitsFromTheExistingProfile()
    {
        // Arrange
        // Act
        await LoadAsync();

        // Assert
        Sut.StepNumber.Should().Be(1);
        Profile.DisplayName.Should().Be("Eamonn");
        Profile.SelectedWeightUnit!.Value.Should().Be(WeightUnitEnum.Lb);
        Profile.SelectedLengthUnit!.Value.Should().Be(LengthUnitEnum.In);
        Profile.SelectedMethods.Should().Contain(method => method.Id == TestAnglerPreferences.FlyId);
    }

    [Fact]
    public async Task ItShouldNotReloadPreferencesWhenTheWelcomeStepReappears()
    {
        // Arrange
        await LoadAsync();
        await Sut.ContinueAsync(CancellationToken.None);

        // Act
        Sut.Back();
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        await MockEditor.Received(1).LoadAsync(Arg.Any<CancellationToken>());
        Sut.StepNumber.Should().Be(1);
        Profile.DisplayName.Should().Be("Eamonn");
    }

    [Fact]
    public async Task ItShouldShowARetryStateWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        MockEditor.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await LoadAsync();

        // Assert
        Sut.CanContinue.Should().BeFalse();
        Sut.CanRetryLoad.Should().BeTrue();
        Sut.Message.Should().Be(Text["ProfileOfflineError"]);
    }
}
