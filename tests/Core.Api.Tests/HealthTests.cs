using FluentAssertions;
using Xunit;

namespace Core.Api.Tests;

public class HealthTests : IClassFixture<CoreWebAppFactory>
{
    private readonly HttpClient _client;

    public HealthTests(CoreWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task Health_Should_Return_200()
    {
        var res = await _client.GetAsync("/health");
        res.IsSuccessStatusCode.Should().BeTrue();
    }
}