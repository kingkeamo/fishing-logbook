namespace FishingLogBook.Api.Tests.SystemEndpointsTests;

public class BaseSystemEndpointsTest : IClassFixture<SystemApiFactory>
{
    protected readonly SystemApiFactory Factory;

    protected BaseSystemEndpointsTest(SystemApiFactory factory)
    {
        Factory = factory;
    }
}
