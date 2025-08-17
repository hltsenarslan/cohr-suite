using FluentAssertions;
using Xunit;

namespace Core.Api.Tests;

public class HealthTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;
    public HealthTests(TestDatabaseFixture db) => _db = db;

    [Fact]
    public async Task Health_Should_Return_200()
    {
        await using var app = new CustomWebApplicationFactory(_db.ConnectionString);
        var res = await app.CreateClient().GetAsync("/health");
        res.IsSuccessStatusCode.Should().BeTrue();
    }
}