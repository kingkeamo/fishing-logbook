using System.Net;
using System.Text;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public class BaseFishingPreferenceClientTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    protected static FishingPreferenceClient CreateClient(RecordingHandler apiHandler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        var localizer = Substitute.For<ICatalogueLocalizer>();
        localizer.Localize(Arg.Any<FishingLogBook.Shared.Dtos.FishingCatalogueDto>())
            .Returns(call => call.Arg<FishingLogBook.Shared.Dtos.FishingCatalogueDto>());
        localizer.Localize(Arg.Any<FishingLogBook.Shared.Dtos.FishingPreferencesDto>())
            .Returns(call => call.Arg<FishingLogBook.Shared.Dtos.FishingPreferencesDto>());
        return new FishingPreferenceClient(factory, localizer);
    }

    protected sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
