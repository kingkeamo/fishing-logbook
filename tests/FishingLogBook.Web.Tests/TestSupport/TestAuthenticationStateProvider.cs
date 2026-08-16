using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FishingLogBook.Web.Tests.TestSupport;

public sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ClaimsPrincipal _user;

    public TestAuthenticationStateProvider(bool isAuthenticated, string name = "tester@example.test")
    {
        if (isAuthenticated)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, name),
                    new Claim("email", name)
                ],
                authenticationType: "Test");
            _user = new ClaimsPrincipal(identity);
            return;
        }

        _user = new ClaimsPrincipal(new ClaimsIdentity());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_user));
    }
}
