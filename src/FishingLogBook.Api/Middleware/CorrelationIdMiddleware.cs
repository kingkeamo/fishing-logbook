using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadOrCreate(context);
        context.Items[CorrelationHeaders.CorrelationId] = correlationId;
        context.Response.Headers[CorrelationHeaders.CorrelationId] = correlationId.ToString("D");

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.ToString(),
            ["HttpMethod"] = context.Request.Method
        });

        var started = DateTime.UtcNow;
        await _next(context);
        var elapsedMilliseconds = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        var statusCode = context.Response.StatusCode;

        if (statusCode >= 500)
        {
            _logger.LogError(
                "HTTP {HttpMethod} {RequestPath} {StatusCode} in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path.ToString(),
                statusCode,
                elapsedMilliseconds);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(
                "HTTP {HttpMethod} {RequestPath} {StatusCode} in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path.ToString(),
                statusCode,
                elapsedMilliseconds);
        }
        else
        {
            _logger.LogDebug(
                "HTTP {HttpMethod} {RequestPath} {StatusCode} in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path.ToString(),
                statusCode,
                elapsedMilliseconds);
        }
    }

    public static Guid GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationHeaders.CorrelationId, out var value) && value is Guid guid)
        {
            return guid;
        }

        return Guid.NewGuid();
    }

    private static Guid ReadOrCreate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeaders.CorrelationId, out var header) &&
            Guid.TryParse(header.ToString(), out var parsed) &&
            parsed != Guid.Empty)
        {
            return parsed;
        }

        return Guid.NewGuid();
    }
}
