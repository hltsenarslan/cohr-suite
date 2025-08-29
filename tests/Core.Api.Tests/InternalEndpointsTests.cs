using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class InternalEndpointsTests : IClassFixture<CoreWebAppFactory>
{
    private readonly HttpClient _client;

    public InternalEndpointsTests(CoreWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Domains_pys_local_returns_slug_mode()
    {
        var res = await _client.GetAsync("/internal/domains/pys.local");
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("pathMode").GetInt32()); // slug=1
    }

    [Fact]
    public async Task Resolve_slug_works()
    {
        var res = await _client.GetAsync("/internal/tenants/resolve/firm1");
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac", json.GetProperty("tenantId").GetString());
    }
}