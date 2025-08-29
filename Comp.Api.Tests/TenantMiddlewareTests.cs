using System.Net;
using Assert = Xunit.Assert;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Comp.Api.Tests;

public class TenantMiddlewareTests : IClassFixture<CompWebAppFactory>
{
    private readonly CompWebAppFactory _factory;
    public TenantMiddlewareTests(CompWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Bypasses_Tenant()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.True(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Protected_Requires_Tenant()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("tenant_required", text);
    }

    [Fact]
    public async Task With_Tenant_Header_Returns_200()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/me");
        req.Headers.Add("X-Tenant-Id", "44709835-d55a-ef2a-2327-5fdca19e55d8"); // firm2
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}