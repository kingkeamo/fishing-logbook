using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public class WhenTestingOpenCatch : BaseDashboardViewModelTest
{
    [Fact]
    public async Task ItShouldIgnoreIdentifiersWhichAreNotDisplayedWinners()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var id = Guid.NewGuid();

        // Act
        await Sut.OpenCatchAsync(id, CancellationToken.None);

        // Assert
        await MockCatches.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldFetchTheWinningCatchAndNavigateOnlyWhenAvailable(bool available)
    {
        // Arrange
        var id = Guid.NewGuid();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 1 },
            FilteredPersonalBests = [new DashboardPersonalBestDto { CatchId = id, Weight = 2 }]
        }));
        MockCatches.GetAsync(id, Arg.Any<CancellationToken>()).Returns(available
            ? ApiResultModel<CatchViewDto>.Succeeded(new CatchViewDto(id, Lease.UserId, DateTimeOffset.UtcNow) { SpeciesName = "Pike", Weight = 2 })
            : ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound));
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.OpenCatchAsync(id, CancellationToken.None);

        // Assert
        await MockCatches.Received(1).GetAsync(id, Arg.Any<CancellationToken>());
        if (available)
        {
            await MockNavigator.Received(1).GoToAsync("catch-detail", Arg.Is<CatchPresentationModel>(item => item.Id == id && item.Species == "Pike"), null, null, Arg.Any<CancellationToken>());
        }
        else
        {
            Sut.StatusText.Should().Be(Text["DashboardCatchUnavailable"]);
            await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<CatchPresentationModel>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        }
    }
}
