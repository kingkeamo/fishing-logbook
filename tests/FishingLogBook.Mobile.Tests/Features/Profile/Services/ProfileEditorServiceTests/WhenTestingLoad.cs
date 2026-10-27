using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.ProfileEditorServiceTests;

public sealed class WhenTestingLoad : BaseProfileEditorServiceTest
{
    [Fact]
    public async Task ItShouldCombineTheCurrentProfileAndPreferences()
    {
        // Arrange
        await SignInAsync(UserId);
        var preferences = TestAnglerPreferences.Create();
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Profile));
        MockPreferencesService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(preferences));

        // Act
        var result = await Sut.LoadAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Profile.Should().BeSameAs(Profile);
        result.Value.Preferences.Should().BeSameAs(preferences);
    }

    [Fact]
    public async Task ItShouldDiscardResultsWhenTheAccountChangesDuringLoading()
    {
        // Arrange
        await SignInAsync(UserId);
        MockProfileClient.GetAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await SignInAsync(OtherUserId);
            return ApiResultModel<ProfileDto>.Succeeded(Profile);
        });
        MockPreferencesService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Create()));

        // Act
        var action = () => Sut.LoadAsync(CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
