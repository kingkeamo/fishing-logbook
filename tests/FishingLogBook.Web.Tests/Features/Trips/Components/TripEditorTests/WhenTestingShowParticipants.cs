using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Trips.Components.TripEditor;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class WhenTestingShowParticipants : BaseTripEditorTest
{
    [Fact]
    public async Task ItShouldOpenTheParticipantsModalForThisTrip()
    {
        // Arrange
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                Arg.Any<TripParticipantsModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns((TripParticipantsModalResult?)null);
        await using var context = CreateContext(modalService: modalService);
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.ShowParticipants, true));

        // Act
        await cut.Find("#trip-editor-participants").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                Arg.Is<TripParticipantsModalModel>(model => model.TripId == TripId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRaiseContentChangedWhenTheParticipantsChanged()
    {
        // Arrange
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                Arg.Any<TripParticipantsModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsModalResult(new TripParticipantsDto(TripId, "Owner")));
        await using var context = CreateContext(modalService: modalService);
        var contentChanged = 0;
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.ShowParticipants, true)
            .Add(component => component.OnContentChanged, () => contentChanged++));

        // Act
        await cut.Find("#trip-editor-participants").ClickAsync();

        // Assert
        contentChanged.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNotRaiseContentChangedWhenTheModalWasDismissed()
    {
        // Arrange
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                Arg.Any<TripParticipantsModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns((TripParticipantsModalResult?)null);
        await using var context = CreateContext(modalService: modalService);
        var contentChanged = 0;
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.ShowParticipants, true)
            .Add(component => component.OnContentChanged, () => contentChanged++));

        // Act
        await cut.Find("#trip-editor-participants").ClickAsync();

        // Assert
        contentChanged.Should().Be(0);
    }
}
