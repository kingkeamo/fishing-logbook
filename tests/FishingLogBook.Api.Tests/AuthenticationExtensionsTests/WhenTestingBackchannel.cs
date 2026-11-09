using System.Net;
using System.Net.Sockets;
using System.Text;
using AwesomeAssertions;

namespace FishingLogBook.Api.Tests.AuthenticationExtensionsTests;

public class WhenTestingBackchannel : BaseAuthenticationExtensionsTest
{
    private static readonly byte[] Response = Encoding.ASCII.GetBytes(
        "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");

    [Fact]
    public async Task ItShouldReachTheMetadataEndpointOverIpv4()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = ServeOnceAsync(listener);
        using var invoker = new HttpMessageInvoker(CreateConfiguredJwtBearerOptions().BackchannelHttpHandler!);

        // Act
        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/"),
            CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync();
        await served;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("ok");
    }

    [Fact]
    public async Task ItShouldFallBackToIpv6WhenIpv4CannotConnect()
    {
        // Arrange
        if (!Socket.OSSupportsIPv6)
        {
            return;
        }

        using var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Server.DualMode = false;
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = ServeOnceAsync(listener);
        using var invoker = new HttpMessageInvoker(CreateConfiguredJwtBearerOptions().BackchannelHttpHandler!);

        // Act
        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{port}/"),
            CancellationToken.None);
        await served;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ItShouldFailWhenNeitherIpv4NorIpv6CanConnect()
    {
        // Arrange
        int port;
        using (var reserved = new TcpListener(IPAddress.Loopback, 0))
        {
            reserved.Start();
            port = ((IPEndPoint)reserved.LocalEndpoint).Port;
        }

        using var invoker = new HttpMessageInvoker(CreateConfiguredJwtBearerOptions().BackchannelHttpHandler!);

        // Act
        var act = () => invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static async Task ServeOnceAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        var buffer = new byte[4096];
        _ = await stream.ReadAsync(buffer);
        await stream.WriteAsync(Response);
        await stream.FlushAsync();
    }
}
