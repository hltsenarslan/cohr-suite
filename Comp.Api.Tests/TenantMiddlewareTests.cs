using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Assert = Xunit.Assert;

namespace Comp.Api.Tests;

public class TenantMiddlewareTests : IClassFixture<CompWebAppFactory>
{
    private readonly CompWebAppFactory _f;
    public TenantMiddlewareTests(CompWebAppFactory f) => _f = f;

    [Fact]
    public async Task Health_Bypasses_Tenant()
    {
        var c = _f.CreateClient();
        var res = await c.GetAsync("/health");
        Assert.True(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Protected_Requires_Tenant()
    {
        var c = _f.CreateClient();
        var res = await c.GetAsync("/me");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("tenant_required", body);
    }

    [Fact]
    public async Task With_Tenant_Header_Returns_200_And_Is_Isolated()
    {
        var c = _f.CreateClient();

        var r1 = new HttpRequestMessage(HttpMethod.Get, "/me");
        r1.Headers.Add("X-Tenant-Id", "a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
        var res1 = await c.SendAsync(r1);
        var b1 = await res1.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        Assert.Contains("\"tenant\":\"a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac\"", b1);

        var r2 = new HttpRequestMessage(HttpMethod.Get, "/me");
        r2.Headers.Add("X-Tenant-Id", "44709835-d55a-ef2a-2327-5fdca19e55d8");
        var res2 = await c.SendAsync(r2);
        var b2 = await res2.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        Assert.Contains("\"tenant\":\"44709835-d55a-ef2a-2327-5fdca19e55d8\"", b2);

        Assert.NotEqual(b1, b2);
    }
}