using System.Reflection;
using FishingLogBook.Api.Endpoints;
using Microsoft.AspNetCore.Http;

namespace FishingLogBook.Api.Tests.DependencyInjectionTests;

public class BaseDependencyInjectionTest : IClassFixture<DependencyInjectionApiFactory>
{
    protected readonly DependencyInjectionApiFactory Factory;

    protected BaseDependencyInjectionTest(DependencyInjectionApiFactory factory)
    {
        Factory = factory;
    }

    protected static IReadOnlyCollection<Type> GetEndpointInjectedServiceTypes()
    {
        return typeof(SystemEndpoints).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(SystemEndpoints).Namespace)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(method => !method.Name.StartsWith("Map", StringComparison.Ordinal))
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Where(IsContainerService)
            .Distinct()
            .ToArray();
    }

    private static bool IsContainerService(Type type)
    {
        if (type == typeof(CancellationToken) ||
            type == typeof(HttpContext) ||
            type == typeof(HttpRequest) ||
            type == typeof(HttpResponse))
        {
            return false;
        }

        if (type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(Guid) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeOnly) ||
            type.IsEnum)
        {
            return false;
        }

        if (type.Namespace is not null &&
            (type.Namespace.StartsWith("FishingLogBook.Shared", StringComparison.Ordinal) ||
             type.Namespace.StartsWith("FishingLogBook.Domain", StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }
}
