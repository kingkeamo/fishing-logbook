using System.Net;
using System.Net.Sockets;
using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Photographs.Clients;

public sealed class PhotographDownloadClient : IPhotographDownloadClient
{
    public const long MaximumBytes = 25L * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PhotographDownloadClient> _logger;

    public PhotographDownloadClient(IHttpClientFactory httpClientFactory, ILogger<PhotographDownloadClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PhotographDownloadResultModel> DownloadAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return PhotographDownloadResultModel.Failed(PhotographDownloadOutcomeEnum.Rejected);
        }

        try
        {
            var client = _httpClientFactory.CreateClient(AuthenticationHttpClientConstants.Anonymous);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Photograph download returned {StatusCode}.", (int)response.StatusCode);
                return PhotographDownloadResultModel.Failed(
                    response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                        ? PhotographDownloadOutcomeEnum.Rejected
                        : PhotographDownloadOutcomeEnum.Unavailable);
            }

            if (response.Content.Headers.ContentLength > MaximumBytes)
            {
                return PhotographDownloadResultModel.Failed(PhotographDownloadOutcomeEnum.TooLarge);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaximumBytes)
                {
                    return PhotographDownloadResultModel.Failed(PhotographDownloadOutcomeEnum.TooLarge);
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.Length == 0
                ? PhotographDownloadResultModel.Failed(PhotographDownloadOutcomeEnum.Unavailable)
                : PhotographDownloadResultModel.Downloaded(buffer.ToArray());
        }
        catch (Exception exception) when (
            exception is HttpRequestException or WebException or IOException or SocketException)
        {
            _logger.LogWarning("Photograph download failed with {ExceptionType}.", exception.GetType().FullName);
            return PhotographDownloadResultModel.Failed(PhotographDownloadOutcomeEnum.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Photograph download timed out.");
            return PhotographDownloadResultModel.Failed(PhotographDownloadOutcomeEnum.Unavailable);
        }
    }
}
