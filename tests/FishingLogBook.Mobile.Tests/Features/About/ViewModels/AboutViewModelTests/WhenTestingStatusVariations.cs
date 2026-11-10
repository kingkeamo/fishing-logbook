using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.About.ViewModels;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.About.ViewModels.AboutViewModelTests;

public sealed class WhenTestingStatusVariations : BaseAboutViewModelTest
{
    private AboutViewModel CreateWithApiUrl(string apiBaseUrl)
    {
        return new AboutViewModel(
            MockStatus,
            MockApp,
            MockClipboard,
            MockConnectivity,
            TestMobileAuthConfig.Create() with { ApiBaseUrl = apiBaseUrl },
            Text);
    }

    [Fact]
    public async Task ItShouldPropagateCancellationWithoutMarkingTheServicesUnreachable()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        MockStatus.GetApiHealthAsync(Arg.Any<CancellationToken>()).Returns<Task<ApiResultModel<HealthDto>>>(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        // Act
        var act = () => Sut.RefreshAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        Sut.ApiStatusText.Should().Be(Text["AboutChecking"]);
    }

    [Fact]
    public async Task ItShouldTreatAnUnhealthyApiAsOfflineAndCopyThatToTheDatabase()
    {
        // Arrange
        MockStatus.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<HealthDto>.Succeeded(new HealthDto("Unhealthy")));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.ApiStatusText.Should().Be(Text["AboutOffline"]);
        Sut.DatabaseStatusText.Should().Be(Text["AboutOffline"]);
        await MockStatus.DidNotReceive().GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
        await MockStatus.DidNotReceive().GetBuildMetadataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAServerErrorFromTheHealthCheckAsOffline()
    {
        // Arrange
        MockStatus.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<HealthDto>.Failed(ApiOutcomeEnum.ServerError));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.ApiStatusText.Should().Be(Text["AboutOffline"]);
    }

    [Fact]
    public async Task ItShouldKeepApiBuildDetailsUnavailableWhenBuildMetadataFails()
    {
        // Arrange
        MockStatus.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<BuildMetadataDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.ApiVersion.Should().Be(Text["AboutUnavailable"]);
        Sut.ApiCommit.Should().Be(Text["AboutUnavailable"]);
        Sut.ApiEnvironment.Should().Be(Text["AboutUnavailable"]);
        await MockStatus.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportADegradedDatabaseWithItsName()
    {
        // Arrange
        MockStatus.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DatabaseTestDto>.Succeeded(new DatabaseTestDto("Degraded", "logbook")));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.DatabaseStatusText.Should().StartWith(Text["AboutDegraded"]).And.EndWith("logbook");
    }

    [Fact]
    public async Task ItShouldReportAnUnknownDatabaseStatusAsOfflineWithoutAName()
    {
        // Arrange
        MockStatus.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DatabaseTestDto>.Succeeded(new DatabaseTestDto("Weird", " ")));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.DatabaseStatusText.Should().Be(Text["AboutOffline"]);
    }

    [Fact]
    public async Task ItShouldReportAnUnavailableDatabaseCheckAsUnreachable()
    {
        // Arrange
        MockStatus.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DatabaseTestDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.DatabaseStatusText.Should().Be(Text["AboutUnreachable"]);
    }

    [Fact]
    public async Task ItShouldReportAServerErrorFromTheDatabaseCheckAsOffline()
    {
        // Arrange
        MockStatus.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DatabaseTestDto>.Failed(ApiOutcomeEnum.ServerError));

        // Act
        await Sut.RefreshAsync(CancellationToken.None);

        // Assert
        Sut.DatabaseStatusText.Should().Be(Text["AboutOffline"]);
    }

    [Fact]
    public async Task ItShouldStopAfterTheApiBuildReadWhenARefreshWasSuperseded()
    {
        // Arrange
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var calls = 0;
        MockStatus.GetBuildMetadataAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.SetResult();
                await release.Task;
            }

            return ApiResultModel<BuildMetadataDto>.Succeeded(new BuildMetadataDto("1", "sha", "Development", null));
        });

        // Act
        var first = Sut.RefreshAsync(CancellationToken.None);
        await entered.Task;
        await Sut.RefreshAsync(CancellationToken.None);
        release.SetResult();
        await first;

        // Assert
        await MockStatus.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
        Sut.IsChecking.Should().BeFalse();
    }

    [Fact]
    public void ItShouldShowOnlyThePlatformWhenThereIsNoPlatformVersion()
    {
        // Arrange
        MockApp.PlatformVersion.Returns(" ");

        // Act
        var platform = Sut.Platform;

        // Assert
        platform.Should().Be("Android");
    }

    [Fact]
    public void ItShouldShowUnavailableWhenTheAppCommitIsBlank()
    {
        // Arrange
        MockApp.Commit.Returns(" ");

        // Act
        var commit = Sut.AppCommit;

        // Assert
        commit.Should().Be(Text["AboutUnavailable"]);
    }

    [Theory]
    [InlineData("http://localhost:5088/", "AboutEnvironmentLocal")]
    [InlineData("http://127.0.0.1:5088/", "AboutEnvironmentLocal")]
    [InlineData("https://fishing-logbook-dev-api.fly.dev", "AboutEnvironmentDevelopment")]
    public void ItShouldNameKnownEnvironmentsFromTheApiHost(string url, string key)
    {
        // Arrange
        var sut = CreateWithApiUrl(url);

        // Act
        var environment = sut.EnvironmentName;

        // Assert
        environment.Should().Be(Text[key]);
    }

    [Fact]
    public void ItShouldShowTheHostForAnUnrecognisedApiAndTheRawTextWhenItIsNotAUrl()
    {
        // Arrange
        var production = CreateWithApiUrl("https://api.fishinglogbook.app");
        var invalid = CreateWithApiUrl("not a url");

        // Act
        var productionName = production.EnvironmentName;
        var invalidName = invalid.EnvironmentName;

        // Assert
        productionName.Should().Be("api.fishinglogbook.app");
        invalidName.Should().Be("not a url");
    }

    [Fact]
    public void ItShouldRaiseAnEmptyPropertyChangeWhenTheLanguageChanges()
    {
        // Arrange
        var raised = new List<string?>();
        Sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        Text.SetCulture("fr-FR");
        Text.SetCulture("en-GB");

        // Assert
        raised.Should().NotBeEmpty();
        raised.Should().OnlyContain(name => name == string.Empty);
    }
}
