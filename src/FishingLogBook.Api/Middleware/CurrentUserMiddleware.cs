using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Shared.Constants;
using MediatR;

namespace FishingLogBook.Api.Middleware;

public sealed class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CurrentUserMiddleware> _logger;

    public CurrentUserMiddleware(RequestDelegate next, ILogger<CurrentUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IMediator mediator,
        ICurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var subject = context.User.FindFirst("sub")?.Value;
        var email = context.User.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var authenticatedEmail = email;

        try
        {
            var response = await mediator.Send(
                new ResolveCurrentUserCommand
                {
                    Provider = IdentityProviderConstants.Cognito,
                    Subject = subject,
                    Email = authenticatedEmail
                },
                context.RequestAborted);
            if (!TryAssignCurrentUser(context, currentUser, response, authenticatedEmail, subject))
            {
                return;
            }
        }
        catch (OperationCanceledException exception) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug(exception, "Current user resolution was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to resolve FishingLogBook user.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        await _next(context);
    }

    private static bool TryAssignCurrentUser(
        HttpContext context,
        ICurrentUser currentUser,
        ResolveCurrentUserResponse response,
        string email,
        string subject)
    {
        if (response.IsFailure)
        {
            context.Response.StatusCode = IsMissingIdentity(response)
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status503ServiceUnavailable;
            return false;
        }

        currentUser.Assign(response.UserId, email, IdentityProviderConstants.Cognito, subject);
        return true;
    }

    private static bool IsMissingIdentity(ResolveCurrentUserResponse response)
    {
        if (response.ValidationErrors is { Count: > 0 })
        {
            return true;
        }

        return string.Equals(
            response.ErrorMessage,
            "External identity is missing.",
            StringComparison.Ordinal)
            || string.Equals(
                response.ErrorMessage,
                "Authenticated email is missing.",
                StringComparison.Ordinal);
    }
}
