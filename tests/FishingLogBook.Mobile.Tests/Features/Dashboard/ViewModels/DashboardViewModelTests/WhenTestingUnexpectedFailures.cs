using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public class WhenTestingUnexpectedFailures : BaseDashboardViewModelTest
{
    [Fact]
    public async Task ItShouldShowAnErrorAndKeepNoDataWhenTheDashboardRequestThrows()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ApiResultModel<DashboardDto>>(new InvalidOperationException("boom")));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.Data.Should().BeNull();
        Sut.StatusText.Should().Be(Text["DashboardError"]);
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowAnErrorWhenTheUnitPreferencesCannotBeRead()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MeasurementUnitsModel>(new InvalidOperationException("boom")));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.Data.Should().BeNull();
        Sut.StatusText.Should().Be(Text["DashboardError"]);
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowAnErrorAndCloseTheModalWhenTheFilterModalThrows()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        MockModals.EditFiltersAsync(
                Arg.Any<DashboardFilterSelectionModel>(),
                Arg.Any<DashboardFilterOptionsModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DashboardFilterSelectionModel?>(new InvalidOperationException("boom")));

        // Act
        await Sut.EditFiltersAsync(CancellationToken.None);

        // Assert
        Sut.StatusText.Should().Be(Text["DashboardError"]);
        Sut.IsFilterModalOpen.Should().BeFalse();
        Sut.CanApply.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldStayQuietWhenTheFilterModalFailsAfterTheAccountChanged()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        MockModals.EditFiltersAsync(
                Arg.Any<DashboardFilterSelectionModel>(),
                Arg.Any<DashboardFilterOptionsModel>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                MockSession.IsCurrent(Lease).Returns(false);
                return Task.FromException<DashboardFilterSelectionModel?>(new InvalidOperationException("boom"));
            });

        // Act
        await Sut.EditFiltersAsync(CancellationToken.None);

        // Assert
        Sut.HasStatus.Should().BeFalse();
        Sut.IsFilterModalOpen.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSwallowACancellationFromTheFilterModal()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        MockModals.EditFiltersAsync(
                Arg.Any<DashboardFilterSelectionModel>(),
                Arg.Any<DashboardFilterOptionsModel>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<DashboardFilterSelectionModel?>>(call =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        // Act
        var act = () => Sut.EditFiltersAsync(cts.Token);

        // Assert
        await act.Should().NotThrowAsync();
        Sut.HasStatus.Should().BeFalse();
        Sut.CanApply.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReportTheCatchAsUnavailableWhenNavigationThrows()
    {
        // Arrange
        var id = Guid.NewGuid();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
            {
                Lifetime = new DashboardTotalsDto { Catches = 1 },
                FilteredPersonalBests = [new DashboardPersonalBestDto { CatchId = id, Weight = 2 }]
            }));
        MockCatches.GetAsync(id, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<CatchViewDto>.Succeeded(
                new CatchViewDto(id, Lease.UserId, DateTimeOffset.UtcNow) { SpeciesName = "Pike", Weight = 2 }));
        MockNavigator.GoToAsync(
                Arg.Any<string>(),
                Arg.Any<CatchPresentationModel>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.OpenCatchAsync(id, CancellationToken.None);

        // Assert
        Sut.StatusText.Should().Be(Text["DashboardCatchUnavailable"]);
    }

    [Fact]
    public async Task ItShouldNotNavigateWhenTheAccountChangesWhileTheCatchLoads()
    {
        // Arrange
        var id = Guid.NewGuid();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
            {
                Lifetime = new DashboardTotalsDto { Catches = 1 },
                FilteredPersonalBests = [new DashboardPersonalBestDto { CatchId = id, Weight = 2 }]
            }));
        MockCatches.GetAsync(id, Arg.Any<CancellationToken>()).Returns(call =>
        {
            MockSession.IsCurrent(Lease).Returns(false);
            return ApiResultModel<CatchViewDto>.Succeeded(
                new CatchViewDto(id, Lease.UserId, DateTimeOffset.UtcNow) { SpeciesName = "Pike", Weight = 2 });
        });
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.OpenCatchAsync(id, CancellationToken.None);

        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<CatchPresentationModel>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        Sut.HasStatus.Should().BeFalse();
    }
}
