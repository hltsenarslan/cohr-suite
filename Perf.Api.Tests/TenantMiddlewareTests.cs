using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

public class TenantMiddlewareTests : IClassFixture<PerfWebAppFactory>
{
    private readonly PerfWebAppFactory _factory;
    public TenantMiddlewareTests(PerfWebAppFactory factory) => _factory = factory;

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
        req.Headers.Add("X-Tenant-Id", "a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}