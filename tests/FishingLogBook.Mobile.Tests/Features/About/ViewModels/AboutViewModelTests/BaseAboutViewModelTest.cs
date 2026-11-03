using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.About.Clients;
using FishingLogBook.Mobile.Features.About.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.About.ViewModels.AboutViewModelTests;

public abstract class BaseAboutViewModelTest
{
    protected readonly ISystemStatusClient MockStatus = Substitute.For<ISystemStatusClient>();
    protected readonly IAppInfoAdapter MockApp = Substitute.For<IAppInfoAdapter>();
    protected readonly IClipboardAdapter MockClipboard = Substitute.For<IClipboardAdapter>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly AboutViewModel Sut;

    protected BaseAboutViewModelTest()
    {
        MockConnectivity.IsOnline.Returns(true);
        MockApp.Version.Returns("0.1.0");
        MockApp.Build.Returns("12");
        MockApp.Platform.Returns("Android");
        MockApp.PlatformVersion.Returns("16");
        MockApp.Commit.Returns("abc1234");
        MockStatus.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<HealthDto>.Succeeded(new HealthDto("Healthy")));
        MockStatus.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DatabaseTestDto>.Succeeded(new DatabaseTestDto("Healthy", "logbook")));
        MockStatus.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<BuildMetadataDto>.Succeeded(new BuildMetadataDto("2.4.0", "def5678", "Development", null)));
        Sut = new AboutViewModel(
            MockStatus,
            MockApp,
            MockClipboard,
            MockConnectivity,
            TestMobileAuthConfig.Create(),
            Text);
    }
}
