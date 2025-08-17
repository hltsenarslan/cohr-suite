using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gateway.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_Should_Return_200()
    {
        var app = new WebApplicationFactory<Program>();
        var res = await app.CreateClient().GetAsync("/health");
        res.IsSuccessStatusCode.Should().BeTrue();
    }
}