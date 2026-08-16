using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.LoginDisplayTests;

public class BaseLoginDisplayTest
{
    protected static BunitContext CreateContext(bool isAuthenticated, params Claim[] claims)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<ISignedInUserDisplayService, SignedInUserDisplayService>();
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized(" ");
            if (claims.Length > 0)
            {
                authorization.SetClaims(claims);
            }
        }
        else
        {
            authorization.SetNotAuthorized();
        }

        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }
}
