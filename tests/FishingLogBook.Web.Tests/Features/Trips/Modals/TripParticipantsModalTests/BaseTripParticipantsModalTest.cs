using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.TripParticipantsModalTests;

public class BaseTripParticipantsModalTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid ParticipantUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly DateTimeOffset InvitedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected static BunitContext CreateContext(
        ITripParticipantClient participantClient,
        IModalService? modalService = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(participantClient);
        context.Services.AddSingleton(modalService ?? Substitute.For<IModalService>());
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<IRenderedComponent<MudDialogProvider>> ShowModalAsync(BunitContext context)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TripParticipantsModal>
        {
            { modal => modal.Model, new TripParticipantsModalModel(TripId) }
        };
        await dialogs.ShowAsync<TripParticipantsModal>(parameters, new DialogOptions());
        return cut;
    }

    protected static TripParticipantsDto Participants(string role, params TripParticipantDto[] participants)
    {
        return new TripParticipantsDto(TripId, role)
        {
            Participants = participants
        };
    }

    protected static TripParticipantDto Owner(string? displayName = "Eamonn")
    {
        return new TripParticipantDto(
            OwnerUserId,
            "Accepted",
            displayName,
            null,
            DateTimeOffset.MinValue)
        {
            IsOwner = true
        };
    }

    protected static TripParticipantDto Participant(
        string status = "Accepted",
        string? displayName = "Mark",
        Guid? userId = null)
    {
        return new TripParticipantDto(
            userId ?? ParticipantUserId,
            status,
            displayName,
            null,
            InvitedOn);
    }
}
