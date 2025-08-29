using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

public sealed class FakeCoreHandler : HttpMessageHandler
{
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var path = req.RequestUri!.AbsolutePath;

        // /internal/domains/pys.local → slug (1)
        if (path.Equals("/internal/domains/pys.local", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Json200(new { id="111...", host="pys.local", module=0, tenantId=(string?)null, pathMode=1, tenantSlug=(string?)null, isActive=true }));

        // /internal/domains/pay.local → host (0) + tenantId fixed
        if (path.Equals("/internal/domains/pay.local", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Json200(new { id="222...", host="pay.local", module=1, tenantId="44709835-d55a-ef2a-2327-5fdca19e55d8", pathMode=0, tenantSlug=(string?)null, isActive=true }));

        // /internal/tenants/resolve/firm1
        if (path.Equals("/internal/tenants/resolve/firm1", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Json200(new { tenantId = "a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac" }));

        // /internal/tenants/resolve/firm2
        if (path.Equals("/internal/tenants/resolve/firm2", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Json200(new { tenantId = "44709835-d55a-ef2a-2327-5fdca19e55d8" }));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private HttpResponseMessage Json200(object o) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(o, _json), System.Text.Encoding.UTF8, "application/json")
        };
}