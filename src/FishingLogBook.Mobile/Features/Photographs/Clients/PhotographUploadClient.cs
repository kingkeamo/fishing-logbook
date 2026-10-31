using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Photographs.Clients;

public sealed class PhotographUploadClient : IPhotographUploadClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PhotographUploadClient> _logger;

    public PhotographUploadClient(IHttpClientFactory httpClientFactory, ILogger<PhotographUploadClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PhotographUploadOutcomeEnum> UploadAsync(
        string uploadUrl,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!Uri.TryCreate(uploadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return PhotographUploadOutcomeEnum.UrlRejected;
        }

        using var body = new StreamContent(content);
        body.Headers.ContentType = mediaType;
        try
        {
            var client = _httpClientFactory.CreateClient(AuthenticationHttpClientConstants.Anonymous);
            using var response = await client.PutAsync(uri, body, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return PhotographUploadOutcomeEnum.Uploaded;
            }

            _logger.LogWarning("Photograph upload returned {StatusCode}.", (int)response.StatusCode);
            return response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden
                ? PhotographUploadOutcomeEnum.UrlRejected
                : PhotographUploadOutcomeEnum.Unavailable;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or WebException or IOException or SocketException)
        {
            _logger.LogWarning("Photograph upload failed with {ExceptionType}.", exception.GetType().FullName);
            return PhotographUploadOutcomeEnum.Unavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Photograph upload timed out.");
            return PhotographUploadOutcomeEnum.Unavailable;
        }
    }
}
