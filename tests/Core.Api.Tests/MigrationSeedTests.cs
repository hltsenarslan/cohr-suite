using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Core.Api.Tests;

public class MigrationSeedTests : IClassFixture<CoreWebAppFactory>
{
    private readonly HttpClient _client;

    public MigrationSeedTests(CoreWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }
    [Fact]
    public async Task App_Should_Migrate_And_Seed_DomainMappings()
    {

        // Act
        var res  = await _client.GetAsync("/internal/domains/pys.local");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<DomainMappingDto>();

        // Assert
        dto.Should().NotBeNull();
        dto!.host.Should().Be("pys.local");
        dto.module.Should().Be(0);
        dto.pathMode.Should().Be(1);
    }

    // Test için minimal DTO
    private record DomainMappingDto(
        Guid id, string host, int module, Guid? tenantId, int pathMode, string? tenantSlug, bool isActive);
}