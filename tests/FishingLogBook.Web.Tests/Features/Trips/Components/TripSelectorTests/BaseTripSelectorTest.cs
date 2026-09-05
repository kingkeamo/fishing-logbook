using Bunit;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripSelectorTests;

public class BaseTripSelectorTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}
