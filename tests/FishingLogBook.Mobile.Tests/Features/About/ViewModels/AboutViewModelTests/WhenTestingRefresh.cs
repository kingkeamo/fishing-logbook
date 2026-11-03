using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.About.ViewModels;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.About.ViewModels.AboutViewModelTests;

public sealed class WhenTestingRefresh : BaseAboutViewModelTest
{
    [Fact]
    public async Task ItShouldShowInstalledAppMetadataAndLiveServiceStatus()
    {
        // Arrange
        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.Platform.Should().Be("Android 16");
        Sut.AppVersion.Should().Be("0.1.0");
        Sut.AppBuild.Should().Be("12");
        Sut.AppCommit.Should().Be("abc1234");
        Sut.EnvironmentName.Should().Be("api.example.com");
        Sut.ApiStatusText.Should().Be(Text["AboutOnline"]);
        Sut.ApiVersion.Should().Be("2.4.0");
        Sut.ApiEnvironment.Should().Be("Development");
        Sut.DatabaseStatusText.Should().Be("Online · logbook");
        Sut.IsChecking.Should().BeFalse();
        await MockStatus.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
        await MockStatus.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCallServicesWhenTheDeviceIsOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.ApiStatusText.Should().Be(Text["AboutDeviceOffline"]);
        Sut.DatabaseStatusText.Should().Be(Text["AboutDeviceOffline"]);
        await MockStatus.DidNotReceive().GetApiHealthAsync(Arg.Any<CancellationToken>());
        await MockStatus.DidNotReceive().GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAFailedHealthCheckAsUnreachableWithoutInventingDatabaseStatus()
    {
        // Arrange
        MockStatus.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<HealthDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.ApiStatusText.Should().Be(Text["AboutUnreachable"]);
        Sut.DatabaseStatusText.Should().Be(Text["AboutUnreachable"]);
        Sut.ApiVersion.Should().Be(Text["AboutUnavailable"]);
        await MockStatus.DidNotReceive().GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatADatabaseServerErrorAsAnOutage()
    {
        // Arrange
        MockStatus.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DatabaseTestDto>.Failed(ApiOutcomeEnum.ServerError));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.ApiStatusText.Should().Be(Text["AboutOnline"]);
        Sut.DatabaseStatusText.Should().Be(Text["AboutOffline"]);
    }

    [Fact]
    public async Task ItShouldCopyNonSensitiveDiagnosticsWithoutAccountDetails()
    {
        // Arrange
        await Sut.RefreshAsync(CancellationToken.None);
        string? copied = null;
        MockClipboard.SetTextAsync(Arg.Do<string>(text => copied = text)).Returns(Task.CompletedTask);

        // Act
        await Sut.CopyDiagnosticsAsync(CancellationToken.None);

        // Assert
        Sut.Copied.Should().BeTrue();
        copied.Should().Contain("0.1.0");
        copied.Should().Contain("Android 16");
        copied.Should().NotContain("@");
        copied.Should().NotContain("token");
        await MockClipboard.Received(1).SetTextAsync(Arg.Any<string>());
    }

    [Fact]
    public void ItShouldDescribeALoopbackApiHostAsLocal()
    {
        // Arrange
        var sut = new AboutViewModel(
            MockStatus,
            MockApp,
            MockClipboard,
            MockConnectivity,
            TestMobileAuthConfig.Create() with { ApiBaseUrl = "http://10.0.2.2:5088/" },
            Text);

        // Act
        var environment = sut.EnvironmentName;

        // Assert
        environment.Should().Be(Text["AboutEnvironmentLocal"]);
    }
}
