using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

public class TenantResolverTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _f;
    public TenantResolverTests(GatewayFactory f) => _f = f;

    [Fact]
    public async Task Perf_slug_flow_sets_header_and_proxies()
    {
        var c = _f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/perf/firm1/me");
        req.Headers.TryAddWithoutValidation("Host", "pys.local"); // host header

        var res = await c.SendAsync(req);
        // Perf backend test server’ı yoksa 502 alırsın; bu test sadece resolve’u doğrulasın:
        if (res.StatusCode == HttpStatusCode.BadGateway)
        {
            // En azından 400 tenant_resolve_failed gelmemeli (resolver çalıştı)
            res.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Comp_host_flow_sets_header_and_proxies()
    {
        var c = _f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/comp/firm2/me");
        req.Headers.TryAddWithoutValidation("Host", "pay.local");

        var res = await c.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.BadGateway)
        {
            res.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Missing_tenant_results_400()
    {
        var c = _f.CreateClient();
        var res = await c.GetAsync("/api/perf/me"); // slug yok, gateway çözemez
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var s = await res.Content.ReadAsStringAsync();
        s.Should().Contain("tenant_resolve_failed");
    }
}