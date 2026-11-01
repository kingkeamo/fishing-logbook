using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace FishingLogBook.Mobile.Features.Authentication.Factories;

public static class NoRedirectHttpMessageHandlerFactory
{
    public static HttpClientHandler Create()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = AcceptLocalDevelopmentCertificate;
#endif
        return handler;
    }

    private static bool AcceptLocalDevelopmentCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        return certificate is not null &&
            request.RequestUri is not null &&
            IsLocalApiHost(request.RequestUri.Host) &&
            IsAspNetHttpsDevelopmentCertificate(certificate);
    }

    private static bool IsLocalApiHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            host.Equals("10.0.2.2", StringComparison.Ordinal);
    }

    private static bool IsAspNetHttpsDevelopmentCertificate(X509Certificate2 certificate)
    {
        return certificate.Subject.Contains("CN=localhost", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase);
    }
}
