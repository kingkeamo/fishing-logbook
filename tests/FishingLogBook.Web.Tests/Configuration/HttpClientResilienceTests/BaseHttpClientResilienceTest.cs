using System.Net;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Http;
using FishingLogBook.Web.Features.Diagnostics.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Configuration.HttpClientResilienceTests;

public class BaseHttpClientResilienceTest
{
    protected static (HttpClient Client, TestPrimaryHandler Handler) CreateResilientClient(
        Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        bool includeCorrelationHandler = false)
    {
        var primary = new TestPrimaryHandler(responseFactory);
        var services = new ServiceCollection();
        services.AddLogging();

        if (includeCorrelationHandler)
        {
            services.AddScoped<CorrelationContext>();
            services.AddTransient<CorrelationDelegatingHandler>();
        }

        var builder = services.AddHttpClient(HttpClientNames.Anonymous, client =>
            {
                client.BaseAddress = new Uri("https://api.test/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => primary);

        if (includeCorrelationHandler)
        {
            builder.AddHttpMessageHandler<CorrelationDelegatingHandler>();
        }

        builder.ConfigureGetReadResilience();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return (factory.CreateClient(HttpClientNames.Anonymous), primary);
    }

    protected static HttpResponseMessage CreateResponse(
        HttpRequestMessage request,
        HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            RequestMessage = request
        };
    }

    protected sealed class TestPrimaryHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;
        private int _invocationCount;

        public TestPrimaryHandler(
            Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int InvocationCount => _invocationCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _invocationCount);
            return _responseFactory(attempt, request, cancellationToken);
        }
    }
}
