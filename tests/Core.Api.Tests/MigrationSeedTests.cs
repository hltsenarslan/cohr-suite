using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Core.Api.Tests;

public class MigrationSeedTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public MigrationSeedTests(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task App_Should_Migrate_And_Seed_DomainMappings()
    {
        // Custom factory: Core.Api uygulamasını test DB'si ile başlat
        await using var appFactory = new CustomWebApplicationFactory(_db.ConnectionString);
        using var client = appFactory.CreateClient();

        // Act
        var res  = await client.GetAsync("/internal/domains/pys.local");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<DomainMappingDto>();

        // Assert
        dto.Should().NotBeNull();
        dto!.host.Should().Be("pys.local");
        dto.module.Should().Be("performance");
        dto.pathMode.Should().Be("slug");
    }

    // Test için minimal DTO
    private record DomainMappingDto(
        Guid id, string host, string module, Guid? tenantId, string pathMode, string? tenantSlug, bool isActive);
}