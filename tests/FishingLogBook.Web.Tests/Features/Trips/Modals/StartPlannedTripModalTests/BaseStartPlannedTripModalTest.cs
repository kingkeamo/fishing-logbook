using Bunit;
using FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.StartPlannedTripModalTests;

public class BaseStartPlannedTripModalTest
{
    protected static readonly Guid FirstTripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid SecondTripId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid OrganiserUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    protected static BunitContext CreateContext(ITripDisplayService? tripDisplay = null)
    {
        var display = tripDisplay ?? Substitute.For<ITripDisplayService>();
        if (tripDisplay is null)
        {
            display.DescribeInstantAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns("12 Sep 2026, 07:30");
        }

        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(display);
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)> ShowAsync(
        BunitContext context,
        params PlannedTripOptionModel[] trips)
    {
        var provider = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<StartPlannedTripModal>
        {
            { modal => modal.Model, new StartPlannedTripModalModel(trips) }
        };
        var reference = await dialogs.ShowAsync<StartPlannedTripModal>(parameters, new DialogOptions());
        return (provider, reference);
    }

    protected static PlannedTripOptionModel Organised(Guid? tripId = null, string? title = "Corrib")
    {
        return new PlannedTripOptionModel(tripId ?? FirstTripId, PlannedStart, OrganiserUserId, "Alex", IsOrganiser: true)
        {
            Title = title
        };
    }

    protected static PlannedTripOptionModel Invited(Guid? tripId = null)
    {
        return new PlannedTripOptionModel(tripId ?? SecondTripId, PlannedStart.AddHours(2), OrganiserUserId, "Alex", IsOrganiser: false)
        {
            Title = "Lough Mask",
            Guests =
            [
                new PlannedTripGuestModel(Guid.NewGuid(), "Sam", IsPending: false),
                new PlannedTripGuestModel(Guid.NewGuid(), "Pat", IsPending: true)
            ]
        };
    }
}
