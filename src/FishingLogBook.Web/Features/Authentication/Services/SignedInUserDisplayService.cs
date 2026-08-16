using System.Security.Claims;

namespace FishingLogBook.Web.Features.Authentication.Services;

public sealed class SignedInUserDisplayService : ISignedInUserDisplayService
{
    public string? GetEmail(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var email = user.FindFirst("email")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        email = user.FindFirst(ClaimTypes.Email)?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email;
    }
}
