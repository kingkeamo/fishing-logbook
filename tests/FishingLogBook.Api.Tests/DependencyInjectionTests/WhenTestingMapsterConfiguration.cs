using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AwesomeAssertions;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.DependencyInjection;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Api.Tests.DependencyInjectionTests;

public class WhenTestingMapsterConfiguration
{
    private static readonly ImmutableHashSet<string> BannedMembers =
    [
        "Mapster.TypeAdapterConfig.get_GlobalSettings",
        "Mapster.TypeAdapterExtensions.Adapt",
        "Mapster.TypeAdapterExtensions.BuildAdapter",
        "Mapster.TypeAdapter.Adapt"
    ];

    public static TheoryData<string> ProductionAssemblies =>
    [
        typeof(CatchService).Assembly.Location,
        typeof(CatchRepository).Assembly.Location,
        typeof(FishingLogBook.DependencyInjection.ServiceCollectionExtensions).Assembly.Location,
        typeof(Program).Assembly.Location
    ];

    [Theory]
    [MemberData(nameof(ProductionAssemblies))]
    public void ItShouldNotReferenceStaticMapsterConfiguration(string assemblyPath)
    {
        // Arrange
        var assembly = Path.GetFileName(assemblyPath);

        // Act
        var referenced = ReferencedMapsterMembers(assemblyPath);

        // Assert
        referenced.Should().NotContain(
            member => BannedMembers.Contains(member),
            $"{assembly} must map through the DI-owned TypeAdapterConfig, not process-wide Mapster state");
    }

    [Fact]
    public void ItShouldGiveEachContainerItsOwnMapperConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var first = BuildMappingServices(configuration);
        var second = BuildMappingServices(configuration);

        // Assert
        first.Config.Should().NotBeSameAs(second.Config);
        first.Config.Should().NotBeSameAs(TypeAdapterConfig.GlobalSettings);
        second.Config.Should().NotBeSameAs(TypeAdapterConfig.GlobalSettings);
        first.Mapper.Should().NotBeSameAs(second.Mapper);
    }

    [Fact]
    public void ItShouldNotConfigureTheGlobalMapsterSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var before = TypeAdapterConfig.GlobalSettings.RuleMap.Count;

        // Act
        BuildMappingServices(configuration);

        // Assert
        TypeAdapterConfig.GlobalSettings.RuleMap.Count.Should().Be(before);
        BuildMappingServices(configuration).Config.RuleMap.Should().NotBeEmpty();
    }

    private static (TypeAdapterConfig Config, IMapper Mapper) BuildMappingServices(IConfiguration configuration)
    {
        var provider = new ServiceCollection()
            .AddFishingLogBook(configuration)
            .BuildServiceProvider();
        return (provider.GetRequiredService<TypeAdapterConfig>(), provider.GetRequiredService<IMapper>());
    }

    private static IReadOnlyCollection<string> ReferencedMapsterMembers(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var members = new List<string>();
        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var declaringType = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            var typeNamespace = reader.GetString(declaringType.Namespace);
            if (!typeNamespace.StartsWith("Mapster", StringComparison.Ordinal))
            {
                continue;
            }

            members.Add($"{typeNamespace}.{reader.GetString(declaringType.Name)}.{reader.GetString(member.Name)}");
        }

        return members;
    }
}
