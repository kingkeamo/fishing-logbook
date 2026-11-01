using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Modals.AnglerPicker;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Components.TripEditor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class WhenTestingShowParticipants : BaseTripEditorTest
{
    [Fact]
    public async Task ItShouldDisplayTheReusableParticipantsSectionForThisTrip()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(new TripParticipantsDto(TripId, "Owner")
        {
            Participants = [new TripParticipantDto(OwnerUserId, "Accepted", "Organiser", null, StartedOn) { IsOwner = true }]
        });
        await using var context = CreateContext(participants: client);
        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.ShowParticipants, true));
        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-participants-section").TextContent.Should().Contain("Organiser"));
        await client.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheAnglerPickerDirectlyFromTheInlineInviteButton()
    {
        // Arrange
        var modals = Substitute.For<IModalService>();
        await using var context = CreateContext(modalService: modals);
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.ShowParticipants, true));
        cut.WaitForAssertion(() => cut.Find("#trip-participants-invite").Should().NotBeNull());
        // Act
        await cut.Find("#trip-participants-invite").ClickAsync();
        // Assert
        await modals.Received(1).ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
            Arg.Is<AnglerPickerModalModel>(model => model.ExcludedUserIds.Count == 0), Arg.Any<CancellationToken>());
    }
}
