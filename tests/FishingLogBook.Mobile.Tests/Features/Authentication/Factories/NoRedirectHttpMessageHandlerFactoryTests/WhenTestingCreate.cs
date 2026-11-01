using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Factories;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Factories.NoRedirectHttpMessageHandlerFactoryTests;

public class WhenTestingCreate
{
    [Fact]
    public void ItShouldDisableAutomaticRedirectsToPreventBearerForwarding()
    {
        // Arrange
        // Act
        using var handler = NoRedirectHttpMessageHandlerFactory.Create();
        // Assert
        handler.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public void ItShouldTrustTheAspNetDevelopmentCertificateOnlyOnLoopbackHosts()
    {
        // Arrange
        using var handler = NoRedirectHttpMessageHandlerFactory.Create();
        using var development = CreateSelfSigned("localhost");
        using var other = CreateSelfSigned("example.com");
        using var localRequest = new HttpRequestMessage(HttpMethod.Get, "https://10.0.2.2:7256/api/users/current");
        using var remoteRequest = new HttpRequestMessage(HttpMethod.Get, "https://fishing-logbook-dev-api.fly.dev/api/users/current");
        var callback = handler.ServerCertificateCustomValidationCallback;
        // Act
        // Assert
        callback.Should().NotBeNull();
        callback!(localRequest, development, null, SslPolicyErrors.None).Should().BeTrue();
        callback(localRequest, development, null, SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeTrue();
        callback(localRequest, other, null, SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse();
        callback(remoteRequest, development, null, SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse();
    }

    private static X509Certificate2 CreateSelfSigned(string commonName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
