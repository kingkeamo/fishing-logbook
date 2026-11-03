using System.Net;
using System.Text;
using FishingLogBook.Mobile.Features.About.Clients;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.About.Clients.SystemStatusClientTests;

public abstract class BaseSystemStatusClientTest
{
    protected static (SystemStatusClient Client, RecordingHttpMessageHandler Server) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var server = new RecordingHttpMessageHandler(respond);
        var httpClient = new HttpClient(server) { BaseAddress = new Uri("https://api.example.com/") };
        return (new SystemStatusClient(httpClient), server);
    }

    protected static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
